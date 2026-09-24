using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace FateLauncher {
public sealed class DiscoveryResult {
 public List<string> Isos=new List<string>(),Emulators=new List<string>(),Memsticks=new List<string>();
 public Dictionary<string,string> EmulatorMemsticks=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
 public int Directories;public bool Limited;
 public void Merge(DiscoveryResult other) {
  foreach(var pair in new[]{Tuple.Create(Isos,other.Isos),Tuple.Create(Emulators,other.Emulators),Tuple.Create(Memsticks,other.Memsticks)})foreach(string p in pair.Item2)if(!pair.Item1.Contains(p,StringComparer.OrdinalIgnoreCase))pair.Item1.Add(p);
  foreach(var pair in other.EmulatorMemsticks)EmulatorMemsticks[pair.Key]=pair.Value;Directories+=other.Directories;Limited|=other.Limited;
 }
 public void Prefer64Bit() {foreach(string p in Emulators.ToArray())if(Path.GetFileName(p).Equals("PPSSPPWindows.exe",StringComparison.OrdinalIgnoreCase)&&Emulators.Contains(Path.Combine(Path.GetDirectoryName(p),"PPSSPPWindows64.exe"),StringComparer.OrdinalIgnoreCase))Emulators.Remove(p);}
}
// Read-only discovery. Never executes candidates, follows directory links, or
// hashes whole disc images during startup. The installer still verifies ISO SHA.
public sealed class Discovery {
 readonly CancellationToken cancel;readonly Stopwatch watch=Stopwatch.StartNew();readonly DiscoveryResult found=new DiscoveryResult();
 readonly Dictionary<string,int> seen=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
 readonly string documents;readonly int maxDirs,maxSeconds;
 public Discovery(CancellationToken token,string documentsFolder=null,int directoryLimit=2500,int seconds=15) {cancel=token;documents=documentsFolder??Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);maxDirs=directoryLimit;maxSeconds=seconds;}
 static bool Local(string p) {try{if(String.IsNullOrWhiteSpace(p)||!Path.IsPathRooted(p)||p.StartsWith("\\\\")||p.StartsWith("//"))return false;var kind=new DriveInfo(Path.GetPathRoot(p)).DriveType;return kind==DriveType.Fixed||kind==DriveType.Removable;}catch{return false;}}
 static bool Plain(string p) {try{for(string current=Path.GetFullPath(p);!String.IsNullOrEmpty(current);current=Path.GetDirectoryName(current))if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)return false;return true;}catch{return false;}}
 static void Add(List<string> list,string p) {p=Path.GetFullPath(p);if(!list.Contains(p,StringComparer.OrdinalIgnoreCase))list.Add(p);}
 bool Check() {cancel.ThrowIfCancellationRequested();if(found.Directories>=maxDirs||watch.Elapsed.TotalSeconds>=maxSeconds){found.Limited=true;return false;}return true;}
 static byte[] ReadAt(FileStream f,long offset,int count) {if(offset<0||count<0||offset+count>f.Length)throw new IOException();var b=new byte[count];f.Position=offset;int n=0,k;while(n<count&&(k=f.Read(b,n,count-n))>0)n+=k;if(n!=count)throw new IOException();return b;}
 static uint U32(byte[] b,int p) {return BitConverter.ToUInt32(b,p);}
 static byte[] Child(FileStream f,byte[] directory,string name) {
  uint size=U32(directory,10);if(size==0||size>1024*1024)throw new IOException();var block=ReadAt(f,(long)U32(directory,2)*2048,(int)size);
  for(int p=0;p<block.Length;) {int len=block[p];if(len==0){p=(p/2048+1)*2048;continue;}if(len<34||p+len>block.Length)throw new IOException();int n=block[p+32];if(33+n>len)throw new IOException();string id=Encoding.ASCII.GetString(block,p+33,n).Split(';')[0];if(id==name){var record=new byte[len];Array.Copy(block,p,record,0,len);return record;}p+=len;}throw new IOException();
 }
 public static bool IsOriginalCandidate(string p) {
  try {if(!Local(p)||!Plain(p)||new FileInfo(p).Length!=Engine.SourceSize)return false;
   using(var f=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)) {
    var volume=ReadAt(f,16*2048,2048);if(volume[0]!=1||Encoding.ASCII.GetString(volume,1,5)!="CD001")return false;var root=new byte[34];Array.Copy(volume,156,root,0,34);
    var game=Child(f,root,"PSP_GAME");var entry=Child(f,game,"PARAM.SFO");uint bytes=U32(entry,10);if(bytes<20||bytes>65536)return false;var sfo=ReadAt(f,(long)U32(entry,2)*2048,(int)bytes);if(U32(sfo,0)!=0x46535000)return false;
    uint keys=U32(sfo,8),data=U32(sfo,12),count=U32(sfo,16);if(count>128)return false;string id="",version="";
    for(int i=0;i<count;i++){int pos=20+i*16;if(pos+16>sfo.Length)return false;long key=(long)keys+BitConverter.ToUInt16(sfo,pos),value=(long)data+U32(sfo,pos+12),len=U32(sfo,pos+4);if(key>=sfo.Length||value+len>sfo.Length||len>1024)return false;int end=(int)key;while(end<sfo.Length&&sfo[end]!=0)end++;string k=Encoding.ASCII.GetString(sfo,(int)key,end-(int)key),v=Encoding.UTF8.GetString(sfo,(int)value,(int)len).TrimEnd('\0');if(k=="DISC_ID")id=v;if(k=="DISC_VERSION")version=v;}
    return id=="NPJH50247"&&version=="1.01";
   }
  }catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}catch(ArgumentException){return false;}catch(OverflowException){return false;}
 }
 public static bool IsEmulator(string path) {
  try {string name=Path.GetFileName(path);if(!name.Equals("PPSSPPWindows64.exe",StringComparison.OrdinalIgnoreCase)&&!name.Equals("PPSSPPWindows.exe",StringComparison.OrdinalIgnoreCase))return false;string assets=Path.Combine(Path.GetDirectoryName(path),"assets");if(!Local(path)||!Plain(path)||!Directory.Exists(assets)||!Plain(assets))return false;using(var f=File.OpenRead(path)){var dos=ReadAt(f,0,64);if(dos[0]!='M'||dos[1]!='Z')return false;uint pe=U32(dos,60);if(pe<64||pe>1024*1024)return false;return U32(ReadAt(f,pe,4),0)==0x00004550;}}catch{return false;}
 }
 public static string MemoryFor(string exe,string docs) {
  try {if(!Local(exe)||!Plain(exe))return "";string folder=Path.GetDirectoryName(exe),marker=Path.Combine(folder,"installed.txt"),memory;
   if(File.Exists(marker)){if(!Plain(marker)||new FileInfo(marker).Length>16384)return "";memory=File.ReadAllText(marker).TrimStart('\uFEFF').Split('\n')[0].Trim();if(memory=="")memory=Path.Combine(docs,"PPSSPP");else if(!Path.IsPathRooted(memory))memory=Path.GetFullPath(Path.Combine(folder,memory));}
   else memory=Path.Combine(folder,"memstick");
   return Local(memory)&&Directory.Exists(memory)&&Plain(memory)?Path.GetFullPath(memory):"";
  }catch{return "";}
 }
 void Emulator(string path) {if(!IsEmulator(path)||found.Emulators.Contains(Path.GetFullPath(path),StringComparer.OrdinalIgnoreCase))return;Add(found.Emulators,path);string memory=MemoryFor(path,documents);if(memory!=""){found.EmulatorMemsticks[Path.GetFullPath(path)]=memory;Memory(memory);}}
 void Memory(string p) {
  if(!Local(p)||!Directory.Exists(p)||!Plain(p))return;string system=Path.Combine(Engine.PspRoot(p),"SYSTEM","ppsspp.ini");if(!File.Exists(system)||!Plain(system))return;Add(found.Memsticks,p);
  try {if(new FileInfo(system).Length>2*1024*1024)return;bool recent=false;int hints=0;foreach(string line in File.ReadLines(system)) {if(!Check())break;string t=line.Trim();if(t.StartsWith("[")){recent=t=="[Recent]";continue;}if(!recent||!t.StartsWith("FileName",StringComparison.Ordinal))continue;int eq=t.IndexOf('=');if(eq<0)continue;string file=t.Substring(eq+1).Trim();if(!Local(file))continue;if(IsOriginalCandidate(file))Add(found.Isos,file);if(++hints<=20)Walk(Path.GetDirectoryName(file),0);}}
  catch(IOException){}catch(UnauthorizedAccessException){}
 }
 static bool Skip(string path) {string n=Path.GetFileName(path);return n.StartsWith(".")||new[]{"Windows","$Recycle.Bin","System Volume Information","node_modules",".git","TEXTURES","SAVEDATA","cache","staging"}.Contains(n,StringComparer.OrdinalIgnoreCase);}
 void ProbeMemory(string directory) {if(Path.GetFileName(directory).Equals("PSP",StringComparison.OrdinalIgnoreCase))Memory(Path.GetDirectoryName(directory));}
 void Walk(string root,int depth) {
  if(!Check()||!Local(root)||!Directory.Exists(root)||!Plain(root))return;string full=Path.GetFullPath(root);int previous;if(seen.TryGetValue(full,out previous)&&previous>=depth)return;seen[full]=depth;found.Directories++;
  try {
   ProbeMemory(root);
   foreach(string p in Directory.EnumerateFiles(root)) {if(!Check())return;string name=Path.GetFileName(p);if(name.Equals("PPSSPPWindows64.exe",StringComparison.OrdinalIgnoreCase)||name.Equals("PPSSPPWindows.exe",StringComparison.OrdinalIgnoreCase))Emulator(p);else if(name.EndsWith(".iso",StringComparison.OrdinalIgnoreCase)&&IsOriginalCandidate(p))Add(found.Isos,p);}
   if(depth>0)foreach(string p in Directory.EnumerateDirectories(root)){if(!Check())return;if(!Skip(p))Walk(p,depth-1);}
  }catch(IOException){}catch(UnauthorizedAccessException){}
 }
 public DiscoveryResult Search(IEnumerable<string> roots,int depth,Settings settings=null) {
  if(settings!=null){if(IsOriginalCandidate(settings.SourceIso))Add(found.Isos,settings.SourceIso);Emulator(settings.Emulator);if(settings.Memstick!="")Memory(settings.Memstick);}
  foreach(string root in roots){if(!Check())break;Walk(root,depth);}
  found.Prefer64Bit();return found;
 }
 public static string[] LocalDrives() {var roots=new List<string>();foreach(var drive in DriveInfo.GetDrives())try{if((drive.DriveType==DriveType.Fixed||drive.DriveType==DriveType.Removable)&&drive.IsReady)roots.Add(drive.RootDirectory.FullName);}catch(IOException){}return roots.ToArray();}
 public static string[] DeviceRoots(string launcherRoot) {var roots=new List<string>();string path=Path.GetFullPath(launcherRoot);for(int i=0;i<5&&!String.IsNullOrEmpty(path);i++,path=Path.GetDirectoryName(path))roots.Add(path);roots.AddRange(LocalDrives());return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();}
 // Breadth-first traversal gives every drive an equal chance to reach deep
 // portable installs. Check only emulator/config names, not every ISO or asset.
 public DiscoveryResult Devices(IEnumerable<string> roots,int maxDepth=32,Action<int> progress=null) {
  var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var queue=new Queue<Tuple<string,int>>();foreach(string root in roots)if(Local(root))queue.Enqueue(Tuple.Create(root,0));
  while(queue.Count>0&&Check()) {
   var item=queue.Dequeue();string path=item.Item1;if(!Directory.Exists(path)||!Plain(path))continue;string full=Path.GetFullPath(path);if(!visited.Add(full))continue;found.Directories++;
   try {
    Emulator(Path.Combine(path,"PPSSPPWindows64.exe"));Emulator(Path.Combine(path,"PPSSPPWindows.exe"));ProbeMemory(path);
    if(item.Item2<maxDepth)foreach(string child in Directory.EnumerateDirectories(path)) {if(!Check())break;string name=Path.GetFileName(child);if(!Skip(child)&&!new[]{"assets","venv","__pycache__","WinSxS","WindowsApps","KoreanHD","ShaderCache"}.Contains(name,StringComparer.OrdinalIgnoreCase))queue.Enqueue(Tuple.Create(child,item.Item2+1));}
   }catch(IOException){}catch(UnauthorizedAccessException){}
   if(progress!=null&&found.Directories%500==0)progress(found.Directories);
  }
  found.Prefer64Bit();return found;
 }
 public static string HistoryExecutable(string name) {
  if(String.IsNullOrEmpty(name))return "";
  var match=Regex.Match(name,@"^(.*[\\/]PPSSPPWindows(?:64)?\.exe)(?:\.(?:FriendlyAppName|ApplicationCompany))?$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  return match.Success&&Local(match.Groups[1].Value)?match.Groups[1].Value:"";
 }
 static string[] WindowsEmulatorHistory() {
  var candidates=new List<string>();
  foreach(string location in new[]{@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store",@"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache"})
   try {using(var key=Registry.CurrentUser.OpenSubKey(location,false))if(key!=null)foreach(string name in key.GetValueNames()){string path=HistoryExecutable(name);if(path!=""&&!candidates.Contains(path,StringComparer.OrdinalIgnoreCase))candidates.Add(path);}}
   catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}catch(IOException){}
  return candidates.ToArray();
 }
 public DiscoveryResult Common(string launcherRoot,Settings settings,IEnumerable<string> history=null) {
  // Use existing per-user execution hints before bounded tree traversal. Paths
  // are validated as local PE/assets candidates; registry values are not read.
  foreach(string entry in history??WindowsEmulatorHistory()){if(!Check())break;string path=HistoryExecutable(entry);if(path!="")Emulator(path);}
  var roots=new List<string>{launcherRoot};string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
  foreach(string p in new[]{"Downloads","Desktop","Games","Emulators"})roots.Add(Path.Combine(profile,p));
  roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));roots.Add(Path.Combine(documents,"PPSSPP"));
  foreach(string special in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)})if(special!="")roots.Add(Path.Combine(special,"PPSSPP"));
  foreach(var drive in DriveInfo.GetDrives())try{if((drive.DriveType==DriveType.Fixed||drive.DriveType==DriveType.Removable)&&drive.IsReady)foreach(string name in new[]{"DOWN","Downloads","Games","Emulators","PSP","PPSSPP","ROMs"})roots.Add(Path.Combine(drive.RootDirectory.FullName,name));}catch(IOException){}
  foreach(string name in new[]{"PPSSPPWindows64","PPSSPPWindows"})foreach(var process in Process.GetProcessesByName(name))using(process)try{Emulator(process.MainModule.FileName);}catch{}
  Memory(Path.Combine(documents,"PPSSPP"));return Search(roots.Distinct(StringComparer.OrdinalIgnoreCase),3,settings);
 }
}
}
