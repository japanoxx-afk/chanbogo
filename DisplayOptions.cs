using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ChangpogoLauncher {
  static class DisplayOptions {
    internal static string Set(string text,string section,string key,string value) {
      var block=new Regex(@"(?ms)^\["+Regex.Escape(section)+@"\][^\r\n]*\r?\n.*?(?=^\[|\z)");
      if(!block.IsMatch(text))return text+"\r\n["+section+"]\r\n"+key+" = "+value+"\r\n";
      return block.Replace(text,m=> {
        var setting=new Regex(@"(?m)^([ \t]*"+Regex.Escape(key)+@"[ \t]*=[ \t]*)[^\r\n]*");
        return setting.IsMatch(m.Value)?setting.Replace(m.Value,x=>x.Groups[1].Value+value):m.Value.TrimEnd('\r','\n')+"\r\n"+key+" = "+value+"\r\n";
      });
    }
    static void Save(string file,string text) {
      string backup=file+".launcher-display.bak";
      if(!File.Exists(backup))File.Copy(file,backup,false);
      string temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
      try { File.WriteAllText(temp,text,Encoding.GetEncoding(28591));File.Replace(temp,file,null); }
      finally { if(File.Exists(temp))File.Delete(temp); }
    }
    internal static void Prepare(string game,bool borderless,bool widescreen) {
      PrepareCompatible(game,borderless,widescreen,false);
    }
    internal static void PrepareCompatible(string game,bool borderless,bool widescreen,bool compatibility) {
      string directory=Path.GetDirectoryName(game),ini=Path.Combine(directory,"Changpogo.ini"),wrapper=Path.Combine(directory,"dgVoodoo.conf");
      if(!File.Exists(ini)||!File.Exists(wrapper)||!File.Exists(Path.Combine(directory,"DDraw.dll")))
        throw new FileNotFoundException("화면 설정에 필요한 Changpogo.ini, dgVoodoo.conf, DDraw.dll을 게임 폴더에서 찾을 수 없습니다.");
      var encoding=Encoding.GetEncoding(28591);
      if(compatibility) {
        // Restore the pre-launcher profile as a pair. Do not partially restore it.
        bool iniBackup=File.Exists(ini+".launcher-display.bak"),wrapperBackup=File.Exists(wrapper+".launcher-display.bak");
        if(iniBackup!=wrapperBackup)throw new IOException("화면 설정 백업이 한 개만 있습니다. 두 설정의 원본 백업을 확인하세요.");
        if(iniBackup) {
          string savedIni=File.ReadAllText(ini+".launcher-display.bak",encoding);
          string savedWrapper=Set(File.ReadAllText(wrapper+".launcher-display.bak",encoding),"DirectX","DisableAltEnterToToggleScreenMode","true");
          foreach(string file in new[]{ini,wrapper})
            if(!File.Exists(file+".before-safe-profile.bak"))File.Copy(file,file+".before-safe-profile.bak");
          if(File.ReadAllText(ini,encoding)!=savedIni)Save(ini,savedIni);
          if(File.ReadAllText(wrapper,encoding)!=savedWrapper)Save(wrapper,savedWrapper);
        }
        else {
          string current=File.ReadAllText(wrapper,encoding);
          string protectedConfig=Set(current,"DirectX","DisableAltEnterToToggleScreenMode","true");
          if(current!=protectedConfig) {
            // Save both originals before making the first change.
            if(!File.Exists(ini+".launcher-display.bak"))File.Copy(ini,ini+".launcher-display.bak");
            Save(wrapper,protectedConfig);
          }
        }
        return;
      }
      string config=File.ReadAllText(wrapper,encoding);
      config=Set(config,"General","FullScreenMode","false");
      // Launcher owns clipping so it can release immediately on focus loss/F8.
      config=Set(config,"General","CaptureMouse","false");
      config=Set(config,"General","CenterAppWindow","true");
      config=Set(config,"General","ScalingMode",widescreen?"stretched":"stretched_ar");
      config=Set(config,"DirectX","AppControlledScreenMode","false");
      config=Set(config,"DirectX","DisableAltEnterToToggleScreenMode","true");
      config=Set(config,"DirectX","Resolution",widescreen?"h:1280, v:720":"unforced");
      // Conservative A/B profile; this is not a confirmed minimap fix.
      // Set both branches explicitly so turning the option off restores the launcher profile.
      config=Set(config,"DirectX","FastVideoMemoryAccess",compatibility?"false":"true");
      config=Set(config,"DirectXExt","RTTexturesForceScaleAndMSAA",compatibility?"false":"true");
      if(compatibility)config=Set(config,"DirectX","Resolution","unforced");
      config=Set(config,"GeneralExt","WindowedAttributes",borderless?"borderless, fullscreensize":"");
      config=Set(config,"GeneralExt","FullscreenAttributes","fake");
      Save(wrapper,config);
      // dgVoodoo's forced window mode requires a fullscreen DirectDraw surface.
      // A native windowed primary surface plus fullscreensize produces overlapping blits.
      string video=Set(File.ReadAllText(ini,encoding),"VideoState","Fullscreen","1");
      video=Set(video,"VideoState","Width","1280");
      video=Set(video,"VideoState","Height","960");
      video=Set(video,"VideoState","Depth","32");
      Save(ini,video);
    }
  }

  sealed class MouseCapture : IDisposable {
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int left,top,right,bottom; }
    [StructLayout(LayoutKind.Sequential)] struct Point { public int x,y; }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window,ref Point point);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern bool ClipCursor(ref Rect rect);
    [DllImport("user32.dll",EntryPoint="ClipCursor")] static extern bool ReleaseCursor(IntPtr rect);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    readonly Timer timer=new Timer { Interval=30 };
    readonly Process game;
    readonly Action<string> log;
    bool clipped,enabled,lastF8,lastF9,disposed;
    public static void InitializeDpi() { SetProcessDPIAware(); }
    public MouseCapture(Process process,bool capture,Action<string> logger) {
      game=process;enabled=capture;log=logger;timer.Tick+=(s,e)=>Tick();timer.Start();
    }
    void Release() { if(clipped) { ReleaseCursor(IntPtr.Zero);clipped=false; } }
    void Tick() {
      if(disposed)return;
      try {
        if(game.HasExited) { Dispose();return; }
        IntPtr window=GetForegroundWindow();uint pid;GetWindowThreadProcessId(window,out pid);
        bool active=pid==(uint)game.Id&&!IsIconic(window);
        bool f8=(GetAsyncKeyState(0x77)&0x8000)!=0;
        if(active&&f8&&!lastF8) { enabled=!enabled;log("마우스 가두기 "+(enabled?"켜짐":"해제됨")+" (F8)"); }
        lastF8=f8;
        bool f9=(GetAsyncKeyState(0x78)&0x8000)!=0;
        if(active&&f9&&!lastF9)log("USER_MARK F9: 미니맵/명령 지연 등 문제 발생 시점");
        lastF9=f9;
        if(!active||!enabled) { Release();return; }
        Rect client;if(!GetClientRect(window,out client)||client.right<=0||client.bottom<=0) { Release();return; }
        Point first=new Point(),last=new Point { x=client.right,y=client.bottom };
        if(!ClientToScreen(window,ref first)||!ClientToScreen(window,ref last)) { Release();return; }
        var rect=new Rect { left=first.x,top=first.y,right=last.x,bottom=last.y };
        clipped=ClipCursor(ref rect)||clipped;
      } catch(InvalidOperationException) { Dispose(); }
    }
    public void Dispose() { if(disposed)return;disposed=true;timer.Stop();timer.Dispose();Release(); }
  }
}
