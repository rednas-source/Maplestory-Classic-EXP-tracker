using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Tesseract;

namespace ViddyExp {
 public class Word { public string Text; public Rectangle Rect; public float Confidence; }
 public class OcrReader : IDisposable {
  TesseractEngine engine;NeuralDigits expModel;
  Rectangle expBox,levelBox;
  Size previousSize;
  public int ExpectedLevel;
  public string Diagnostic="Starting OCR";
  public string Trace="";
  public Rectangle ExpBox {get{return expBox;}}
  public Rectangle LevelBox {get{return levelBox;}}
  public OcrReader(){engine=new TesseractEngine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tessdata"),"eng",EngineMode.LstmOnly);engine.SetVariable("user_defined_dpi",300);}
  public void Reset(){expBox=Rectangle.Empty;levelBox=Rectangle.Empty;}
  public void Dispose(){engine.Dispose();if(expModel!=null)expModel.Dispose();}
  public Reading Read(Bitmap frame) {
   Trace="";
   if(frame.Size!=previousSize){Reset();previousSize=frame.Size;}
   if(!expBox.IsEmpty) {var cached=ReadBoxes(frame);if(cached!=null)return cached;Reset();}
   // Start with the HUD-only strip: nearby chat and buttons can make sparse OCR
   // discard all three labels even when their pixels are clear. Broader scans
   // remain available for moved/scaled HUD layouts.
   foreach(double portion in new[]{0.06,0.09,0.24}) {
    int y=(int)(frame.Height*(1-portion));var region=new Rectangle(0,y,frame.Width,frame.Height-y);
    List<Word> words;string scanText=Scan(frame,region,2,false,PageSegMode.SparseText,out words,false);Trace+="\nSCAN "+portion+": "+scanText;
    var exp=words.Where(w=>Regex.IsMatch(w.Text.Trim(),"^EXP[.:]?$",RegexOptions.IgnoreCase)).OrderByDescending(w=>w.Rect.Y).FirstOrDefault();
    if(exp==null)continue;
    var lv=words.Where(w=>Regex.IsMatch(w.Text.Trim(),"^L[VYv][.:]?$",RegexOptions.IgnoreCase)&&w.Rect.X<exp.Rect.X&&Math.Abs(w.Rect.Y-exp.Rect.Y)<exp.Rect.Height*2.5).OrderByDescending(w=>w.Rect.X).FirstOrDefault();
    // An overlay/video may cover LV. Only allow the percentage-table fallback
    // beside the real HP and MP labels, not an arbitrary EXP sentence.
    if(lv==null&&!new[]{"HP","MP"}.All(label=>words.Any(w=>Regex.IsMatch(w.Text.Trim(),"^"+label+"[.:]?$",RegexOptions.IgnoreCase)&&w.Rect.X<exp.Rect.X&&Math.Abs(w.Rect.Y-exp.Rect.Y)<exp.Rect.Height*2.5)))continue;
    int h=Math.Max(10,exp.Rect.Height);
    var row=words.Where(w=>w.Rect.X>=exp.Rect.X && w.Rect.X<exp.Rect.X+h*20 && Math.Abs(w.Rect.Y+w.Rect.Height/2-(exp.Rect.Y+exp.Rect.Height/2))<h*.65).OrderBy(w=>w.Rect.X).ToList();
    var percentWord=row.FirstOrDefault(w=>w.Text.Contains("%"));
    int right=percentWord==null?exp.Rect.X+h*15:percentWord.Rect.Right;
    expBox=Clamp(new Rectangle(exp.Rect.X-3,exp.Rect.Y-2,Math.Min(frame.Width-exp.Rect.X+3,right-exp.Rect.X+6),h+4),frame.Size);
    levelBox=lv==null?Rectangle.Empty:Clamp(new Rectangle(lv.Rect.Right+2,lv.Rect.Y-3,lv.Rect.Height*3,lv.Rect.Height+6),frame.Size);
    var reading=ReadBoxes(frame);if(reading!=null)return reading;Reset();
   }
   Diagnostic="HUD not readable · show EXP amount and percentage; LV is needed when progress is ambiguous";return null;
  }
  Reading ReadBoxes(Bitmap frame){
   List<Word> words;string lvl=levelBox.IsEmpty?"":Scan(frame,levelBox,3,true,PageSegMode.SingleLine,out words,true);Trace+="\nLV: "+lvl;
   int level=0;var m=Regex.Match(lvl,@"\d{1,3}");if(!m.Success||!Int32.TryParse(m.Value,out level)||level<1||level>99)level=0;
   string exp=Scan(frame,expBox,3,false,PageSegMode.SingleLine,out words,false);Trace+="\nEXP: "+exp;double confidence=words.Count==0?0:words.Average(w=>w.Confidence);
   Trace+="\nEXP confidence: "+confidence.ToString("0.0");
   var result=ReadingParser.ParseHud(level,ExpectedLevel,exp,confidence);
   if(result==null){exp=Scan(frame,expBox,2,false,PageSegMode.SparseText,out words,false);Trace+="\nEXP fallback: "+exp;confidence=words.Count==0?0:words.Average(w=>w.Confidence);Trace+="\nEXP fallback confidence: "+confidence.ToString("0.0");result=ReadingParser.ParseHud(level,ExpectedLevel,exp,confidence);}
   if(result==null&&level>0&&!levelBox.IsEmpty){
    if(expModel==null)expModel=new NeuralDigits();var independentLevel=expModel.Read(frame,levelBox);
    var alternate=Scan(frame,levelBox,3,false,PageSegMode.SingleLine,out words,true);
    var independentExp=expModel.Read(frame,expBox);
    Trace+="\nLV cross-check: "+alternate.Trim()+" / "+independentLevel.Text.Trim()+" @"+independentLevel.Confidence.ToString("0.00");
    result=ReadingParser.RecoverConflictingLevel(ExpectedLevel,alternate,independentLevel.Text,independentLevel.Confidence*100,exp,independentExp.Text,independentExp.Confidence*100);
   }
   if(result==null&&level==0){
    if(expModel==null)expModel=new NeuralDigits();var neural=expModel.Read(frame,expBox);Trace+="\nEXP independent: "+neural.Text+" @"+neural.Confidence.ToString("0.00");
    result=ReadingParser.InferCorroborated(exp,neural.Text,neural.Confidence*100);if(!ReadingParser.CompatibleInference(result,ExpectedLevel))result=null;
   }
   if(result==null){Diagnostic="EXP / percentage mismatch: "+exp.Trim();return null;}
   Diagnostic="LV "+result.Level+(result.LevelInferred?" (verified from EXP / %)":"")+" · "+Format.Number(result.Exp)+" EXP · "+result.Percent.ToString("0.00")+"%";return result;
  }
  public string Scan(Bitmap original,Rectangle crop,int scale,bool whiteMask,PageSegMode mode,out List<Word> words,bool digits){
   crop=Clamp(crop,original.Size);words=new List<Word>();if(crop.Width<3||crop.Height<3)return "";
   using(var b=new Bitmap(crop.Width*scale+24,crop.Height*scale+24,PixelFormat.Format24bppRgb)){
    using(var g=Graphics.FromImage(b)){g.Clear(whiteMask?Color.White:Color.FromArgb(55,59,62));g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(original,new Rectangle(12,12,crop.Width*scale,crop.Height*scale),crop,GraphicsUnit.Pixel);}
    if(whiteMask) Mask(b);
    engine.SetVariable("tessedit_char_whitelist",digits?"0123456789":"");
    using(var ms=new MemoryStream()){b.Save(ms,System.Drawing.Imaging.ImageFormat.Png);using(var pix=Pix.LoadFromMemory(ms.ToArray()))using(var page=engine.Process(pix,mode)){
     string text=page.GetText();using(var it=page.GetIterator()){it.Begin();do{Rect r;if(it.TryGetBoundingBox(PageIteratorLevel.Word,out r)){var txt=it.GetText(PageIteratorLevel.Word);if(!String.IsNullOrWhiteSpace(txt))words.Add(new Word{Text=txt,Confidence=it.GetConfidence(PageIteratorLevel.Word),Rect=new Rectangle(crop.X+(r.X1-12)/scale,crop.Y+(r.Y1-12)/scale,Math.Max(1,r.Width/scale),Math.Max(1,r.Height/scale))});}}while(it.Next(PageIteratorLevel.Word));}return text;
    }}
   }
  }
  static void Mask(Bitmap b){
   var data=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadWrite,PixelFormat.Format24bppRgb);
   try{var bytes=new byte[data.Stride*b.Height];Marshal.Copy(data.Scan0,bytes,0,bytes.Length);
    for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++){int i=y*data.Stride+x*3;byte v=(byte)((x>=12&&y>=12&&x<b.Width-12&&y<b.Height-12&&Math.Min(bytes[i],Math.Min(bytes[i+1],bytes[i+2]))>170)?0:255);bytes[i]=bytes[i+1]=bytes[i+2]=v;}Marshal.Copy(bytes,0,data.Scan0,bytes.Length);
   }finally{b.UnlockBits(data);}
  }
  static Rectangle Clamp(Rectangle r,Size s){return Rectangle.Intersect(r,new Rectangle(Point.Empty,s));}
 }
}
