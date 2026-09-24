using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace FateLauncher {
public sealed class LauncherForm : Form {
 readonly string settingsPath,tools,launcherRoot;
 Settings config; ReleaseInfo release; Engine engine; CancellationTokenSource cancel;
 bool busy,preview; TextBox source,emulator,memstick,dataRoot,log;
 Label current,available,message,selectionSummary;
 CheckBox baseBox,hdBox,cheatsBox,saveBox;
 Button check,install,play,stop,autoFind,folderFind; ProgressBar bar; TableLayoutPanel paths,components;
 readonly Color bg=Color.FromArgb(9,20,34),panel=Color.FromArgb(17,36,55),ink=Color.FromArgb(224,239,249),muted=Color.FromArgb(152,179,198),cyan=Color.FromArgb(77,220,239);
 public LauncherForm(string root,bool previewMode) {
  preview=previewMode;launcherRoot=root;settingsPath=Path.Combine(root,"launcher-settings.json");tools=Path.Combine(root,"tools");
  config=preview?new Settings():Settings.Load(settingsPath);if(config.DataRoot=="")config.DataRoot=Path.Combine(root,"Data");
  Text="Fate/EXTRA 한국어 패치 런처";ClientSize=new Size(980,810);MinimumSize=new Size(900,760);StartPosition=FormStartPosition.CenterScreen;
  AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("맑은 고딕",10F);BackColor=bg;ForeColor=ink;
  var outer=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(26,18,26,18),ColumnCount=1,RowCount=10};Controls.Add(outer);
  foreach(float h in new[]{88F,68F,202F,42F,119F,32F,62F,32F})outer.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
  outer.RowStyles.Add(new RowStyle(SizeType.Percent,100));outer.RowStyles.Add(new RowStyle(SizeType.Absolute,26));
  var header=new Panel{Dock=DockStyle.Fill};outer.Controls.Add(header,0,0);
  var brand=new Label{Text="FATE / EXTRA",Font=new Font("Segoe UI",27,FontStyle.Bold),ForeColor=ink,AutoSize=true,Location=new Point(0,0)};header.Controls.Add(brand);
  header.Controls.Add(new Label{Text="한국어 패치 런처   ·   "+Engine.Version,AutoSize=true,ForeColor=muted,Location=new Point(3,53)});
  var site=Button("GitHub 배포 페이지",()=>Open("https://github.com/"+Engine.Repository+"/releases/latest"));site.Dock=DockStyle.Top;site.Height=36;
  var sitePanel=new Panel{Dock=DockStyle.Right,Width=180,Padding=new Padding(0,12,0,0)};sitePanel.Controls.Add(site);header.Controls.Add(sitePanel);
  var status=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,BackColor=panel,Padding=new Padding(13,8,13,8)};status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));outer.Controls.Add(status,0,1);
  current=Label("설치된 버전",ink);available=Label("GitHub 버전을 확인합니다.",cyan);status.Controls.Add(current,0,0);status.Controls.Add(available,1,0);
  paths=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=4,Padding=new Padding(0,12,0,6)};paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,164));paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,86));outer.Controls.Add(paths,0,2);
  source=PathRow(0,"일본판 원본 ISO",config.SourceIso,false,"ISO 이미지|*.iso");emulator=PathRow(1,"PPSSPP 실행 파일",config.Emulator,false,"PPSSPP 실행 파일|PPSSPPWindows*.exe|실행 파일|*.exe");
  memstick=PathRow(2,"메모리스틱 폴더",config.Memstick,true,"");dataRoot=PathRow(3,"본편·다운로드 폴더",config.DataRoot,true,"");
  var searchRow=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};outer.Controls.Add(searchRow,0,3);
  autoFind=Button("경로 자동 찾기",async()=>await FindPaths(false,false));folderFind=Button("폴더 안에서 찾기…",async()=>await FindPaths(false,true));foreach(var b in new[]{autoFind,folderFind}){b.Width=155;b.Height=33;searchRow.Controls.Add(b);}
  searchRow.Controls.Add(new Label{Text="빈 경로를 채우고, 후보가 여러 개면 선택합니다.",AutoSize=true,ForeColor=muted,Margin=new Padding(12,8,0,0),Font=new Font("맑은 고딕",9)});
  var options=new Panel{Dock=DockStyle.Fill,BackColor=panel,Padding=new Padding(12)};outer.Controls.Add(options,0,4);
  var optionsTitle=new Label{Text="설치할 항목",ForeColor=ink,AutoSize=true,Font=new Font(Font,FontStyle.Bold),Location=new Point(14,10)};options.Controls.Add(optionsTitle);
  components=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=70,ColumnCount=4,RowCount=2};for(int i=0;i<4;i++)components.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));options.Controls.Add(components);
  baseBox=Option(0,"본편 한국어 패치","원본 ISO로 새 본편 생성",config.SelectBase);hdBox=Option(1,"HD 리팩","선택 · 약 2.1 GB 다운로드",config.SelectHD);
  cheatsBox=Option(2,"치트","19종 · 모두 꺼진 상태",config.SelectCheats);saveBox=Option(3,"클리어 세이브","캐스터 Lv.52 · DATA80",config.SelectSave);
  selectionSummary=Label("선택한 자료만 설치합니다. 기존 HD·치트·세이브는 자동 백업합니다.",muted);selectionSummary.Font=new Font("맑은 고딕",9);outer.Controls.Add(selectionSummary,0,5);
  var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Padding=new Padding(0,9,0,4)};outer.Controls.Add(buttons,0,6);
  check=Button("업데이트 확인",()=>CheckLatest());install=Button("선택 항목 설치 / 업데이트",()=>Install());play=Button("게임 실행",()=>Launch());stop=Button("취소",()=>{if(cancel!=null)cancel.Cancel();});
  check.Width=145;install.Width=240;play.Width=150;stop.Width=90;install.BackColor=cyan;install.ForeColor=bg;play.BackColor=Color.FromArgb(31,91,121);
  foreach(var b in new[]{check,install,play,stop}){b.Height=42;b.Margin=new Padding(0,0,10,0);buttons.Controls.Add(b);}install.Enabled=false;stop.Enabled=false;
  var folders=Button("설치 폴더",()=>{CaptureSettings();Directory.CreateDirectory(config.DataRoot);Open(config.DataRoot);});folders.Width=120;folders.Height=42;buttons.Controls.Add(folders);
  var progressPanel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));outer.Controls.Add(progressPanel,0,7);
  message=Label("새 버전은 알림만 표시하며, 버튼을 눌러야 다운로드합니다.",muted);message.Font=new Font("맑은 고딕",9);progressPanel.Controls.Add(message,0,0);
  bar=new ProgressBar{Dock=DockStyle.Fill,Height=16,Margin=new Padding(8,6,0,6)};progressPanel.Controls.Add(bar,1,0);
  log=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=panel,ForeColor=muted,BorderStyle=BorderStyle.None,Font=new Font("맑은 고딕",9)};outer.Controls.Add(log,0,8);
  var footer=new Label{Dock=DockStyle.Fill,Text="원본 ISO·에뮬레이터는 별도로 준비하세요. PSP 실기 호환은 미검증입니다.",ForeColor=muted,TextAlign=ContentAlignment.BottomLeft,Font=new Font("맑은 고딕",9)};outer.Controls.Add(footer,0,9);
  RefreshInstalled();FormClosing+=(s,e)=>{if(busy){e.Cancel=true;if(cancel!=null)cancel.Cancel();SetMessage("작업을 취소하고 정리하는 중입니다. 완료 후 창을 닫아 주세요.");}else if(!preview){try{CaptureSettings();Json.Write(settingsPath,config);}catch{}}};
  if(!preview)Shown+=async(s,e)=>{await FindPaths(true,false);CheckLatest();};else{available.Text="새 버전  v8e + HD v39\n업데이트 버튼으로 적용";current.Text="설치됨  본편 v8d · HD v38\n치트 미설치 · 클리어 세이브 미설치";log.Text="시작 시 원본 ISO와 PPSSPP 위치를 찾아 빈 경로를 채웁니다.\r\n여러 후보가 있으면 경로 자동 찾기를 눌러 고를 수 있습니다.";}
 }
 Label Label(string text,Color color) {return new Label{Dock=DockStyle.Fill,Text=text,ForeColor=color,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};}
 Button Button(string text,Action click) {var b=new Button{Text=text,FlatStyle=FlatStyle.Flat,BackColor=panel,ForeColor=ink,Cursor=Cursors.Hand};b.FlatAppearance.BorderColor=Color.FromArgb(42,83,109);b.Click+=(s,e)=>click();return b;}
 TextBox PathRow(int row,string caption,string value,bool folder,string filter) {
  paths.RowStyles.Add(new RowStyle(SizeType.Percent,25));paths.Controls.Add(Label(caption,muted),0,row);
  var box=new TextBox{Dock=DockStyle.Fill,Text=value,BackColor=panel,ForeColor=ink,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,8,8,7)};paths.Controls.Add(box,1,row);
  var b=Button("찾기…",()=>{
   if(folder){using(var d=new FolderBrowserDialog{Description=caption+"를 선택하세요.",SelectedPath=Directory.Exists(box.Text)?box.Text:""})if(d.ShowDialog(this)==DialogResult.OK)box.Text=d.SelectedPath;}
   else using(var d=new OpenFileDialog{Title=caption+" 선택",Filter=filter,CheckFileExists=true})if(d.ShowDialog(this)==DialogResult.OK){box.Text=d.FileName;if(row==1&&memstick.Text=="")memstick.Text=Discovery.MemoryFor(d.FileName,Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));}
  });b.Dock=DockStyle.Fill;b.Margin=new Padding(0,5,0,5);paths.Controls.Add(b,2,row);return box;
 }
 static string Pick(IWin32Window owner,string title,System.Collections.Generic.List<string> choices) {
  if(choices.Count==0)return "";if(choices.Count==1)return choices[0];using(var dialog=new Form{Text=title,Width=840,Height=280,StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false}) {
   var list=new ListBox{Dock=DockStyle.Fill,HorizontalScrollbar=true};list.Items.AddRange(choices.Cast<object>().ToArray());list.SelectedIndex=0;dialog.Controls.Add(list);
   var ok=new Button{Text="선택",Dock=DockStyle.Bottom,Height=38,DialogResult=DialogResult.OK};dialog.Controls.Add(ok);dialog.AcceptButton=ok;
   return dialog.ShowDialog(owner)==DialogResult.OK?Convert.ToString(list.SelectedItem):"";
  }
 }
 public static void FillPaths(DiscoveryResult result,Settings target) {
  if(target.SourceIso==""&&result.Isos.Count==1)target.SourceIso=result.Isos[0];if(target.Emulator==""&&result.Emulators.Count==1)target.Emulator=result.Emulators[0];
  if(target.Memstick==""){string memory;if(result.EmulatorMemsticks.TryGetValue(target.Emulator,out memory))target.Memstick=memory;else if(result.Emulators.Count<=1&&result.Memsticks.Count==1)target.Memstick=result.Memsticks[0];}
 }
 async Task FindPaths(bool startup,bool chooseFolder) {
  if(busy)return;string folder="";if(chooseFolder)using(var dialog=new FolderBrowserDialog{Description="원본 ISO 또는 PPSSPP가 있는 상위 폴더를 선택하세요."}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;folder=dialog.SelectedPath;}
  if(startup&&source.Text!=""&&emulator.Text!=""&&memstick.Text!="")return;
  cancel=new CancellationTokenSource();Busy(true);try {
   var snapshot=new Settings{SourceIso=source.Text.Trim(),Emulator=emulator.Text.Trim(),Memstick=memstick.Text.Trim()};SetMessage("원본 ISO·PPSSPP·메모리스틱 위치를 찾는 중…");
   var result=await Task.Run(()=>{var search=new Discovery(cancel.Token,null,chooseFolder?10000:2500,chooseFolder?45:15);return chooseFolder?search.Search(new[]{folder},8,snapshot):search.Common(launcherRoot,snapshot);});
   FillPaths(result,snapshot);
   if(!startup){if(snapshot.SourceIso=="")snapshot.SourceIso=Pick(this,"원본 ISO 선택",result.Isos);if(snapshot.Emulator=="")snapshot.Emulator=Pick(this,"PPSSPP 선택",result.Emulators);FillPaths(result,snapshot);if(snapshot.Memstick=="")snapshot.Memstick=Pick(this,"메모리스틱 선택",result.Memsticks);}
   source.Text=snapshot.SourceIso;emulator.Text=snapshot.Emulator;memstick.Text=snapshot.Memstick;
   SetMessage("탐색 완료 · 원본 후보 "+result.Isos.Count+"개, PPSSPP "+result.Emulators.Count+"개. "+(result.Limited?"남은 위치는 폴더 안에서 찾기를 사용하세요.":"빈 칸은 자동 찾기 또는 찾기…로 지정하세요."));
  }catch(OperationCanceledException){SetMessage("경로 탐색을 취소했습니다.");}catch(Exception e){SetMessage("경로 탐색: "+e.Message);}finally{Busy(false);cancel.Dispose();cancel=null;}
 }
 CheckBox Option(int column,string caption,string description,bool selected) {
  var b=new CheckBox{Text=caption,Checked=selected,Dock=DockStyle.Fill,ForeColor=ink,AutoSize=true};components.Controls.Add(b,column,0);
  var note=Label(description,muted);note.Font=new Font("맑은 고딕",8.5F);components.Controls.Add(note,column,1);return b;
 }
 void CaptureSettings() {
  bool installed=config.BaseVersion!=""||config.HdVersion!=""||config.CheatsVersion!=""||config.SaveVersion!=""||File.Exists(Path.Combine(config.DataRoot,"install-journal.json"));
  string next=Path.GetFullPath(dataRoot.Text.Trim());if(installed&&!String.Equals(next,config.DataRoot,StringComparison.OrdinalIgnoreCase))throw new Exception("설치한 본편 폴더는 여기서 옮길 수 없습니다. 새 폴더에서 런처를 따로 시작하세요.");
  string memory=memstick.Text.Trim();if(installed&&config.Memstick!=""&&!String.Equals(memory,config.Memstick,StringComparison.OrdinalIgnoreCase))throw new Exception("설치한 메모리스틱 폴더는 여기서 옮길 수 없습니다. 새 폴더에서 런처를 따로 시작하세요.");
  config.SourceIso=source.Text.Trim();config.Emulator=emulator.Text.Trim();config.Memstick=memory;
  config.DataRoot=next;config.SelectBase=baseBox.Checked;config.SelectHD=hdBox.Checked;config.SelectCheats=cheatsBox.Checked;config.SelectSave=saveBox.Checked;
 }
 Engine EngineForWork() {CaptureSettings();Json.Write(settingsPath,config);var e=new Engine(config,settingsPath,tools);e.RecoverPending();config=e.Config;e.Progress=(s,p)=>{if(!IsDisposed)BeginInvoke((Action)(()=>{SetMessage(s);if(p>=0){bar.Style=ProgressBarStyle.Continuous;bar.Value=Math.Max(0,Math.Min(100,p));}else bar.Style=ProgressBarStyle.Marquee;}));};e.Cancel=cancel.Token;return e;}
 string V(string s){return s==""?"미설치":s;}
 void RefreshInstalled(){current.Text="본편 "+V(config.BaseVersion)+"  ·  HD "+V(config.HdVersion)+"\n치트 "+V(config.CheatsVersion)+"  ·  클리어 세이브 "+V(config.SaveVersion);if(play!=null)play.Enabled=!busy&&File.Exists(config.GameIso);}
 void SetMessage(string s){if(message.Text!=s){message.Text=s;log.AppendText(DateTime.Now.ToString("HH:mm")+"  "+s+Environment.NewLine);}}
 void Busy(bool value){busy=value;check.Enabled=!value;install.Enabled=!value&&release!=null;play.Enabled=!value&&File.Exists(config.GameIso);stop.Enabled=value;paths.Enabled=!value;components.Enabled=!value;autoFind.Enabled=!value;folderFind.Enabled=!value;if(!value){bar.Style=ProgressBarStyle.Continuous;RefreshInstalled();}}
 async void CheckLatest() {
  if(busy)return;cancel=new CancellationTokenSource();Busy(true);
  try{engine=EngineForWork();SetMessage("GitHub 새 버전 확인 중…");release=await Task.Run(()=>engine.Latest());available.Text="배포 중  "+release.BaseVersion+" + "+release.HdVersion+"\n"+((config.BaseVersion==release.BaseVersion&&(!config.SelectHD||config.HdVersion==release.HdVersion))?"선택한 설치 버전이 최신입니다.":"선택 항목 설치 / 업데이트 버튼으로 적용");SetMessage("확인 완료. 설치할 항목을 고른 뒤 업데이트 버튼을 눌러 주세요.");}
  catch(Exception e){available.Text="새 버전을 확인하지 못했습니다.";SetMessage(e is OperationCanceledException?"확인을 취소했습니다.":e.Message+"  기존 설치본은 실행할 수 있습니다.");}
  finally{Busy(false);cancel.Dispose();cancel=null;}
 }
 async void Install() {
  if(busy||release==null)return;cancel=new CancellationTokenSource();Busy(true);
  try{engine=EngineForWork();bool b=baseBox.Checked,h=hdBox.Checked,c=cheatsBox.Checked,s=saveBox.Checked;await Task.Run(()=>engine.Install(release,b,h,c,s));config=engine.Config;RefreshInstalled();SetMessage("설치 완료. 게임 실행 버튼으로 시작하세요.");}
  catch(Exception e){if(engine!=null)config=engine.Config;SetMessage(e is OperationCanceledException?"취소했습니다. 받은 데이터는 다음에 이어받습니다.":e.Message);}
  finally{Busy(false);cancel.Dispose();cancel=null;}
 }
 void Launch() {
  try{CaptureSettings();if(busy||Engine.GameRunning())throw new Exception("이미 PPSSPP가 실행 중입니다.");if(!File.Exists(config.Emulator))throw new Exception("PPSSPP 실행 파일을 지정해 주세요.");if(!File.Exists(config.GameIso))throw new Exception("본편 패치를 먼저 설치해 주세요.");
   if(config.Memstick=="")throw new Exception("세이브와 HD를 사용할 메모리스틱 폴더를 지정해 주세요.");Directory.CreateDirectory(config.Memstick);Json.Write(settingsPath,config);
   var launcherEngine=new Engine(config,settingsPath,tools);Process.Start(launcherEngine.GameStartInfo());SetMessage("PPSSPP를 실행했습니다. 예전 상태 저장 대신 게임 내 불러오기를 사용하세요.");
  }catch(Exception e){SetMessage(e.Message);}
 }
 static void Open(string target){try{Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}catch(Exception e){MessageBox.Show(e.Message);}}
}
public static class Program {
 [STAThread] public static void Main(string[] args) {
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  string root=AppDomain.CurrentDomain.BaseDirectory;bool preview=args.Length==2&&args[0]=="--preview";
  try{using(var mutex=new Mutex(false,"Local\\FateExtraKoreanLauncher")){if(!preview&&!mutex.WaitOne(0)){MessageBox.Show("런처가 이미 실행 중입니다.");return;}try{
   using(var form=new LauncherForm(root,preview)){if(preview){form.Opacity=0;form.ShowInTaskbar=false;form.Show();Application.DoEvents();form.PerformLayout();using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.GetFullPath(args[1]));}form.Hide();}else Application.Run(form);}
  }finally{if(!preview)mutex.ReleaseMutex();}}}catch(Exception e){MessageBox.Show(e.Message,"Fate/EXTRA 런처",MessageBoxButtons.OK,MessageBoxIcon.Error);}
 }
}
}
