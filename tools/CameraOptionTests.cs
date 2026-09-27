using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
class CameraOptionTests {
  [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr p,IntPtr address,byte[] bytes,UIntPtr size,out UIntPtr read);
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
  [STAThread] static void Main(string[] args) {
    var a=Assembly.LoadFrom(Path.GetFullPath(args[0]));var flags=BindingFlags.NonPublic|BindingFlags.Static;
    var patch=a.GetType("ChangpogoLauncher.SinglePlayerPatch");var distance=patch.GetMethod("CameraDistance",flags);
    for(int i=100;i<=150;i++)Check(Math.Abs((float)distance.Invoke(null,new object[]{i})-36f*i/100f)<0.00001,"Conversion");
    foreach(int i in new[]{-1,0,99,151,int.MaxValue}) {
      bool rejected=false;try{distance.Invoke(null,new object[]{i});}catch(TargetInvocationException e){rejected=e.InnerException is ArgumentOutOfRangeException;}Check(rejected,"Range guard");
    }
    var formType=a.GetType("ChangpogoLauncher.LauncherForm");
    using(var form=(Form)Activator.CreateInstance(formType,true)) {
      var n=(NumericUpDown)formType.GetField("cameraPercent",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
      Check(n.Minimum==100&&n.Maximum==150&&n.Increment==5,"UI range");
      foreach(Control c in form.Controls)Check(form.ClientRectangle.Contains(c.Bounds),"Control outside form: "+c.Text);
      using(var bitmap=new Bitmap(form.ClientSize.Width,form.ClientSize.Height)) {
        using(var g=Graphics.FromImage(bitmap))g.Clear(form.BackColor);
        foreach(Control c in form.Controls){var handle=c.Handle;c.DrawToBitmap(bitmap,c.Bounds);}
        bitmap.Save(args[2]);
      }
    }
    // Install into our own suspended child, inspect, then deliberately abort
    // before ResumeThread. No live game is attached, resumed or terminated.
    foreach(int percent in new[]{100,125,150}) {
      bool inspected=false;int pid=0;
      Action<Process> observe=p=>{
        pid=p.Id;var bytes=new byte[4];UIntPtr read;
        Check(ReadProcessMemory(p.Handle,new IntPtr(0x69d7bc),bytes,(UIntPtr)4,out read)&&read.ToUInt64()==4,"Camera read");
        Check(BitConverter.ToSingle(bytes,0)==36f*percent/100f,"Installed camera distance");
        inspected=true;throw new OperationCanceledException("Probe complete; do not resume game");
      };
      try {patch.GetMethod("StartConfigured",flags).Invoke(null,new object[]{args[1],true,false,percent,observe});throw new Exception("Unexpected resume");}
      catch(TargetInvocationException e){Check(e.InnerException is OperationCanceledException,"Unexpected startup failure: "+e.InnerException);}
      Check(inspected,"Probe missing");
      try {using(var p=Process.GetProcessById(pid))Check(p.WaitForExit(3000),"Child cleanup");}catch(ArgumentException){}
    }
    Console.WriteLine("PASS 51 numeric values, invalid bounds, UI layout, suspended-child memory 100/125/150%, child cleanup");
  }
}
