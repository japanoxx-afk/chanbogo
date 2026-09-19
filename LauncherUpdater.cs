using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;

static class LauncherUpdater {
  static readonly string[] Files={"ChangpogoLauncher.exe","LauncherUpdater.exe","README.md"};
  [STAThread] static int Main(string[] args) {
    string target=null,stage=null;Process parent=null;bool exited=false;
    var replaced=new List<string>();var backups=new Dictionary<string,string>();
    try {
      if(args.Length<4)throw new ArgumentException("업데이트 인수 오류");
      var zip=Path.GetFullPath(args[0]);target=Path.GetFullPath(args[2]);var ready=Path.GetFullPath(args[3]);
      parent=Process.GetProcessById(int.Parse(args[1]));stage=Path.Combine(Path.GetTempPath(),"Changpogo-stage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
      using(var archive=ZipFile.OpenRead(zip))foreach(var name in Files) {
        var entry=archive.GetEntry(name);if(entry==null||entry.Length==0)throw new InvalidDataException("업데이트 필수 파일 누락: "+name);
        var staged=Path.Combine(stage,name);entry.ExtractToFile(staged);
        if(name.EndsWith(".exe"))using(var stream=File.OpenRead(staged))if(stream.ReadByte()!=77||stream.ReadByte()!=90)throw new InvalidDataException("실행 파일 형식 오류: "+name);
      }
      var probe=Path.Combine(target,".update-probe-"+Guid.NewGuid().ToString("N"));using(var file=new FileStream(probe,FileMode.CreateNew,FileAccess.Write,FileShare.None,1,FileOptions.DeleteOnClose)) { }
      foreach(var name in Files) { var destination=Path.Combine(target,name);if(!File.Exists(destination))continue;var backup=Path.Combine(stage,name+".bak");File.Copy(destination,backup);backups[name]=backup; }
      File.WriteAllText(ready,"READY");if(!parent.WaitForExit(30000))throw new IOException("기존 런처가 종료되지 않아 업데이트를 취소했습니다.");exited=true;
      foreach(var name in Files) { replaced.Add(name);File.Copy(Path.Combine(stage,name),Path.Combine(target,name),true); }
      Process.Start(new ProcessStartInfo { FileName=Path.Combine(target,"ChangpogoLauncher.exe"),WorkingDirectory=target,UseShellExecute=true });
      File.AppendAllText(Path.Combine(target,"update.log"),DateTime.Now.ToString("s")+" 업데이트 성공"+Environment.NewLine);return 0;
    } catch(Exception ex) {
      var detail=ex.ToString();for(int i=replaced.Count-1;i>=0;i--)try { string backup,name=replaced[i];if(backups.TryGetValue(name,out backup))File.Copy(backup,Path.Combine(target,name),true);else File.Delete(Path.Combine(target,name)); } catch(Exception rollback) { detail+="\n복구 실패: "+rollback.Message; }
      try { File.AppendAllText(Path.Combine(target??Path.GetTempPath(),"update.log"),DateTime.Now.ToString("s")+" "+detail+Environment.NewLine); } catch { }
      if(exited)try { Process.Start(new ProcessStartInfo { FileName=Path.Combine(target,"ChangpogoLauncher.exe"),WorkingDirectory=target,UseShellExecute=true }); } catch { }
      MessageBox.Show("업데이트 실패. 기존 파일 복구를 시도했습니다.\n"+ex.Message+"\n자세한 내용: update.log","런처 업데이트",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;
    } finally { if(parent!=null)parent.Dispose(); }
  }
}
