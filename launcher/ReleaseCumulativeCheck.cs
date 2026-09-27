using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using FateLauncher;

class ReleaseCumulativeCheck {
 static void Check(bool b,string label){if(!b)throw new Exception(label);Console.WriteLine("PASS "+label);}
 static int Main(string[] a){Console.OutputEncoding=new UTF8Encoding(false);try{Run(a);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static void Run(string[] a){
  string mode=a[0],w=Path.GetFullPath(a[1]),feed=a[2],source=a[3],expectedTag=a[4];
  Check(mode=="local"||mode=="online","known mode");
  string root=Path.Combine(w,"install-test-"+mode);
  Check(!Directory.Exists(root),"fresh isolated fixture");Directory.CreateDirectory(root);
  string psp=Path.Combine(root,"memstick","PSP");Directory.CreateDirectory(Path.Combine(psp,"SYSTEM"));
  string global=Path.Combine(psp,"SYSTEM","ppsspp.ini"),save=Path.Combine(psp,"SAVEDATA","TEST-KEEP","sentinel.bin"),cheat=Path.Combine(psp,"Cheats","OTHER-GAME.ini");
  Directory.CreateDirectory(Path.GetDirectoryName(save));Directory.CreateDirectory(Path.GetDirectoryName(cheat));
  File.WriteAllText(global,"[CPU]\r\nCPUCore = 0\r\n[Sound]\r\nAudioBackend = 1\r\n");
  File.WriteAllText(save,"isolated existing save fixture");File.WriteAllText(cheat,"isolated unrelated cheat fixture");
  var protectedPaths=new[]{global,save,cheat};var hashes=protectedPaths.Select(Engine.Hash).ToArray();
  string gameIni=Path.Combine(psp,"SYSTEM","NPJH50247_ppsspp.ini");
  File.WriteAllText(gameIni,"[CPU]\r\nCPUCore = 0\r\n[Sound]\r\nReverbVolume = 73\r\n[General]\r\nEnableCheats = False\r\n");
  var settings=new Settings{SourceIso=source,DataRoot=Path.Combine(root,"Data"),Memstick=Path.Combine(root,"memstick"),SelectBase=true,SelectHD=true,SelectUI=true};
  var engine=new Engine(settings,Path.Combine(root,"launcher-settings.json"),Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tools"));
  Check(Path.GetFullPath(settings.DataRoot).StartsWith(root+Path.DirectorySeparatorChar)&&Path.GetFullPath(settings.Memstick).StartsWith(root+Path.DirectorySeparatorChar),"all writable game targets isolated");
  // The user's live emulator is unrelated to this empty fixture. Suppress only
  // this Engine instance's process guard; use the unmodified release assembly.
  typeof(Engine).GetField("RunningCheck",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(engine,new Func<bool>(()=>false));
  var timer=Stopwatch.StartNew();long previous=-10000;string last="";
  engine.Progress=(s,p)=>{if(p<0||s!=last&&timer.ElapsedMilliseconds-previous>5000||p==100){Console.WriteLine(s);previous=timer.ElapsedMilliseconds;last=s;}};
  ReleaseInfo release;
  if(mode=="local"){
   release=ReleaseInfo.Read(File.ReadAllText(feed),expectedTag);
   string cache=Path.Combine(settings.DataRoot,"downloads",release.Tag);Directory.CreateDirectory(cache);
   foreach(var asset in new[]{release.Base,release.Ui}.Concat(release.Hd)) File.Copy(Path.Combine(Path.GetDirectoryName(feed),asset.Name),Path.Combine(cache,asset.Name));
  }else{
   Check(!Directory.Exists(Path.Combine(settings.DataRoot,"downloads")),"empty public download cache");
   release=engine.Latest();
  }
  Check(release.Tag==expectedTag,"latest release tag");
  engine.Install(release,true,true,false,false,true,false);
  Check(settings.BaseVersion=="v8g","installed base version");Engine.Verify(settings.GameIso,release.TargetBytes,release.TargetHash);
  Check(protectedPaths.Select((p,i)=>Engine.Hash(p)==hashes[i]).All(x=>x),"global settings, savedata and unrelated cheats preserved");
  Check(settings.CheatsVersion==""&&settings.SaveVersion==""&&!File.Exists(Path.Combine(psp,"Cheats","NPJH50247.ini")),"unselected extras absent");
  {
   Check(settings.HdVersion=="HD-v53"&&settings.UiVersion=="UI-v3","HD and optional Korean UI installed");
   Check(Engine.Hash(Path.Combine(psp,"TEXTURES","NPJH50247","textures.ini"))==release.UiKoreanHash,"Korean UI mapping");
   string ini=File.ReadAllText(gameIni);
   Check(PpssppSettings.Get(ini,"CPU","CPUCore")=="1","CPU JIT correction");
   Check(PpssppSettings.Get(ini,"Graphics","InternalResolution")=="8"&&PpssppSettings.Get(ini,"Graphics","MultiSampleLevel")=="2"&&PpssppSettings.Get(ini,"Graphics","VerticalSync")=="True"&&PpssppSettings.Get(ini,"Graphics","ReplaceTextures")=="True","requested graphics defaults");
   Check(PpssppSettings.Get(ini,"Sound","ReverbVolume")=="73"&&PpssppSettings.Get(ini,"General","EnableCheats")=="False","audio and unselected cheat setting preserved");
   Check(settings.CpuDefaultsProfile==psp,"CPU migration recorded");
  }
  string textures=Path.Combine(psp,"TEXTURES","NPJH50247");
  var expected=Json.Parse(File.ReadAllText(a[5]));
  foreach(var f in expected) {var spec=Json.Map(f.Value);Engine.Verify(Path.Combine(textures,f.Key),Json.N(spec,"bytes"),Json.S(spec,"sha256"));}
  Check(true,"all expected installed PNG hashes: "+expected.Count);
  var artwork=Directory.GetFiles(textures,"*.png",SearchOption.AllDirectories).ToDictionary(p=>p,Engine.Hash);
  engine.Install(release,false,false,false,false,false,true);
  Check(settings.UiVersion==""&&Engine.Hash(Path.Combine(textures,"textures.ini"))==release.UiOriginalHash,"UI off restores original English mapping");
  Check(artwork.All(p=>File.Exists(p.Key)&&Engine.Hash(p.Key)==p.Value),"UI off preserves every installed PNG");
  engine.Install(release,false,false,false,false,true,false);
  Check(settings.UiVersion=="UI-v3"&&Engine.Hash(Path.Combine(textures,"textures.ini"))==release.UiKoreanHash,"UI on restores Korean mapping");
  Check(artwork.All(p=>File.Exists(p.Key)&&Engine.Hash(p.Key)==p.Value),"UI on preserves every installed PNG");
  Check(protectedPaths.Select((p,i)=>Engine.Hash(p)==hashes[i]).All(x=>x),"protected fixtures preserved through both UI transitions");
  Json.Write(Path.Combine(w,"install-"+mode+"-check.json"),new{status="PASS",mode=mode,release=release.Tag,launcher_assembly_sha256=Engine.Hash(typeof(Engine).Assembly.Location),seconds=timer.Elapsed.TotalSeconds,base_iso_sha256=release.TargetHash,hd=true,optional_ui=true,ui_off_on=true,verified_pngs=expected.Count,downloaded_from_public_github=mode=="online",empty_cache=mode=="online",live_game_touched=false,real_emulator_started=false,isolated_process_guard_override=true,ui_clicks_tested=false});
 }
}
