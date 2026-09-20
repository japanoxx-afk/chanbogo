using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ChangpogoLauncher {
  // Local-only, x86 debugger. Never uploads reports or changes network timing.
  sealed class GameDiagnostics : IDisposable {
    internal static string Root { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ChangpogoLauncher","Diagnostics"); } }
    internal readonly string DirectoryPath;
    readonly object gate=new object();
    readonly ManualResetEvent ready=new ManualResetEvent(false);
    Thread worker;Process game;volatile bool stop;bool supported;long written;int samples;
    Exception attachError;
    [StructLayout(LayoutKind.Sequential)] struct DebugEvent {
      public uint kind,pid,tid;
      [MarshalAs(UnmanagedType.ByValArray,SizeConst=84)] public byte[] data;
    }
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool DebugActiveProcess(uint pid);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool DebugActiveProcessStop(uint pid);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool DebugSetProcessKillOnExit(bool kill);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool WaitForDebugEvent(out DebugEvent e,uint timeout);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool ContinueDebugEvent(uint pid,uint tid,uint status);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(IntPtr p,IntPtr a,byte[] b,UIntPtr n,out UIntPtr read);
    [DllImport("dbghelp.dll",SetLastError=true)] static extern bool MiniDumpWriteDump(IntPtr p,uint pid,IntPtr file,uint type,IntPtr exception,IntPtr streams,IntPtr callback);

    internal GameDiagnostics(string path,string options) {
      DirectoryPath=Path.Combine(Root,DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
      Directory.CreateDirectory(DirectoryPath);
      Write("launcher="+typeof(GameDiagnostics).Assembly.GetName().Version+" options="+options);
      Write("OS="+Environment.OSVersion+" CPU-count="+Environment.ProcessorCount);
      foreach(string name in new[]{"Changpogo.exe","DDraw.dll","D3DImm.dll"}) {
        string file=Path.Combine(Path.GetDirectoryName(path),name);
        if(!File.Exists(file)){Write("missing="+name);continue;}
        using(var stream=File.OpenRead(file))using(var sha=SHA256.Create()) {
          string hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
          Write(name+" sha256="+hash+" version="+FileVersionInfo.GetVersionInfo(file).FileVersion);
          if(name=="Changpogo.exe")supported=hash=="16E4C3D3D17438928839A09901A4159DA154EB0EC822C784856707B77F9F4B1F";
        }
      }
      foreach(string name in new[]{"Changpogo.ini","dgVoodoo.conf"}) {
        string file=Path.Combine(Path.GetDirectoryName(path),name);
        if(File.Exists(file)&&new FileInfo(file).Length<1024*1024)File.Copy(file,Path.Combine(DirectoryPath,name));
      }
      File.WriteAllText(Path.Combine(DirectoryPath,"README.txt"),"Local diagnostic report. Nothing is uploaded automatically. Dumps may contain game memory, paths or network addresses; share privately, not in a public issue.\r\nException address and thread id are in events.log. crash.dmp contains thread stacks and modules (no explicit ExceptionStream).\r\nTiming values are sampled engine counters, NOT measured command latency or ping. Include map, faction, players and reproduction time.\r\n",Encoding.UTF8);
      File.WriteAllText(Path.Combine(DirectoryPath,"timing.csv"),"utc,mode,active,scheduler_clock,drift_accumulator,simulation_credit,net_state,net_counter_0c,net_counter_14,private_bytes,cpu_ms\r\n");
    }
    internal void Write(string message) {
      lock(gate) {
        if(written>4*1024*1024)return;
        string line=DateTime.UtcNow.ToString("o")+" "+message+Environment.NewLine;
        try { File.AppendAllText(Path.Combine(DirectoryPath,"events.log"),line,Encoding.UTF8);written+=Encoding.UTF8.GetByteCount(line); }catch { }
      }
    }
    // Called while the newly created game main thread is suspended.
    internal void Attach(Process process) {
      game=Process.GetProcessById(process.Id);
      worker=new Thread(Run) { IsBackground=true,Name="Changpogo diagnostics" };worker.Start();
      if(!ready.WaitOne(10000)) { stop=true;throw new TimeoutException("진단 연결 시간 초과"); }
      if(attachError!=null)throw new InvalidOperationException("진단 연결 실패: "+attachError.Message,attachError);
    }
    uint U(byte[] data,int offset) { return BitConverter.ToUInt32(data,offset); }
    void Run() {
      bool attached=false,dumped=false,initialBreakpoint=true;
      var watch=Stopwatch.StartNew();long next=0;
      try {
        if(!DebugActiveProcess((uint)game.Id))throw new Win32Exception(Marshal.GetLastWin32Error());attached=true;
        if(!DebugSetProcessKillOnExit(false))throw new Win32Exception(Marshal.GetLastWin32Error());
        Write("debugger attached PID="+game.Id);ready.Set();
        while(!stop) {
          DebugEvent e;
          if(WaitForDebugEvent(out e,200)) {
            uint status=0x10002;bool exited=e.kind==5;
            try {
              if(e.kind==1) {
                uint code=U(e.data,0),address=U(e.data,12),first=U(e.data,80);
                // Only consume the debugger's initial attach breakpoint.
                if(code==0x80000003&&first!=0&&initialBreakpoint)initialBreakpoint=false;
                else {
                  status=0x80010001;
                  Write("exception code=0x"+code.ToString("X8")+" address=0x"+address.ToString("X8")+" thread="+e.tid+" firstChance="+first+" info0="+U(e.data,20)+" info1=0x"+U(e.data,24).ToString("X8"));
                  if(first==0&&!dumped) { dumped=true;Dump(); }
                }
              } else if(e.kind==3||e.kind==6) {
                Write("module event="+e.kind+" base=0x"+U(e.data,e.kind==3?12:4).ToString("X8"));
                IntPtr file=new IntPtr(unchecked((int)U(e.data,0)));if(file!=IntPtr.Zero&&file!=new IntPtr(-1))CloseHandle(file);
              } else if(exited)Write("exit=0x"+U(e.data,0).ToString("X8"));
            } catch(Exception ex) { Write("capture error="+ex.Message); }
            finally { if(!ContinueDebugEvent(e.pid,e.tid,status))Write("continue error="+Marshal.GetLastWin32Error()); }
            if(exited){attached=false;break;}
          } else {
            int error=Marshal.GetLastWin32Error();
            if(error!=121&&error!=0) { Write("debug wait error="+error);break; }
          }
          if(watch.ElapsedMilliseconds>=next) { Sample();next=watch.ElapsedMilliseconds+1000; }
        }
      } catch(Exception ex) { attachError=ex;Write("diagnostic failure="+ex); }
      finally {
        if(attached)Write("detach="+DebugActiveProcessStop((uint)game.Id));
        ready.Set();if(game!=null)game.Dispose();
      }
    }
    byte[] Read(int address,int size) {
      var data=new byte[size];UIntPtr read;
      if(!ReadProcessMemory(game.Handle,new IntPtr(address),data,(UIntPtr)size,out read)||read.ToUInt64()!=(ulong)size)throw new IOException("memory read failed");
      return data;
    }
    void Sample() {
      if(samples++>=100000)return;
      try {
        game.Refresh();string fields="NA,NA,NA,NA,NA,NA,NA,NA";
        if(supported) {
          var state=Read(0x71d1f0+0x80,0x2c);
          fields=BitConverter.ToUInt32(Read(0x70e8c4,4),0)+","+BitConverter.ToUInt32(Read(0x71ccfc,4),0)+","+U(state,0x14)+","+BitConverter.ToInt32(state,0x28)+","+BitConverter.ToSingle(state,0).ToString(System.Globalization.CultureInfo.InvariantCulture);
          // Raw fields returned by 0x5EFC6F / 0x5EFC97; not packet latency.
          var net=Read(0x71c768+0x84,0x18);fields+=","+U(net,0)+","+U(net,0x0c)+","+U(net,0x14);
        }
        File.AppendAllText(Path.Combine(DirectoryPath,"timing.csv"),DateTime.UtcNow.ToString("o")+","+fields+","+game.PrivateMemorySize64+","+((long)game.TotalProcessorTime.TotalMilliseconds)+"\r\n");
      }catch(Exception ex){if(samples<4)Write("sample unavailable="+ex.Message);}
    }
    void Dump() {
      using(var file=new FileStream(Path.Combine(DirectoryPath,"crash.dmp"),FileMode.Create,FileAccess.Write,FileShare.Read)) {
        bool ok=MiniDumpWriteDump(game.Handle,(uint)game.Id,file.SafeFileHandle.DangerousGetHandle(),0x1020,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
        Write("dump="+ok+" win32="+(ok?0:Marshal.GetLastWin32Error()));
      }
    }
    public void Dispose() {
      stop=true;
      if(worker!=null&&Thread.CurrentThread!=worker)worker.Join(3000);
      // The background thread owns the event until it exits; never dispose it early.
    }
  }
}
