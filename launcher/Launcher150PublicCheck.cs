using System;using System.IO;using System.Reflection;using System.Threading;using FateLauncher;
public static class Launcher150PublicCheck {
 public static int Main(string[] args){try{
  string root=args[0];if(Directory.Exists(root))throw new Exception("Fresh test directory required");Directory.CreateDirectory(root);
  var config=new Settings{SourceIso=args[1],DataRoot=Path.Combine(root,"Data"),UseExternalData=true};
  var engine=new Engine(config,Path.Combine(root,"settings.json"),args[2]);
  typeof(Engine).GetField("RunningCheck",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(engine,new Func<bool>(()=>false));
  engine.Progress=(s,p)=>Console.WriteLine(s+" "+p);var release=engine.Latest();if(release.Tag!=args[3])throw new Exception("Unexpected public release "+release.Tag);
  engine.Install(release,true,false,false,false);if(config.GameIso!=""||!Engine.HasGame(config)||Directory.GetFiles(root,"*.iso",SearchOption.AllDirectories).Length!=0)throw new Exception("External installation state invalid");
  using(var image=new ExternalImage(config.SourceIso,config.GameData,release.TargetHash))image.VerifyTarget(CancellationToken.None);
  Json.Write(Path.Combine(root,"verification.json"),new{status="PASS",release=release.Tag,base_version=release.BaseVersion,target_sha256=release.TargetHash,launcher_sha256=Engine.Hash(typeof(Engine).Assembly.Location),empty_cache=true,downloaded_from_public_github=true,full_virtual_hash_verified=true,generated_iso_files=0,user_profile_touched=false});
  Console.WriteLine("PASS "+release.Tag+" external install from public GitHub");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
