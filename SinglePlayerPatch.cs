using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ChangpogoLauncher {
  // Starts the original executable suspended, installs a private scheduler clone,
  // then resumes it. No on-disk game changes or attachment to existing processes.
  static class SinglePlayerPatch {
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    struct StartupInfo {
      public int cb; public string reserved,desktop,title;
      public int x,y,xSize,ySize,xChars,yChars,fill,flags;
      public short show,reserved2; public IntPtr reservedPtr,input,output,error;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct ProcessInfo { public IntPtr process,thread; public uint pid,tid; }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern bool CreateProcess(string app,StringBuilder command,IntPtr pa,IntPtr ta,bool inherit,uint flags,IntPtr env,string cwd,ref StartupInfo si,out ProcessInfo pi);
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr VirtualAllocEx(IntPtr p,IntPtr address,UIntPtr size,uint type,uint protect);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool VirtualProtectEx(IntPtr p,IntPtr address,UIntPtr size,uint protect,out uint old);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool WriteProcessMemory(IntPtr p,IntPtr address,byte[] bytes,UIntPtr size,out UIntPtr written);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(IntPtr p,IntPtr address,byte[] bytes,UIntPtr size,out UIntPtr read);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool FlushInstructionCache(IntPtr p,IntPtr address,UIntPtr size);
    [DllImport("kernel32.dll",SetLastError=true)] static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr p,uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    static void Require(bool ok) { if(!ok)throw new Win32Exception(Marshal.GetLastWin32Error()); }
    static byte[] Decode(string value) {
      var data=new byte[value.Length/2];for(int i=0;i<data.Length;i++)data[i]=Convert.ToByte(value.Substring(i*2,2),16);return data;
    }
    internal static Process Start(string path) {
      return StartObserved(path,true,null);
    }
    internal static Process StartObserved(string path,bool latency,Action<Process> observe) {
      string hash,hex,relocations;
      using(var reader=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("latency.manifest"))) {
        hash=reader.ReadLine();hex=reader.ReadLine();relocations=reader.ReadLine();
      }
      // Keep the source locked against writes/replacement through process creation.
      using(var source=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
        using(var sha=SHA256.Create())
          if(latency&&!string.Equals(BitConverter.ToString(sha.ComputeHash(source)).Replace("-",""),hash,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("이 게임 버전은 명령 지연 패치 지원 대상이 아닙니다. 명령 지연 옵션을 끄면 원본으로 실행할 수 있습니다.");
        var si=new StartupInfo();si.cb=Marshal.SizeOf(typeof(StartupInfo));ProcessInfo pi;
        Require(CreateProcess(path,new StringBuilder(LauncherForm.Quote(path)),IntPtr.Zero,IntPtr.Zero,false,4,IntPtr.Zero,Path.GetDirectoryName(path),ref si,out pi));
        bool resumed=false;Process managed=null;
        try {
          UIntPtr count;
          if(latency) {
          var original=new byte[6];
          Require(ReadProcessMemory(pi.process,new IntPtr(0x488bb3),original,(UIntPtr)6,out count));
          if(count.ToUInt64()!=6 || BitConverter.ToString(original)!="55-8B-EC-83-EC-40")throw new InvalidDataException("실행 중 게임 명령어 검증 실패");
          var code=Decode(hex);var region=VirtualAllocEx(pi.process,IntPtr.Zero,(UIntPtr)code.Length,0x3000,4);Require(region!=IntPtr.Zero);
          long address=unchecked((uint)region.ToInt32());
          foreach(var item in relocations.Split(',')) {
            int offset=int.Parse(item);int relative=unchecked((int)(BitConverter.ToInt32(code,offset)+0x10000000L-address));
            Buffer.BlockCopy(BitConverter.GetBytes(relative),0,code,offset,4);
          }
          Require(WriteProcessMemory(pi.process,region,code,(UIntPtr)code.Length,out count));Require(count.ToUInt64()==(ulong)code.Length);
          uint old;Require(VirtualProtectEx(pi.process,region,(UIntPtr)code.Length,0x20,out old));
          var hook=new byte[]{0xe9,0,0,0,0,0x90};Buffer.BlockCopy(BitConverter.GetBytes(unchecked((int)(address-0x488bb8))),0,hook,1,4);
          var entry=new IntPtr(0x488bb3);Require(VirtualProtectEx(pi.process,entry,(UIntPtr)6,0x40,out old));
          Require(WriteProcessMemory(pi.process,entry,hook,(UIntPtr)6,out count));Require(count.ToUInt64()==6);
          uint unused;Require(VirtualProtectEx(pi.process,entry,(UIntPtr)6,old,out unused));
          Require(FlushInstructionCache(pi.process,IntPtr.Zero,UIntPtr.Zero));
          }
          managed=Process.GetProcessById((int)pi.pid);
          // Retain a process handle before it can exit, so ExitCode stays available.
          var retainedHandle=managed.Handle;
          if(observe!=null)observe(managed);
          Require(ResumeThread(pi.thread)!=uint.MaxValue);resumed=true;return managed;
        } finally {
          if(!resumed) { TerminateProcess(pi.process,1);if(managed!=null)managed.Dispose(); }
          CloseHandle(pi.thread);CloseHandle(pi.process);
        }
      }
    }
  }
}
