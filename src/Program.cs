using System;
using System.Threading;
using System.Windows.Forms;
using System.Reflection;

[assembly: AssemblyTitle("Viddy EXP Only")]
[assembly: AssemblyDescription("Standalone screen-reading EXP tracker")]
[assembly: AssemblyCompany("Viddy EXP contributors")]
[assembly: AssemblyProduct("Viddy EXP Only")]
[assembly: AssemblyVersion("1.0.3.0")]
[assembly: AssemblyFileVersion("1.0.3.0")]
namespace ViddyExp {
 static class Program {
  [STAThread] static void Main() {
   try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch(EntryPointNotFoundException) { Native.SetProcessDPIAware(); }
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   bool created;
   using(var mutex=new Mutex(true,"Local\\ViddyExpOnly-1",out created)) {
    if(!created) { MessageBox.Show("Viddy EXP Only is already running. Look for its tray icon.","Viddy EXP Only");return; }
    try { Application.Run(new Tracker(true)); }
    catch(Exception e) { MessageBox.Show(e.Message,"Viddy EXP Only could not start",MessageBoxButtons.OK,MessageBoxIcon.Error); }
   }
  }
 }
}
