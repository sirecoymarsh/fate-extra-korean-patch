using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Drawing.Drawing2D;

namespace FateLauncher {
// 1.6.0 화면 원칙: 처음 켜면 경로는 저절로 채워지고, 고를 것은 「전체 / 본편만」 하나, 누를 것은 「설치」 하나.
// 나머지(개별 항목·설치 방식·설치 폴더·도구 버튼·기록)는 「고급」 아래에 접어 둔다.
// 필드 이름(source, emulator, memstick, baseBox, hdBox, uiBox, cheatsBox, saveBox, install, play, autoFind, uiRemove, graphics,
// release, discoveries)은 기존 시험 하네스가 리플렉션으로 찾으므로 그대로 둔다.
public sealed class LauncherForm : Form {
 readonly string settingsPath,tools,launcherRoot;
 Settings config; ReleaseInfo release; Engine engine; CancellationTokenSource cancel;
 bool busy,preview,shown,launchSetup,previousExtras,previousBase,previousMemory,advancedOpen,syncing,firstRun;
 Control source,emulator,memstick,dataRoot;TextBox log;DiscoveryResult discoveries;string automaticMemory="";
 Label current,available,message,selectionSummary;
 CheckBox baseBox,hdBox,uiBox,cheatsBox,saveBox;
 RadioButton fullPlan,basePlan,customPlan;
 ComboBox installMode;
 Button check,install,play,stop,autoFind,folderFind,uiRemove,graphics,advanced,folders,ppsspp; ProgressStrip bar; TableLayoutPanel outer,paths,components; Panel advancedPanel; Banner banner; string artPath=""; int artToken;
 readonly Color bg=Color.FromArgb(9,20,34),panel=Color.FromArgb(17,36,55),ink=Color.FromArgb(224,239,249),muted=Color.FromArgb(152,179,198),cyan=Color.FromArgb(77,220,239),navy=Color.FromArgb(31,91,121);
 const int SimpleHeight=572,AdvancedHeight=260;
 public LauncherForm(string root,bool previewMode) {
  preview=previewMode;launcherRoot=root;settingsPath=Path.Combine(root,"launcher-settings.json");tools=Path.Combine(root,"tools");
  firstRun=!preview&&!File.Exists(settingsPath);
  config=preview?new Settings():Settings.Load(settingsPath);if(config.DataRoot=="")config.DataRoot=Path.Combine(root,"Data");
  if(firstRun){config.SelectBase=true;config.SelectHD=true;config.SelectUI=true;}
  config.Memstick=Engine.MemstickRoot(config.Memstick);
  Text="Fate/EXTRA 한국어 패치";ClientSize=new Size(900,SimpleHeight);MinimumSize=new Size(880,SimpleHeight+40);StartPosition=FormStartPosition.CenterScreen;
  AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("맑은 고딕",10F);BackColor=bg;ForeColor=ink;
  outer=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(26,14,26,10),ColumnCount=1,RowCount=8};Controls.Add(outer);
  foreach(float h in new[]{132F,56F,126F,108F,58F,42F,0F})outer.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
  outer.RowStyles.Add(new RowStyle(SizeType.Percent,100));
  // 0. 머리
  // 0. 머리 배너 — 사용자 ISO 의 키 비주얼(PIC1)과 로고(ICON0)를 실행 때 읽어 그린다. 없으면 글자 로고.
  banner=new Banner(bg,ink,muted){Dock=DockStyle.Fill,Subtitle="한국어 패치   ·   런처 "+Engine.Version,Margin=new Padding(0,0,0,4)};outer.Controls.Add(banner,0,0);
  // 1. 상태 띠: 왼쪽 = 이 컴퓨터, 가운데 = 최신 배포, 오른쪽 = 배포 페이지
  var status=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,BackColor=panel,Padding=new Padding(13,3,6,3)};status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,104));outer.Controls.Add(status,0,1);
  current=Label("",ink);available=Label("최신 배포를 확인하는 중…",cyan);status.Controls.Add(current,0,0);status.Controls.Add(available,1,0);
  var site=Button("배포 페이지",()=>Open("https://github.com/"+Engine.Repository+"/releases/latest"));site.Dock=DockStyle.Fill;site.Margin=new Padding(0,6,0,6);site.Font=new Font("맑은 고딕",9);status.Controls.Add(site,2,0);
  // 2. 경로 세 줄 — 자동으로 채워지고, 바꿀 때만 손댄다
  paths=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=3,Padding=new Padding(0,8,0,0)};paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,86));outer.Controls.Add(paths,0,2);
  source=PathRow(0,"일본판 원본 ISO",config.SourceIso,false,"ISO 이미지|*.iso");emulator=PathRow(1,"PPSSPP 실행 파일",config.Emulator,false,"PPSSPP 실행 파일|PPSSPPWindows*.exe|실행 파일|*.exe");
  memstick=PathRow(2,"메모리스틱 폴더",config.Memstick,true,"");
  // 3. 설치 구성 — 고를 것은 하나
  var plan=new Panel{Dock=DockStyle.Fill,BackColor=panel,Padding=new Padding(12,6,12,6),Margin=new Padding(0,6,0,4)};outer.Controls.Add(plan,0,3);
  var planTable=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=3};planTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,330));planTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));plan.Controls.Add(planTable);
  fullPlan=Plan(planTable,0,"전체 설치 (추천)","본편 + HD 고화질 + 한국어 UI · 약 2.4GB · HD 팩은 PPSSPP 메모리스틱에 들어갑니다");
  basePlan=Plan(planTable,1,"본편만","한국어 대사·설명, UI는 원래 영문 · 약 250MB 다운로드, 완성 ISO 약 2GB");
  customPlan=Plan(planTable,2,"직접 선택","고급에서 항목을 하나씩 고릅니다");
  // 4. 버튼 — 설치 하나, 실행 하나
  var actions=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Padding=new Padding(0,6,0,0)};actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));actions.RowStyles.Add(new RowStyle(SizeType.Percent,100));outer.Controls.Add(actions,0,4);
  var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false};actions.Controls.Add(buttons,0,0);
  install=Button("설치",()=>Install());play=Button("게임 실행",()=>Launch());stop=Button("취소",()=>{if(cancel!=null)cancel.Cancel();});
  install.Width=270;play.Width=160;stop.Width=90;install.Font=new Font("맑은 고딕",11F,FontStyle.Bold);install.BackColor=cyan;install.ForeColor=bg;play.BackColor=navy;
  foreach(var b in new[]{install,play,stop}){b.Height=44;b.Margin=new Padding(0,0,10,0);buttons.Controls.Add(b);}install.Enabled=false;stop.Enabled=false;
  ppsspp=Button("PPSSPP 내려받기",()=>Open("https://www.ppsspp.org/download/"));ppsspp.Width=150;ppsspp.Height=44;ppsspp.Margin=new Padding(0,0,10,0);ppsspp.Visible=false;ppsspp.ForeColor=cyan;buttons.Controls.Add(ppsspp);
  advanced=Button("고급 ▼",()=>ToggleAdvanced(!advancedOpen));advanced.Dock=DockStyle.Fill;advanced.Height=44;advanced.Margin=new Padding(0,0,0,0);actions.Controls.Add(advanced,1,0);
  // 5. 한 줄 안내 + 진행 막대
  var progressPanel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,68));progressPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,32));outer.Controls.Add(progressPanel,0,5);
  message=Label("",muted);message.Font=new Font("맑은 고딕",9);message.AutoEllipsis=false;message.TextAlign=ContentAlignment.MiddleLeft;progressPanel.Controls.Add(message,0,0);
  bar=new ProgressStrip(panel,cyan,ink,bg){Dock=DockStyle.Fill,Margin=new Padding(8,7,0,7),Font=new Font("맑은 고딕",8.5F,FontStyle.Bold)};progressPanel.Controls.Add(bar,1,0);
  // 6. 고급 — 접혀 있다
  advancedPanel=new Panel{Dock=DockStyle.Fill,BackColor=panel,Padding=new Padding(12,8,12,8),Visible=false};outer.Controls.Add(advancedPanel,0,6);
  var adv=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5};foreach(float h in new[]{66F,38F,38F,44F})adv.RowStyles.Add(new RowStyle(SizeType.Absolute,h));adv.RowStyles.Add(new RowStyle(SizeType.Percent,100));advancedPanel.Controls.Add(adv);
  components=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=5,RowCount=2};for(int i=0;i<5;i++)components.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));adv.Controls.Add(components,0,0);
  baseBox=Option(0,"본편 한국어 패치","대사·설명·글꼴",config.SelectBase);hdBox=Option(1,"HD 고화질 팩","그림·글꼴 고화질 · 2.1GB",config.SelectHD);
  uiBox=Option(2,"한국어 UI","메뉴·아이콘·전투 문구 · HD 필요",config.SelectUI);
  cheatsBox=Option(3,"치트 (전부 꺼짐)","PPSSPP 안에서 골라 켬",config.SelectCheats);saveBox=Option(4,"클리어 세이브","캐스터 Lv.52 · DATA80 칸",config.SelectSave);
  var modeRow=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};adv.Controls.Add(modeRow,0,1);
  var modeLabel=new Label{Text="본편 설치 방식",ForeColor=muted,AutoSize=true,Margin=new Padding(0,9,10,0)};modeRow.Controls.Add(modeLabel);
  installMode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=470,BackColor=bg,ForeColor=ink,Margin=new Padding(0,4,0,0)};
  installMode.Items.AddRange(new object[]{"한국어 ISO 파일 만들기 (기본)","외부 데이터 로딩 · ISO를 만들지 않음 · 실행 때도 원본 ISO 필요"});installMode.SelectedIndex=config.UseExternalData?1:0;modeRow.Controls.Add(installMode);
  var dataRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4};dataRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,124));dataRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));dataRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,86));dataRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,86));adv.Controls.Add(dataRow,0,2);
  dataRow.Controls.Add(Label("설치 폴더",muted),0,0);var dataBox=new TextBox{BorderStyle=BorderStyle.FixedSingle,Dock=DockStyle.Fill,Text=config.DataRoot,BackColor=bg,ForeColor=ink,Margin=new Padding(0,6,8,4)};dataRow.Controls.Add(dataBox,1,0);dataRoot=dataBox;
  var dataFind=Button("찾기…",()=>{using(var d=new FolderBrowserDialog{Description="한국어 ISO·다운로드를 둘 폴더를 선택하세요.",SelectedPath=Directory.Exists(dataBox.Text)?dataBox.Text:""})if(d.ShowDialog(this)==DialogResult.OK)dataBox.Text=d.SelectedPath;});dataFind.Dock=DockStyle.Fill;dataFind.Margin=new Padding(0,3,6,3);dataRow.Controls.Add(dataFind,2,0);
  folders=Button("열기",()=>{try{CaptureSettings();Directory.CreateDirectory(config.DataRoot);Open(config.DataRoot);}catch(Exception e){SetMessage(e.Message);}});folders.Dock=DockStyle.Fill;folders.Margin=new Padding(0,3,0,3);dataRow.Controls.Add(folders,3,0);
  var toolRow=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};adv.Controls.Add(toolRow,0,3);
  check=Button("배포 다시 확인",()=>CheckLatest());autoFind=Button("경로 다시 찾기",async()=>await FindPaths(false,false));folderFind=Button("폴더 안에서 찾기…",async()=>await FindPaths(false,true));
  graphics=Button("PPSSPP 기본 설정 적용",()=>ApplyGraphics());uiRemove=Button("한국어 UI 해제",()=>RemoveUI());uiRemove.Enabled=false;
  foreach(var b in new[]{check,autoFind,folderFind,graphics,uiRemove}){b.Height=34;b.AutoSize=true;b.Padding=new Padding(8,0,8,0);b.Margin=new Padding(0,4,8,0);toolRow.Controls.Add(b);}
  log=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=bg,ForeColor=muted,BorderStyle=BorderStyle.None,Font=new Font("맑은 고딕",9),Margin=new Padding(0,4,0,0)};adv.Controls.Add(log,0,4);
  // 7. 바닥글
  selectionSummary=new Label{Dock=DockStyle.Fill,Text="",ForeColor=muted,TextAlign=ContentAlignment.BottomLeft,Font=new Font("맑은 고딕",9),AutoEllipsis=true};outer.Controls.Add(selectionSummary,0,7);
  RefreshInstalled();FormClosing+=(s,e)=>{if(busy){e.Cancel=true;if(cancel!=null)cancel.Cancel();SetMessage("작업을 취소하고 정리하는 중입니다. 끝나면 창을 닫아 주세요.");}else if(!preview){try{CaptureSettings();Json.Write(settingsPath,config);}catch{}}};
  previousExtras=cheatsBox.Checked||saveBox.Checked;previousBase=baseBox.Checked;previousMemory=NeedsMemory();
  foreach(var option in new[]{baseBox,hdBox,uiBox,cheatsBox,saveBox})option.CheckedChanged+=SelectionChanged;
  foreach(var r in new[]{fullPlan,basePlan,customPlan})r.CheckedChanged+=(s,e)=>{if(((RadioButton)s).Checked)ApplyPlan();};
  installMode.SelectedIndexChanged+=(s,e)=>{RefreshPathRequirements();UpdatePrimary();};
  SyncPlanFromBoxes();RefreshPathRequirements();UpdatePrimary();LoadArt();
  if(!preview)Shown+=async(s,e)=>{shown=true;await FindPaths(true,false);CheckLatest();};
  else{available.Text="최신 배포  v8h · HD v54 · 한국어 UI v4";current.Text="이 컴퓨터: 아직 설치 안 됨";message.Text="원본 ISO·PPSSPP·메모리스틱을 찾았습니다. 「설치」를 누르면 됩니다.";}
 }
 // ---------- 화면 조각 ----------
 Label Label(string text,Color color) {return new Label{Dock=DockStyle.Fill,Text=text,ForeColor=color,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};}
 Button Button(string text,Action click) {var b=new Button{Text=text,FlatStyle=FlatStyle.Flat,BackColor=panel,ForeColor=ink,Cursor=Cursors.Hand};b.FlatAppearance.BorderColor=Color.FromArgb(42,83,109);b.Click+=(s,e)=>click();return b;}
 RadioButton Plan(TableLayoutPanel table,int row,string caption,string note) {
  table.RowStyles.Add(new RowStyle(SizeType.Percent,33.3F));
  var r=new RadioButton{Text=caption,ForeColor=ink,AutoSize=true,Cursor=Cursors.Hand,Margin=new Padding(2,4,0,0)};if(row==0)r.Font=new Font(Font,FontStyle.Bold);table.Controls.Add(r,0,row);
  var n=Label(note,muted);n.Font=new Font("맑은 고딕",9);table.Controls.Add(n,1,row);return r;
 }
 Control PathRow(int row,string caption,string value,bool folder,string filter) {
  paths.RowStyles.Add(new RowStyle(SizeType.Percent,33.3F));paths.Controls.Add(Label(caption,muted),0,row);
  var box=new ComboBox{DropDownStyle=ComboBoxStyle.DropDown,DropDownWidth=900,MaxDropDownItems=12,Dock=DockStyle.Fill,Text=value,BackColor=panel,ForeColor=ink,Margin=new Padding(0,8,8,7)};paths.Controls.Add(box,1,row);
  if(row==1)box.SelectionChangeCommitted+=(s,e)=>PairSelectedEmulator();
  if(row==2){box.TextChanged+=(s,e)=>{automaticMemory="";};box.SelectionChangeCommitted+=(s,e)=>{automaticMemory="";};}
  box.TextChanged+=(s,e)=>{UpdatePrimary();RefreshPathRequirements();if(row==0)LoadArt();};
  var b=Button("찾기…",()=>{
   if(folder){using(var d=new FolderBrowserDialog{Description="PPSSPP 메모리스틱 폴더(안에 PSP 폴더가 있는 곳)를 선택하세요.",SelectedPath=Directory.Exists(box.Text)?box.Text:""})if(d.ShowDialog(this)==DialogResult.OK)box.Text=Engine.MemstickRoot(d.SelectedPath);}
   else using(var d=new OpenFileDialog{Title=caption+" 선택",Filter=filter,CheckFileExists=true})if(d.ShowDialog(this)==DialogResult.OK){box.Text=d.FileName;if(row==1&&memstick.Text=="")memstick.Text=Discovery.MemoryFor(d.FileName,Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));}
  });b.Dock=DockStyle.Fill;b.Margin=new Padding(0,5,0,5);paths.Controls.Add(b,2,row);return box;
 }
 CheckBox Option(int column,string caption,string description,bool selected) {
  var b=new CheckBox{Text=caption,Checked=selected,Dock=DockStyle.Fill,ForeColor=ink,AutoSize=true};components.Controls.Add(b,column,0);
  var note=Label(description,muted);note.Font=new Font("맑은 고딕",8.5F);components.Controls.Add(note,column,1);return b;
 }
 void ToggleAdvanced(bool open) {
  advancedOpen=open;advancedPanel.Visible=open;outer.RowStyles[6].Height=open?AdvancedHeight:0;advanced.Text=open?"고급 ▲":"고급 ▼";
  int target=open?SimpleHeight+AdvancedHeight:SimpleHeight;MinimumSize=new Size(MinimumSize.Width,target+40);ClientSize=new Size(ClientSize.Width,target);
 }
 // ---------- 선택 구성 ----------
 bool NeedsMemory() {return hdBox.Checked||uiBox.Checked||cheatsBox.Checked||saveBox.Checked||launchSetup;}
 async void ApplyPlan() {
  if(syncing)return;syncing=true;
  try{if(fullPlan.Checked){baseBox.Checked=true;hdBox.Checked=true;uiBox.Checked=true;}else if(basePlan.Checked){baseBox.Checked=true;hdBox.Checked=false;uiBox.Checked=false;}else if(!advancedOpen)ToggleAdvanced(true);}
  finally{syncing=false;}
  await AfterSelection();
 }
 void SyncPlanFromBoxes() {
  if(syncing)return;syncing=true;
  try{bool full=baseBox.Checked&&hdBox.Checked&&uiBox.Checked,baseOnly=baseBox.Checked&&!hdBox.Checked&&!uiBox.Checked;fullPlan.Checked=full;basePlan.Checked=baseOnly;customPlan.Checked=!full&&!baseOnly;}
  finally{syncing=false;}
 }
 async void SelectionChanged(object sender,EventArgs e) {SyncPlanFromBoxes();if(!syncing)await AfterSelection();}
 async Task AfterSelection() {
  bool extras=cheatsBox.Checked||saveBox.Checked,memory=NeedsMemory();
  bool discover=(memory&&!previousMemory&&memstick.Text.Trim()=="")||(baseBox.Checked&&!previousBase&&source.Text.Trim()=="");
  previousExtras=extras;previousBase=baseBox.Checked;previousMemory=memory;RefreshPathRequirements();UpdatePrimary();
  if(shown&&!preview&&!busy&&discover)await FindPaths(true,false);
 }
 void RefreshPathRequirements() {
  bool memory=NeedsMemory();
  installMode.Enabled=!busy&&baseBox.Checked;
  bool[] enabled={baseBox.Checked||(launchSetup&&config.GameData!=""),memory,memory};
  for(int row=0;row<3;row++)for(int col=0;col<3;col++)paths.GetControlFromPosition(col,row).Enabled=enabled[row];
  Control[] boxes={source,emulator,memstick};for(int row=0;row<3;row++)paths.GetControlFromPosition(0,row).ForeColor=enabled[row]&&boxes[row].Text.Trim()==""&&shown?cyan:muted;
  if(ppsspp!=null)ppsspp.Visible=!busy&&shown&&NeedsMemory()&&emulator.Text.Trim()==""&&memstick.Text.Trim()=="";
  autoFind.Enabled=folderFind.Enabled=!busy&&(baseBox.Checked||memory);
 }
 string Pretty(string v) {return v.StartsWith("HD-")||v.StartsWith("UI-")?v.Substring(3):v;}
 bool Installed() {return config.BaseVersion!="";}
 bool NeedsUpdate() {
  if(release==null||!Installed())return false;
  if(baseBox.Checked&&config.BaseVersion!=release.BaseVersion)return true;
  if(hdBox.Checked&&config.HdVersion!=release.HdVersion)return true;
  if(uiBox.Checked&&config.UiVersion!=release.UiVersion)return true;
  if(cheatsBox.Checked&&config.CheatsVersion=="")return true;if(saveBox.Checked&&config.SaveVersion=="")return true;
  return false;
 }
 // 큰 버튼의 글자는 지금 상태를 그대로 말한다: 설치 / 업데이트 / 다시 설치.
 void UpdatePrimary() {
  if(install==null||play==null)return;
  bool any=baseBox.Checked||hdBox.Checked||uiBox.Checked||cheatsBox.Checked||saveBox.Checked;
  string text=release==null?"배포 확인 중…":!any?"설치할 항목 없음":!Installed()?"한국어 패치 설치":NeedsUpdate()?"업데이트":"다시 설치";
  install.Text=text;install.Enabled=!busy&&release!=null&&any;
  bool upToDate=Installed()&&release!=null&&!NeedsUpdate();
  install.BackColor=upToDate?panel:cyan;install.ForeColor=upToDate?ink:bg;play.BackColor=upToDate?cyan:navy;play.ForeColor=upToDate?bg:ink;
  play.Enabled=!busy&&Engine.HasGame(config);
  if(!busy)selectionSummary.Text=Footer();
 }
 string Footer() {
  string need=NeedsMemory()?"원본 ISO·PPSSPP·메모리스틱":"원본 ISO";
  return "필요한 것: 일본판 "+need+" (직접 준비)   ·   PSP 실기 미검증   ·   문제가 생기면 설치 폴더의 launcher.log 를 함께 알려 주세요";
 }
 string PrimaryLabel() {return !Installed()?"한국어 패치 설치":NeedsUpdate()?"업데이트":"다시 설치";}
 string ReadyHint() {
  bool noIso=baseBox.Checked&&source.Text.Trim()=="",noEmu=NeedsMemory()&&emulator.Text.Trim()==""&&memstick.Text.Trim()=="",noMemory=NeedsMemory()&&memstick.Text.Trim()=="";
  if(noIso&&noEmu)return "원본 ISO와 PPSSPP를 찾지 못했습니다. ISO는 갖고 계신 일본판 UMD를 덤프한 파일을 「찾기…」로, PPSSPP는 「PPSSPP 내려받기」로 받아 푼 뒤 그 실행 파일을 「찾기…」로 지정해 주세요.";
  if(noIso)return "일본판 원본 ISO를 찾지 못했습니다. 갖고 계신 UMD를 덤프한 ISO 파일을 「찾기…」로 지정해 주세요.";
  if(noEmu)return "PPSSPP를 찾지 못했습니다. 아직 없다면 「PPSSPP 내려받기」로 받아 아무 폴더에나 푼 뒤, 그 안의 PPSSPPWindows64.exe 를 「찾기…」로 지정해 주세요.";
  if(noMemory)return "메모리스틱 폴더를 「찾기…」로 지정해 주세요. PPSSPP 폴더 안의 memstick 이거나 문서\\PPSSPP 입니다.";
  if(!Installed())return "준비됐습니다. 「"+PrimaryLabel()+"」를 누르면 다운로드부터 설치까지 진행합니다.";
  if(NeedsUpdate())return "새 버전이 있습니다. 「업데이트」를 누르면 됩니다. 기존 파일은 옆에 백업됩니다.";
  return "최신 상태입니다. 「게임 실행」을 누르세요.";
 }
 void PairSelectedEmulator() {
  if(discoveries==null||config.BaseVersion!=""||config.HdVersion!=""||config.UiVersion!=""||config.CheatsVersion!=""||config.SaveVersion!="")return;
  string paired;if((memstick.Text==""||memstick.Text==automaticMemory)&&discoveries.EmulatorMemsticks.TryGetValue(emulator.Text,out paired)){memstick.Text=paired;automaticMemory=paired;SetMessage("선택한 PPSSPP의 메모리스틱을 연결했습니다.");}
 }
 void ShowCandidates(DiscoveryResult result,bool isos,bool devices) {
  if(devices)discoveries=result;var boxes=new[]{(ComboBox)source,(ComboBox)emulator,(ComboBox)memstick};var lists=new[]{result.Isos,result.Emulators,result.Memsticks};
  for(int i=0;i<boxes.Length;i++){if(i==0?!isos:!devices)continue;string currentText=boxes[i].Text;boxes[i].BeginUpdate();boxes[i].Items.Clear();boxes[i].Items.AddRange(lists[i].Cast<object>().ToArray());boxes[i].Text=currentText;boxes[i].EndUpdate();}
 }
 static string Pick(IWin32Window owner,string title,List<string> choices) {
  if(choices.Count==0)return "";if(choices.Count==1)return choices[0];using(var dialog=new Form{Text=title,Width=840,Height=280,StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false}) {
   var list=new ListBox{Dock=DockStyle.Fill,HorizontalScrollbar=true};list.Items.AddRange(choices.Cast<object>().ToArray());list.SelectedIndex=0;dialog.Controls.Add(list);
   var ok=new Button{Text="선택",Dock=DockStyle.Bottom,Height=38,DialogResult=DialogResult.OK};dialog.Controls.Add(ok);dialog.AcceptButton=ok;
   return dialog.ShowDialog(owner)==DialogResult.OK?Convert.ToString(list.SelectedItem):"";
  }
 }
 // 찾은 후보는 첫 번째를 바로 넣는다. 후보는 전부 크기·PARAM.SFO 로 걸러진 것이고, 설치 직전에 해시를 다시 검사한다.
 // 다른 후보는 칸의 ▼ 목록에 남는다.
 public static void FillPaths(DiscoveryResult result,Settings target) {
  if(target.SourceIso==""&&result.Isos.Count>=1)target.SourceIso=result.Isos[0];if(target.Emulator==""&&result.Emulators.Count>=1)target.Emulator=result.Emulators[0];
  if(target.Memstick==""){string memory;if(result.EmulatorMemsticks.TryGetValue(target.Emulator,out memory))target.Memstick=memory;else if(result.Memsticks.Count>=1)target.Memstick=result.Memsticks[0];}
 }
 async Task FindPaths(bool startup,bool chooseFolder,bool forLaunch=false) {
  bool findIsos=baseBox.Checked&&!forLaunch,findDevices=NeedsMemory()||forLaunch;
  if(startup){findIsos&=source.Text.Trim()=="";findDevices&=memstick.Text.Trim()=="";}
  if(!findIsos&&!findDevices){if(startup)SetMessage(ReadyHint());return;}
  if(busy)return;string folder="";if(chooseFolder)using(var dialog=new FolderBrowserDialog{Description="원본 ISO 또는 PPSSPP가 있는 상위 폴더를 선택하세요."}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;folder=dialog.SelectedPath;}
  cancel=new CancellationTokenSource();Busy(true);try {
   var snapshot=new Settings{SourceIso=source.Text.Trim(),Emulator=emulator.Text.Trim(),Memstick=memstick.Text.Trim()};SetMessage(findDevices?(findIsos?"원본 ISO·PPSSPP·메모리스틱을 찾는 중…":"PPSSPP·메모리스틱을 찾는 중…"):"일본판 원본 ISO를 찾는 중…");
   var result=await Task.Run(()=>{var search=new Discovery(cancel.Token,null,chooseFolder?20000:2500,chooseFolder?45:15,findIsos,findDevices);var hits=chooseFolder?search.Search(new[]{folder},16,snapshot):search.Common(launcherRoot,snapshot);
    if(findDevices&&!chooseFolder&&hits.Memsticks.Count==0){BeginInvoke((Action)(()=>SetMessage("PPSSPP·메모리스틱을 드라이브 안쪽에서 찾는 중… 최대 45초")));var deep=new Discovery(cancel.Token,null,50000,45,findIsos,true);hits.Merge(deep.Devices(Discovery.DeviceRoots(launcherRoot)));hits.Prefer64Bit();}return hits;});
   ShowCandidates(result,findIsos,findDevices);bool memoryWasEmpty=snapshot.Memstick=="";
   FillPaths(result,snapshot);bool memoryInferred=memoryWasEmpty&&snapshot.Memstick!="";
   if(!startup){if(findIsos&&snapshot.SourceIso=="")snapshot.SourceIso=Pick(this,"원본 ISO 선택",result.Isos);if(findDevices&&snapshot.Emulator=="")snapshot.Emulator=Pick(this,"PPSSPP 선택",result.Emulators);FillPaths(result,snapshot);memoryInferred=memoryWasEmpty&&snapshot.Memstick!="";if(findDevices&&snapshot.Memstick=="")snapshot.Memstick=Pick(this,"메모리스틱 선택",result.Memsticks);}
   source.Text=snapshot.SourceIso;emulator.Text=snapshot.Emulator;memstick.Text=Engine.MemstickRoot(snapshot.Memstick);if(memoryWasEmpty)automaticMemory=memoryInferred?memstick.Text:"";
   int extra=(findIsos?Math.Max(0,result.Isos.Count-1):0)+(findDevices?Math.Max(0,result.Emulators.Count-1)+Math.Max(0,result.Memsticks.Count-1):0);
   var foundList=new List<string>();if(findIsos&&result.Isos.Count>0)foundList.Add("원본 ISO");if(findDevices&&result.Emulators.Count>0)foundList.Add("PPSSPP");if(findDevices&&result.Memsticks.Count>0)foundList.Add("메모리스틱");
   string found=String.Join(" · ",foundList),particle=foundList.Count>0&&foundList[foundList.Count-1]=="메모리스틱"?"을":"를";
   SetMessage((found==""?"자동으로 찾은 것이 없습니다. ":found+particle+" 자동으로 찾았습니다. ")+(extra>0?"다른 후보 "+extra+"개는 칸의 ▼에 있습니다. ":"")+ReadyHint()+(result.Limited?" 일부 경로는 탐색 한도에 걸렸습니다.":""));
  }catch(OperationCanceledException){SetMessage("경로 탐색을 취소했습니다.");}catch(Exception e){SetMessage("경로 탐색: "+e.Message);}finally{Busy(false);cancel.Dispose();cancel=null;}
 }
 // ---------- 설정 ----------
 void CaptureSettings() {
  bool installed=config.BaseVersion!=""||config.HdVersion!=""||config.UiVersion!=""||config.CheatsVersion!=""||config.SaveVersion!=""||File.Exists(Path.Combine(config.DataRoot,"install-journal.json"));
  string next=Path.GetFullPath(dataRoot.Text.Trim());if(installed&&!String.Equals(next,config.DataRoot,StringComparison.OrdinalIgnoreCase))throw new Exception("설치한 뒤에는 설치 폴더를 옮길 수 없습니다. 새 폴더에서 런처를 따로 시작하세요.");
  string memory=Engine.MemstickRoot(memstick.Text.Trim());if(installed&&config.Memstick!=""&&!String.Equals(memory,Engine.MemstickRoot(config.Memstick),StringComparison.OrdinalIgnoreCase))throw new Exception("설치한 뒤에는 메모리스틱 폴더를 옮길 수 없습니다. 새 폴더에서 런처를 따로 시작하세요.");
  if(memory!=memstick.Text.Trim())memstick.Text=memory;
  config.SourceIso=source.Text.Trim();config.Emulator=emulator.Text.Trim();config.Memstick=memory;
  config.DataRoot=next;config.SelectBase=baseBox.Checked;config.SelectHD=hdBox.Checked;config.SelectUI=uiBox.Checked;config.SelectCheats=cheatsBox.Checked;config.SelectSave=saveBox.Checked;
  config.UseExternalData=installMode.SelectedIndex==1;
 }
 Engine EngineForWork() {CaptureSettings();Json.Write(settingsPath,config);var e=new Engine(config,settingsPath,tools);e.RecoverPending();config=e.Config;e.Progress=(s,p)=>{if(!IsDisposed)BeginInvoke((Action)(()=>{SetMessage(s);if(p>=0){bar.Indeterminate=false;bar.Value=p;}else bar.Indeterminate=true;}));};e.Cancel=cancel.Token;return e;}
 void RefreshInstalled() {
  if(!Installed()&&config.HdVersion==""&&config.CheatsVersion==""&&config.SaveVersion=="")current.Text="이 컴퓨터: 아직 설치 안 됨";
  else {
   var parts=new List<string>();if(Installed())parts.Add("본편 "+config.BaseVersion+(config.GameData!=""?" (외부 데이터)":""));if(config.HdVersion!="")parts.Add("HD "+Pretty(config.HdVersion));
   if(config.UiVersion!="")parts.Add("한국어 UI");else if(config.HdVersion=="HD-v40")parts.Add("한국어 UI (통합 v40)");if(config.CheatsVersion!="")parts.Add("치트");if(config.SaveVersion!="")parts.Add("클리어 세이브");
   current.Text="이 컴퓨터: "+String.Join(" · ",parts)+(config.InstalledAt!=""?"\n"+config.InstalledAt+" 설치":"");
  }
  UpdatePrimary();
 }
 void SetMessage(string s) {
  if(message.Text==s)return;message.Text=s;string line=DateTime.Now.ToString("HH:mm:ss")+"  "+s;log.AppendText(line+Environment.NewLine);
  if(!preview)try{Directory.CreateDirectory(config.DataRoot);File.AppendAllText(Path.Combine(config.DataRoot,"launcher.log"),DateTime.Now.ToString("yyyy-MM-dd ")+line+Environment.NewLine);}catch{}
 }
 void Busy(bool value) {
  busy=value;check.Enabled=!value;stop.Enabled=value;graphics.Enabled=!value;uiRemove.Enabled=!value&&release!=null;paths.Enabled=!value;components.Enabled=!value;fullPlan.Enabled=basePlan.Enabled=customPlan.Enabled=!value;
  RefreshPathRequirements();UpdatePrimary();if(value)bar.Value=0;else{bar.Indeterminate=false;RefreshInstalled();}
 }
 // ---------- 동작 ----------
 async void CheckLatest() {
  if(busy)return;cancel=new CancellationTokenSource();Busy(true);play.Enabled=Engine.HasGame(config);
  try{engine=EngineForWork();available.Text="최신 배포를 확인하는 중…";release=await Task.Run(()=>engine.Latest());
   available.Text="최신 배포  "+release.BaseVersion+" · HD "+Pretty(release.HdVersion)+(release.UiVersion!=""?" · 한국어 UI "+Pretty(release.UiVersion):"")+(release.Published.HasValue?"\n"+release.Published.Value.ToString("yyyy-MM-dd")+" 공개":"");
   UpdatePrimary();SetMessage(ReadyHint());}
  catch(Exception e){available.Text="최신 배포를 확인하지 못했습니다.";SetMessage(e is OperationCanceledException?"확인을 취소했습니다.":e.Message+(Engine.HasGame(config)?"  설치된 게임은 실행할 수 있습니다.":""));}
  finally{Busy(false);cancel.Dispose();cancel=null;}
 }
 async void Install() {
  if(busy||release==null)return;
  if(baseBox.Checked&&source.Text.Trim()==""){SetMessage("일본판 원본 ISO를 「찾기…」로 지정해 주세요.");((ComboBox)source).Focus();return;}
  if(NeedsMemory()&&memstick.Text.Trim()==""){SetMessage("HD·한국어 UI·치트·세이브는 PPSSPP 메모리스틱 폴더에 들어갑니다. 「찾기…」로 지정해 주세요.");((ComboBox)memstick).Focus();return;}
  cancel=new CancellationTokenSource();Busy(true);
  try{engine=EngineForWork();bool b=baseBox.Checked,h=hdBox.Checked,c=cheatsBox.Checked,s=saveBox.Checked,u=uiBox.Checked;await Task.Run(()=>engine.Install(release,b,h,c,s,u));config=engine.Config;RefreshInstalled();
   SetMessage("설치 완료. 「게임 실행」을 누른 뒤 타이틀 화면에서 「로드」로 일반 세이브를 불러오세요. 예전 상태 저장은 쓰지 마세요.");play.Focus();}
  catch(Exception e){if(engine!=null)config=engine.Config;SetMessage(e is OperationCanceledException?"취소했습니다. 받은 데이터는 다음에 이어받습니다.":e.Message);}
  finally{Busy(false);cancel.Dispose();cancel=null;}
 }
 async void RemoveUI() {
  if(busy||release==null)return;cancel=new CancellationTokenSource();Busy(true);
  try{engine=EngineForWork();await Task.Run(()=>engine.Install(release,false,false,false,false,false,true));config=engine.Config;uiBox.Checked=false;config.SelectUI=false;Json.Write(settingsPath,config);SetMessage("원래 영문 UI로 되돌렸습니다. HD 화질은 그대로입니다.");}
  catch(Exception e){if(engine!=null)config=engine.Config;SetMessage(e.Message);}
  finally{Busy(false);cancel.Dispose();cancel=null;}
 }
 async void ApplyGraphics() {
  if(busy)return;launchSetup=true;RefreshPathRequirements();if(memstick.Text.Trim()==""){SetMessage("설정을 적용할 메모리스틱 폴더를 지정한 뒤 다시 누르세요.");return;}
  cancel=new CancellationTokenSource();Busy(true);
  try{engine=EngineForWork();await Task.Run(()=>engine.ConfigureDefaults());config=engine.Config;SetMessage("Fate/EXTRA 전용 설정을 적용했습니다: JIT · 렌더링 8배 · MSAA 4배 · 수직동기화 · 텍스처 교체. 치트는 PPSSPP 목록에서 켜세요.");}
  catch(Exception e){if(engine!=null)config=engine.Config;SetMessage(e.Message);}
  finally{Busy(false);cancel.Dispose();cancel=null;}
 }
 static string ExternalLaunchMessage(Dictionary<string,object> status) {
  string state=Json.S(status,"status"),error=status.ContainsKey("error")?Json.S(status,"error"):"";
  switch(state) {
   case "running":return "게임을 실행했습니다. 이 창은 닫아도 됩니다.";
   case "ready":return "게임 실행 확인이 늦어지고 있습니다. 잠시 기다린 뒤 PPSSPP 창을 확인하세요.";
   case "exited":return "PPSSPP가 종료되었습니다.";
   case "error":case "io-error":throw new Exception(String.IsNullOrEmpty(error)?"외부 패치 데이터를 읽거나 게임을 실행하지 못했습니다.":error);
   default:throw new Exception("외부 로더 실행 응답 오류: "+state);
  }
 }
 async void Launch() {
  try{if(busy)return;launchSetup=true;RefreshPathRequirements();if(emulator.Text.Trim()==""||memstick.Text.Trim()=="")await FindPaths(false,false,true);CaptureSettings();if(busy||Engine.GameRunning())throw new Exception("이미 PPSSPP가 실행 중입니다.");if(!File.Exists(config.Emulator))throw new Exception("PPSSPP 실행 파일을 「찾기…」로 지정해 주세요.");if(!Engine.HasGame(config))throw new Exception("먼저 「설치」를 눌러 본편을 설치해 주세요.");
   if(config.Memstick=="")throw new Exception("세이브와 HD를 둘 메모리스틱 폴더를 지정해 주세요.");Directory.CreateDirectory(config.Memstick);Json.Write(settingsPath,config);
   var launcherEngine=new Engine(config,settingsPath,tools);
   if(config.GameData!="") {
    cancel=new CancellationTokenSource();Busy(true);SetMessage("외부 패치 데이터를 검사하는 중… 원본 ISO와 패치 데이터를 다시 읽으므로 디스크 속도에 따라 몇 분 걸릴 수 있습니다.");ExternalTicket ticket=null;
    try {
     launcherEngine.Cancel=cancel.Token;ticket=await Task.Run(()=>launcherEngine.StartExternalSession());config=launcherEngine.Config;string session=ticket.StatusPath;
     // 도우미는 원본·패치·조합 결과를 전부 해시한 뒤에야 ready 를 쓴다. 살아 있는 동안은 기다린다(최대 30분).
     var watch=Stopwatch.StartNew();while(!File.Exists(session)&&ticket.HelperAlive&&watch.Elapsed.TotalMinutes<30){cancel.Token.ThrowIfCancellationRequested();await Task.Delay(250);}
     if(!File.Exists(session))throw new Exception(ticket.HelperAlive?"외부 로더의 준비가 30분 안에 끝나지 않았습니다. 설치 폴더의 sessions 상태 기록을 확인하세요.":"외부 로더가 준비 중에 종료되었습니다. 설치 폴더의 launcher.log 와 sessions 기록을 확인하세요.");
     var status=Json.Parse(File.ReadAllText(session));if(Json.S(status,"status")=="error")throw new Exception(Json.S(status,"error"));
     if(Json.S(status,"status")!="ready")throw new Exception("외부 로더 준비 응답 오류");cancel.Token.ThrowIfCancellationRequested();ticket.Commit();stop.Enabled=false;
     watch.Restart();do{await Task.Delay(100);status=Json.Parse(File.ReadAllText(session));}while(Json.S(status,"status")=="ready"&&watch.Elapsed.TotalSeconds<10);
     SetMessage(ExternalLaunchMessage(status));
    }finally{if(ticket!=null)ticket.Dispose();Busy(false);cancel.Dispose();cancel=null;}
   }else{Process.Start(launcherEngine.GameStartInfo());SetMessage("PPSSPP를 실행했습니다. 타이틀 화면에서 「로드」로 일반 세이브를 불러오세요.");}
  }catch(Exception e){SetMessage(e.Message);}
 }
 static void Open(string target){try{Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}catch(Exception e){MessageBox.Show(e.Message);}}
 // 배너 그림: 원본 ISO 가 있으면 거기서, 없고 설치된 ISO 가 있으면 거기서. 백그라운드로 읽고 늦게 온 결과는 버린다.
 void LoadArt() {
  if(banner==null||preview)return;string path=source.Text.Trim();if(path==""||!File.Exists(path))path=config.GameIso!=""&&File.Exists(config.GameIso)?config.GameIso:"";
  if(String.Equals(path,artPath,StringComparison.OrdinalIgnoreCase))return;artPath=path;int token=++artToken;
  if(path==""){banner.SetImages(null,null);return;}
  Task.Run(()=>{Image art=Decode(Discovery.ReadGameFile(path,"PIC1.PNG",4*1024*1024)),logo=Decode(Discovery.ReadGameFile(path,"ICON0.PNG",1024*1024));
   try{if(!IsDisposed)BeginInvoke((Action)(()=>{if(token!=artToken){if(art!=null)art.Dispose();if(logo!=null)logo.Dispose();return;}banner.SetImages(art,logo);}));}catch(InvalidOperationException){}});
 }
 public void PreviewEmpty(){shown=true;fullPlan.Checked=true;source.Text="";emulator.Text="";memstick.Text="";available.Text="최신 배포  v8h · HD v54 · 한국어 UI v4\n2026-09-28 공개";install.Text="한국어 패치 설치";install.Enabled=true;RefreshPathRequirements();message.Text=ReadyHint();}
 public void PreviewProgress(int percent){bar.Indeterminate=false;bar.Value=percent;message.Text="다운로드 · Fate-Extra-Korean-HD-v54.z01   1210 / 1811 MB";}
 public void LoadArtFrom(string iso){banner.SetImages(Decode(Discovery.ReadGameFile(iso,"PIC1.PNG",4*1024*1024)),Decode(Discovery.ReadGameFile(iso,"ICON0.PNG",1024*1024)));}
 static Image Decode(byte[] data){if(data==null)return null;try{using(var ms=new MemoryStream(data))using(var img=Image.FromStream(ms))return new Bitmap(img);}catch(ArgumentException){return null;}catch(OutOfMemoryException){return null;}}
 // --preview 진단: 주요 부품의 자리·크기·글자를 적는다(시험 하네스가 화면 구조를 대조할 때 쓴다).
 public void DumpLayout(string path) {
  var lines=new List<string>();
  foreach(var c in new Control[]{install,play,stop,advanced,advancedPanel,paths,fullPlan,basePlan,customPlan,message,selectionSummary})
   lines.Add(c.GetType().Name+"  text='"+c.Text+"'  bounds="+c.Bounds+"  visible="+c.Visible+"  enabled="+c.Enabled+"  font="+c.Font.Name+" "+c.Font.Size+"  parent="+(c.Parent==null?"-":c.Parent.GetType().Name+" "+c.Parent.Bounds));
  File.WriteAllLines(path,lines,Encoding.UTF8);
 }
}
// 머리 배너. 오른쪽에 키 비주얼을 높이에 맞춰 놓고 왼쪽으로 배경색에 녹인다. 로고가 없으면 글자 로고.
public sealed class Banner : Panel {
 Image art,logo;public string Subtitle="";readonly Color bg,ink,muted;
 public Banner(Color background,Color text,Color dim){bg=background;ink=text;muted=dim;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
 public void SetImages(Image picture,Image icon){var oldArt=art;var oldLogo=logo;art=picture;logo=icon;Invalidate();if(oldArt!=null)oldArt.Dispose();if(oldLogo!=null)oldLogo.Dispose();}
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;g.Clear(bg);int h=Height;
  if(art!=null&&art.Height>0) {
   int aw=art.Width*h/art.Height;var dest=new Rectangle(Width-aw,0,aw,h);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(art,dest);
   int fade=Math.Min(aw,260);var band=new Rectangle(dest.Left,0,fade,h);
   using(var brush=new LinearGradientBrush(band,bg,Color.FromArgb(0,bg),LinearGradientMode.Horizontal)){brush.WrapMode=WrapMode.TileFlipX;g.FillRectangle(brush,band);}
  }
  int y=8;
  if(logo!=null&&logo.Height>0){int lh=80,lw=logo.Width*lh/logo.Height;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(logo,new Rectangle(0,y,lw,lh));y+=lh+6;}
  else{using(var f=new Font("Segoe UI",24,FontStyle.Bold))TextRenderer.DrawText(g,"FATE / EXTRA",f,new Point(-3,y-2),ink);y+=58;}
  using(var f=new Font("맑은 고딕",10F))TextRenderer.DrawText(g,Subtitle,f,new Point(1,y),muted);
 }
 protected override void Dispose(bool disposing){if(disposing)SetImages(null,null);base.Dispose(disposing);}
}
// 퍼센트 숫자를 함께 그리는 진행 막대. 값이 0 이면 빈 막대, 진행 중이면 채움 위로 숫자, 알 수 없으면 움직이는 토막.
public sealed class ProgressStrip : Control {
 int value;bool indeterminate;int phase;readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer{Interval=40};readonly Color track,fill,light,dark;
 public ProgressStrip(Color trackColor,Color fillColor,Color lightText,Color darkText){track=trackColor;fill=fillColor;light=lightText;dark=darkText;SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);timer.Tick+=(s,e)=>{phase=(phase+5)%(Math.Max(1,Width)+140);Invalidate();};}
 public int Value{get{return value;}set{int v=Math.Max(0,Math.Min(100,value));if(v!=this.value){this.value=v;Invalidate();}}}
 public bool Indeterminate{get{return indeterminate;}set{if(indeterminate==value)return;indeterminate=value;timer.Enabled=value;phase=0;Invalidate();}}
 protected override void OnPaint(PaintEventArgs e) {
  var g=e.Graphics;g.Clear(track);int w=Width,h=Height;
  if(indeterminate){using(var b=new SolidBrush(fill))g.FillRectangle(b,new Rectangle(phase-140,0,140,h));return;}
  if(value<=0)return;int fw=(int)((long)w*value/100);using(var b=new SolidBrush(fill))g.FillRectangle(b,0,0,fw,h);
  string s=value+" %";var size=TextRenderer.MeasureText(g,s,Font);var pt=new Point((w-size.Width)/2,(h-size.Height)/2);
  TextRenderer.DrawText(g,s,Font,pt,light);var clip=g.Clip;g.SetClip(new Rectangle(0,0,fw,h));TextRenderer.DrawText(g,s,Font,pt,dark);g.Clip=clip;
 }
 protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}
public static class Program {
 [STAThread] public static void Main(string[] args) {
  if(args.Length==2&&args[0]=="--external-session"){ExternalSession.Run(Path.GetFullPath(args[1]));return;}
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  string root=AppDomain.CurrentDomain.BaseDirectory;bool preview=args.Length>=2&&args[0]=="--preview";
  try{using(var mutex=new Mutex(false,"Local\\FateExtraKoreanLauncher")){if(!preview&&!mutex.WaitOne(0)){MessageBox.Show("런처가 이미 실행 중입니다.");return;}try{
   using(var form=new LauncherForm(root,preview)){if(preview){if(args.Length>=3&&args[2]!="")form.LoadArtFrom(args[2]);if(args.Length>=4&&args[3]!="0")form.PreviewProgress(Int32.Parse(args[3]));if(args.Length>=5&&args[4]=="empty")form.PreviewEmpty();form.Opacity=0;form.ShowInTaskbar=false;form.Show();Application.DoEvents();form.PerformLayout();using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.GetFullPath(args[1]));}form.DumpLayout(Path.GetFullPath(args[1])+".txt");form.Hide();}else Application.Run(form);}
  }finally{if(!preview)mutex.ReleaseMutex();}}}catch(Exception e){MessageBox.Show(e.Message,"Fate/EXTRA 한국어 패치",MessageBoxButtons.OK,MessageBoxIcon.Error);}
 }
}
}
