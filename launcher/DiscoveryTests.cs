using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using FateLauncher;
public static class DiscoveryTests {
 static int count;static void Assert(bool b,string label){if(!b)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
 static string Emulator(string root) {Directory.CreateDirectory(Path.Combine(root,"assets"));string exe=Path.Combine(root,"PPSSPPWindows64.exe");var pe=new byte[128];pe[0]=77;pe[1]=90;pe[60]=64;pe[64]=80;pe[65]=69;File.WriteAllBytes(exe,pe);return exe;}
 public static void Main(string[] args){try{Run(args);}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}}
 static void Run(string[] args){string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);string docs=Path.Combine(root,"Documents");Directory.CreateDirectory(Path.Combine(docs,"PPSSPP"));string exe=Emulator(Path.Combine(root,"portable"));string memory=Path.Combine(root,"portable","memstick");Directory.CreateDirectory(Path.Combine(memory,"PSP","SYSTEM"));File.WriteAllText(Path.Combine(memory,"PSP","SYSTEM","ppsspp.ini"),"[General]\r\n");
  Assert(Discovery.MemoryFor(exe,docs)==memory,"portable memstick route");
  string marker=Path.Combine(Path.GetDirectoryName(exe),"installed.txt");File.WriteAllText(marker,"",new UTF8Encoding(false));Assert(Discovery.MemoryFor(exe,docs)==Path.Combine(docs,"PPSSPP"),"installed empty marker routes Documents");
  File.WriteAllText(marker,memory+"\r\n",new UTF8Encoding(true));Assert(Discovery.MemoryFor(exe,docs)==memory,"BOM absolute marker routes selected folder");File.Delete(marker);
  Assert(Discovery.IsEmulator(exe),"candidate needs executable header and assets");string decoy=Path.Combine(root,"PPSSPPWindows64.exe");File.WriteAllText(decoy,"not executable");Assert(!Discovery.IsEmulator(decoy),"reject named decoy executable");
  string iso=args[1];Assert(Discovery.IsOriginalCandidate(iso),"actual original ISO disc identity/size");Assert(!Discovery.IsOriginalCandidate(args[2]),"patched ISO excluded");
  File.WriteAllText(Path.Combine(memory,"PSP","SYSTEM","ppsspp.ini"),"[Recent]\r\nFileName0 = "+iso+"\r\n");
  var result=new Discovery(CancellationToken.None,docs,100,10).Search(new[]{root},2);Assert(result.Isos.Contains(Path.GetFullPath(iso)),"recent game path supplies original ISO");Assert(result.Emulators.Count==1&&result.EmulatorMemsticks[exe]==memory,"candidate and matching memory route paired");
  var config=new Settings();LauncherForm.FillPaths(result,config);Assert(config.SourceIso==Path.GetFullPath(iso)&&config.Emulator==exe&&config.Memstick==memory,"unique candidates fill three empty paths");
  config=new Settings{SourceIso="keep.iso",Emulator="keep.exe",Memstick="keep-memory"};LauncherForm.FillPaths(result,config);Assert(config.SourceIso=="keep.iso"&&config.Emulator=="keep.exe"&&config.Memstick=="keep-memory","explicit paths preserved");
  result.Isos.Add("another.iso");result.Emulators.Add("another.exe");config=new Settings();LauncherForm.FillPaths(result,config);Assert(config.SourceIso==""&&config.Emulator==""&&config.Memstick=="","ambiguous candidates not silently selected");
  var budget=new Discovery(CancellationToken.None,docs,1,10).Search(new[]{root},5);Assert(budget.Limited,"scan respects directory limit");
  bool cancelled=false;try{new Discovery(new CancellationToken(true),docs).Search(new[]{root},5);}catch(OperationCanceledException){cancelled=true;}Assert(cancelled,"scan honors cancellation");
  var real=new Discovery(CancellationToken.None,docs,200,10).Search(new[]{Path.GetDirectoryName(iso)},1);Assert(real.Isos.Contains(Path.GetFullPath(iso)),"folder search finds real original");
  string deepRoot=Path.Combine(root,"recent-deep"),deepExe=Emulator(Path.Combine(deepRoot,"nested"));File.WriteAllText(Path.Combine(memory,"PSP","SYSTEM","ppsspp.ini"),"[Recent]\r\nFileName0 = "+Path.Combine(deepRoot,"missing.iso")+"\r\n");var deep=new Discovery(CancellationToken.None,docs,100,10).Search(new[]{deepRoot},3,new Settings{Memstick=memory});Assert(deep.Emulators.Contains(deepExe),"recent shallow visit allows later deeper folder search");
  var net=new Discovery(CancellationToken.None,docs,100,10).Search(new[]{"\\\\invalid.invalid\\share"},2);Assert(net.Directories==0,"UNC excluded before traversal");
  string standalone=Path.Combine(root,"separate-profile");Directory.CreateDirectory(Path.Combine(standalone,"PSP","SYSTEM"));File.WriteAllText(Path.Combine(standalone,"PSP","SYSTEM","ppsspp.ini"),"[General]\r\n");var profiles=new Discovery(CancellationToken.None,docs,100,10).Search(new[]{standalone},2);Assert(profiles.Memsticks.Contains(standalone),"standalone memory stick discovered without emulator");
  string buried=Path.Combine(root,"custom","nested","a","b","c","d","e","f","g","h","emulator");string buriedExe=Emulator(buried);Directory.CreateDirectory(Path.Combine(buried,"memstick"));File.Copy(buriedExe,Path.Combine(buried,"PPSSPPWindows.exe"));var devices=new Discovery(CancellationToken.None,docs,500,10).Devices(new[]{Path.Combine(root,"custom"),standalone});Assert(devices.Emulators.Contains(buriedExe)&&devices.EmulatorMemsticks[buriedExe]==Path.Combine(buried,"memstick"),"deep custom emulator and paired memory found");Assert(!devices.Emulators.Contains(Path.Combine(buried,"PPSSPPWindows.exe")),"same-install 32-bit duplicate suppressed");Assert(devices.Memsticks.Contains(standalone),"drive scan finds separate memory stick");
  File.WriteAllBytes(decoy,new byte[]{77,90,0,0});Assert(!Discovery.IsEmulator(decoy),"MZ-only fake excluded");
  var explicit32=new Settings{Emulator=Path.Combine(buried,"PPSSPPWindows.exe")};LauncherForm.FillPaths(devices,explicit32);Assert(explicit32.Memstick==Path.Combine(buried,"memstick"),"explicit 32-bit executable retains matching profile");
  Assert(Discovery.HistoryExecutable(exe)==exe,"execution-history path recovered");
  Assert(Discovery.HistoryExecutable(exe+".FriendlyAppName")==exe&&Discovery.HistoryExecutable(exe+".ApplicationCompany")==exe,"MuiCache metadata suffix stripped");
  Assert(Discovery.HistoryExecutable(exe+".bad.exe")==""&&Discovery.HistoryExecutable("\\\\server\\share\\PPSSPPWindows64.exe")=="","history rejects unrelated suffix and network path");
  string absent=Path.Combine(root,"missing","PPSSPPWindows64.exe");var hinted=new Discovery(CancellationToken.None,docs,1,10).Common(Path.Combine(root,"unrelated-download"),new Settings(),new[]{absent,exe+".FriendlyAppName",exe+".ApplicationCompany"});Assert(hinted.Emulators.Count==1&&hinted.Emulators.Contains(exe)&&hinted.EmulatorMemsticks[exe]==memory,"validated history finds emulator and profile before unrelated tree budget");
  var isoOnly=new Discovery(CancellationToken.None,docs,100,10,true,false).Common(root,new Settings{SourceIso=iso,Emulator=exe,Memstick=memory},new[]{exe});Assert(isoOnly.Isos.Contains(Path.GetFullPath(iso))&&isoOnly.Emulators.Count==0&&isoOnly.Memsticks.Count==0&&isoOnly.EmulatorMemsticks.Count==0,"ISO-only search ignores device hints and profiles");
  Assert(new Discovery(CancellationToken.None,docs,100,10,true,false).Devices(new[]{root}).Directories==0,"disabled device mode never traverses drives");
  File.WriteAllText(Path.Combine(memory,"PSP","SYSTEM","ppsspp.ini"),"[Recent]\r\nFileName0 = "+iso+"\r\n");var deviceOnly=new Discovery(CancellationToken.None,docs,100,10,false,true).Search(new[]{root},2,new Settings{SourceIso=iso});Assert(deviceOnly.Isos.Count==0&&deviceOnly.Emulators.Contains(exe)&&deviceOnly.Memsticks.Contains(memory),"extras-only discovery skips ISO and Recent game probing");
  if(args.Length>3){string linked=args[3];Assert(!Discovery.IsEmulator(Path.Combine(linked,"PPSSPPWindows64.exe")),"parent junction excluded from executable probe");Assert(new Discovery(CancellationToken.None,docs).Search(new[]{linked},3).Directories==0,"junction search root not traversed");}
  Json.Write(Path.Combine(root,"results.json"),new{status="PASS",tests=count});
 }
}
