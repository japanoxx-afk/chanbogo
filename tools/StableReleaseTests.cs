using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
class StableReleaseTests {
  [STAThread] static void Main(string[] args) {
    var a=Assembly.LoadFrom(args[0]);
    if(a.GetName().Version.ToString()!="1.6.3.0")throw new Exception("Unexpected stable version");
    using(var resource=a.GetManifestResourceStream("multiplayer-75ms.manifest"))
    using(var reader=new StreamReader(resource))
      if(reader.ReadToEnd().Trim()!=File.ReadAllText(args[1]).Trim())throw new Exception("Gameplay manifest changed");
    var t=a.GetType("ChangpogoLauncher.LauncherForm");
    using(var form=(Form)Activator.CreateInstance(t,true)) {
      if(!form.Text.Contains("정식 버전")||form.Text.Contains("시험판"))throw new Exception("Wrong release title");
      var f=BindingFlags.Instance|BindingFlags.NonPublic;
      var single=(CheckBox)t.GetField("commandLatency",f).GetValue(form);
      if(!single.Checked||single.Enabled)throw new Exception("Verified patch must remain enabled");
    }
    Console.WriteLine("PASS stable version/title, exact verified 75ms resource and enabled patch");
  }
}
