using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ViddyExp {
 public sealed class Overlay : Form {
  readonly Tracker app;
  readonly ToolTip tip=new ToolTip { InitialDelay=250,ReshowDelay=100,AutoPopDelay=15000 };
  Point dragStart,windowStart;
  bool dragging,buttonPressed,buttonHover;
  DateTime nextRefresh=DateTime.MinValue;
  internal DisplaySnapshot Display;
  public const int RefreshSeconds=12;
  string lastTip="";
  static readonly Color Ink=Color.FromArgb(64,43,27),Muted=Color.FromArgb(155,135,105);
  public Overlay(Tracker owner) {
   app=owner; FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false;TopMost=true;
   DoubleBuffered=true;AutoScaleMode=AutoScaleMode.None;StartPosition=FormStartPosition.Manual;
   BackColor=Color.FromArgb(153,111,58);TransparencyKey=Color.Empty;Text="Viddy EXP Only";
   Location=new Point(app.Settings.X,app.Settings.Y);ContextMenuStrip=app.Menu;
   RefreshData(DateTime.UtcNow,true);
   UpdateLayout();
   MouseDown+=delegate(object sender,MouseEventArgs e) {
    if(e.Button!=MouseButtons.Left||app.Settings.Locked) return;
    if(IsToggle(e.Location)) { buttonPressed=true;Capture=true;Invalidate();return; }
    dragging=true;dragStart=Cursor.Position;windowStart=Location;Capture=true;
   };
   MouseMove+=delegate(object sender,MouseEventArgs e) {
    if(dragging) { var p=Cursor.Position;Location=new Point(windowStart.X+p.X-dragStart.X,windowStart.Y+p.Y-dragStart.Y);return; }
    bool over=IsToggle(e.Location)&&!app.Settings.Locked;
    if(buttonHover!=over) { buttonHover=over;Invalidate(); }
    Cursor=app.Settings.Locked?Cursors.Default:over?Cursors.Hand:Cursors.SizeAll;
    UpdateTip(e.Location);
   };
   MouseUp+=delegate(object sender,MouseEventArgs e) {
    bool activate=buttonPressed&&e.Button==MouseButtons.Left;
    bool moved=dragging;buttonPressed=false;dragging=false;Capture=false;
    if(activate) ToggleAt(e.Location);
    else if(moved)app.Save();
    Invalidate();
   };
   MouseCaptureChanged+=delegate { dragging=false;buttonPressed=false; };
   MouseLeave+=delegate { if(buttonHover) { buttonHover=false;Invalidate(); } };
   Shown+=delegate { ApplyLock(); };
  }
  protected override bool ShowWithoutActivation { get { return true; } }
  protected override CreateParams CreateParams { get { var p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x80000;return p; } }
  protected override void OnHandleCreated(EventArgs e) {
   base.OnHandleCreated(e);
   // Fully opaque layered content keeps click-through working without a color key.
   Native.SetLayeredWindowAttributes(Handle,0,255,2);
   ApplyLock();
  }
  public void ApplyLock() {
   if(!IsHandleCreated) return;
   int style=Native.GetWindowLong(Handle,-20);
   Native.SetWindowLong(Handle,-20,app.Settings.Locked?style|0x20:style&~0x20);
   Cursor=app.Settings.Locked?Cursors.Default:Cursors.SizeAll;
  }
  public void UpdateLayout() {
   Size design=app.Settings.Tiny?new Size(480,38):new Size(660,170);
   ClientSize=new Size((int)Math.Round(design.Width*app.Settings.Scale),(int)Math.Round(design.Height*app.Settings.Scale));
   var screen=Screen.FromRectangle(Bounds).WorkingArea;
   Location=new Point(Math.Max(screen.Left,Math.Min(Left,screen.Right-Width)),Math.Max(screen.Top,Math.Min(Top,screen.Bottom-Height)));
   using(var shape=WindowShape()) {
    using(var transform=new Matrix()) { transform.Scale((float)app.Settings.Scale,(float)app.Settings.Scale);shape.Transform(transform); }
    var old=Region;Region=new Region(shape);if(old!=null)old.Dispose();
   }
   buttonHover=false;buttonPressed=false;Invalidate();
  }
  GraphicsPath WindowShape() {
   return app.Settings.Tiny?Round(new RectangleF(.5f,.5f,478,36),8):Round(new RectangleF(1,2,657,166),15);
  }
  internal RectangleF ToggleBounds { get { return app.Settings.Tiny?new RectangleF(451,2,26,33):new RectangleF(302,12,56,23); } }
  bool IsToggle(Point point) { return ToggleBounds.Contains(point.X/(float)app.Settings.Scale,point.Y/(float)app.Settings.Scale); }
  internal bool ToggleAt(Point point) {
   if(app.Settings.Locked||!IsToggle(point))return false;
   app.ToggleTiny();return true;
  }
  internal bool RefreshData(DateTime now,bool force) {
   bool firstReading=Display!=null&&Display.Current==null&&app.Session.Current!=null;
   if(!force&&!firstReading&&Display!=null&&now<nextRefresh)return false;
   Display=DisplaySnapshot.Capture(app);nextRefresh=now.AddSeconds(RefreshSeconds);Invalidate();return true;
  }
  protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); Draw(e.Graphics,app.Settings.Scale); }
  public Bitmap Render() {
   var bitmap=new Bitmap(Width,Height);
   using(var g=Graphics.FromImage(bitmap)) { g.Clear(Color.Transparent);Draw(g,app.Settings.Scale); }
   return bitmap;
  }
  void Draw(Graphics g,double scale) {
   var saved=g.Save();g.ScaleTransform((float)scale,(float)scale);
   g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
   using(var shape=WindowShape())using(var background=new SolidBrush(BackColor))g.FillPath(background,shape);
   if(app.Settings.Tiny) DrawTiny(g);else DrawFull(g);
   DrawToggle(g);
   g.Restore(saved);
  }
  static GraphicsPath Round(RectangleF r,float radius) {
   var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
   p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
  }
  static void Panel(Graphics g,RectangleF r,float radius,Color top,Color bottom,Color border,float width) {
   using(var p=Round(r,radius)) using(var b=new LinearGradientBrush(r,top,bottom,90)) using(var pen=new Pen(border,width)) {g.FillPath(b,p);g.DrawPath(pen,p);}
  }
  static void DrawText(Graphics g,string text,float size,FontStyle style,Color color,RectangleF r,StringAlignment align) {
   using(var f=new Font("Segoe UI",size,style,GraphicsUnit.Pixel)) using(var b=new SolidBrush(color)) using(var format=new StringFormat { Alignment=align,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap }) g.DrawString(text,f,b,r,format);
  }
  static void DrawText(Graphics g,string text,float size,FontStyle style,Color color,float x,float y,float w,float h) { DrawText(g,text,size,style,color,new RectangleF(x,y,w,h),StringAlignment.Near); }
  static void Leaf(Graphics g,float x,float y,float size) {
   var points=new[]{new PointF(.5f,0),new PointF(.58f,.28f),new PointF(.8f,.13f),new PointF(.76f,.43f),new PointF(1,.4f),new PointF(.72f,.68f),new PointF(.6f,.74f),new PointF(.57f,1),new PointF(.45f,1),new PointF(.45f,.74f),new PointF(.28f,.67f),new PointF(0,.4f),new PointF(.25f,.44f),new PointF(.2f,.13f),new PointF(.42f,.29f)};
   for(int i=0;i<points.Length;i++)points[i]=new PointF(x+points[i].X*size,y+points[i].Y*size);
   using(var b=new SolidBrush(Color.FromArgb(255,228,163)))g.FillPolygon(b,points);
  }
  void Level(Graphics g,RectangleF rect,bool small) {
   Panel(g,rect,6,Color.FromArgb(70,68,69),Color.FromArgb(42,42,44),Color.FromArgb(103,99,94),1.5f);
   DrawText(g,"LV.",small?12:21,FontStyle.Bold|FontStyle.Italic,Color.White,rect.X+7,rect.Y,small?28:38,rect.Height);
   var current=Display.Current;
   string level=current==null?"--":current.Level.ToString("00");
   float x=rect.X+(small?35:46),w=small?13:21;
   foreach(char c in level) {
    Panel(g,new RectangleF(x,rect.Y+6,w,rect.Height-12),3,Color.FromArgb(255,190,85),Color.FromArgb(238,131,36),Color.FromArgb(189,119,42),1);
    DrawText(g,c.ToString(),small?12:21,FontStyle.Bold,Color.White,new RectangleF(x,rect.Y+4,w,rect.Height-8),StringAlignment.Center);x+=w+3;
   }
  }
  void Progress(Graphics g,RectangleF rect,bool label) {
   Panel(g,rect,label?6:2,Color.FromArgb(72,71,76),Color.FromArgb(99,98,103),Color.FromArgb(65,63,60),label?2:.5f);
   var r=Display.Current;double percent=r==null?0:r.Percent;
   float inset=label?2:.5f;
   var inner=new RectangleF(rect.X+inset,rect.Y+inset,rect.Width-inset*2,rect.Height-inset*2);
   if(percent>0) using(var clip=Round(inner,4)) {
    var state=g.Save();g.SetClip(clip);
    using(var b=new LinearGradientBrush(inner,Color.FromArgb(234,255,131),Color.FromArgb(139,194,0),90))g.FillRectangle(b,inner.X,inner.Y,(float)(inner.Width*percent/100),inner.Height);
    using(var pen=new Pen(Color.FromArgb(224,255,143),1))g.DrawLine(pen,inner.X,inner.Y+2,inner.X+(float)(inner.Width*percent/100),inner.Y+2);
    g.Restore(state);
   }
   if(!label) return;
   string text=r==null?(Display.Paused?"PAUSED":"Waiting for MapleStory…"):"EXP "+Format.Number(r.Exp)+" ["+r.Percent.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"%]";
   var area=new RectangleF(rect.X+5,rect.Y+1,rect.Width-10,rect.Height-2);
   var shadow=area;shadow.Offset(1,1);DrawText(g,text,16,FontStyle.Bold,Color.FromArgb(65,68,50),shadow,StringAlignment.Center);
   DrawText(g,text,16,FontStyle.Bold,Color.White,area,StringAlignment.Center);
  }
  void DrawFull(Graphics g) {
   Panel(g,new RectangleF(2,3,655,164),15,Color.FromArgb(255,253,245),Color.FromArgb(246,238,220),Color.FromArgb(153,111,58),2);
   using(var outline=Round(new RectangleF(3,4,653,162),13)) {
    var state=g.Save();g.SetClip(outline);
    using(var b=new LinearGradientBrush(new RectangleF(3,4,653,39),Color.FromArgb(200,148,78),Color.FromArgb(157,102,47),90))g.FillRectangle(b,3,4,653,39);
    using(var p=new Pen(Color.FromArgb(232,188,124)))g.DrawLine(p,15,6,645,6);
    g.Restore(state);
   }
   Leaf(g,16,15,16);
   DrawText(g,"EXP TRACKER",16,FontStyle.Bold,Color.FromArgb(255,248,229),40,9,260,28);
   DrawText(g,Display.Paused?"PAUSED":Display.Current!=null&&!Display.Live?"WAITING":app.Settings.DisplayName,16,FontStyle.Bold,Color.FromArgb(255,248,229),new RectangleF(390,9,246,28),StringAlignment.Far);
   Level(g,new RectangleF(16,57,103,39),false);
   Progress(g,new RectangleF(133,63,507,27),true);
   string[] labels={"LAST 5 MIN","EXP/H · 30M","THIS SESSION",Display.Current==null?"TO NEXT LEVEL":"TO LV. "+(Display.Current.Level+1)};
   string[] values={Display.Current==null?"—":Display.LastFive,Display.Rate,Display.Current==null?"—":Display.Net,Display.Eta};
   for(int i=0;i<4;i++) {
    float x=16+i*162;
    if(i>0)using(var p=new Pen(Color.FromArgb(226,212,186)))g.DrawLine(p,x-12,105,x-12,154);
    DrawText(g,labels[i],12,FontStyle.Bold,Muted,x,105,148,20);
    DrawText(g,values[i],24,FontStyle.Bold,Ink,x,125,148,32);
   }
   // The mode switch is the only on-overlay button; the menu holds other controls.
  }
  void DrawTiny(Graphics g) {
   Panel(g,new RectangleF(1,1,477,35),8,Color.FromArgb(255,251,237),Color.FromArgb(243,231,205),Color.FromArgb(166,119,60),1.5f);
   Level(g,new RectangleF(5,4,70,28),true);
   var reading=Display.Current;
   DrawText(g,reading==null?(Display.Paused?"PAUSED":"WAITING"):reading.Percent.ToString("0.00")+"%",12,FontStyle.Bold,Ink,83,3,63,23);
   Progress(g,new RectangleF(83,27,60,5),false);
   TinyCell(g,154,"EXP/H",Display.Rate,96);
   TinyCell(g,260,"SESSION",reading==null?"—":Display.Net,96);
   TinyCell(g,365,reading==null?"NEXT":"LV. "+(reading.Level+1),Display.Eta,80);
   if(Display.Paused)using(var b=new SolidBrush(Color.FromArgb(168,102,43))) {g.FillRectangle(b,432,5,2,7);g.FillRectangle(b,436,5,2,7);}
  }
  void DrawToggle(Graphics g) {
   var r=ToggleBounds;
   float x=r.X+r.Width/2,y=r.Y+r.Height/2;
   bool tiny=app.Settings.Tiny,active=buttonHover||buttonPressed;
   // Keep the larger hit area, but let the icon blend into the header at rest.
   if(active) {
    var highlight=new RectangleF(x-11,y-7,22,14);
    using(var shape=Round(highlight,5))
    using(var brush=new SolidBrush(Color.FromArgb(buttonPressed?48:27,tiny?Ink:Color.White)))g.FillPath(brush,shape);
   }
   Color color=tiny?Color.FromArgb(active?230:155,139,106,62):Color.FromArgb(active?255:160,255,238,205);
   float halfWidth=tiny?4:4.5f,halfHeight=tiny?1.75f:2;
   float direction=tiny?-1:1;
   using(var pen=new Pen(color,1.6f)) {
    pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
    g.DrawLines(pen,new[]{new PointF(x-halfWidth,y-direction*halfHeight),new PointF(x,y+direction*halfHeight),new PointF(x+halfWidth,y-direction*halfHeight)});
   }
  }
  static void TinyCell(Graphics g,float x,string label,string value,float width) {
   using(var p=new Pen(Color.FromArgb(220,201,170)))g.DrawLine(p,x-7,7,x-7,30);
   DrawText(g,label,8.5f,FontStyle.Bold,Muted,x,3,width,11);DrawText(g,value,16,FontStyle.Bold,Ink,x,14,width,20);
  }
  void UpdateTip(Point p) {
   var r=Display.Current;
   string text=Display.Status+"\n"+Display.Detail;
   if(r!=null)text="Level "+r.Level+" · "+Format.Number(r.Exp)+" / "+Format.Number(ExpTable.Required[r.Level])+" EXP\n"+Format.Number(ExpTable.Required[r.Level]-r.Exp)+" EXP to level "+(r.Level+1)+"\n\n"+Display.Status+" · "+Format.Duration(Display.TrackedSeconds)+" tracked\nEXP/h uses the last 30 minutes of observed time.\n"+(Display.Live?"":"Last values retained; waiting for a fresh reading.\n")+"Drag anywhere to move · Right-click for controls";
   if(IsToggle(p))text=app.Settings.Tiny?"Switch to full view":"Switch to tiny view";
   if(text==lastTip)return;lastTip=text;tip.SetToolTip(this,text);
  }
  protected override void Dispose(bool disposing) { if(disposing)tip.Dispose();base.Dispose(disposing); }
 }
 internal sealed class DisplaySnapshot {
  public Reading Current;
  public string Rate,LastFive,Net,Eta,Status,Detail;
  public bool Paused,Live;
  public double TrackedSeconds;
  public static DisplaySnapshot Capture(Tracker app) {
   var r=app.Session.Current;
   return new DisplaySnapshot {
    Current=r==null?null:new Reading { Level=r.Level,Exp=r.Exp,Percent=r.Percent },
    Rate=app.RateText,LastFive=Format.Signed(app.Session.GainIn(300)),Net=Format.Signed(app.Session.Net),Eta=Format.Duration(app.Session.Eta),
    Status=app.Status,Detail=app.Detail,Paused=app.Paused,Live=app.Live,TrackedSeconds=app.Session.ActiveSeconds
   };
  }
 }
}
