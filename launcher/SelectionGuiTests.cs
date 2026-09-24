using System;using System.IO;using System.Linq;using System.Threading;using System.Reflection;using System.Diagnostics;using System.Windows.Forms;using System.Drawing;using FateLauncher;
public static class SelectionGuiTests {
 static int count;static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(object form,string name){return form.GetType().GetField(name,flags).GetValue(form);}
 static void Assert(bool ok,string label){if(!ok)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
 static void Wait(LauncherForm form){var t=Stopwatch.StartNew();do{Application.DoEvents();Thread.Sleep(20);}while((bool)Field(form,"busy")&&t.Elapsed.TotalSeconds<95);Assert(!(bool)Field(form,"busy"),"pending GUI operation finishes");SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());}
 static void Snapshot(LauncherForm form,string path){using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(path);}}
 [STAThread]public static void Main(string[] args){try{Run(args);}catch(Exception e){Console.Error.WriteLine(e);Environment.ExitCode=1;}}
 static void Run(string[] args){string work=args[0],root=args[1],expected=args[2];Directory.CreateDirectory(root);Application.EnableVisualStyles();
  Json.Write(Path.Combine(root,"launcher-settings.json"),new Settings());
  using(var form=new LauncherForm(root,false)){form.Opacity=0;form.ShowInTaskbar=false;form.Show();Wait(form);
   var source=(ComboBox)Field(form,"source");var exe=(ComboBox)Field(form,"emulator");var mem=(ComboBox)Field(form,"memstick");var b=(CheckBox)Field(form,"baseBox");var h=(CheckBox)Field(form,"hdBox");var c=(CheckBox)Field(form,"cheatsBox");var s=(CheckBox)Field(form,"saveBox");
   Assert(Field(form,"release")!=null,"base-only startup checks update feed");Assert(source.Enabled&&!exe.Enabled&&!mem.Enabled,"base-only enables only source path");Assert(exe.Items.Count==0&&mem.Items.Count==0&&Field(form,"discoveries")==null,"base-only startup performs no emulator/profile discovery");Assert(((Button)Field(form,"install")).Enabled,"base-only install available without emulator/profile");Snapshot(form,Path.Combine(work,"base-only.png"));
   b.Checked=false;h.Checked=true;Assert(!source.Enabled&&!exe.Enabled&&mem.Enabled,"HD-only asks for texture destination without emulator");Assert(Field(form,"discoveries")==null&&!((Button)Field(form,"autoFind")).Enabled,"HD-only does not start device scan");
   h.Checked=false;c.Checked=true;Wait(form);Assert(!source.Enabled&&exe.Enabled&&mem.Enabled,"cheats-only enables device paths without ISO");Assert(exe.Items.Contains(expected),"checking cheats triggers actual PPSSPP discovery");
   exe.Text=expected;typeof(ComboBox).GetMethod("OnSelectionChangeCommitted",flags).Invoke(exe,new object[]{EventArgs.Empty});Assert(mem.Text!="","cheats selection pairs the memory stick");Snapshot(form,Path.Combine(work,"cheats-selected.png"));
   string paired=mem.Text;c.Checked=false;Assert(!exe.Enabled&&!mem.Enabled&&mem.Text==paired,"unchecking extras disables paths without clearing selection");mem.Text="";s.Checked=true;Wait(form);Assert(!source.Enabled&&exe.Enabled&&mem.Enabled&&exe.Items.Contains(expected),"save-only also triggers discovery without ISO");
   s.Checked=false;b.Checked=true;Wait(form);Assert(!exe.Enabled&&!mem.Enabled,"return to base-only removes device requirement");Assert(!Directory.Exists(Path.Combine(root,"Data","downloads"))&&!Directory.Exists(Path.Combine(root,"Data","games")),"selection changes never install or download components");form.Close();
  }
  Json.Write(Path.Combine(work,"selection-gui-results.json"),new{status="PASS",tests=count});
 }
}
