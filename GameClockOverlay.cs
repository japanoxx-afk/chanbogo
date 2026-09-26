using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ChangpogoLauncher {
  // Verified engine getter 0x489A86 returns scheduler+0x78;
  // original 0x489E9B multiplies that tick count by 100 milliseconds.
  sealed class GameClockOverlay : Form {
    [StructLayout(LayoutKind.Sequential)] struct Rect {public int left,top,right,bottom;}
    [StructLayout(LayoutKind.Sequential)] struct Point {public int x,y;}
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window,ref Point point);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr handle,IntPtr address,byte[] bytes,UIntPtr size,out UIntPtr count);
    readonly Timer timer=new Timer { Interval=200 };
    readonly Label label=new Label { Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.White,BackColor=Color.Black };
    readonly int pid;IntPtr handle;
    internal GameClockOverlay(Process game) {
      pid=game.Id;handle=OpenProcess(0x1010,false,pid);
      if(handle==IntPtr.Zero)throw new InvalidOperationException("게임 시간 읽기 권한이 없습니다.");
      FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;Opacity=0.85;
      ClientSize=new Size(180,32);Font=new Font("맑은 고딕",11F,FontStyle.Bold);Controls.Add(label);
      timer.Tick+=(s,e)=>RefreshClock();timer.Start();
    }
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x20|0x80000;return p;}}
    protected override void WndProc(ref Message m) {if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}base.WndProc(ref m);}
    internal static string FormatTicks(uint ticks) {
      ulong seconds=ticks/10;return string.Format("게임 {0:00}:{1:00}:{2:00}",seconds/3600,seconds/60%60,seconds%60);
    }
    bool Read(int address,out uint value) {
      var bytes=new byte[4];UIntPtr count;value=0;
      if(!ReadProcessMemory(handle,new IntPtr(address),bytes,(UIntPtr)4,out count)||count.ToUInt64()!=4)return false;
      value=BitConverter.ToUInt32(bytes,0);return true;
    }
    void RefreshClock() {
      uint owner,active,ticks;IntPtr window=GetForegroundWindow();GetWindowThreadProcessId(window,out owner);
      if(owner!=(uint)pid||!Read(0x71ccfc,out active)||active!=1||!Read(0x71d268,out ticks)){Hide();return;}
      Rect rect;if(!GetClientRect(window,out rect)||rect.right<200||rect.bottom<100){Hide();return;}
      var point=new Point {x=rect.right-Width-12,y=40};if(!ClientToScreen(window,ref point)){Hide();return;}
      Location=new System.Drawing.Point(point.x,point.y);label.Text=FormatTicks(ticks);if(!Visible)Show();
    }
    protected override void Dispose(bool disposing) {
      if(disposing)timer.Dispose();if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}base.Dispose(disposing);
    }
  }
}
