using System;using System.IO;using System.Linq;using System.Text;using System.Net;using System.Reflection;using System.Security.Cryptography;using System.Threading;using System.Collections.Generic;using FateLauncher;
public static class ExternalChecks {
 static int count;static void Check(bool b,string s){if(!b)throw new Exception(s);count++;Console.WriteLine("PASS "+s);}
 static void Reject(Action a,string s){try{a();}catch{Check(true,s);return;}throw new Exception("accepted: "+s);}
 public static int Main(string[] a){try{Run(a);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static void Run(string[] a){string w=a[0],source=a[1],feed=a[2],reference=a[3];Directory.CreateDirectory(w);
  var release=ReleaseInfo.Read(File.ReadAllText(feed),"v8h-hd-v54-ui-v4");string fixture=Path.Combine(w,"external-install");Check(!Directory.Exists(fixture),"fresh installation fixture");Directory.CreateDirectory(fixture);
  var c=new Settings{SourceIso=source,Emulator=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(w)),"tools","ppsspp-1.20.4","PPSSPPWindows64.exe"),DataRoot=Path.Combine(fixture,"Data"),Memstick=Path.Combine(fixture,"memstick"),UseExternalData=true};
  string settings=Path.Combine(fixture,"launcher-settings.json"),tools=Path.Combine(w,"tools");
  var engine=new Engine(c,settings,tools);typeof(Engine).GetField("RunningCheck",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(engine,new Func<bool>(()=>false));
  engine.Progress=(s,p)=>{if(p<0||p%20==0)Console.WriteLine(s+" "+p);};
  string cache=Path.Combine(c.DataRoot,"downloads",release.Tag);Directory.CreateDirectory(cache);File.Copy(Path.Combine(Path.GetDirectoryName(feed),release.Base.Name),Path.Combine(cache,release.Base.Name));
  string sentinel=Path.Combine(c.Memstick,"PSP","SAVEDATA","keep.txt");Directory.CreateDirectory(Path.GetDirectoryName(sentinel));File.WriteAllText(sentinel,"keep");
  engine.Install(release,true,false,false,false);
  Check(c.GameIso==""&&c.GameData!=""&&Engine.HasGame(c),"external mode installed and runnable");Check(Directory.GetFiles(fixture,"*.iso",SearchOption.AllDirectories).Length==0,"no ISO file generated, including staging/downloads");Check(File.ReadAllText(sentinel)=="keep","existing save preserved");
  Check(Engine.Hash(c.GameData)==c.GameDataHash&&c.GameTargetHash==release.TargetHash,"installed index and release binding");
  using(var image=new ExternalImage(source,c.GameData,release.TargetHash))using(var original=File.OpenRead(reference)) {
   image.VerifyTarget(CancellationToken.None);Check(true,"entire virtual image SHA256 equals frozen v8h");
   var positions=new List<long>{0,1,2047,2048,32768,image.Info.TargetBytes-65536};positions.AddRange(image.Info.Extents.SelectMany(e=>new[]{Math.Max(0,e.Start-17),e.Start}));var rng=new Random(4815);for(int i=0;i<128;i++)positions.Add((long)(rng.NextDouble()*(image.Info.TargetBytes-65536)));
   foreach(long p in positions.Distinct()){int n=(int)Math.Min(65536,image.Info.TargetBytes-p);byte[] actual=new byte[n],expected=new byte[n];image.ReadAt(p,actual,n);original.Position=p;int read=original.Read(expected,0,n);Check(read==n&&actual.SequenceEqual(expected),"range byte match "+p);}
   using(var server=new ExternalServer(image)) {
    var head=(HttpWebRequest)WebRequest.Create(server.Url);head.Proxy=null;head.Method="HEAD";using(var response=(HttpWebResponse)head.GetResponse()){Check(response.ContentLength==image.Info.TargetBytes&&response.Headers["Accept-Ranges"]=="bytes","HTTP HEAD range metadata");}
    long p=image.Info.Extents.Count>1?Math.Max(0,image.Info.Extents[1].Start-9):32759;int n=65536;var req=(HttpWebRequest)WebRequest.Create(server.Url);req.Proxy=null;req.AddRange(p,p+n-1);using(var response=(HttpWebResponse)req.GetResponse())using(var ms=new MemoryStream()){response.GetResponseStream().CopyTo(ms);var expected=new byte[n];image.ReadAt(p,expected,n);Check(response.StatusCode==HttpStatusCode.PartialContent&&ms.ToArray().SequenceEqual(expected),"HTTP cross-extent read exact");}
    Reject(()=>{var bad=(HttpWebRequest)WebRequest.Create(server.Url);bad.Proxy=null;bad.AddRange(image.Info.TargetBytes,image.Info.TargetBytes+7);using(bad.GetResponse()){}},"HTTP beyond end rejected");
    Reject(()=>{var bad=(HttpWebRequest)WebRequest.Create(server.Url+"/../manifest.json");bad.Proxy=null;using(bad.GetResponse()){}},"HTTP unknown path rejected");
   }
   Reject(()=>image.ReadAt(-1,new byte[1],1),"negative virtual offset rejected");Reject(()=>image.ReadAt(image.Info.TargetBytes,new byte[1],1),"out of range virtual read rejected");
  }
  string badIndex=Path.Combine(Path.GetDirectoryName(c.GameData),"bad-index.json");var m=Json.Serializer().Deserialize<ExternalManifest>(File.ReadAllText(c.GameData));m.Extents[0].Start=1;Json.Write(badIndex,m);Reject(()=>{using(var bad=new ExternalImage(source,badIndex,release.TargetHash)){}},"manifest gap rejected");File.Delete(badIndex);
  m=Json.Serializer().Deserialize<ExternalManifest>(File.ReadAllText(c.GameData));m.Extents[0].Offset=Int64.MaxValue;Json.Write(badIndex,m);Reject(()=>{using(var bad=new ExternalImage(source,badIndex,release.TargetHash)){}},"manifest source overflow rejected");File.Delete(badIndex);
  string badSource=Path.Combine(fixture,"bad-source.bin");File.WriteAllText(badSource,"bad");Reject(()=>{using(var bad=new ExternalImage(badSource,c.GameData,release.TargetHash)){}},"wrong original refused");
  c.UseExternalData=false;Check(Engine.HasGame(c)&&c.GameData!="","changing selected install mode preserves existing launch mode");Json.Write(settings,c);
  Check(Engine.Hash(source)==Engine.SourceHash,"source ISO remains unchanged");
  Json.Write(Path.Combine(w,"external-checks.json"),new{status="PASS",assertions=count,target_sha256=release.TargetHash,generated_iso_files=0,source_preserved=true,settings=settings,manifest=c.GameData,manifest_hash=c.GameDataHash,payload_bytes=new FileInfo(Path.Combine(Path.GetDirectoryName(c.GameData),"patch.bin")).Length,ppsspp_runtime_tested=false});
 }
}
