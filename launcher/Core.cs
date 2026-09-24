using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;

namespace FateLauncher {
public static class Json {
 public static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength=64*1024*1024, RecursionLimit=80 }; }
 public static Dictionary<string,object> Parse(string s) { return Serializer().Deserialize<Dictionary<string,object>>(s); }
 public static Dictionary<string,object> Map(object o) { return (Dictionary<string,object>)o; }
 public static string S(Dictionary<string,object> d,string k) { return Convert.ToString(d[k]); }
 public static long N(Dictionary<string,object> d,string k) { return Convert.ToInt64(d[k]); }
 public static object[] A(Dictionary<string,object> d,string k) { return ((System.Collections.IEnumerable)d[k]).Cast<object>().ToArray(); }
 public static void Write(string path, object o) {
  Directory.CreateDirectory(Path.GetDirectoryName(path)); string tmp=path+".writing";
  File.WriteAllText(tmp,Serializer().Serialize(o),new UTF8Encoding(false));
  if(File.Exists(path)) File.Replace(tmp,path,null); else File.Move(tmp,path);
 }
}
public sealed class Settings {
 public string SourceIso="", Emulator="", Memstick="", DataRoot="";
 public string BaseVersion="", HdVersion="", HdBaseVersion="", CheatsVersion="", SaveVersion="", GameIso="";
 public bool SelectBase=true, SelectHD=false, SelectCheats=false, SelectSave=false;
 public static Settings Load(string p) { return File.Exists(p)?Json.Serializer().Deserialize<Settings>(File.ReadAllText(p)):new Settings(); }
}
public sealed class Asset {
 public string Name,Url,Hash; public long Size;
 public static Asset Read(Dictionary<string,object> x,string tag) {
  string n=Json.S(x,"name"); Engine.SafeRelative(n); if(n.Contains("/")||n.Contains("\\"))throw new Exception("잘못된 다운로드 파일 이름");
  string h=Json.S(x,"sha256").ToUpperInvariant(); if(!Regex.IsMatch(h,"^[0-9A-F]{64}$"))throw new Exception("다운로드 검증 정보가 없습니다.");
  long sz=Json.N(x,"bytes"); if(sz<=0||sz>2500000000L)throw new Exception("지원하지 않는 다운로드 크기");
  return new Asset{Name=n,Size=sz,Hash=h,Url=Engine.ReleaseRoot+Uri.EscapeDataString(tag)+"/"+Uri.EscapeDataString(n)};
 }
}
public sealed class ReleaseInfo {
 public string Tag,BaseVersion,HdVersion,BaseRoot,HdRoot,PatchName,TargetHash,HdIniHash; public long TargetBytes;
 public Asset Base; public Asset[] Hd;
 public static ReleaseInfo Read(string json,string tag) {
  var d=Json.Parse(json);
  if(Json.N(d,"schema")!=1 || Json.S(d,"repository")!=Engine.Repository || Json.S(d,"release_tag")!=tag)throw new Exception("이 런처에서 지원하지 않는 업데이트 정보입니다.");
  Version required; if(!Version.TryParse(Json.S(d,"minimum_launcher"),out required)||required>new Version(Engine.Version))throw new Exception("런처 새 버전이 필요합니다. 배포 페이지에서 런처를 받아 주세요.");
  var src=Json.Map(d["source"]);var dst=Json.Map(d["target"]);
  if(Json.N(src,"bytes")!=Engine.SourceSize||Json.S(src,"sha256").ToUpperInvariant()!=Engine.SourceHash)throw new Exception("지원 원본이 다른 업데이트입니다.");
  var r=new ReleaseInfo { Tag=tag,BaseVersion=Json.S(d,"base_version"),HdVersion=Json.S(d,"hd_version"),BaseRoot=Json.S(d,"base_root"),HdRoot=Json.S(d,"hd_root"),PatchName=Json.S(d,"patch_name"),TargetHash=Json.S(dst,"sha256").ToUpperInvariant(),TargetBytes=Json.N(dst,"bytes") };
  foreach(string s in new[]{tag,r.BaseVersion,r.HdVersion,r.BaseRoot,r.HdRoot,r.PatchName}) { Engine.SafeRelative(s); if(s.IndexOfAny(new[]{'/','\\'})>=0)throw new Exception("업데이트 이름 오류"); }
  if(!Regex.IsMatch(r.TargetHash,"^[0-9A-F]{64}$")||r.TargetBytes<=0||r.TargetBytes>8L*1024*1024*1024)throw new Exception("대상 ISO 정보 오류");
  r.HdIniHash=Json.S(d,"hd_textures_ini_sha256").ToUpperInvariant();if(!Regex.IsMatch(r.HdIniHash,"^[0-9A-F]{64}$"))throw new Exception("HD 매핑 정보 오류");
  r.Base=Asset.Read(Json.Map(d["base_asset"]),tag);r.Hd=Json.A(d,"hd_assets").Select(x=>Asset.Read(Json.Map(x),tag)).ToArray();
  if(r.Hd.Length<1||r.Hd.Length>12||r.Hd.Select(a=>a.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=r.Hd.Length||r.Hd.Count(a=>a.Name.EndsWith(".zip",StringComparison.OrdinalIgnoreCase))!=1)throw new Exception("HD 분할 파일 정보 오류");
  return r;
 }
}
public sealed class Swap {
 public string Target,Backup,Staged; public bool HadOld;
}
public sealed class Journal {
 public string PreviousSettings; public bool Committed; public List<Swap> Operations=new List<Swap>();
}
public sealed class Engine {
 public const string Version="1.3.0",Repository="sirecoymarsh/fate-extra-korean-patch";
 public const string ReleaseRoot="https://github.com/"+Repository+"/releases/download/";
 public const string SourceHash="60399D610CBCDA96601374A2E621A22BB58505C403C6FA4221EEC5E87235667B";
 public const long SourceSize=1280933888;
 public readonly string Root,ConfigPath,ToolRoot;
 public Settings Config; public Action<string,int> Progress;
 public CancellationToken Cancel;
 public Engine(Settings c,string configPath,string toolRoot) { Config=c;ConfigPath=Path.GetFullPath(configPath);ToolRoot=Path.GetFullPath(toolRoot);Root=Path.GetFullPath(c.DataRoot);if(Root==Path.GetPathRoot(Root))throw new Exception("본편 폴더로 드라이브 전체를 지정할 수 없습니다.");NoLinkParents(Root);Directory.CreateDirectory(Root); }
 public static string Hash(string path) { using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-",""); }
 public static void Verify(string p,long bytes,string hash) { if(!File.Exists(p)||new FileInfo(p).Length!=bytes||Hash(p)!=hash.ToUpperInvariant())throw new Exception("파일이 손상되었거나 지원 파일과 다릅니다: "+Path.GetFileName(p)); }
 public static void SafeRelative(string p) {
  if(String.IsNullOrWhiteSpace(p)||Path.IsPathRooted(p)||p.IndexOf(':')>=0||p.IndexOf('\0')>=0)throw new Exception("안전하지 않은 파일 경로");
  foreach(string part in p.Replace('\\','/').Split('/')) {
   if(part==""||part=="."||part==".."||part.EndsWith(".")||part.EndsWith(" ")||part.IndexOfAny(Path.GetInvalidFileNameChars())>=0||Regex.IsMatch(part,"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\\.|$)",RegexOptions.IgnoreCase))throw new Exception("안전하지 않은 파일 경로: "+p);
  }
 }
 public static bool Under(string path,string root) { return Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase); }
 public static void NoLinks(string p) { if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new Exception("링크 대신 실제 폴더를 지정해 주세요: "+p); }
 public static void NoLinkParents(string p) { for(string q=Path.GetFullPath(p);!String.IsNullOrEmpty(q);q=Path.GetDirectoryName(q))NoLinks(q); }
 public static void DeleteTree(string p,string allowed) {
  if(!Under(p,allowed))throw new Exception("작업 폴더 밖의 삭제를 거부했습니다.");NoLinks(p);
  if(Directory.Exists(p)) { foreach(string d in Directory.GetDirectories(p))DeleteTree(d,allowed);foreach(string f in Directory.GetFiles(p)){NoLinks(f);File.Delete(f);}Directory.Delete(p); }
  else if(File.Exists(p))File.Delete(p);
 }
 static void Move(string a,string b) { if(Directory.Exists(a))Directory.Move(a,b);else File.Move(a,b); }
 static bool Exists(string p) { return File.Exists(p)||Directory.Exists(p); }
 void Say(string s,int p=-1) { if(Progress!=null)Progress(s,p); }
 void Check() { Cancel.ThrowIfCancellationRequested(); }
 static HttpClient Client() { ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;var c=new HttpClient();c.Timeout=Timeout.InfiniteTimeSpan;c.DefaultRequestHeaders.UserAgent.ParseAdd("FateExtraKoreanLauncher/"+Version);return c; }
 public ReleaseInfo Latest() {
  using(var c=Client())using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(Cancel)) {
   c.Timeout=TimeSpan.FromSeconds(35);
   timeout.CancelAfter(TimeSpan.FromSeconds(35));
   string json;using(var api=c.GetAsync("https://api.github.com/repos/"+Repository+"/releases/latest",timeout.Token).GetAwaiter().GetResult()){api.EnsureSuccessStatusCode();json=api.Content.ReadAsStringAsync().GetAwaiter().GetResult();}var release=Json.Parse(json);
   if(Convert.ToBoolean(release["draft"])||Convert.ToBoolean(release["prerelease"]))throw new Exception("정식 배포본이 아닙니다.");
   string tag=Json.S(release,"tag_name");SafeRelative(tag);if(tag.Contains("/")||tag.Contains("\\"))throw new Exception("잘못된 버전 태그");
   var assets=Json.A(release,"assets").Select(Json.Map).ToArray();var meta=assets.SingleOrDefault(a=>Json.S(a,"name")=="launcher-update.json");
   if(meta==null)throw new Exception("이 배포본에는 런처 업데이트 정보가 없습니다. 배포 페이지를 확인해 주세요.");
   if(Json.N(meta,"size")>2*1024*1024)throw new Exception("업데이트 정보 크기 오류");
   string url=ReleaseRoot+Uri.EscapeDataString(tag)+"/launcher-update.json";
   using(var res=c.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,timeout.Token).GetAwaiter().GetResult()) {
    res.EnsureSuccessStatusCode();using(var stream=res.Content.ReadAsStreamAsync().GetAwaiter().GetResult())using(var ms=new MemoryStream()) {
     var buf=new byte[8192];int n;while((n=stream.ReadAsync(buf,0,buf.Length,timeout.Token).GetAwaiter().GetResult())>0){if(ms.Length+n>2*1024*1024)throw new Exception("업데이트 정보 크기 초과");ms.Write(buf,0,n);}
     var r=ReleaseInfo.Read(Encoding.UTF8.GetString(ms.ToArray()).TrimStart('\uFEFF'),tag);
     foreach(var a in new[]{r.Base}.Concat(r.Hd)) { var remote=assets.SingleOrDefault(x=>Json.S(x,"name")==a.Name);if(remote==null||Json.N(remote,"size")!=a.Size)throw new Exception("릴리스 파일이 아직 준비되지 않았습니다: "+a.Name); }
     return r;
    }
   }
  }
 }
 public string Download(Asset a,string tag) {
  Check();
  SafeRelative(tag);SafeRelative(a.Name);if(tag.IndexOfAny(new[]{'/','\\'})>=0||a.Name.IndexOfAny(new[]{'/','\\'})>=0)throw new Exception("다운로드 경로 오류");
  string dir=Path.Combine(Root,"downloads",tag);NoLinkParents(dir);Directory.CreateDirectory(dir);string dest=Path.Combine(dir,a.Name),part=dest+".partial";NoLinks(dest);NoLinks(part);
  if(File.Exists(dest)){Say("받은 파일 확인: "+a.Name);try{Verify(dest,a.Size,a.Hash);return dest;}catch{File.Move(dest,dest+".damaged-"+Guid.NewGuid().ToString("N"));Say("손상된 다운로드를 다시 받습니다: "+a.Name);}}
  long offset=File.Exists(part)?new FileInfo(part).Length:0;if(offset>a.Size){File.Delete(part);offset=0;}
  if(offset<a.Size)using(var c=Client())using(var req=new HttpRequestMessage(HttpMethod.Get,a.Url)) {
   if(offset>0)req.Headers.Range=new RangeHeaderValue(offset,null);
   using(var res=c.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,Cancel).GetAwaiter().GetResult()) {
    res.EnsureSuccessStatusCode();
    if(res.StatusCode==HttpStatusCode.PartialContent) { if(res.Content.Headers.ContentRange==null||res.Content.Headers.ContentRange.From!=offset)throw new Exception("이어받기 응답 오류"); }
    else offset=0;
    using(var input=res.Content.ReadAsStreamAsync().GetAwaiter().GetResult())using(var output=new FileStream(part,offset==0?FileMode.Create:FileMode.Append,FileAccess.Write,FileShare.Read)) {
     byte[] buf=new byte[1024*1024];long total=offset;var watch=Stopwatch.StartNew();int n;
     while((n=input.ReadAsync(buf,0,buf.Length,Cancel).GetAwaiter().GetResult())>0) { Check();if(total+n>a.Size)throw new Exception("다운로드 파일이 예상보다 큽니다.");output.Write(buf,0,n);total+=n;if(watch.ElapsedMilliseconds>200){Say("다운로드 · "+a.Name+"   "+(total/1048576)+" / "+(a.Size/1048576)+" MB",(int)(total*100/a.Size));watch.Restart();} }
    }
   }
  }
  Say("다운로드 확인: "+a.Name);try{Verify(part,a.Size,a.Hash);}catch{if(File.Exists(part))File.Delete(part);throw;}File.Move(part,dest);return dest;
 }
 public static bool GameRunning() { return Process.GetProcessesByName("PPSSPPWindows64").Length>0||Process.GetProcessesByName("PPSSPPWindows").Length>0; }
 public static string PspRoot(string memory) {string full=Path.GetFullPath(memory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);return String.Equals(Path.GetFileName(full),"PSP",StringComparison.OrdinalIgnoreCase)?full:Path.Combine(full,"PSP");}
 public ProcessStartInfo GameStartInfo() {
  if(GameRunning())throw new Exception("이미 PPSSPP가 실행 중입니다.");
  RecoverPending();
  if(!File.Exists(Config.Emulator)||!File.Exists(Config.GameIso))throw new Exception("PPSSPP 경로와 설치된 본편을 확인해 주세요.");
  if(Config.Memstick=="")throw new Exception("메모리스틱 폴더를 지정해 주세요.");
  NoLinkParents(Config.Memstick);Directory.CreateDirectory(Config.Memstick);string probe=Path.Combine(Config.Memstick,".fate-write-"+Guid.NewGuid().ToString("N"));File.WriteAllText(probe,"");File.Delete(probe);
  string sourceDir=Path.GetDirectoryName(Config.Emulator),assets=Path.Combine(sourceDir,"assets");if(!Directory.Exists(assets))throw new Exception("PPSSPP의 assets 폴더가 없습니다. PPSSPP 배포본 전체를 압축 해제해 주세요.");
  // v1.20.4 has no --memstick option. Use its installed.txt routing in a local
  // copy of the user's emulator; never rewrite the user's original installation.
  string runtime=Path.Combine(Root,"emulator",Hash(Config.Emulator).Substring(0,16));
  NoLinkParents(runtime);
  string exe=Path.Combine(runtime,Path.GetFileName(Config.Emulator));
  if(!Directory.Exists(runtime)) {
   Directory.CreateDirectory(Path.GetDirectoryName(runtime));string stage=runtime+".stage-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(stage);
   try { File.Copy(Config.Emulator,Path.Combine(stage,Path.GetFileName(Config.Emulator)));CopyDirectory(assets,Path.Combine(stage,"assets"));foreach(string dll in Directory.GetFiles(sourceDir,"*.dll"))File.Copy(dll,Path.Combine(stage,Path.GetFileName(dll)));Directory.Move(stage,runtime); }
   finally {if(Directory.Exists(stage))DeleteTree(stage,Path.GetDirectoryName(runtime));}
  }
  if(!File.Exists(exe)||!Directory.Exists(Path.Combine(runtime,"assets")))throw new Exception("런처 내부 PPSSPP 복사본이 불완전합니다. 본편 폴더의 emulator 폴더를 옮긴 뒤 다시 실행해 주세요.");
  File.WriteAllText(Path.Combine(runtime,"installed.txt"),Path.GetFullPath(Config.Memstick),new UTF8Encoding(false));
  return new ProcessStartInfo(exe,Quote(Config.GameIso)){UseShellExecute=false,WorkingDirectory=runtime};
 }
 string Tool(string name) {
  var hashes=Json.Parse(File.ReadAllText(Path.Combine(ToolRoot,"tools.json")));string p=Path.Combine(ToolRoot,name);
  if(Hash(p)!=Json.S(hashes,name))throw new Exception("런처 도구가 손상되었습니다. 런처 ZIP을 다시 풀어 주세요.");return p;
 }
 public static string Quote(string x) { if(x.IndexOf('"')>=0||x.IndexOf('\n')>=0||x.IndexOf('\r')>=0)throw new Exception("인수 경로 오류");return "\""+x.TrimEnd('\\')+"\""; }
 string Run(string exe,string args) {
  var info=new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
  using(var p=Process.Start(info))using(Cancel.Register(()=>{try{if(!p.HasExited)p.Kill();}catch{}})) {
   var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();p.WaitForExit();string o=stdout.Result,e=stderr.Result;Check();if(p.ExitCode!=0)throw new Exception("도구 실행 실패 ("+p.ExitCode+"): "+e.Substring(0,Math.Min(2000,e.Length)));return o;
  }
 }
 public void Extract(string archive,string output,string expectedRoot) {
  Say("압축 확인: "+Path.GetFileName(archive));string seven=Tool("7za.exe");string listing=Run(seven,"l -slt -sccUTF-8 "+Quote(archive));
  int delimiter=listing.IndexOf("----------");if(delimiter<0)throw new Exception("압축 목록을 읽을 수 없습니다.");string entries=listing.Substring(delimiter+10);long bytes=0;int count=0;
  foreach(string raw in entries.Split('\n')) {
   string line=raw.TrimEnd('\r');
   if(line.StartsWith("Path = ")) {string rel=line.Substring(7).Replace('\\','/');SafeRelative(rel);if(rel!=expectedRoot&&!rel.StartsWith(expectedRoot+"/",StringComparison.Ordinal))throw new Exception("압축 내부 폴더 오류: "+rel);if(++count>60000)throw new Exception("압축 파일 수 초과");}
   if(line.StartsWith("Size = ")){long n;if(!Int64.TryParse(line.Substring(7),out n)||n<0)throw new Exception("압축 크기 오류");bytes+=n;}
   if(line=="Encrypted = +"||line.StartsWith("Symbolic Link = ")||line.StartsWith("Hard Link = "))throw new Exception("지원하지 않는 압축 항목");
  }
  if(count==0||bytes>6L*1024*1024*1024)throw new Exception("압축 내용 크기 오류");Directory.CreateDirectory(output);Say("압축 푸는 중: "+Path.GetFileName(archive));Run(seven,"x -y -bso0 -bsp0 -sccUTF-8 "+Quote(archive)+" "+Quote("-o"+output));
  foreach(string p in Directory.EnumerateFileSystemEntries(output,"*",SearchOption.AllDirectories)){if(!Under(p,output))throw new Exception("압축 해제 경로 오류");NoLinks(p);}
 }
 static void CopyDirectory(string source,string dest) {
  Directory.CreateDirectory(dest);foreach(string p in Directory.GetFiles(source)){NoLinks(p);File.Copy(p,Path.Combine(dest,Path.GetFileName(p)));}
  foreach(string p in Directory.GetDirectories(source)){NoLinks(p);CopyDirectory(p,Path.Combine(dest,Path.GetFileName(p)));}
 }
 string JournalPath { get {return Path.Combine(Root,"install-journal.json");} }
 public void RecoverPending() { if(!File.Exists(JournalPath))return;if(GameRunning())throw new Exception("이전 설치 복원이 필요합니다. PPSSPP를 종료해 주세요.");using(var gate=new FileStream(Path.Combine(Root,"install.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))Recover(); }
 void Allowed(string path) { if(!Under(path,Root)&&!(Config.Memstick!=""&&Under(path,Config.Memstick)))throw new Exception("설치 대상 폴더 밖의 작업을 거부했습니다."); }
 public void Recover() {
  if(!File.Exists(JournalPath))return;var j=Json.Serializer().Deserialize<Journal>(File.ReadAllText(JournalPath));
  if(!j.Committed){Rollback(j);Config=Json.Serializer().Deserialize<Settings>(j.PreviousSettings);Json.Write(ConfigPath,Config);}File.Delete(JournalPath);
 }
 void Rollback(Journal j) {
  foreach(var op in Enumerable.Reverse(j.Operations)) {
   Allowed(op.Target);Allowed(op.Backup);Allowed(op.Staged);NoLinkParents(op.Target);NoLinkParents(op.Backup);NoLinkParents(op.Staged);
   if(Exists(op.Backup)){if(Exists(op.Target))DeleteTree(op.Target,Path.GetDirectoryName(op.Target));Move(op.Backup,op.Target);}
   else if(!op.HadOld&&!Exists(op.Staged)&&Exists(op.Target))DeleteTree(op.Target,Path.GetDirectoryName(op.Target));
  }
 }
 Swap Prepare(string target,string src,string id) {
  target=Path.GetFullPath(target);Allowed(target);NoLinkParents(target);string parent=Path.GetDirectoryName(target);Directory.CreateDirectory(parent);
  string staged=target+".fate-stage-"+id,backup=target+".fate-backup-"+id;if(Exists(staged)||Exists(backup))throw new Exception("임시 경로가 이미 있습니다.");
  try {if(Directory.Exists(src))CopyDirectory(src,staged);else File.Copy(src,staged);}catch{if(Exists(staged))DeleteTree(staged,parent);throw;}
  return new Swap{Target=target,Staged=staged,Backup=backup,HadOld=Exists(target)};
 }
 void PrepareHdSetting(Journal journal,string ini,string stage,string id) {
  NoLinkParents(ini);string text=File.Exists(ini)?File.ReadAllText(ini):"[Graphics]\r\n";
  if(Regex.IsMatch(text,@"(?m)^ReplaceTextures\s*="))text=Regex.Replace(text,@"(?m)^ReplaceTextures\s*=.*$","ReplaceTextures = True");
  else if(Regex.IsMatch(text,@"(?m)^\[Graphics\]\s*$"))text=Regex.Replace(text,@"(?m)^\[Graphics\]\s*$","[Graphics]\r\nReplaceTextures = True");
  else text+="\r\n[Graphics]\r\nReplaceTextures = True\r\n";
  File.WriteAllText(stage,text,new UTF8Encoding(false));journal.Operations.Add(Prepare(ini,stage,id));
 }
 public void Install(ReleaseInfo r,bool installBase,bool installHD,bool installCheats,bool installSave) {
  RecoverPending();
  Check();
  if(!installBase&&!installHD&&!installCheats&&!installSave)throw new Exception("설치할 항목을 선택하세요.");
  if(GameRunning())throw new Exception("PPSSPP를 종료한 뒤 업데이트해 주세요.");
  if((installHD||installCheats||installSave)&&String.IsNullOrWhiteSpace(Config.Memstick))throw new Exception("PPSSPP 메모리스틱 폴더를 지정해 주세요.");
  if(Config.Memstick!="") { Config.Memstick=Path.GetFullPath(Config.Memstick);NoLinkParents(Config.Memstick);if(Path.GetPathRoot(Config.Memstick)==Config.Memstick)throw new Exception("드라이브 전체를 메모리스틱으로 지정할 수 없습니다."); }
  if(installHD&&!installBase&&Config.BaseVersion!=r.BaseVersion)throw new Exception("이 HD 팩과 맞는 본편 패치도 함께 선택해 주세요.");
  string psp=Config.Memstick==""?"":PspRoot(Config.Memstick);string existingHD=Path.Combine(psp,"TEXTURES","NPJH50247");
  if(installBase&&!installHD&&Config.Memstick!=""&&Directory.Exists(existingHD)) {string ini=Path.Combine(existingHD,"textures.ini");NoLinkParents(ini);if(!File.Exists(ini)||Hash(ini)!=r.HdIniHash)throw new Exception("이 메모리스틱의 기존 HD 팩과 새 본편의 조합을 확인할 수 없습니다. HD도 함께 선택하거나 HD가 없는 별도 메모리스틱 폴더를 지정해 주세요.");}
  if(installBase){Say("원본 ISO 확인");if(Config.SourceIso=="")throw new Exception("보유한 일본판 원본 ISO를 지정해 주세요.");Verify(Config.SourceIso,SourceSize,SourceHash);}
  using(var gate=new FileStream(Path.Combine(Root,"install.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
   Recover();string id=DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);string work=Path.Combine(Root,"staging",id);Directory.CreateDirectory(work);
   var j=new Journal{PreviousSettings=Json.Serializer().Serialize(Config)};bool journalWritten=false;
   try {
    string packDir="",patch="";Dictionary<string,object> pack=null;
    if(installBase||installCheats||installSave) {
     string zip=Download(r.Base,r.Tag);Extract(zip,Path.Combine(work,"base"),r.BaseRoot);packDir=Path.Combine(work,"base",r.BaseRoot);pack=Json.Parse(File.ReadAllText(Path.Combine(packDir,"manifest.json")));
     if(Json.S(pack,"version")!=r.BaseVersion)throw new Exception("본편 버전 정보 불일치");var source=Json.Map(pack["source"]);var target=Json.Map(pack["target"]);
     if(Json.N(source,"bytes")!=SourceSize||Json.S(source,"sha256")!=SourceHash||Json.N(target,"bytes")!=r.TargetBytes||Json.S(target,"sha256")!=r.TargetHash)throw new Exception("패치 원본/결과 정보 불일치");
     var pi=Json.Map(pack["patch"]);if(Json.S(pi,"path")!=r.PatchName)throw new Exception("패치 이름 불일치");patch=Path.Combine(packDir,r.PatchName);Verify(patch,Json.N(pi,"bytes"),Json.S(pi,"sha256"));
    }
    if(installBase) {
     Check();Say("본편 패치 적용 중 · 원본은 보존됩니다");string iso=Path.Combine(work,"game.iso");Run(Tool("xdelta3.exe"),"-d -s "+Quote(Config.SourceIso)+" "+Quote(patch)+" "+Quote(iso));Say("완성된 본편 확인");Verify(iso,r.TargetBytes,r.TargetHash);
     j.Operations.Add(Prepare(Path.Combine(Root,"games","Fate-Extra-Korean-"+r.BaseVersion+".iso"),iso,id));
    }
    if(installHD) {
     string archive="";foreach(var a in r.Hd){string path=Download(a,r.Tag);if(a.Name.EndsWith(".zip",StringComparison.OrdinalIgnoreCase))archive=path;}
     string extracted=Path.Combine(work,"hd");Extract(archive,extracted,r.HdRoot);string hdRoot=Path.Combine(extracted,r.HdRoot);var hm=Json.Parse(File.ReadAllText(Path.Combine(hdRoot,"manifest.json")));
     if(Json.S(hm,"base_iso_sha256")!=r.TargetHash||Json.S(hm,"base_version")!=r.BaseVersion)throw new Exception("본편과 HD 버전이 다릅니다.");
     foreach(var f in Json.Map(hm["files"])) {SafeRelative(f.Key);string path=Path.Combine(hdRoot,f.Key.Replace('/',Path.DirectorySeparatorChar));if(!File.Exists(path)||new FileInfo(path).Length!=Json.N(Json.Map(f.Value),"bytes"))throw new Exception("HD 파일 누락: "+f.Key);}
     j.Operations.Add(Prepare(Path.Combine(psp,"TEXTURES","NPJH50247"),Path.Combine(hdRoot,"NPJH50247"),id));
     PrepareHdSetting(j,Path.Combine(psp,"SYSTEM","ppsspp.ini"),Path.Combine(work,"ppsspp.ini"),id);
     string gameIni=Path.Combine(psp,"SYSTEM","NPJH50247_ppsspp.ini");if(File.Exists(gameIni))PrepareHdSetting(j,gameIni,Path.Combine(work,"game-ppsspp.ini"),id);
    }
    if(installCheats||installSave)j.Operations.Add(Prepare(Path.Combine(Root,"Extras",r.BaseVersion),Path.Combine(packDir,"Extras"),id));
    if(installCheats) {string src=Path.Combine(packDir,"Extras","Cheats","PPSSPP","NPJH50247.ini");if(Regex.IsMatch(File.ReadAllText(src),@"(?m)^_C[12] "))throw new Exception("켜진 치트가 포함되어 있습니다.");j.Operations.Add(Prepare(Path.Combine(psp,"Cheats","NPJH50247.ini"),src,id));}
    if(installSave) {string src=Path.Combine(packDir,"Extras","Clear-Save","NPJH50247DATA80");foreach(string f in new[]{"PARAM.SFO","ICON0.PNG","PIC1.PNG","SECURE.BIN"})if(!File.Exists(Path.Combine(src,f)))throw new Exception("클리어 세이브 파일 누락");j.Operations.Add(Prepare(Path.Combine(psp,"SAVEDATA","NPJH50247DATA80"),src,id));}
    Check();if(GameRunning())throw new Exception("PPSSPP가 실행되어 적용을 미뤘습니다. 종료 후 다시 눌러 주세요.");
    Say("설치 적용 중 · 기존 파일은 옆에 백업됩니다");Json.Write(JournalPath,j);journalWritten=true;
    foreach(var op in j.Operations){if(op.HadOld)Move(op.Target,op.Backup);Move(op.Staged,op.Target);}
    if(installBase){Config.BaseVersion=r.BaseVersion;Config.GameIso=Path.Combine(Root,"games","Fate-Extra-Korean-"+r.BaseVersion+".iso");}
    if(installHD){Config.HdVersion=r.HdVersion;Config.HdBaseVersion=r.BaseVersion;}
    if(installCheats)Config.CheatsVersion=r.BaseVersion;if(installSave)Config.SaveVersion=r.BaseVersion;
    Json.Write(ConfigPath,Config);j.Committed=true;Json.Write(JournalPath,j);File.Delete(JournalPath);Say("선택한 항목 설치 완료",100);
   } catch {
    if(journalWritten){Rollback(j);Config=Json.Serializer().Deserialize<Settings>(j.PreviousSettings);Json.Write(ConfigPath,Config);if(File.Exists(JournalPath))File.Delete(JournalPath);}throw;
   } finally {
    // Delete only paths prepared by this attempt; backups and user saves remain.
    foreach(var op in j.Operations)if(Exists(op.Staged))DeleteTree(op.Staged,Path.GetDirectoryName(op.Staged));
    if(Directory.Exists(work))DeleteTree(work,Path.Combine(Root,"staging"));
   }
  }
 }
}
}
