using System;
using System.IO;
using System.Reflection;
class DisplayTests {
  static int Main(string[] args) {
    var assembly=Assembly.LoadFrom(args[0]);
    var type=assembly.GetType("ChangpogoLauncher.DisplayOptions");
    var method=type.GetMethod("Prepare",BindingFlags.NonPublic|BindingFlags.Static);
    var root=Path.Combine(Path.GetTempPath(),"Changpogo-display-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
    foreach(var name in new[]{"Changpogo.ini","dgVoodoo.conf","DDraw.dll"})File.Copy(Path.Combine(args[1],name),Path.Combine(root,name));
    string original=File.ReadAllText(Path.Combine(root,"dgVoodoo.conf"));
    foreach(bool wide in new[]{false,true}) foreach(bool borderless in new[]{false,true,false}) {
      method.Invoke(null,new object[]{Path.Combine(root,"Changpogo.exe"),borderless,wide});
      string wrapper=File.ReadAllText(Path.Combine(root,"dgVoodoo.conf"));
      if(!System.Text.RegularExpressions.Regex.IsMatch(wrapper,@"(?m)^FullScreenMode\s*=\s*false\s*$"))throw new Exception("Exclusive fullscreen not disabled");
      if(!System.Text.RegularExpressions.Regex.IsMatch(wrapper,@"(?m)^FullscreenAttributes\s*=\s*fake\s*$"))throw new Exception("Alt+Enter fallback missing");
      if(wrapper.Contains("borderless, fullscreensize")!=borderless)throw new Exception("Wrong display attributes");
      if(File.ReadAllText(Path.Combine(root,"dgVoodoo.conf.launcher-display.bak"))!=original)throw new Exception("Backup overwritten");
      string ini=File.ReadAllText(Path.Combine(root,"Changpogo.ini"));
      if(!System.Text.RegularExpressions.Regex.IsMatch(ini,@"(?m)^Fullscreen\s*=\s*1\s*$"))throw new Exception("Game must request fullscreen surfaces");
      if(!System.Text.RegularExpressions.Regex.IsMatch(ini,@"(?m)^Height\s*=\s*960\s*$"))throw new Exception("Unsupported native resolution");
      if(wrapper.Contains("h:1280, v:720")!=wide)throw new Exception("Wrong widescreen output");
      string before=wrapper;method.Invoke(null,new object[]{Path.Combine(root,"Changpogo.exe"),borderless,wide});
      if(File.ReadAllText(Path.Combine(root,"dgVoodoo.conf"))!=before)throw new Exception("Not idempotent");
    }
    Console.WriteLine("PASS window/borderless/window configuration, fake fullscreen, backup preservation, idempotence. Fixture: "+root);
    return 0;
  }
}
