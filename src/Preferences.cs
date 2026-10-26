using System;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;

namespace ViddyExp {
 public sealed class Preferences {
  public int X=40,Y=40;
  public double Scale=1;
  public bool Tiny,Locked;
  public string DisplayName="Viddy EXP";
  public void Validate() {
   if(Double.IsNaN(Scale)||Double.IsInfinity(Scale)) Scale=1;
   Scale=Math.Max(.75,Math.Min(1.5,Scale));
   DisplayName=(DisplayName??"Viddy EXP").Trim();
   if(DisplayName.Length>20) DisplayName=DisplayName.Substring(0,20);
  }
 }
 public static class PreferencesStore {
  public static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ViddyExpOnly");
  public static string Error="";
  public static Preferences Load() {
   try {
    string p=Path.Combine(Folder,"preferences.json");
    var c=File.Exists(p)?new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(p)):new Preferences();
    c=c??new Preferences(); c.Validate(); return c;
   } catch(Exception e) { Error="Preferences could not be read: "+e.Message; return new Preferences(); }
  }
  public static void Save(Preferences settings) {
   try {
    Directory.CreateDirectory(Folder);
    string path=Path.Combine(Folder,"preferences.json"),temp=path+".tmp";
    File.WriteAllText(temp,new JavaScriptSerializer().Serialize(settings));
    if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
    Error="";
   } catch(Exception e) { Error="Preferences could not be saved: "+e.Message; }
  }
 }
}
