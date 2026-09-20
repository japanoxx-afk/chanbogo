using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
class DiagnosticTests {
  static int Main(string[] args) {
    var asm=Assembly.LoadFrom(args[0]);var type=asm.GetType("ChangpogoLauncher.GameDiagnostics");
    var diagnostic=Activator.CreateInstance(type,BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{args[1],"integration test"},null);
    string folder=(string)type.GetField("DirectoryPath",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(diagnostic);
    var callback=(Action<Process>)Delegate.CreateDelegate(typeof(Action<Process>),diagnostic,type.GetMethod("Attach",BindingFlags.Instance|BindingFlags.NonPublic));
    Process game=null;
    try {
      game=(Process)asm.GetType("ChangpogoLauncher.SinglePlayerPatch").GetMethod("StartObserved",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{args[1],args.Length>3,callback});
      if(args.Length>2) {
        if(game.WaitForExit(5000))throw new Exception("Game exited during diagnostic startup");
        ((IDisposable)diagnostic).Dispose();
        if(game.WaitForExit(500))throw new Exception("Detaching killed game");
        string timing=File.ReadAllText(Path.Combine(folder,"timing.csv"));
        if(timing.Contains("NA,")||timing.Split('\n').Length<3)throw new Exception("Game telemetry absent");
        Console.WriteLine("PASS real game startup, verified-build telemetry and safe debugger detach: "+folder);
        game.CloseMainWindow();if(!game.WaitForExit(2000)){game.Kill();game.WaitForExit();}return 0;
      }
      if(!game.WaitForExit(15000))throw new Exception("Debugger did not allow target to exit");
      ((IDisposable)diagnostic).Dispose();
      bool crash=Path.GetFileNameWithoutExtension(args[1])=="CrashTarget";
      if(File.Exists(Path.Combine(folder,"crash.dmp"))!=crash)throw new Exception("Wrong dump presence");
      if(crash) {
        if(unchecked((uint)game.ExitCode)!=0xE1234567)throw new Exception("Original exception code changed");
        byte[] dump=File.ReadAllBytes(Path.Combine(folder,"crash.dmp"));
        if(dump.Length<100||BitConverter.ToUInt32(dump,0)!=0x504d444d)throw new Exception("Invalid minidump");
        string events=File.ReadAllText(Path.Combine(folder,"events.log"));
        if(!events.Contains("firstChance=0")||!events.Contains("dump=True"))throw new Exception("Missing fatal exception");
      } else if(game.ExitCode!=0)throw new Exception("Normal target exit changed");
      Console.WriteLine("PASS "+(crash?"fatal exception, valid dump and original termination":"normal exit without false crash dump")+": "+folder);return 0;
    } catch(Exception ex) { Console.WriteLine("FAIL "+ex.GetType().Name+": "+ex.Message);return 1; }
    finally { ((IDisposable)diagnostic).Dispose();if(game!=null){if(!game.HasExited)game.Kill();game.Dispose();} }
  }
}
