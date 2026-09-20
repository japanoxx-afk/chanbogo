using System;
using System.Runtime.InteropServices;
using System.Threading;
class DiagnosticTarget {
  [DllImport("kernel32.dll")] static extern void RaiseException(uint code,uint flags,uint count,IntPtr args);
  static void Main() {
    Thread.Sleep(1500);
    if(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location)=="CrashTarget")
      RaiseException(0xE1234567,1,0,IntPtr.Zero);
  }
}
