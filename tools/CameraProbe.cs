using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
class CameraProbe {
  [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr p,IntPtr a,byte[] b,IntPtr size,out IntPtr read);
  static int Main(string[] args) {
    if(Process.GetProcessesByName("Changpogo").Length!=0)throw new Exception("Close game before probe");
    var a=Assembly.LoadFrom(args[0]);
    var start=a.GetType("ChangpogoLauncher.SinglePlayerPatch").GetMethod("StartConfigured",BindingFlags.NonPublic|BindingFlags.Static);
    int option=int.Parse(args[2]);bool latency=args.Length>3;
    var p=(Process)start.Invoke(null,new object[]{args[1],latency,option});
    try {
    var data=new byte[4];IntPtr n;
    if(!ReadProcessMemory(p.Handle,new IntPtr(0x69d7bc),data,new IntPtr(4),out n)||n.ToInt32()!=4||BitConverter.ToSingle(data,0)!=(option==0?36f:option==1?45f:54f))throw new Exception("Wrong camera default");
    var entry=new byte[1];
    if(!ReadProcessMemory(p.Handle,new IntPtr(0x488bb3),entry,new IntPtr(1),out n)||entry[0]!=(latency?0xe9:0x55))throw new Exception("Latency toggle ignored");
    if(p.WaitForExit(5000))throw new Exception("Early exit: "+p.ExitCode);
    Console.WriteLine("PASS memory and 5s startup: camera="+BitConverter.ToSingle(data,0)+", latency="+latency+", PID="+p.Id);
    return 0;
    } finally {
      if(!p.HasExited) { p.CloseMainWindow();if(!p.WaitForExit(2000))p.Kill();p.WaitForExit(); }
      p.Dispose();
    }
  }
}
