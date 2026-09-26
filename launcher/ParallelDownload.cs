using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FateLauncher {
// Four independently resumable byte ranges. The caller verifies the final SHA-256
// before publishing the combined file. Small/legacy partial downloads stay serial.
internal static class ParallelDownload {
 internal const long MinimumSize=16L*1024*1024;
 sealed class RangeUnavailable:Exception { }
 static long Start(long size,int i){return size*i/4;}
 static string Parts(string output){return output+".parts";}
 static HttpClient Client(){
  ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
  ServicePointManager.DefaultConnectionLimit=Math.Max(8,ServicePointManager.DefaultConnectionLimit);
  var c=new HttpClient(new HttpClientHandler{MaxConnectionsPerServer=4});c.Timeout=Timeout.InfiniteTimeSpan;
  c.DefaultRequestHeaders.UserAgent.ParseAdd("FateExtraKoreanLauncher/"+Engine.Version);return c;
 }
 static void CheckRange(HttpResponseMessage response,long start,long end,long size){
  if(response.StatusCode==HttpStatusCode.OK)throw new RangeUnavailable();
  response.EnsureSuccessStatusCode();var range=response.Content.Headers.ContentRange;
  if(response.StatusCode!=HttpStatusCode.PartialContent||range==null||range.Unit!="bytes"||range.From!=start||range.To!=end||range.Length!=size)
   throw new IOException("분할 다운로드의 응답 범위가 다릅니다.");
  long? length=response.Content.Headers.ContentLength;
  if(length.HasValue&&length.Value!=end-start+1)throw new IOException("분할 다운로드의 응답 크기가 다릅니다.");
 }
 static void CheckOwned(Asset asset,string directory){
  Engine.NoLinkParents(directory);
  var allowed=new HashSet<string>(new[]{"layout.json","0.part","1.part","2.part","3.part"},StringComparer.Ordinal);
  foreach(string path in Directory.GetFileSystemEntries(directory)){
   Engine.NoLinks(path);if(Directory.Exists(path)||!allowed.Contains(Path.GetFileName(path)))throw new IOException("분할 다운로드 임시 폴더에 알 수 없는 파일이 있습니다.");
  }
  string manifest=Path.Combine(directory,"layout.json");var d=Json.Parse(File.ReadAllText(manifest));
  if(Json.N(d,"schema")!=1||Json.N(d,"parts")!=4||Json.N(d,"bytes")!=asset.Size||Json.S(d,"sha256")!=asset.Hash)
   throw new IOException("분할 다운로드의 이어받기 정보가 다릅니다.");
 }
 internal static void Clear(Asset asset,string output,bool invalid){
  string directory=Parts(output);if(!Directory.Exists(directory))return;CheckOwned(asset,directory);
  if(invalid){
   string parent=Path.GetDirectoryName(Path.GetFullPath(output)),destination=directory+".damaged-"+Guid.NewGuid().ToString("N");
   if(!Engine.Under(directory,parent)||!Engine.Under(destination,parent))throw new IOException("임시 다운로드 경로 오류");
   Directory.Move(directory,destination);
  }
  else Engine.DeleteTree(directory,Path.GetDirectoryName(directory));
 }
 internal static bool Receive(Asset asset,string output,CancellationToken cancel,Action<string,int> progress){
  using(var client=Client()){
   // A server must acknowledge byte ranges, including the complete object size.
   // Dispose a 200 response without reading its body, then let the serial path run.
   using(var request=new HttpRequestMessage(HttpMethod.Get,asset.Url)){
    request.Headers.Range=new RangeHeaderValue(0,0);
    using(var response=client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel).GetAwaiter().GetResult()){
     try{CheckRange(response,0,0,asset.Size);}catch(RangeUnavailable){progress("서버가 분할 다운로드를 지원하지 않아 일반 다운로드로 받습니다.",-1);return false;}
    }
   }
   string directory=Parts(output);Engine.NoLinkParents(directory);
   if(!Directory.Exists(directory)){
    Directory.CreateDirectory(directory);
    Json.Write(Path.Combine(directory,"layout.json"),new{schema=1,parts=4,bytes=asset.Size,sha256=asset.Hash});
   }
   CheckOwned(asset,directory);
   long received=0;var offsets=new long[4];
   for(int i=0;i<4;i++){
    string path=Path.Combine(directory,i+".part");offsets[i]=File.Exists(path)?new FileInfo(path).Length:0;
    if(offsets[i]>Start(asset.Size,i+1)-Start(asset.Size,i))throw new IOException("분할 다운로드 임시 파일의 크기가 잘못되었습니다.");
    received+=offsets[i];
   }
   progress("4개 연결로 다운로드 · "+asset.Name,-1);
   var watch=Stopwatch.StartNew();var progressLock=new object();
   using(var group=CancellationTokenSource.CreateLinkedTokenSource(cancel)){
    var tasks=Enumerable.Range(0,4).Select(index=>Task.Run(()=>{
     try{
      long begin=Start(asset.Size,index),end=Start(asset.Size,index+1)-1,offset=offsets[index];
      if(offset==end-begin+1)return;
      using(var request=new HttpRequestMessage(HttpMethod.Get,asset.Url)){
       request.Headers.Range=new RangeHeaderValue(begin+offset,end);
       using(var response=client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,group.Token).GetAwaiter().GetResult()){
        CheckRange(response,begin+offset,end,asset.Size);
        using(var input=response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
        using(var file=new FileStream(Path.Combine(directory,index+".part"),offset==0?FileMode.Create:FileMode.Append,FileAccess.Write,FileShare.Read)){
         byte[] buffer=new byte[1024*1024];int n;
         while((n=input.ReadAsync(buffer,0,buffer.Length,group.Token).GetAwaiter().GetResult())>0){
          group.Token.ThrowIfCancellationRequested();if(offset+n>end-begin+1)throw new IOException("분할 다운로드가 지정 범위를 넘었습니다.");
          file.Write(buffer,0,n);offset+=n;Interlocked.Add(ref received,n);
          lock(progressLock){if(watch.ElapsedMilliseconds>=200){long total=Interlocked.Read(ref received);progress("다운로드 · 4개 연결 · "+asset.Name+"   "+total/1048576+" / "+asset.Size/1048576+" MB",(int)(total*100/asset.Size));watch.Restart();}}
         }
         if(offset!=end-begin+1)throw new IOException("분할 다운로드 연결이 파일 끝에 도달하기 전에 종료되었습니다.");
        }
       }
      }
     }catch{group.Cancel();throw;}
    })).ToArray();
    try{Task.WaitAll(tasks);}catch(AggregateException ex){
     cancel.ThrowIfCancellationRequested();var errors=ex.Flatten().InnerExceptions;
     if(errors.Any(e=>e is RangeUnavailable)&&errors.All(e=>e is RangeUnavailable||e is OperationCanceledException)){
      progress("서버가 분할 응답을 지원하지 않아 일반 다운로드로 다시 받습니다.",-1);return false;
     }
     throw errors.FirstOrDefault(e=>!(e is OperationCanceledException))??ex;
    }
   }
   cancel.ThrowIfCancellationRequested();progress("분할 다운로드 합치는 중 · "+asset.Name,-1);
   bool created=false;
   try{
    using(var merged=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
     created=true;
     var buffer=new byte[1024*1024];
     for(int i=0;i<4;i++)using(var input=File.OpenRead(Path.Combine(directory,i+".part"))){
      if(input.Length!=Start(asset.Size,i+1)-Start(asset.Size,i))throw new IOException("분할 파일 크기 불일치");
      int n;while((n=input.Read(buffer,0,buffer.Length))>0){cancel.ThrowIfCancellationRequested();merged.Write(buffer,0,n);}
     }
     if(merged.Length!=asset.Size)throw new IOException("합친 다운로드 파일 크기 불일치");
    }
   }catch{if(created&&File.Exists(output))File.Delete(output);throw;}
   return true;
  }
 }
}
}
