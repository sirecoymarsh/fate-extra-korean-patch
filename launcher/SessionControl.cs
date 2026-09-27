using System;using System.IO;using System.Threading;using System.Diagnostics;using FateLauncher;
class SessionControl {
 static int Main(string[] a){try{var settings=Settings.Load(a[0]);var engine=new Engine(settings,a[0],Path.Combine(Path.GetDirectoryName(a[0]),"tools"));
  var ticket=engine.StartExternalSession();string status=ticket.StatusPath;
  if(a[1]=="cancel"){ticket.Dispose();Thread.Sleep(800);if(Engine.GameRunning())throw new Exception("Cancelled preparation launched game");Json.Write(a[0]+".cancel-check.json",new{status="PASS",game_launched=false});return 0;}
  using(ticket){var watch=Stopwatch.StartNew();while(!File.Exists(status)&&watch.Elapsed.TotalSeconds<120)Thread.Sleep(100);if(!File.Exists(status))throw new Exception("helper timeout");var state=Json.Parse(File.ReadAllText(status));if(Json.S(state,"status")!="ready")throw new Exception(File.ReadAllText(status));ticket.Commit();watch.Restart();do{Thread.Sleep(100);state=Json.Parse(File.ReadAllText(status));}while(Json.S(state,"status")=="ready"&&watch.Elapsed.TotalSeconds<15);if(Json.S(state,"status")!="running")throw new Exception(File.ReadAllText(status));Console.WriteLine(status);Console.WriteLine(File.ReadAllText(status));File.WriteAllText(a[0]+".active-session.txt",status);return 0;}
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
