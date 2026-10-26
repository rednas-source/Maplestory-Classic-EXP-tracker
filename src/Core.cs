using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ViddyExp {
 public static class ExpTable {
  public const string Source = "https://meowdb.com/msclassic/guides/exp-table-level-1-to-100";
  public const string Verified = "2026-10-26";
  public static readonly long[] Required = Build();
  static long[] Build() {
   var a = new long[101];
   for(int l=1;l<=99;l++) {
    if(l<=5) a[l]=(l*l/2+15)*l;
    else if(l<=50) { long q=l*l/3; a[l]=(q+19)*q; }
    else if(l<=89) a[l]=(long)decimal.Truncate(a[l-1]*1.0548m);
    else a[l]=(long)decimal.Truncate(a[l-1]*2.1m);
   }
   return a;
  }
  public static long Total(int level,long exp) {long n=exp;for(int i=1;i<level;i++) n+=Required[i];return n;}
  public static bool Valid(int level,long exp,double percent) {
   return level>=1 && level<=99 && exp>=0 && exp<Required[level] && percent>=0 && percent<=100
    && Math.Abs(exp*100.0/Required[level]-percent)<=0.011;
  }
 }
 public class Reading {
  public int Level; public long Exp; public double Percent; public double Confidence;
  public string Text;public bool LevelInferred;
  public long Total {get{return ExpTable.Total(Level,Exp);}}
 }
 public static class ReadingParser {
  public static Reading InferLevel(string text,double confidence){
   // Require the EXP label and a unique agreement between integer EXP and
   // its displayed percentage. Zero/ambiguous progress must wait for LV.
   if(confidence<70||!Regex.IsMatch(text??"",@"^\s*EXP(?=\s*[0-9])",RegexOptions.IgnoreCase))return null;
   Reading match=null;
   for(int level=1;level<=99;level++){var r=Parse(level,text,confidence);if(r==null)continue;if(match!=null)return null;match=r;}
   if(match!=null)match.LevelInferred=true;
   return match;
  }
  public static Reading ParseHud(int visibleLevel,int expectedLevel,string text,double confidence){
   if(visibleLevel>0)return Parse(visibleLevel,text,confidence);
   // The known session level constrains fallback OCR. A proportional digit error
   // can coincidentally match the EXP table for an unrelated, much higher level.
   var inferred=InferLevel(text,confidence);
   return CompatibleInference(inferred,expectedLevel)?inferred:null;
  }
  public static bool CompatibleInference(Reading r,int expectedLevel){return r!=null&&(expectedLevel<1||!r.LevelInferred||r.Level==expectedLevel||r.Level==expectedLevel+1);}
  public static Reading InferCorroborated(string anchored,string independent,double confidence){
   if(confidence<84||!Regex.IsMatch(anchored??"",@"^\s*EXP(?=\s*[0-9])",RegexOptions.IgnoreCase))return null;
   // The HUD anchor and first engine already read EXP. The neural engine can
   // confuse its outlined P with R; correct only that label, never its digits.
   independent=Regex.Replace(independent??"",@"^\s*EXR(?=\s*[0-9])","EXP",RegexOptions.IgnoreCase);
   var inferred=InferLevel(independent,confidence);var other=inferred==null?null:Parse(inferred.Level,anchored,0);
   return other!=null&&other.Exp==inferred.Exp&&Math.Abs(other.Percent-inferred.Percent)<.001?inferred:null;
  }
  public static Reading RecoverConflictingLevel(int expectedLevel,string colorLevel,string independentLevel,double levelConfidence,string anchoredExp,string independentExp,double expConfidence){
   // A white-mask 45 can resemble 43. Never discard a conflicting LV merely
   // because EXP fits: require two alternate LV reads plus exact EXP agreement.
   int level;string color=(colorLevel??"").Trim(),independent=(independentLevel??"").Trim();
   if(Double.IsNaN(levelConfidence)||levelConfidence<65||!Regex.IsMatch(color,@"^[1-9][0-9]?$")||color!=independent||!Int32.TryParse(color,out level))return null;
   var recovered=InferCorroborated(anchoredExp,independentExp,expConfidence);
   return recovered!=null&&recovered.Level==level&&CompatibleInference(recovered,expectedLevel)?recovered:null;
  }
  public static Reading Parse(int level,string text,double confidence) {
   if(level<1 || level>99 || String.IsNullOrWhiteSpace(text)) return null;
   // The game uses either comma or period thousands separators depending on locale/OCR.
   // A visible percent sign is mandatory: HP/MP or chat digits must never become EXP.
   var m=Regex.Match(text,@"(?:EXP\s*)?([0-9][0-9,.\s]*?)\s*[\[\(\{]\s*(\d{1,3}[.,]\d{1,2})\s*%",RegexOptions.IgnoreCase);
   // Classic's thin green opening bracket is often read as 1, I, or |.
   // Accept that specific OCR substitution only after an EXP anchor, and still
   // require exact integer EXP to agree with the independently read percentage.
   if(!m.Success)m=Regex.Match(text,@"EXP\s*([0-9][0-9,.\s]*?)\s*[1Il|]\s*(\d{1,2}[.,]\d{1,2})\s*%",RegexOptions.IgnoreCase);
   if(!m.Success) return null;
   long exp; double percent;
   if(!Int64.TryParse(Regex.Replace(m.Groups[1].Value,@"\D",""),out exp))return null;
   if(!Double.TryParse(m.Groups[2].Value.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out percent))return null;
   if(!ExpTable.Valid(level,exp,percent))return null;
   return new Reading {Level=level,Exp=exp,Percent=percent,Confidence=confidence,Text=text.Trim()};
  }
 }
 public class Sample { public double At; public DateTime Utc; public long Delta; public int Level; public long Exp; public string Kind; }
 public class Session {
  public readonly List<Sample> Samples=new List<Sample>();
  public Reading Current;
  public DateTime StartedUtc=DateTime.UtcNow;
  public double ActiveSeconds;
  public long Gained,Lost;
  public int Levels;
  public int Gaps;
  public string Notice="Waiting for your game";
  DateTime? lastSeen;
  bool rebase=true;
  Reading candidate;
  DateTime candidateAt;
  int confirmations;
  public long Net {get{return Gained-Lost;}}
  public void Break() { if(!rebase && Current!=null)Gaps++; rebase=true;lastSeen=null;candidate=null;confirmations=0; }
  public void CaptureGap(DateTime now){
   // Keep a pending baseline through a short OCR gap. Focus loss and explicit
   // pauses still use Break and discard it immediately; unseen gains stay excluded.
   if(rebase&&candidate!=null&&(now-candidateAt).TotalSeconds<=30)return;
   Break();
  }
  public string Observe(Reading r,DateTime now) {
   if(r==null || !ExpTable.Valid(r.Level,r.Exp,r.Percent)) return "Invalid reading rejected";
   // Inferred level changes never prove a character switch, including after a
   // focus/capture gap. Keep scanning until a compatible frame returns.
   if(Current!=null&&!ReadingParser.CompatibleInference(r,Current.Level)){candidate=null;confirmations=0;Notice="Uncertain inferred level · rechecking HUD";return Notice;}
   if(lastSeen.HasValue && (now-lastSeen.Value).TotalSeconds>6) Break();
   // Require two compatible frames for a baseline, any loss, a level transition,
   // or a sudden >20% jump. Ordinary gains are already cross-checked with the %.
   bool unusual=Current==null || rebase || r.Level!=Current.Level || r.Exp<Current.Exp || r.Exp-Current.Exp>ExpTable.Required[r.Level]/5;
   if(unusual) {
    bool consistent=candidate!=null && (now-candidateAt).TotalSeconds<=30 && r.Level==candidate.Level && r.Exp>=candidate.Exp && r.Exp-candidate.Exp<Math.Max(50,ExpTable.Required[r.Level]/20);
    confirmations=consistent?confirmations+1:1;candidate=r;candidateAt=now;
    if(confirmations<2) {Notice="Confirming reading";return Notice;}
   }
   candidate=null;confirmations=0;
   if(Current==null || rebase) {
    Current=r;lastSeen=now;rebase=false;
    Samples.Add(new Sample{At=ActiveSeconds,Utc=now,Level=r.Level,Exp=r.Exp,Kind="baseline"});
    Notice=Gaps>0?"Resumed · unseen EXP excluded":"Tracking";return Notice;
   }
   if(r.Level<Current.Level || r.Level>Current.Level+3) {
    Break();Notice="Character changed · start a new session";return Notice;
   }
   var delta=r.Total-Current.Total;
   ActiveSeconds+=Math.Max(0,Math.Min(6,(now-lastSeen.Value).TotalSeconds));
   if(delta>=0)Gained+=delta;else Lost-=delta;
   if(r.Level>Current.Level)Levels+=r.Level-Current.Level;
   Samples.Add(new Sample{At=ActiveSeconds,Utc=now,Delta=delta,Level=r.Level,Exp=r.Exp,Kind=delta<0?"loss":r.Level>Current.Level?"level-up":"reading"});
   Current=r;lastSeen=now;
   Samples.RemoveAll(x=>x.At<ActiveSeconds-1800);
   Notice=delta<0?"EXP loss recorded":"Tracking";
   return Notice;
  }
  public long GainIn(double seconds) {double start=Math.Max(0,ActiveSeconds-seconds);return Samples.Where(x=>x.At>start && x.Kind!="baseline").Sum(x=>x.Delta);}
  public double Rate(double seconds) {double span=Math.Min(ActiveSeconds,seconds);return span<30?0:GainIn(seconds)*3600.0/span;}
  public double Eta {get{return Current==null || Rate(1800)<=0 ? Double.NaN:(ExpTable.Required[Current.Level]-Current.Exp)/Rate(1800)*3600;}}
 }
 public static class Format {
  public static string Number(long n){return n.ToString("N0",CultureInfo.InvariantCulture);}
  public static string Compact(double n){double a=Math.Abs(n);return a>=1e9?(n/1e9).ToString("0.00",CultureInfo.InvariantCulture)+"B":a>=1e6?(n/1e6).ToString("0.00",CultureInfo.InvariantCulture)+"M":a>=1000?(n/1000).ToString("0.0",CultureInfo.InvariantCulture)+"K":n.ToString("0",CultureInfo.InvariantCulture);}
  public static string Signed(long n){return (n>=0?"+":"−")+Compact(Math.Abs(n));}
  public static string Duration(double seconds){if(Double.IsNaN(seconds)||Double.IsInfinity(seconds))return "—";if(seconds>=86400)return ((int)(seconds/86400))+"d "+((int)(seconds/3600)%24)+"h";if(seconds>=3600)return ((int)(seconds/3600))+"h "+((int)(seconds/60)%60)+"m";return ((int)(seconds/60))+"m "+((int)seconds%60)+"s";}
 }
}
