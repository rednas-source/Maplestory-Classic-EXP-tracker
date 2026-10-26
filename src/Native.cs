using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace ViddyExp {
 public sealed class GameWindow {
  public IntPtr Handle;
  public string Title;
 }
 // Window enumeration, visible-screen capture and our own overlay only.
 public static class Native {
  delegate bool EnumProc(IntPtr h, IntPtr unused);
  [StructLayout(LayoutKind.Sequential)] struct RECT { public int L,T,R,B; }
  [StructLayout(LayoutKind.Sequential)] struct POINT { public int X,Y; }
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p,IntPtr unused);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder s,int max);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
  [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h,out RECT r);
  [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h,ref POINT p);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h,int index);
  [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h,int index,int value);
  [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
  [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr context);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  public static Rectangle ClientBounds(IntPtr h) {
   RECT r; var p=new POINT();
   if(!GetClientRect(h,out r)||!ClientToScreen(h,ref p)) return Rectangle.Empty;
   return new Rectangle(p.X,p.Y,Math.Max(0,r.R-r.L),Math.Max(0,r.B-r.T));
  }
  public static List<GameWindow> Games() {
   var list=new List<GameWindow>(); int self=Process.GetCurrentProcess().Id;
   EnumWindows(delegate(IntPtr h,IntPtr ignored) {
    if(!IsWindowVisible(h)||IsIconic(h)) return true;
    uint pid; GetWindowThreadProcessId(h,out pid); if(pid==self) return true;
    var text=new StringBuilder(512); GetWindowText(h,text,text.Capacity);
    try { using(var p=Process.GetProcessById((int)pid)) {
     string n=p.ProcessName.ToLowerInvariant(),t=text.ToString().ToLowerInvariant();
     bool excluded=n.Contains("chrome")||n.Contains("edge")||n.Contains("opera")||n.Contains("firefox")||n.Contains("brave")||n.Contains("codex")||n.Contains("discord")||n.Contains("obs");
     bool maple=n.Contains("maple")||n=="msw"||n.Contains("mswclient")||t.Contains("maplestory")||t.Contains("maple story");
     var bounds=ClientBounds(h);
     if(!excluded&&maple&&bounds.Width>=500&&bounds.Height>=300) list.Add(new GameWindow { Handle=h,Title=text.ToString() });
    }} catch(ArgumentException){} catch(System.ComponentModel.Win32Exception){}
    return true;
   },IntPtr.Zero);
   return list;
  }
  public static Bitmap Capture(Rectangle r) {
   var b=new Bitmap(r.Width,r.Height,PixelFormat.Format24bppRgb);
   try { using(var g=Graphics.FromImage(b)) g.CopyFromScreen(r.Location,Point.Empty,r.Size,CopyPixelOperation.SourceCopy); return b; }
   catch { b.Dispose(); throw; }
  }
 }
}
