using System;
using System.Diagnostics;
using System.Reflection;
class DisplayProbe {
  static void Main(string[] args) {
    if(Process.GetProcessesByName("Changpogo").Length!=0)throw new Exception("Close game before probe");
    var asm=Assembly.LoadFrom(args[0]);
    asm.GetType("ChangpogoLauncher.DisplayOptions").GetMethod("Prepare",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{args[1],true,args.Length<3});
    var p=Process.Start(new ProcessStartInfo(args[1]) { WorkingDirectory=System.IO.Path.GetDirectoryName(args[1]),UseShellExecute=false });
    Console.WriteLine("Test PID="+p.Id);
  }
}
