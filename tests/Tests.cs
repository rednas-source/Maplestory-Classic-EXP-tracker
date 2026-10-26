using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ViddyExp {
 public static class Tests {
  static readonly List<string> results=new List<string>();
  static void Check(string name,bool okay) { results.Add((okay?"PASS ":"FAIL ")+name); if(!okay)throw new Exception(name); }
  static Reading Read(int level,long exp) { return new Reading { Level=level,Exp=exp,Percent=exp*100.0/ExpTable.Required[level],Confidence=99 }; }
  static Session Baseline(int level,long exp,DateTime at) { var s=new Session();s.Observe(Read(level,exp),at.AddSeconds(-1));s.Observe(Read(level,exp),at);return s; }
  [STAThread] public static int Main(string[] args) {
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   string folder=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"../artifacts"));Directory.CreateDirectory(folder);
   try {
    if(args.Length>=4&&args[0]=="--ocr") {
     using(var bitmap=new Bitmap(args[1])) using(var reader=new OcrReader()) {
      int level=Int32.Parse(args[2]);long exp=Int64.Parse(args[3]);
      var r=reader.Read(bitmap);Check("recorded full-frame OCR",r!=null&&r.Level==level&&r.Exp==exp);
      int y=(int)(bitmap.Height*.7);
      using(var strip=bitmap.Clone(new Rectangle(0,y,bitmap.Width,bitmap.Height-y),System.Drawing.Imaging.PixelFormat.Format24bppRgb)) {
       reader.Reset();r=reader.Read(strip);Check("recorded HUD-strip OCR",r!=null&&r.Level==level&&r.Exp==exp);
       r=reader.Read(strip);Check("cached HUD-strip OCR",r!=null&&r.Level==level&&r.Exp==exp);
       using(var hidden=new Bitmap(strip)) {
        using(var g=Graphics.FromImage(hidden))g.Clear(Color.Black);
        Check("obscured HUD rejected",reader.Read(hidden)==null);
       }
       r=reader.Read(strip);Check("recovery after obstruction",r!=null&&r.Level==level&&r.Exp==exp);
      }
     }
    } else {
     Check("level 41 EXP table",ExpTable.Required[41]==324240);
     Check("level 69 EXP table",ExpTable.Required[69]==1955750);
     Check("level 90 Classic curve",ExpTable.Required[90]==11938059);
     Check("long integer table",ExpTable.Required[99]==9482161548L);
     Check("visible HUD parses",ReadingParser.Parse(41,"EXP 148,255 [45.72%]",99)!=null);
     Check("wrong percent rejected",ReadingParser.Parse(41,"EXP 148,255 [85.72%]",99)==null);
     Check("missing percentage rejected",ReadingParser.Parse(41,"EXP 148,255",99)==null);
     Check("out of range level rejected",ReadingParser.Parse(100,"EXP 148,255 [45.72%]",99)==null);
     Check("unambiguous inference",ReadingParser.InferLevel("EXP 327,564 [91.78%]",99).Level==42);
     Check("zero progress is ambiguous",ReadingParser.InferLevel("EXP 0 [0.00%]",99)==null);
     Check("weak inference rejected",ReadingParser.InferLevel("EXP 327,564 [91.78%]",50)==null);
     Check("wrong visible level rejected",ReadingParser.ParseHud(43,42,"EXP 327,564 [91.78%]",99)==null);
     Check("separate EXP agreement required",ReadingParser.InferCorroborated("EXP 345.747196.87%1","EXR 345.748[96.87%]",96)==null);
     Check("level mask conflict recovery",ReadingParser.RecoverConflictingLevel(45,"45","45",70,"EXP 234.206 [50.00%]","EXP 234.206 [50.00%]",96)!=null);
     Check("conflicting levels rejected",ReadingParser.RecoverConflictingLevel(45,"43","45",99,"EXP 234.206 [50.00%]","EXP 234.206 [50.00%]",99)==null);
     var now=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);
     var s=new Session();s.Observe(Read(41,1000),now);Check("initial read waits for confirmation",s.Current==null);
     s.Observe(Read(41,1000),now.AddSeconds(1));Check("baseline confirmed without gain",s.Current!=null&&s.Net==0);
     s=Baseline(41,1000,now);
     for(int i=1;i<=60;i++)s.Observe(Read(41,1000+i*10),now.AddSeconds(i));
     Check("session EXP gain",s.Net==600&&s.ActiveSeconds==60);
     Check("rolling hourly rate",s.Rate(1800)==36000);
     Check("last five minutes",s.GainIn(300)==600);
     Check("ETA uses remaining EXP",Math.Abs(s.Eta-(324240-1600)/10.0)<.001);
     s.Observe(Read(41,1500),now.AddSeconds(61));Check("one-frame loss held",s.Lost==0);
     s.Observe(Read(41,1500),now.AddSeconds(62));Check("confirmed loss recorded",s.Lost==100&&s.Net==500);
     s.Break();s.Observe(Read(41,2000),now.AddSeconds(70));s.Observe(Read(41,2000),now.AddSeconds(71));
     Check("unseen gain excluded on resume",s.Net==500);
     Check("hidden time excluded",s.ActiveSeconds==62);
     var levelUp=Baseline(41,324200,now);levelUp.Observe(Read(42,60),now.AddSeconds(1));Check("level transition confirms",levelUp.Levels==0);
     levelUp.Observe(Read(42,60),now.AddSeconds(2));Check("level-up exact delta",levelUp.Net==100&&levelUp.Levels==1);
     var outlier=Baseline(41,1000,now);outlier.Observe(Read(41,100000),now.AddSeconds(1));outlier.Observe(Read(41,1010),now.AddSeconds(2));
     Check("transient large gain rejected",outlier.Net==10);
     var gap=Baseline(41,1000,now);gap.Observe(Read(41,1200),now.AddSeconds(10));gap.Observe(Read(41,1210),now.AddSeconds(11));
     Check("long OCR gap rebases",gap.Net==0&&gap.Gaps==1);
     var warm=Baseline(41,1000,now);warm.Observe(Read(41,1010),now.AddSeconds(1));Check("rate warmup",warm.Rate(1800)==0);
     var soak=Baseline(69,1000,now);
     for(int i=1;i<=28800;i++)soak.Observe(Read(69,1000+i),now.AddSeconds(i));
     Check("eight-hour session totals retained",soak.Net==28800&&soak.ActiveSeconds==28800);
     Check("eight-hour rate stays correct",soak.Rate(1800)==3600&&soak.GainIn(300)==300);
     Check("rolling sample storage bounded",soak.Samples.Count<=1801);
     Check("locale-independent thousands",Format.Number(1234567)=="1,234,567");
     var settings=new Preferences { Scale=Double.NaN,DisplayName=new String('x',40) };settings.Validate();Check("invalid preference values sanitized",settings.Scale==1&&settings.DisplayName.Length==20);
     var app=new Tracker(false);
     try {
      app.Settings.DisplayName="Mapler";
      app.Session=new Session();app.Session.Current=Read(69,1174232);app.Session.ActiveSeconds=3600;app.Session.Gained=782700;
      // A deliberately synthetic preview, with the reference's recent rates.
      for(int i=1;i<=30;i++)app.Session.Samples.Add(new Sample { At=1800+i*60,Delta=i>25?3700:3766,Kind="reading" });
      app.Live=true;app.Status="Tracking";app.Detail="Synthetic preview";
      app.View.RefreshData(DateTime.UtcNow,true);
      app.View.UpdateLayout();Check("full size",app.View.Width==660&&app.View.Height==170);
      using(var b=app.View.Render())b.Save(Path.Combine(folder,"full.png"));
      app.Settings.Tiny=true;app.View.UpdateLayout();Check("tiny size",app.View.Width==480&&app.View.Height==38);
      using(var b=app.View.Render())b.Save(Path.Combine(folder,"tiny.png"));
      app.Settings.Scale=1.5;app.View.UpdateLayout();Check("tiny scales correctly",app.View.Width==720&&app.View.Height==57);
      using(var b=app.View.Render())b.Save(Path.Combine(folder,"tiny-150.png"));
      app.Settings.Scale=.75;app.Settings.Tiny=false;app.View.UpdateLayout();Check("small full view",app.View.Width==495);
      using(var b=app.View.Render())b.Save(Path.Combine(folder,"full-75.png"));
      app.Session=new Session();app.Status="Waiting for MapleStory";app.Live=false;app.Settings.Scale=1;app.View.UpdateLayout();
      app.View.RefreshData(DateTime.UtcNow,true);
      using(var b=app.View.Render())b.Save(Path.Combine(folder,"waiting.png"));Check("empty rate is explicit",app.RateText=="—");
      app.Session=Baseline(41,1000,now);Check("warmup displayed",app.RateText=="Wait 30s");
      var original=app.Session;app.Settings.Tiny=true;app.View.UpdateLayout();Check("layout changes preserve session",Object.ReferenceEquals(original,app.Session));
      var displayClock=DateTime.UtcNow;
      app.Session=Baseline(41,1000,now);app.View.RefreshData(displayClock,true);
      var frozen=app.View.Display;
      for(int i=1;i<=11;i++) {
       app.Session.Observe(Read(41,1000+i*10),now.AddSeconds(i));
       Check("display holds on background reading "+i,!app.View.RefreshData(displayClock.AddSeconds(i),false)&&Object.ReferenceEquals(frozen,app.View.Display));
      }
      Check("background EXP still counts between visual updates",app.Session.Net==110&&app.View.Display.Current.Exp==1000);
      using(var repaint=app.View.Render())Check("repainting does not publish live values",app.View.Display.Current.Exp==1000);
      Check("display publishes at twelve seconds",app.View.RefreshData(displayClock.AddSeconds(12),false)&&app.View.Display.Current.Exp==1110);
      app.Session.Current.Exp=1120;Check("display owns a copy of its reading",app.View.Display.Current.Exp==1110);
      app.TogglePause();Check("explicit pause responds immediately",app.View.Display.Paused);
      app.TogglePause();Check("explicit resume responds immediately",!app.View.Display.Paused);
      app.Session=new Session();app.View.RefreshData(displayClock,true);app.Session=Baseline(41,1200,now);
      Check("first confirmed reading displays without twelve-second delay",app.View.RefreshData(displayClock.AddSeconds(1),false)&&app.View.Display.Current.Exp==1200);
      app.Settings.Scale=1.5;app.Settings.Tiny=false;app.View.UpdateLayout();
      Check("button switches full to tiny at display scale",app.View.ToggleAt(new Point(495,35))&&app.Settings.Tiny);
      Check("button switches tiny to full at display scale",app.View.ToggleAt(new Point(360,12))&&!app.Settings.Tiny);
      Check("drag area does not trigger mode switch",!app.View.ToggleAt(new Point(150,80))&&!app.Settings.Tiny);
      app.Settings.Locked=true;Check("click-through mode preserves lock",!app.View.ToggleAt(new Point(495,35))&&!app.Settings.Tiny);app.Settings.Locked=false;
      Check("rounded window excludes transparent corners",!app.View.Region.IsVisible(0,0)&&app.View.Region.IsVisible(495,35));
      Check("window does not use magenta color-key transparency",app.View.TransparencyKey.IsEmpty&&app.View.BackColor!=Color.Magenta);
     } finally { app.View.Dispose();app.Menu.Dispose();app.Dispose(); }
    }
    results.Add("All "+results.Count+" checks passed.");Console.WriteLine(String.Join(Environment.NewLine,results));
    File.WriteAllLines(Path.Combine(folder,args.Length>0?"ocr-results.txt":"test-results.txt"),results);return 0;
   } catch(Exception e) { results.Add(e.ToString());File.WriteAllLines(Path.Combine(folder,"failed-tests.txt"),results);Console.Error.WriteLine(e);return 1; }
  }
 }
}
