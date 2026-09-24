using System;using System.IO;using System.Linq;using System.Threading;using System.Diagnostics;using FateLauncher;
public static class RootDiscoveryProbe {
 public static void Main(string[] args){try{
  string root=Path.GetFullPath(args[0]),expected=Path.GetFullPath(args[1]),output=args[2];var time=Stopwatch.StartNew();
  var hits=new Discovery(CancellationToken.None).Common(root,new Settings());int quick=hits.Emulators.Count;
  if(hits.Emulators.Count==0||hits.Memsticks.Count==0)hits.Merge(new Discovery(CancellationToken.None,null,50000,45).Devices(Discovery.DeviceRoots(root)));
  hits.Prefer64Bit();string mem;bool found=hits.Emulators.Contains(expected,StringComparer.OrdinalIgnoreCase)&&hits.EmulatorMemsticks.TryGetValue(expected,out mem);
  var result=new{status=found?"PASS":"MISS",launcher_root=root,quick_emulators=quick,seconds=time.Elapsed.TotalSeconds,emulators=hits.Emulators,memsticks=hits.Memsticks,directories=hits.Directories,limited=hits.Limited,expected_found=found};Json.Write(output,result);Console.WriteLine(Json.Serializer().Serialize(new{result.status,result.quick_emulators,result.seconds,emulators=hits.Emulators.Count,memsticks=hits.Memsticks.Count,result.directories,result.limited}));
 }catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}}
}
