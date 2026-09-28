using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FateLauncher {
public sealed class DataExtent { public long Start,Length,Offset; public int Kind; }
public sealed class ExternalManifest {
 public string Format="FateExternalData1",SourceHash,TargetHash,PayloadHash;
 public long SourceBytes,TargetBytes,PayloadBytes;
 public List<DataExtent> Extents=new List<DataExtent>();
}
public static class ExternalBuilder {
 internal static int Fill(Stream stream,byte[] data,int length) {int p=0,n;while(p<length&&(n=stream.Read(data,p,length-p))>0)p+=n;return p;}
 internal static string Digest(Stream stream) {stream.Position=0;using(var h=SHA256.Create()){string result=BitConverter.ToString(h.ComputeHash(stream)).Replace("-","");stream.Position=0;return result;}}
 public static void Build(Stream target,string source,string folder,long length,string targetHash,CancellationToken cancel,Action<string,int> progress) {
  Directory.CreateDirectory(folder);
  var manifest=new ExternalManifest{SourceHash=Engine.SourceHash,SourceBytes=Engine.SourceSize,TargetBytes=length,TargetHash=targetHash};
  string payload=Path.Combine(folder,"patch.bin");
  using(var original=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))
  using(var output=new FileStream(payload,FileMode.CreateNew,FileAccess.Write,FileShare.None))
  using(var hash=SHA256.Create()) {
   if(original.Length!=manifest.SourceBytes||Digest(original)!=manifest.SourceHash)throw new Exception("원본 ISO 확인 실패");
   byte[] current=new byte[65536],old=new byte[65536];long position=0;var watch=Stopwatch.StartNew();
   while(position<length) {
    cancel.ThrowIfCancellationRequested();int count=(int)Math.Min(current.Length,length-position);
    if(Fill(target,current,count)!=count)throw new Exception("패치 출력이 중간에 끝났습니다.");
    hash.TransformBlock(current,0,count,null,0);
    int available=position<original.Length?(int)Math.Min(count,original.Length-position):0;
    original.Position=Math.Min(position,original.Length);if(Fill(original,old,available)!=available)throw new IOException("Original read failed");
    bool same=available==count,zero=true;for(int i=0;i<count;i++){same&=i<available&&current[i]==old[i];zero&=current[i]==0;}
    int kind=same?0:zero?2:1;long offset=kind==0?position:kind==1?output.Position:0;
    if(kind==1)output.Write(current,0,count);
    var last=manifest.Extents.LastOrDefault();
    if(last!=null&&last.Kind==kind&&(kind==2||last.Offset+last.Length==offset))last.Length+=count;
    else manifest.Extents.Add(new DataExtent{Start=position,Length=count,Kind=kind,Offset=offset});
    position+=count;if(watch.ElapsedMilliseconds>250){if(progress!=null)progress("외부 패치 데이터 준비 중",(int)(position*100/length));watch.Restart();}
   }
   if(target.ReadByte()!=-1)throw new Exception("패치 출력 크기가 예상보다 큽니다.");
   hash.TransformFinalBlock(new byte[0],0,0);
   if(BitConverter.ToString(hash.Hash).Replace("-","")!=targetHash)throw new Exception("외부 데이터의 본편 해시가 일치하지 않습니다.");
   manifest.PayloadBytes=output.Length;
  }
  manifest.PayloadHash=Engine.Hash(payload);Json.Write(Path.Combine(folder,"manifest.json"),manifest);
  using(var check=new ExternalImage(source,Path.Combine(folder,"manifest.json"),targetHash))check.VerifyTarget(cancel);
 }
 public static void Decode(string decoder,string patch,string source,string folder,long length,string hash,CancellationToken cancel,Action<string,int> progress) {
  var start=new ProcessStartInfo(decoder,"-d -c -s "+Engine.Quote(source)+" "+Engine.Quote(patch)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
  using(var process=Process.Start(start)) {
   var errors=process.StandardError.ReadToEndAsync();
   using(cancel.Register(()=>{try{if(!process.HasExited)process.Kill();}catch{}})) {
    try{Build(process.StandardOutput.BaseStream,source,folder,length,hash,cancel,progress);process.WaitForExit();if(process.ExitCode!=0)throw new Exception("패치 해석 실패: "+errors.GetAwaiter().GetResult());}
    finally{try{if(!process.HasExited)process.Kill();}catch{}}
   }
  }
 }
}
public sealed class ExternalImage : IDisposable {
 readonly FileStream source,payload;readonly object gate=new object();public readonly ExternalManifest Info;
 public ExternalImage(string sourcePath,string manifestPath,string expectedTarget) {
  try {
   Engine.NoLinkParents(manifestPath);if(new FileInfo(manifestPath).Length>8*1024*1024)throw new Exception("외부 데이터 색인이 너무 큽니다.");
   Info=Json.Serializer().Deserialize<ExternalManifest>(File.ReadAllText(manifestPath));
   if(Info==null||Info.Format!="FateExternalData1"||Info.SourceBytes!=Engine.SourceSize||Info.SourceHash!=Engine.SourceHash||Info.TargetHash!=expectedTarget||Info.TargetBytes<1||Info.TargetBytes>8L*1024*1024*1024||Info.Extents==null||Info.Extents.Count==0||Info.Extents.Count>131072)throw new Exception("외부 데이터 색인 정보 오류");
   source=new FileStream(sourcePath,FileMode.Open,FileAccess.Read,FileShare.Read);
   string payloadPath=Path.Combine(Path.GetDirectoryName(manifestPath),"patch.bin");Engine.NoLinkParents(payloadPath);
   payload=new FileStream(payloadPath,FileMode.Open,FileAccess.Read,FileShare.Read);
   if(source.Length!=Info.SourceBytes||ExternalBuilder.Digest(source)!=Info.SourceHash)throw new Exception("외부 로딩에는 설치 때 사용한 일본판 원본 ISO가 필요합니다.");
   if(payload.Length!=Info.PayloadBytes||ExternalBuilder.Digest(payload)!=Info.PayloadHash)throw new Exception("외부 패치 데이터가 손상되었습니다.");
   long end=0;foreach(var e in Info.Extents) {
    if(e.Start!=end||e.Length<=0||e.Length>Info.TargetBytes-end||e.Kind<0||e.Kind>2||e.Offset<0)throw new Exception("외부 데이터 구간 오류");
    long bound=e.Kind==0?source.Length:e.Kind==1?payload.Length:0;
    if(e.Kind!=2&&(e.Offset>bound||e.Length>bound-e.Offset)||e.Kind==2&&e.Offset!=0)throw new Exception("외부 데이터 참조 범위 오류");end+=e.Length;
   }
   if(end!=Info.TargetBytes)throw new Exception("외부 데이터 구간 누락");
  }catch{Dispose();throw;}
 }
 public void ReadAt(long position,byte[] buffer,int count) {
  if(position<0||count<0||count>buffer.Length||position>Info.TargetBytes-count)throw new ArgumentOutOfRangeException("position");
  lock(gate) {
   int left=0,right=Info.Extents.Count-1;
   while(left<right){int middle=(left+right+1)/2;if(Info.Extents[middle].Start<=position)left=middle;else right=middle-1;}
   int done=0;while(done<count) {
    var e=Info.Extents[left++];long delta=position-e.Start;int n=(int)Math.Min(count-done,e.Length-delta);
    if(e.Kind==2)Array.Clear(buffer,done,n);else {var f=e.Kind==0?source:payload;f.Position=e.Offset+delta;int got=0,k;while(got<n&&(k=f.Read(buffer,done+got,n-got))>0)got+=k;if(got!=n)throw new EndOfStreamException();}
    position+=n;done+=n;
   }
  }
 }
 public void VerifyTarget(CancellationToken cancel) {
  using(var h=SHA256.Create()){byte[] b=new byte[1024*1024];for(long p=0;p<Info.TargetBytes;){cancel.ThrowIfCancellationRequested();int n=(int)Math.Min(b.Length,Info.TargetBytes-p);ReadAt(p,b,n);h.TransformBlock(b,0,n,null,0);p+=n;}h.TransformFinalBlock(new byte[0],0,0);if(BitConverter.ToString(h.Hash).Replace("-","")!=Info.TargetHash)throw new Exception("조합된 본편의 검증 실패");}
 }
 public void Dispose(){if(source!=null)source.Dispose();if(payload!=null)payload.Dispose();}
}
public sealed class ExternalServer : IDisposable {
 readonly ExternalImage image;readonly TcpListener listener;readonly Thread worker;readonly string route;readonly object gate=new object();readonly List<TcpClient> clients=new List<TcpClient>();volatile bool stopped;
 public readonly string Url;public long Requests,Bytes;public string ReadError;
 public ExternalServer(ExternalImage data) {
  image=data;route="/"+Guid.NewGuid().ToString("N")+"/Fate-Extra-"+image.Info.TargetHash.Substring(0,12)+".iso";
  listener=new TcpListener(IPAddress.Loopback,0);listener.Start(16);Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+route;
  worker=new Thread(Accept){IsBackground=true};worker.Start();
 }
 void Accept(){while(!stopped){TcpClient c;try{c=listener.AcceptTcpClient();}catch{if(stopped)return;continue;}lock(gate){if(clients.Count>=16){c.Close();continue;}clients.Add(c);}Task.Run(()=>Serve(c));}}
 static void Header(Stream s,string status,long count,string extra){byte[] b=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+"\r\nContent-Length: "+count+"\r\nAccept-Ranges: bytes\r\nContent-Type: application/octet-stream\r\nCache-Control: no-store\r\nConnection: close\r\n"+extra+"\r\n");s.Write(b,0,b.Length);}
 void Serve(TcpClient client) {
  try{using(client){client.ReceiveTimeout=5000;client.SendTimeout=15000;using(var s=client.GetStream()) {
   var header=new List<byte>();int value;while(header.Count<16384&&(value=s.ReadByte())>=0){header.Add((byte)value);int n=header.Count;if(n>=4&&header[n-4]==13&&header[n-3]==10&&header[n-2]==13&&header[n-1]==10)break;}
   if(header.Count==16384){Header(s,"431 Request Header Fields Too Large",0,"");return;}
   var lines=Encoding.ASCII.GetString(header.ToArray()).Split(new[]{"\r\n"},StringSplitOptions.None);var first=lines[0].Split(' ');
   if(first.Length!=3||first[1]!=route){Header(s,"404 Not Found",0,"");return;}
   if(first[0]!="GET"&&first[0]!="HEAD"){Header(s,"405 Method Not Allowed",0,"Allow: GET, HEAD\r\n");return;}
   long begin=0,end=image.Info.TargetBytes-1;var ranges=lines.Where(x=>x.StartsWith("Range:",StringComparison.OrdinalIgnoreCase)).ToArray();bool partial=ranges.Length>0;
   if(partial){bool valid=false;if(ranges.Length==1){var m=System.Text.RegularExpressions.Regex.Match(ranges[0],@"^Range:\s*bytes=(\d+)-(\d*)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);valid=m.Success&&Int64.TryParse(m.Groups[1].Value,out begin);if(valid&&m.Groups[2].Value!="")valid=Int64.TryParse(m.Groups[2].Value,out end);}
    if(!valid||begin<0||begin>=image.Info.TargetBytes||end<begin){Header(s,"416 Range Not Satisfiable",0,"Content-Range: bytes */"+image.Info.TargetBytes+"\r\n");return;}end=Math.Min(end,image.Info.TargetBytes-1);
   }
   long count=end-begin+1;Header(s,partial?"206 Partial Content":"200 OK",count,partial?"Content-Range: bytes "+begin+"-"+end+"/"+image.Info.TargetBytes+"\r\n":"");Interlocked.Increment(ref Requests);
   if(first[0]=="HEAD")return;byte[] buffer=new byte[65536];while(count>0&&!stopped){int n=(int)Math.Min(buffer.Length,count);try{image.ReadAt(begin,buffer,n);}catch(Exception e){Interlocked.CompareExchange(ref ReadError,e.Message,null);return;}s.Write(buffer,0,n);begin+=n;count-=n;Interlocked.Add(ref Bytes,n);}
  }}}catch(IOException){}catch(SocketException){}catch(ObjectDisposedException){}finally{lock(gate){clients.Remove(client);Monitor.PulseAll(gate);}}
 }
 public void Dispose(){stopped=true;listener.Stop();worker.Join();lock(gate){foreach(var c in clients.ToArray())c.Close();while(clients.Count>0)Monitor.Wait(gate,100);}}
}
public sealed class ExternalTicket : IDisposable {
 public readonly string RequestPath,StatusPath;readonly Process helper;bool committed;
 public ExternalTicket(string request,Process process){RequestPath=request;StatusPath=request+".status.json";helper=process;}
 public bool HelperAlive{get{try{return !helper.HasExited;}catch{return false;}}}
 public void Commit(){File.WriteAllText(RequestPath+".go","");committed=true;}
 public void Dispose(){if(!committed){File.WriteAllText(RequestPath+".cancel","");try{if(!helper.HasExited)helper.Kill();}catch{}}helper.Dispose();}
}
public static class ExternalSession {
 static void Note(string path,object o){try{Json.Write(path,o);}catch{}}
 public static void Run(string requestPath) {
  string status=requestPath+".status.json";try {
   Settings c=Settings.Load(requestPath);if(c.GameData=="")throw new Exception("외부 로딩 설정 오류");
   var engine=new Engine(c,requestPath,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tools"));
   var start=engine.GameStartInfo("http://127.0.0.1/prepare.iso");
   using(var installGate=new FileStream(Path.Combine(c.DataRoot,"install.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
   using(var image=new ExternalImage(c.SourceIso,c.GameData,c.GameTargetHash)) {
    if(Engine.Hash(c.GameData)!=c.GameDataHash)throw new Exception("외부 패치 색인이 설치 기록과 다릅니다.");
    image.VerifyTarget(CancellationToken.None);
    using(var server=new ExternalServer(image)) {
     Json.Write(status,new{status="ready",helper_pid=Process.GetCurrentProcess().Id,target_sha256=image.Info.TargetHash});
     var ready=Stopwatch.StartNew();while(!File.Exists(requestPath+".go")){if(File.Exists(requestPath+".cancel")||ready.Elapsed.TotalSeconds>120)throw new OperationCanceledException("외부 실행 준비를 취소했습니다.");Thread.Sleep(100);}
     if(File.Exists(requestPath+".cancel"))throw new OperationCanceledException("외부 실행 준비를 취소했습니다.");
     if(Engine.GameRunning())throw new Exception("이미 PPSSPP가 실행 중입니다.");start.Arguments=Engine.Quote(server.Url);
     using(var process=Process.Start(start)) {
      // 게임이 뜬 뒤의 상태 기록은 부가 정보다. 기록이 실패해도(런처가 같은 파일을 읽는 순간의 공유 위반 등) 디스크 서버는 게임이 끝날 때까지 산다.
      Note(status,new{status="running",pid=process.Id,helper_pid=Process.GetCurrentProcess().Id,url=server.Url,target_sha256=image.Info.TargetHash});
      bool reported=false;while(!process.WaitForExit(500)){if(server.ReadError!=null&&!reported){reported=true;Note(status,new{status="io-error",pid=process.Id,error=server.ReadError});}}
      Note(status,new{status=server.ReadError==null?"exited":"io-error",error=server.ReadError,pid=process.Id,helper_pid=Process.GetCurrentProcess().Id,requests=server.Requests,bytes=server.Bytes,target_sha256=image.Info.TargetHash});
     }
    }
   }
  }catch(Exception e){Json.Write(status,new{status="error",error=e.Message});}
 }
}
}
