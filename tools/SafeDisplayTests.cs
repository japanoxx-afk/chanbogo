using System;
using System.IO;
using System.Reflection;
class SafeDisplayTests {
  static int Main(string[] args) {
    var method=Assembly.LoadFrom(args[0]).GetType("ChangpogoLauncher.DisplayOptions").GetMethod("PrepareCompatible",BindingFlags.NonPublic|BindingFlags.Static);
    string root=Path.Combine(Path.GetTempPath(),"Changpogo-safe-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
    foreach(string name in new[]{"Changpogo.ini","dgVoodoo.conf","DDraw.dll"})File.Copy(Path.Combine(args[1],name),Path.Combine(root,name));
    string ini=Path.Combine(root,"Changpogo.ini"),conf=Path.Combine(root,"dgVoodoo.conf"),game=Path.Combine(root,"Changpogo.exe");
    string originalIni=Convert.ToBase64String(File.ReadAllBytes(ini)),originalConf=Convert.ToBase64String(File.ReadAllBytes(conf));
    Action<bool,bool,bool> apply=(borderless,wide,safe)=>method.Invoke(null,new object[]{game,borderless,wide,safe});
    var setter=method.DeclaringType.GetMethod("Set",BindingFlags.NonPublic|BindingFlags.Static);
    string expected=(string)setter.Invoke(null,new object[]{File.ReadAllText(conf,System.Text.Encoding.GetEncoding(28591)),"DirectX","DisableAltEnterToToggleScreenMode","true"});
    Action check=()=>{if(Convert.ToBase64String(File.ReadAllBytes(ini))!=originalIni||File.ReadAllText(conf,System.Text.Encoding.GetEncoding(28591))!=expected)throw new Exception("Safe mode changed fields beyond Alt+Enter protection");};
    apply(true,true,true);check();
    foreach(bool borderless in new[]{false,true})foreach(bool wide in new[]{false,true}) {
      apply(borderless,wide,false);
      apply(borderless,wide,true);check();
      apply(borderless,wide,true);check();
      if(Convert.ToBase64String(File.ReadAllBytes(ini+".launcher-display.bak"))!=originalIni)throw new Exception("INI backup overwritten");
      if(Convert.ToBase64String(File.ReadAllBytes(conf+".launcher-display.bak"))!=originalConf)throw new Exception("Wrapper backup overwritten");
    }
    if(!File.Exists(ini+".before-safe-profile.bak")||!File.Exists(conf+".before-safe-profile.bak"))throw new Exception("Recovery copies missing");
    File.Move(conf+".launcher-display.bak",conf+".test-held");
    bool rejected=false;try {apply(false,false,true);}catch(TargetInvocationException e){rejected=e.InnerException is IOException;}
    if(!rejected)throw new Exception("Partial backup not rejected");check();
    Console.WriteLine("PASS exact restoration, no-backup preservation, display selections, idempotence, recovery copies, partial-backup rejection");return 0;
  }
}
