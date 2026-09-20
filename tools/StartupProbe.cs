using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
class StartupProbe {
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(IntPtr p,IntPtr a,byte[] b,IntPtr n,out IntPtr read);
  static int Main(string[] args) {
    Process game=null;
    try {
      var asm=Assembly.LoadFrom(args[0]);
      var method=asm.GetType("ChangpogoLauncher.SinglePlayerPatch").GetMethod("Start",BindingFlags.NonPublic|BindingFlags.Static);
      game=(Process)method.Invoke(null,new object[]{args[1]});
      byte[] entry=new byte[6];IntPtr read;
      if(!ReadProcessMemory(game.Handle,new IntPtr(0x488bb3),entry,new IntPtr(6),out read)||read.ToInt32()!=6||entry[0]!=0xe9||entry[5]!=0x90)throw new Exception("Entry hook missing");
      uint target=unchecked((uint)(0x488bb8+BitConverter.ToInt32(entry,1)));
      byte[] stub=new byte[10];
      if(!ReadProcessMemory(game.Handle,new IntPtr(unchecked((int)target)),stub,new IntPtr(10),out read)||BitConverter.ToString(stub)!="9C-83-3D-C4-E8-70-00-00-75-06")throw new Exception("Mode gate missing");
      if(game.WaitForExit(5000))throw new Exception("Early game exit: "+game.ExitCode);
      Console.WriteLine("PASS original process starts, hook and single-player gate present, alive after 5 seconds, PID="+game.Id);
      return 0;
    } catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
    finally {
      if(game!=null) {
        if(!game.HasExited) { game.CloseMainWindow();if(!game.WaitForExit(2000))game.Kill(); }
        game.Dispose();
      }
    }
  }
}
