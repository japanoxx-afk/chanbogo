using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
class CaptureIntegrationTests {
  [STAThread] static void Main(string[] args) {
    var assembly=Assembly.LoadFrom(args[0]);
    using(var resource=assembly.GetManifestResourceStream("MultiplayerCapture.exe")) {
      if(resource==null||resource.Length<1000||resource.ReadByte()!=77||resource.ReadByte()!=90)throw new Exception("Missing embedded helper");
    }
    var type=assembly.GetType("ChangpogoLauncher.MultiplayerCaptureForm");
    using(var form=(Form)Activator.CreateInstance(type,true)) {
      var flags=BindingFlags.NonPublic|BindingFlags.Instance;
      var role=(ComboBox)type.GetField("role",flags).GetValue(form);
      var code=(TextBox)type.GetField("code",flags).GetValue(form);
      var status=(Label)type.GetField("status",flags).GetValue(form);
      if(role.Items.Count!=2||code.Text!="mp-test-04")throw new Exception("Invalid defaults");
      code.Text="bad code";
      ((Task)type.GetMethod("RunCapture",flags).Invoke(form,null)).GetAwaiter().GetResult();
      if(!status.Text.StartsWith("테스트 코드는"))throw new Exception("Validation missing");
      if((bool)type.GetField("running",flags).GetValue(form))throw new Exception("Invalid input started capture");
      foreach(Control control in form.Controls)
        if(control.Right>form.ClientSize.Width||control.Bottom>form.ClientSize.Height)throw new Exception("Control outside client area");
    }
    Console.WriteLine("PASS embedded helper, role/code defaults, input rejection, control bounds");
  }
}
