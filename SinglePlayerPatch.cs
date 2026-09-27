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
      return StartObserved(path,latency,false,observe);
    }
    internal static Process StartObserved(string path,bool latency,bool multiplayer,Action<Process> observe) {
      return StartConfigured(path,latency,multiplayer,100,observe);
    }
    internal static float CameraDistance(int percent) {
      if(percent<100||percent>150)throw new ArgumentOutOfRangeException("percent","카메라 거리는 100~150% 범위여야 합니다.");
      return 36f*percent/100f;
    }
    internal static Process StartConfigured(string path,bool latency,bool multiplayer,int cameraPercent,Action<Process> observe) {
      float cameraDistance=CameraDistance(cameraPercent);
      string hash,hex,relocations;
#if MULTIPLAYER_75MS
      const string resourceName="multiplayer-75ms.manifest";
#elif MULTIPLAYER_EXPERIMENT
      const string resourceName="multiplayer-experiment.manifest";
#else
      const string resourceName="latency.manifest";
#endif
      using(var reader=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))) {
        hash=reader.ReadLine();hex=reader.ReadLine();relocations=reader.ReadLine();
      }
      // Keep the source locked against writes/replacement through process creation.
      using(var source=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
        using(var sha=SHA256.Create())
          if(!string.Equals(BitConverter.ToString(sha.ComputeHash(source)).Replace("-",""),hash,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("이 게임 버전은 명령 지연·농부 패치 지원 대상이 아닙니다.");
        var si=new StartupInfo();si.cb=Marshal.SizeOf(typeof(StartupInfo));ProcessInfo pi;
        Require(CreateProcess(path,new StringBuilder(LauncherForm.Quote(path)),IntPtr.Zero,IntPtr.Zero,false,4,IntPtr.Zero,Path.GetDirectoryName(path),ref si,out pi));
        bool resumed=false;Process managed=null;
        try {
          UIntPtr count;
          if(cameraPercent!=100) {
            // Shared default for constructor, map load and camera reset. No
            // +0x18 is a world-space view span, NOT a fixed angle. Projection
            // calculates 2*atan2(span/2,distance). Scale BOTH to retain the
            // viewing angle while increasing the ground area shown.
            var location=new IntPtr(0x69d7bc);var original=new byte[8];
            Require(ReadProcessMemory(pi.process,location,original,(UIntPtr)8,out count));
            if(count.ToUInt64()!=8||BitConverter.ToSingle(original,0)!=36f||BitConverter.ToSingle(original,4)!=26f)throw new InvalidDataException("카메라 거리·시야 기본값 검증 실패");
            var values=new byte[8];Buffer.BlockCopy(BitConverter.GetBytes(cameraDistance),0,values,0,4);
            Buffer.BlockCopy(BitConverter.GetBytes(26f*cameraPercent/100f),0,values,4,4);
            uint old,unused;Require(VirtualProtectEx(pi.process,location,(UIntPtr)8,4,out old));
            try {
              Require(WriteProcessMemory(pi.process,location,values,(UIntPtr)8,out count));Require(count.ToUInt64()==8);
              var verified=new byte[8];Require(ReadProcessMemory(pi.process,location,verified,(UIntPtr)8,out count));
              if(count.ToUInt64()!=8||BitConverter.ToString(verified)!=BitConverter.ToString(values))throw new InvalidDataException("카메라 거리·시야 적용 확인 실패");
            } finally {Require(VirtualProtectEx(pi.process,location,(UIntPtr)8,old,out unused));}
          }
          if(multiplayer) {
            // Only the non-single-player initializer: push 3 -> push 2.
            // Do not change turn cadence, readiness checks, packets or simulation speed.
            var entry=new IntPtr(0x488ae1);var original=new byte[6];
            Require(ReadProcessMemory(pi.process,entry,original,(UIntPtr)6,out count));
            if(count.ToUInt64()!=6||BitConverter.ToString(original)!="6A-03-EB-02-6A-01")throw new InvalidDataException("멀티 명령 대기 초기화 코드 검증 실패");
            var operand=new IntPtr(0x488ae2);uint old,unused;
            Require(VirtualProtectEx(pi.process,operand,(UIntPtr)1,0x40,out old));
            Require(WriteProcessMemory(pi.process,operand,new byte[]{2},(UIntPtr)1,out count));Require(count.ToUInt64()==1);
            Require(VirtualProtectEx(pi.process,operand,(UIntPtr)1,old,out unused));
            var verified=new byte[1];Require(ReadProcessMemory(pi.process,operand,verified,(UIntPtr)1,out count));
            if(count.ToUInt64()!=1||verified[0]!=2)throw new InvalidDataException("멀티 명령 대기 패치 읽기 검증 실패");
            Require(FlushInstructionCache(pi.process,operand,(UIntPtr)1));
          }
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
          // Training duration is returned as 16.16 fixed point. Halve only the
          // three playable farmer IDs, AFTER normal upgrades/minimum calculation.
          // Use the argument, not EBX: an early fallback path leaves EBX unset.
          {
            var entry=new IntPtr(0x416237);var expected=Decode("5f5e5bc9c20400");var actual=new byte[7];
            Require(ReadProcessMemory(pi.process,entry,actual,(UIntPtr)7,out count));
            if(count.ToUInt64()!=7||BitConverter.ToString(actual)!=BitConverter.ToString(expected))throw new InvalidDataException("농부 생산시간 코드 검증 실패");
            var code=Decode("837d0803740c837d08197406837d08267502d1e85f5e5bc9c20400");
            var region=VirtualAllocEx(pi.process,IntPtr.Zero,(UIntPtr)code.Length,0x3000,4);Require(region!=IntPtr.Zero);
            Require(WriteProcessMemory(pi.process,region,code,(UIntPtr)code.Length,out count));Require(count.ToUInt64()==(ulong)code.Length);
            uint old,unused;Require(VirtualProtectEx(pi.process,region,(UIntPtr)code.Length,0x20,out old));
            var jump=Decode("e9000000009090");
            Buffer.BlockCopy(BitConverter.GetBytes(unchecked(region.ToInt32()-0x41623c)),0,jump,1,4);
            Require(VirtualProtectEx(pi.process,entry,(UIntPtr)7,0x40,out old));
            Require(WriteProcessMemory(pi.process,entry,jump,(UIntPtr)7,out count));Require(count.ToUInt64()==7);
            Require(VirtualProtectEx(pi.process,entry,(UIntPtr)7,old,out unused));
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
