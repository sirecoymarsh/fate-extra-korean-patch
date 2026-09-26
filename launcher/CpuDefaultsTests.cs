using System;using System.IO;using System.Linq;using FateLauncher;
public static class CpuDefaultsTests {
 static int tests;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);tests++;Console.WriteLine("PASS "+name);}
 static string Get(string s,string section,string key){return PpssppSettings.Get(s,section,key);}
 static void Reject(Action action,string name){try{action();}catch{Check(true,name);return;}throw new Exception("Accepted: "+name);}
 public static void Main(string[] args){try{Run(Path.GetFullPath(args[0]));}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}}
 static void Run(string w){
  string baseText="[CPU]\r\nCPUSpeed = 0\r\n[Graphics]\r\nInternalResolution = 10\r\n[Sound]\r\nReverbRelativeVolume = 100\r\nGameVolume = 73\r\n[General]\r\nEnableCheats = False\r\n";
  foreach(string core in new[]{"","0","2","1","3"}) {
   string input=core==""?baseText:PpssppSettings.Set(baseText,"CPU","CPUCore",core);
   string result=PpssppSettings.Preset(input,false,false),expected=core=="3"?"3":"1";
   Check(Get(result,"CPU","CPUCore")==expected,"core "+core+" -> "+expected);
   Check(Get(result,"Graphics","InternalResolution")=="10"&&Get(result,"Sound","ReverbRelativeVolume")=="100"&&Get(result,"Sound","GameVolume")=="73"&&Get(result,"General","EnableCheats")=="False"&&Get(result,"CPU","CPUSpeed")=="0","CPU-only preserves unrelated settings "+core);
   Check(PpssppSettings.Preset(result,false,false)==result,"CPU preset idempotent "+core);
  }
  string root=Path.Combine(w,"cpu-migration"),memory=Path.Combine(root,"memory"),sys=Path.Combine(memory,"PSP","SYSTEM");Directory.CreateDirectory(sys);
  string global=Path.Combine(sys,"ppsspp.ini"),game=Path.Combine(sys,"NPJH50247_ppsspp.ini"),configPath=Path.Combine(root,"settings.json");
  string original=PpssppSettings.Set(baseText,"CPU","CPUCore","0");File.WriteAllText(global,original);File.WriteAllText(game,original+"[ControlMapping]\r\nCross = CUSTOM\r\n");string before=File.ReadAllText(game);
  string cheats=Path.Combine(memory,"PSP","Cheats","NPJH50247.ini");Directory.CreateDirectory(Path.GetDirectoryName(cheats));File.WriteAllText(cheats,"_C1 user-selected-code\r\n_L 0x00000000 0x00000000\r\n");
  string fake=Path.Combine(root,"fake-emulator");Directory.CreateDirectory(Path.Combine(fake,"assets"));File.WriteAllText(Path.Combine(fake,"PPSSPPWindows64.exe"),"not an executable, never launched");File.WriteAllText(Path.Combine(root,"fake.iso"),"not a disc, never launched");
  var cfg=new Settings{DataRoot=Path.Combine(root,"data"),Memstick=memory,GraphicsDefaultsApplied=true,Emulator=Path.Combine(fake,"PPSSPPWindows64.exe"),GameIso=Path.Combine(root,"fake.iso")};
  Json.Write(configPath,cfg);var engine=new Engine(Settings.Load(configPath),configPath,Path.Combine(w,"build","tools"));engine.RunningCheck=()=>false;
  var start=engine.GameStartInfo();string migrated=File.ReadAllText(game);
  Check(Get(migrated,"CPU","CPUCore")=="1","old installed profile migrates on game-launch preparation");
  Check(Get(migrated,"Graphics","InternalResolution")=="10"&&Get(migrated,"Sound","GameVolume")=="73"&&Get(migrated,"Sound","ReverbRelativeVolume")=="100"&&Get(migrated,"General","EnableCheats")=="False"&&Get(migrated,"ControlMapping","Cross")=="CUSTOM","migration retains user graphics, audio, cheat and controls");
  Check(File.ReadAllText(global)==original&&File.ReadAllText(cheats).StartsWith("_C1 "),"global profile and selected cheat code untouched");
  Check(Directory.GetFiles(sys,"NPJH50247_ppsspp.ini.fate-backup-*").Any(p=>File.ReadAllText(p)==before),"old game-specific INI backed up exactly");
  Check(Settings.Load(configPath).CpuDefaultsProfile==Engine.PspRoot(memory),"migration marker persisted for target profile");
  int backups=Directory.GetFiles(sys,"*.fate-backup-*").Length;engine.GameStartInfo();
  Check(File.ReadAllText(game)==migrated&&Directory.GetFiles(sys,"*.fate-backup-*").Length==backups,"next launch does not rewrite profile");
  File.WriteAllText(game,PpssppSettings.Set(migrated,"CPU","CPUCore","3"));engine.GameStartInfo();
  Check(Get(File.ReadAllText(game),"CPU","CPUCore")=="3","user IR-JIT choice retained after migration");
  engine.ConfigureDefaults();Check(Get(File.ReadAllText(game),"CPU","CPUCore")=="3"&&Get(File.ReadAllText(game),"Graphics","InternalResolution")=="8","explicit preset retains IR-JIT while applying requested graphics");
  string current=File.ReadAllText(game);engine.Config.CpuDefaultsProfile="";engine.RunningCheck=()=>true;
  Reject(()=>engine.PreparePlaybackDefaults(),"running PPSSPP blocks migration");
  Check(File.ReadAllText(game)==current&&engine.Config.CpuDefaultsProfile=="","blocked migration changes neither INI nor completion marker");
  engine.RunningCheck=()=>false;engine.Config.Memstick=Path.Combine(root,"new-memory");engine.PreparePlaybackDefaults();
  Check(File.Exists(Path.Combine(engine.Config.Memstick,"PSP","SYSTEM","NPJH50247_ppsspp.ini"))&&engine.Config.CpuDefaultsProfile==Engine.PspRoot(engine.Config.Memstick),"new destination cannot reuse previous profile marker");
  var fresh=new Engine(new Settings{DataRoot=Path.Combine(root,"fresh-data"),Memstick=Path.Combine(root,"fresh-memory")},Path.Combine(root,"fresh.json"),Path.Combine(w,"build","tools"));fresh.RunningCheck=()=>false;fresh.PreparePlaybackDefaults();
  string freshIni=File.ReadAllText(Path.Combine(fresh.Config.Memstick,"PSP","SYSTEM","NPJH50247_ppsspp.ini"));
  Check(Get(freshIni,"CPU","CPUCore")=="1"&&Get(freshIni,"Graphics","InternalResolution")=="8"&&Get(freshIni,"Graphics","MultiSampleLevel")=="2"&&fresh.Config.GraphicsDefaultsApplied,"fresh installation receives JIT and full requested graphics preset");
  Json.Write(Path.Combine(w,"cpu-defaults-tests.json"),new{status="PASS",tests=tests,real_emulator_started=false,live_profile_changed=false});
 }
}
