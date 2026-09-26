using System;
using System.IO;
using System.Text;
using System.Threading;
using FateLauncher;
class ParallelDownloadCheck {
 static int Main(string[] a){Console.OutputEncoding=new UTF8Encoding(false);try{Run(a);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static void Run(string[] a){
  string mode=a[0],url=a[1],root=a[4];long bytes=Int64.Parse(a[2]);string hash=a[3];
  var config=new Settings{DataRoot=root};var engine=new Engine(config,Path.Combine(root,"settings.json"),AppDomain.CurrentDomain.BaseDirectory);
  var cancel=new CancellationTokenSource();engine.Cancel=cancel.Token;
  engine.Progress=(s,p)=>{Console.WriteLine(s);if(mode=="cancel"&&p>=10)cancel.Cancel();};
  var asset=new Asset{Name="sample.bin",Url=url,Size=bytes,Hash=hash};
  try{
   string result=engine.Download(asset,"range-tests");
   if(mode=="cancel"||mode=="fail")throw new Exception("Download should not have completed");
   if(Engine.Hash(result)!=hash)throw new Exception("Final bytes differ");
   Console.WriteLine("PASS complete "+result);
  }catch(OperationCanceledException){if(mode!="cancel")throw;Console.WriteLine("PASS cancellation");}
  catch(Exception ex){if(mode!="fail"||ex.Message=="Download should not have completed")throw;Console.WriteLine("PASS rejected "+ex.Message);}
 }
}
