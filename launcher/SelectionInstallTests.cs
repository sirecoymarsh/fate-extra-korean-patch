using System;using System.IO;using System.Text.RegularExpressions;using FateLauncher;
public static class SelectionInstallTests {
 static int count;static void Assert(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
 static void Reject(Action action,string label){try{action();}catch{Assert(true,label);return;}throw new Exception("Missing rejection: "+label);}
 public static void Main(string[] args){try{Run(args);}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}}
 static void Run(string[] args){string work=args[0],original=args[1],cache=args[2],root=Path.Combine(work,"selection-install"),cfg=Path.Combine(root,"settings.json");Directory.CreateDirectory(root);var release=ReleaseInfo.Read(File.ReadAllText(Path.Combine(work,"launcher-update.json")),"v8e-hd-v39");
  string downloads=Path.Combine(root,"Data","downloads",release.Tag);Directory.CreateDirectory(downloads);File.Copy(cache,Path.Combine(downloads,release.Base.Name));
  var config=new Settings{DataRoot=Path.Combine(root,"Data"),SourceIso=original};var engine=new Engine(config,cfg,Path.Combine(work,"build","tools"));engine.Progress=(text,p)=>Console.WriteLine(text);
  engine.Install(release,true,false,false,false);Assert(File.Exists(config.GameIso)&&config.BaseVersion==release.BaseVersion,"real base-only patch installs successfully");Assert(config.Emulator==""&&config.Memstick==""&&!Directory.Exists(Path.Combine(root,"Data","emulator")),"base-only needs no emulator or memory stick");
  config.SourceIso="";Reject(()=>engine.Install(release,false,false,true,false),"cheats requires target memory stick");Reject(()=>engine.Install(release,false,false,false,true),"clear save requires target memory stick");Reject(()=>engine.Install(release,false,true,false,false),"HD requires texture target memory stick");
  config.Memstick=Path.Combine(root,"profile");engine.Install(release,false,false,true,true);string cheats=Path.Combine(config.Memstick,"PSP","Cheats","NPJH50247.ini"),save=Path.Combine(config.Memstick,"PSP","SAVEDATA","NPJH50247DATA80","SECURE.BIN");Assert(File.Exists(cheats)&&Regex.Matches(File.ReadAllText(cheats),@"(?m)^_C0 ").Count==19,"selected profile receives all 19 disabled cheats");Assert(File.Exists(save),"selected profile receives clear save");Assert(config.SourceIso==""&&config.Emulator=="","extras install needs target folder but no ISO or PPSSPP executable");
  Json.Write(Path.Combine(work,"selection-install-results.json"),new{status="PASS",tests=count,game_iso=config.GameIso});
 }
}
