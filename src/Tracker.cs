using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ViddyExp {
 public sealed class Tracker : ApplicationContext {
  public readonly Preferences Settings;
  public Session Session=new Session();
  public bool Paused,Live;
  public string Status="Waiting for MapleStory";
  public string Detail="Show your level and EXP bar in the game.";
  public Overlay View;
  public ContextMenuStrip Menu;
  public string ShortcutWarning="";
  Timer clock;
  NotifyIcon tray;
  Hotkeys hotkeys;
  OcrReader reader;
  bool busy,exiting,resetReader=true;
  readonly bool persistPreferences;
  int epoch;
  IntPtr lastWindow;
  DateTime lastGood=DateTime.MinValue;
  public Tracker(bool start) {
   persistPreferences=start;
   Settings=start?PreferencesStore.Load():new Preferences();
   Menu=CreateMenu(); View=new Overlay(this);
   if(!start) return;
   hotkeys=new Hotkeys(this);
   tray=new NotifyIcon { Text="Viddy EXP · EXP only",Icon=SystemIcons.Information,ContextMenuStrip=Menu,Visible=true };
   tray.DoubleClick+=delegate { View.Show(); };
   View.Show();
   clock=new Timer { Interval=1000 };
   clock.Tick+=async delegate { await Tick(); };
   clock.Start();
  }
  async Task Tick() {
   if(exiting||busy||Paused) return;
   var foreground=Native.GetForegroundWindow();
   var target=Native.Games().FirstOrDefault(w=>w.Handle==foreground);
   if(target==null) { Wait("Waiting for MapleStory","Bring the game to the front. Unseen time and EXP are excluded."); return; }
   var bounds=Native.ClientBounds(target.Handle);
   if(bounds.Width<500||bounds.Height<300) { Wait("Waiting for the game HUD","Restore the game window to continue."); return; }
   if(target.Handle!=lastWindow) { lastWindow=target.Handle; epoch++; Session.Break(); resetReader=true; }
   int version=epoch; int expected=Session.Current==null?0:Session.Current.Level;
   // Capture only the bottom 30% containing the status bar. Never save pixels.
   var region=new Rectangle(bounds.X,bounds.Y+(int)(bounds.Height*.70),bounds.Width,bounds.Height-(int)(bounds.Height*.70));
   Rectangle overlay=View.Visible?View.Bounds:Rectangle.Empty;
   bool reset=resetReader||reader==null||reader.ExpectedLevel!=expected; resetReader=false;
   busy=true;
   try {
    var result=await Task.Run(delegate {
     if(reader==null) reader=new OcrReader();
     if(reset) reader.Reset();
     reader.ExpectedLevel=expected;
     if(Native.GetForegroundWindow()!=target.Handle||Native.ClientBounds(target.Handle)!=bounds) return null;
     using(var frame=Native.Capture(region)) {
      var covered=Rectangle.Intersect(region,overlay);
      if(!covered.IsEmpty) using(var g=Graphics.FromImage(frame)) g.FillRectangle(Brushes.Black,covered.X-region.X,covered.Y-region.Y,covered.Width,covered.Height);
      return reader.Read(frame);
     }
    });
    if(exiting||Paused||version!=epoch) return;
    if(Native.GetForegroundWindow()!=target.Handle||Native.ClientBounds(target.Handle)!=bounds) { Wait("Waiting for MapleStory","The window changed while reading; that frame was discarded."); return; }
    Detail=reader.Diagnostic;
    var now=DateTime.UtcNow;
    if(result==null) {
     Live=false; Status="Reading the EXP bar";
     if((now-lastGood).TotalSeconds>6) Session.CaptureGap(now);
    } else {
     Status=Session.Observe(result,now); lastGood=now;
     Live=Status=="Tracking"||Status=="EXP loss recorded"||Status.StartsWith("Resumed");
     if(Status.StartsWith("Character changed")) Paused=true;
    }
   } catch(Exception e) {
    Wait("Reader unavailable",e.Message+"\nKeep the HUD visible. If OCR cannot start, install the Microsoft Visual C++ x64 runtime listed in README.");
   } finally {
    busy=false;
    if(!exiting) View.RefreshData(DateTime.UtcNow,false);
   }
  }
  void Wait(string status,string detail) { Status=status;Detail=detail;Live=false;Session.Break();View.RefreshData(DateTime.UtcNow,false); }
  public string RateText {
   get { return Session.Current==null?"—":Session.ActiveSeconds<30?"Wait "+Math.Ceiling(30-Session.ActiveSeconds)+"s":Format.Compact(Session.Rate(1800)); }
  }
  public void TogglePause() {
   Paused=!Paused; epoch++; Session.Break(); Live=false;
   Status=Paused?"Paused":"Waiting for MapleStory"; View.RefreshData(DateTime.UtcNow,true);
  }
  public void ResetSession() {
   if(Session.Current!=null&&MessageBox.Show("Reset this session's EXP and rates?","New EXP session",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK) return;
   Session=new Session(); epoch++; Live=false; lastGood=DateTime.MinValue; Status="Waiting for the EXP bar"; View.RefreshData(DateTime.UtcNow,true);
  }
  public void ToggleTiny() { Settings.Tiny=!Settings.Tiny; View.UpdateLayout(); Save(); }
  public void ToggleLock() { Settings.Locked=!Settings.Locked; View.ApplyLock(); Save(); }
  public void Save() { if(!persistPreferences)return; Settings.X=View.Left;Settings.Y=View.Top;PreferencesStore.Save(Settings); }
  ContextMenuStrip CreateMenu() {
   var menu=new ContextMenuStrip { Font=new Font("Segoe UI",10),BackColor=Color.FromArgb(255,250,237),ForeColor=Color.FromArgb(68,47,28),ShowImageMargin=false };
   var status=new ToolStripMenuItem("Waiting for MapleStory") { Enabled=false };
   menu.Items.Add(status); menu.Items.Add(new ToolStripSeparator());
   var pause=new ToolStripMenuItem("Pause tracking",null,delegate { TogglePause(); }); menu.Items.Add(pause);
   menu.Items.Add("New EXP session…",null,delegate { ResetSession(); });
   menu.Items.Add(new ToolStripSeparator());
   var tiny=new ToolStripMenuItem("Tiny mode     Ctrl+Alt+T",null,delegate { ToggleTiny(); });menu.Items.Add(tiny);
   var locked=new ToolStripMenuItem("Click through     Ctrl+Alt+L",null,delegate { ToggleLock(); });menu.Items.Add(locked);
   var size=new ToolStripMenuItem("Size");
   foreach(double scale in new[]{.75,1.0,1.25,1.5}) { double value=scale; size.DropDownItems.Add((scale*100)+"%",null,delegate { Settings.Scale=value;View.UpdateLayout();Save(); }); }
   menu.Items.Add(size);
   menu.Items.Add("Header name…",null,delegate { EditName(); });
   menu.Items.Add("Move to primary screen",null,delegate { View.Location=new Point(Screen.PrimaryScreen.WorkingArea.X+30,Screen.PrimaryScreen.WorkingArea.Y+30);Save(); });
   menu.Items.Add(new ToolStripSeparator());
   menu.Items.Add("Reading status…",null,delegate { MessageBox.Show(Status+"\n\n"+Detail+"\n\n"+ShortcutWarning+"\n"+PreferencesStore.Error,"Viddy EXP · Reading status"); });
   menu.Items.Add("Exit Viddy EXP",null,delegate { Exit(); });
   menu.Opening+=delegate { status.Text=Status; pause.Text=Paused?"Resume tracking     Ctrl+Alt+P":"Pause tracking     Ctrl+Alt+P"; tiny.Checked=Settings.Tiny;locked.Checked=Settings.Locked; };
   return menu;
  }
  void EditName() {
   using(var dialog=new Form { Text="Header name",FormBorderStyle=FormBorderStyle.FixedDialog,ClientSize=new Size(310,100),StartPosition=FormStartPosition.CenterScreen,MaximizeBox=false,MinimizeBox=false,TopMost=true,BackColor=Color.FromArgb(255,250,237) }) {
    var input=new TextBox { Text=Settings.DisplayName,MaxLength=20,Location=new Point(16,16),Width=278 };
    var ok=new Button { Text="Save",DialogResult=DialogResult.OK,Location=new Point(204,55),Width=90 };
    dialog.Controls.Add(input);dialog.Controls.Add(ok);dialog.AcceptButton=ok;
    if(dialog.ShowDialog()==DialogResult.OK) { Settings.DisplayName=input.Text.Trim();Save();View.Invalidate(); }
   }
  }
  public void Exit() {
   if(exiting) return; exiting=true; epoch++;
   if(clock!=null) { clock.Stop();clock.Dispose(); }
   Save(); if(hotkeys!=null) hotkeys.Dispose();
   if(tray!=null) { tray.Visible=false;tray.Dispose(); }
   if(!busy&&reader!=null) reader.Dispose();
   View.Close();Menu.Dispose(); ExitThread();
  }
 }
 sealed class Hotkeys : NativeWindow,IDisposable {
  readonly Tracker app;
  public Hotkeys(Tracker a) {
   app=a;CreateHandle(new CreateParams { Caption="Viddy EXP Only shortcuts" });
   uint[] keys={0x54,0x4c,0x50};
   for(int i=0;i<keys.Length;i++) if(!Native.RegisterHotKey(Handle,201+i,0x4003,keys[i])) app.ShortcutWarning="A shortcut is in use. Use the tray icon's menu instead.";
  }
  protected override void WndProc(ref Message m) {
   if(m.Msg==0x312) { switch(m.WParam.ToInt32()) { case 201:app.ToggleTiny();break;case 202:app.ToggleLock();break;case 203:app.TogglePause();break; } }
   base.WndProc(ref m);
  }
  public void Dispose() { for(int i=201;i<=203;i++) Native.UnregisterHotKey(Handle,i);DestroyHandle(); }
 }
}
