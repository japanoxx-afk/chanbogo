using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ChangpogoLauncher {
  sealed class LowLatencySession : IDisposable {
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint period);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool SetProcessPriorityBoost(IntPtr process, bool disablePriorityBoost);
    bool timerActive;
    public void Begin(Process process) {
      if(timeBeginPeriod(1)==0) timerActive=true;
      try { process.PriorityClass=ProcessPriorityClass.High; }
      catch(Exception ex) { throw new InvalidOperationException("게임 프로세스 우선순위를 설정하지 못했습니다.",ex); }
      if(!SetProcessPriorityBoost(process.Handle,false)) throw new Win32Exception(Marshal.GetLastWin32Error(),"게임 프로세스 우선순위 부스트를 설정하지 못했습니다.");
    }
    public void Dispose() { if(timerActive) { timeEndPeriod(1);timerActive=false; } }
  }
}
