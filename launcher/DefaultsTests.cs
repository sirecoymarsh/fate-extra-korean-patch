using System;using System.IO;using System.Linq;using System.Reflection;using System.Text.RegularExpressions;using System.Windows.Forms;using FateLauncher;
class DefaultsTests {
 static int tests;static void Check(bool value,string name){if(!value)throw new Exception(name);tests++;Console.WriteLine("PASS "+name);}
 [STAThread]static void Main(string[] args){try{Run(args);}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}}
 static void Run(string[] args){string w=args[0];foreach(bool embedded in new[]{false,true}) {
  string root=Path.Combine(w,"defaults",embedded?"embedded":"separate"),memory=Path.Combine(root,"memory"),sys=Path.Combine(memory,"PSP","SYSTEM");Directory.CreateDirectory(sys);
  string original="[General]\r\nEnableCheats = False\r\n[Graphics]\r\nInternalResolution = 2\r\n[Sound]\r\nEnable = False\r\n"+(embedded?"[ControlMapping]\r\nCross = ORIGINAL\r\n":"");File.WriteAllText(Path.Combine(sys,"ppsspp.ini"),original);File.WriteAllText(Path.Combine(sys,"controls.ini"),"[ControlMapping]\r\nCross = SEPARATE\r\n");
  var config=new Settings{DataRoot=Path.Combine(root,"data"),Memstick=memory};var e=new Engine(config,Path.Combine(root,"config.json"),Path.Combine(w,"build","tools"));e.RunningCheck=()=>false;e.ConfigureDefaults();string current=File.ReadAllText(Path.Combine(sys,"NPJH50247_ppsspp.ini"));
  Check(current.Contains("InternalResolution = 8")&&current.Contains("MultiSampleLevel = 2")&&current.Contains("VerticalSync = True")&&current.Contains("ReplaceTextures = True"),"four requested defaults "+embedded);
  Check(Regex.Matches(current,@"(?m)^\[ControlMapping\]").Count==1&&current.Contains("Cross = "+(embedded?"ORIGINAL":"SEPARATE")),"control mapping seeded once "+embedded);
  Check(current.Contains("EnableCheats = False")&&current.Contains("Enable = False"),"no cheat file leaves cheats and audio unchanged "+embedded);
  Check(File.ReadAllText(Path.Combine(sys,"ppsspp.ini"))==original,"global config byte-preserved "+embedded);
  e.RunningCheck=()=>true;bool rejected=false;try{e.ConfigureDefaults();}catch(Exception ex){rejected=ex.Message.Contains("종료");}Check(rejected,"running emulator blocks configuration "+embedded);
 }
 using(var form=new LauncherForm(Path.Combine(w,"defaults","form"),true)) {
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;var type=form.GetType();var b=(CheckBox)type.GetField("baseBox",flags).GetValue(form);var ui=(CheckBox)type.GetField("uiBox",flags).GetValue(form);var mem=(Control)type.GetField("memstick",flags).GetValue(form);var exe=(Control)type.GetField("emulator",flags).GetValue(form);var source=(Control)type.GetField("source",flags).GetValue(form);
  Check(!ui.Checked&&source.Enabled&&!mem.Enabled&&!exe.Enabled,"default base-only requires no emulator/profile");b.Checked=false;ui.Checked=true;Check(mem.Enabled&&!source.Enabled&&!exe.Enabled,"UI only requires destination profile without ISO/emulator");
  Check(!((Button)type.GetField("uiRemove",flags).GetValue(form)).Enabled,"removal waits for compatible update feed");
  var cfg=(Settings)type.GetField("config",flags).GetValue(form);cfg.HdVersion="HD-v40";type.GetMethod("RefreshInstalled",flags).Invoke(form,null);Check(((Label)type.GetField("current",flags).GetValue(form)).Text.Contains("통합 v40"),"legacy v40 shown as integrated UI");
  Check(!Directory.Exists(Path.Combine(w,"defaults","form","Data")),"constructing or changing selection installs nothing");
 }
 Json.Write(Path.Combine(w,"defaults-tests.json"),new{status="PASS",tests=tests,computer_use=false});
 }
}
