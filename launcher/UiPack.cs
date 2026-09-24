using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FateLauncher {
// The add-on switches only between two reviewed, hash-bound texture maps.
// Unknown maps are rejected before staging; the installer journals all writes.
public sealed class UiPack {
 public string Root,Version,OriginalHash,KoreanHash;
 Dictionary<string,object> manifest;
 public UiPack(string root,ReleaseInfo release) {
  Root=root;manifest=Json.Parse(File.ReadAllText(Path.Combine(root,"ui-manifest.json")));
  if(Json.N(manifest,"schema")!=1||Json.S(manifest,"version")!=release.UiVersion||Json.S(manifest,"base_version")!=release.BaseVersion||Json.S(manifest,"base_iso_sha256")!=release.TargetHash)throw new Exception("UI 한국어화와 본편 버전이 맞지 않습니다.");
  Version=release.UiVersion;OriginalHash=release.UiOriginalHash;KoreanHash=release.UiKoreanHash;
  foreach(bool enabled in new[]{false,true}) {
   var variant=Variant(enabled);string name=Json.S(variant,"ini");Engine.SafeRelative(name);
   if(Engine.Hash(Path.Combine(root,name))!=(enabled?KoreanHash:OriginalHash))throw new Exception("UI 텍스처 매핑 검증 실패");
  }
  foreach(var f in Json.Map(manifest["files"])) {Engine.SafeRelative(f.Key);if(!f.Key.EndsWith(".png",StringComparison.OrdinalIgnoreCase))throw new Exception("UI 이미지 형식 오류");Engine.Verify(Path.Combine(root,"files",f.Key),Json.N(Json.Map(f.Value),"bytes"),Json.S(Json.Map(f.Value),"sha256"));}
 }
 Dictionary<string,object> Variant(bool enabled){return Json.Map(Json.Map(manifest["variants"])[enabled?"korean":"original"]);}
 public void ValidateInstalled(string textures) {
  string ini=Path.Combine(textures,"textures.ini");Engine.NoLinkParents(ini);
  if(!File.Exists(ini))throw new Exception("UI 한국어화에는 HD 고화질 팩이 필요합니다. HD도 함께 선택하세요.");
  string hash=Engine.Hash(ini);if(hash!=OriginalHash&&hash!=KoreanHash)throw new Exception("지원하는 HD 매핑이 아닙니다. HD 고화질 팩도 함께 선택하세요. 기존 팩은 백업됩니다.");
 }
 public Dictionary<string,string> FilesFor(string textures,bool enabled) {
  var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var variant=Variant(enabled);
  foreach(var o in Json.A(variant,"files")) {
   string rel=Convert.ToString(o);Engine.SafeRelative(rel);string dest=Path.Combine(textures,rel);Engine.NoLinkParents(dest);
   var info=Json.Map(Json.Map(manifest["files"])[rel]);
   if(!File.Exists(dest)||new FileInfo(dest).Length!=Json.N(info,"bytes")||Engine.Hash(dest)!=Json.S(info,"sha256"))result[rel]=Path.Combine(Root,"files",rel);
  }
  result["textures.ini"]=Path.Combine(Root,Json.S(variant,"ini"));
  // Every referenced texture must be supplied either by the HD pack or this add-on.
  foreach(Match match in Regex.Matches(File.ReadAllText(result["textures.ini"]),@"(?m)^[0-9a-fA-F]{24}[ \t]*=[ \t]*(.*?)[ \t\r]*$")) {
   string rel=match.Groups[1].Value;if(rel=="")continue;Engine.SafeRelative(rel);
   if(!result.ContainsKey(rel)){string existing=Path.Combine(textures,rel);Engine.NoLinkParents(existing);if(!File.Exists(existing))throw new Exception("HD 이미지가 누락되었습니다. HD도 함께 설치하세요: "+rel);}
  }
  return result;
 }
}
public static class PpssppSettings {
 public static string Set(string text,string section,string key,string value) {
  string newline=text.Contains("\r\n")?"\r\n":"\n";var lines=text.Replace("\r\n","\n").Split('\n').ToList();
  bool inside=false,foundSection=false,foundKey=false;int insert=lines.Count;
  for(int i=0;i<lines.Count;i++) {
   var header=Regex.Match(lines[i],@"^\s*\[([^\]]+)\]\s*$");
   if(header.Success){if(inside&&!foundKey)insert=i;inside=String.Equals(header.Groups[1].Value,section,StringComparison.OrdinalIgnoreCase);foundSection|=inside;continue;}
   if(inside&&Regex.IsMatch(lines[i],@"^\s*"+Regex.Escape(key)+@"\s*=",RegexOptions.IgnoreCase)){lines[i]=key+" = "+value;foundKey=true;}
  }
  if(!foundKey){if(!foundSection){lines.Add("["+section+"]");lines.Add(key+" = "+value);}else lines.Insert(insert,key+" = "+value);}
  return String.Join(newline,lines)+(!lines.Last().EndsWith(newline)?newline:"");
 }
 public static string Preset(string text,bool graphics,bool cheats) {
  if(graphics){text=Set(text,"Graphics","InternalResolution","8");text=Set(text,"Graphics","MultiSampleLevel","2");text=Set(text,"Graphics","VerticalSync","True");text=Set(text,"Graphics","ReplaceTextures","True");}
  if(cheats)text=Set(text,"General","EnableCheats","True");return text;
 }
}
}
