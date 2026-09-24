using System;using System.IO;using System.Linq;using System.Threading;using System.Reflection;using System.Diagnostics;using System.Windows.Forms;using System.Drawing;using FateLauncher;
public static class DeviceGuiTests {
 static int count;static void Assert(bool b,string text){if(!b)throw new Exception(text);count++;Console.WriteLine("PASS "+text);}
 static readonly BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
 static object Field(object form,string name){return form.GetType().GetField(name,flags).GetValue(form);}
 static void Choose(ComboBox box,string item){box.Text=item;typeof(ComboBox).GetMethod("OnSelectionChangeCommitted",flags).Invoke(box,new object[]{EventArgs.Empty});}
 [STAThread]public static void Main(string[] args){try{Run(args);}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}}
 static void Run(string[] args){string work=Path.GetFullPath(args[0]),expected=args[1],memory=args[2],root=args.Length>3?Path.GetFullPath(args[3]):Path.Combine(work,"gui-actual");Directory.CreateDirectory(root);Application.EnableVisualStyles();string ini=Path.Combine(Engine.PspRoot(memory),"SYSTEM","ppsspp.ini"),before=File.ReadAllText(ini);
  Json.Write(Path.Combine(root,"launcher-settings.json"),new Settings{SelectBase=false,SelectCheats=true});
  using(var form=new LauncherForm(root,false)){form.Opacity=0;form.ShowInTaskbar=false;form.Show();var timer=Stopwatch.StartNew();do{Application.DoEvents();Thread.Sleep(30);}while((bool)Field(form,"busy")&&timer.Elapsed.TotalSeconds<95);
   Assert(!(bool)Field(form,"busy")&&Field(form,"release")!=null,"startup discovery and release check finish");var exe=(ComboBox)Field(form,"emulator");var mem=(ComboBox)Field(form,"memstick");Assert(exe.Items.Contains(expected),"actual player executable listed in UI");Assert(mem.Items.Contains(memory),"actual player memory stick listed in UI");Choose(exe,expected);Assert(mem.Text==memory,"selecting PPSSPP fills its memory stick");
   using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(work,"gui-paired.png"));}
   var alternatives=exe.Items.Cast<string>().Where(p=>p!=expected).ToArray();Assert(alternatives.Length>0,"multiple installation fixture available");Choose(mem,memory);Choose(exe,alternatives[0]);Assert(mem.Text==memory,"explicitly selected profile preserved on emulator change");string manual=Path.Combine(root,"manual-profile");mem.Text=manual;Choose(exe,expected);Assert(mem.Text==manual,"typed profile preserved");Assert(!Directory.Exists(Path.Combine(root,"Data","downloads"))&&!Directory.Exists(Path.Combine(root,"Data","games")),"discovery performs no install/download");form.Close();}
  Assert(File.ReadAllText(ini)==before,"existing player config untouched");Json.Write(Path.Combine(work,"device-gui-results.json"),new{status="PASS",tests=count});
 }
}
