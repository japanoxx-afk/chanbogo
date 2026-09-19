using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: AssemblyTitle("Changpogo Launcher")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace ChangpogoLauncher {
  static class Program {
    [STAThread] static void Main() {
      ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.Run(new LauncherForm());
    }
  }

  sealed class LauncherForm : Form {
    const string VersionText="1.0.0";
    const string DefaultGame=@"C:\Users\seo\Downloads\DGGL\Games\Changpogo_Win_260708\Changpogo.exe";
    readonly TextBox gamePath=new TextBox();
    readonly TextBox log=new TextBox();
    readonly CheckBox lowLatency=new CheckBox { Text="저지연 실행 (권장)",AutoSize=true };
    readonly Button play=new Button();
    readonly Button update=new Button();

    public LauncherForm() {
      Text="해상왕 장보고 런처";ClientSize=new Size(720,430);MinimumSize=new Size(740,470);
      Font=new Font("맑은 고딕",10F);StartPosition=FormStartPosition.CenterScreen;
      var title=new Label { Text="해상왕 장보고",Font=new Font("맑은 고딕",22F,FontStyle.Bold),AutoSize=true,Location=new Point(22,18) };
      var version=new Label { Text="Launcher v"+VersionText,AutoSize=true,ForeColor=Color.SteelBlue,Location=new Point(250,39) };
      var subtitle=new Label { Text="원본 게임을 보존하는 저지연 실행 및 자동 업데이트",AutoSize=true,ForeColor=Color.DimGray,Location=new Point(25,65) };
      var pathLabel=new Label { Text="게임 실행 파일",AutoSize=true,Location=new Point(20,103) };
      gamePath.Text=LoadSetting("game-path.txt",DefaultGame);gamePath.Location=new Point(20,128);gamePath.Width=585;
      var browse=new Button { Text="찾기",Location=new Point(615,125),Size=new Size(80,31) };browse.Click+=Browse;
      lowLatency.Checked=LoadSetting("low-latency.txt","true")=="true";lowLatency.Location=new Point(20,177);
      var lowHint=new Label { Text="1ms 타이머 · 높은 프로세스 우선순위 · 우선순위 부스트",AutoSize=true,ForeColor=Color.DimGray,Location=new Point(190,179) };
      play.Text="게임 실행";play.Font=new Font(Font,FontStyle.Bold);play.Location=new Point(20,215);play.Size=new Size(180,44);
      update.Text="런처 업데이트";update.Location=new Point(215,215);update.Size=new Size(180,44);
      play.Click+=StartGame;update.Click+=async (s,e)=>await CheckUpdate();
      log.Location=new Point(20,280);log.Size=new Size(675,125);log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;
      log.BackColor=Color.FromArgb(25,25,28);log.ForeColor=Color.Gainsboro;log.Font=new Font("Consolas",9F);
      Controls.AddRange(new Control[]{title,version,subtitle,pathLabel,gamePath,browse,lowLatency,lowHint,play,update,log});
      WriteLog("런처 v"+VersionText+" 준비됨. 원본 게임 파일은 변경하지 않습니다.");
    }

    string LoadSetting(string name,string fallback) { try { var path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name);return File.Exists(path)?File.ReadAllText(path).Trim():fallback; } catch { return fallback; } }
    void SaveSetting(string name,string value) { try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name),value); } catch { } }
    void Browse(object sender,EventArgs e) {
      using(var dialog=new OpenFileDialog { Filter="Changpogo.exe|Changpogo.exe|실행 파일|*.exe",FileName=gamePath.Text })
        if(dialog.ShowDialog(this)==DialogResult.OK) { gamePath.Text=dialog.FileName;SaveSetting("game-path.txt",dialog.FileName); }
    }
    async void StartGame(object sender,EventArgs e) {
      play.Enabled=false;LowLatencySession session=null;Process process=null;
      try {
        var source=Path.GetFullPath(gamePath.Text.Trim());
        if(!File.Exists(source))throw new FileNotFoundException("Changpogo.exe를 찾을 수 없습니다.",source);
        if(!string.Equals(Path.GetFileName(source),"Changpogo.exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Changpogo.exe를 선택하세요.");
        SaveSetting("game-path.txt",source);SaveSetting("low-latency.txt",lowLatency.Checked?"true":"false");
        process=Process.Start(new ProcessStartInfo { FileName=source,WorkingDirectory=Path.GetDirectoryName(source),UseShellExecute=true });
        if(process==null)throw new InvalidOperationException("게임 프로세스를 시작하지 못했습니다.");
        session=new LowLatencySession();if(lowLatency.Checked)session.Begin(process);
        WriteLog("게임 시작 PID="+process.Id+" / 저지연 실행="+lowLatency.Checked);
        await Task.Run(()=>process.WaitForExit());WriteLog("게임 종료 / 코드 0x"+unchecked((uint)process.ExitCode).ToString("X8"));
      } catch(Exception ex) { WriteLog("실행 실패: "+ex.Message);MessageBox.Show(this,ex.Message,"게임 실행",MessageBoxButtons.OK,MessageBoxIcon.Error); }
      finally { if(session!=null)session.Dispose();if(process!=null)process.Dispose();play.Enabled=true; }
    }

    async Task CheckUpdate() {
      update.Enabled=false;
      try {
        WriteLog("업데이트 확인 중... 현재 v"+VersionText);
        using(var http=new HttpClient()) {
          http.Timeout=TimeSpan.FromSeconds(20);http.DefaultRequestHeaders.UserAgent.ParseAdd("ChangpogoLauncher/"+VersionText);
          var info=await FindUpdate(http);Version remote,current;
          if(!Version.TryParse(info.Version.TrimStart('v','V'),out remote))throw new InvalidDataException("업데이트 버전 형식 오류: "+info.Version);
          current=new Version(VersionText);
          if(remote<=current) { MessageBox.Show(this,"현재 최신 버전입니다.\n\n설치됨: v"+current+"\n최신: v"+remote,"런처 업데이트");return; }
          var zip=Path.Combine(Path.GetTempPath(),"ChangpogoLauncher-update-"+Guid.NewGuid().ToString("N")+".zip");
          WriteLog("v"+remote+" 다운로드 중...");File.WriteAllBytes(zip,await http.GetByteArrayAsync(info.Url));
          var handoff=Path.Combine(Path.GetTempPath(),"Changpogo-handoff-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(handoff);
          var helper=Path.Combine(handoff,"LauncherUpdater.exe");
          using(var archive=ZipFile.OpenRead(zip)) { var entry=archive.GetEntry("LauncherUpdater.exe");if(entry==null)throw new InvalidDataException("ZIP에 LauncherUpdater.exe가 없습니다.");entry.ExtractToFile(helper); }
          var ready=Path.Combine(handoff,"ready");
          using(var updaterProcess=Process.Start(new ProcessStartInfo { FileName=helper,Arguments=Quote(zip)+" "+Process.GetCurrentProcess().Id+" "+Quote(AppDomain.CurrentDomain.BaseDirectory)+" "+Quote(ready),UseShellExecute=false,CreateNoWindow=true })) {
            var timer=Stopwatch.StartNew();while(!File.Exists(ready)&&!updaterProcess.HasExited&&timer.ElapsedMilliseconds<15000)await Task.Delay(100);
            if(!File.Exists(ready)||updaterProcess.HasExited)throw new IOException("업데이트 준비 실패. update.log를 확인하세요.");
            WriteLog("검증과 백업 완료. 교체 후 자동 재시작합니다.");Application.Exit();
          }
        }
      } catch(Exception ex) { WriteLog("업데이트 실패: "+ex.Message);MessageBox.Show(this,ex.Message,"런처 업데이트",MessageBoxButtons.OK,MessageBoxIcon.Error); }
      finally { update.Enabled=true; }
    }
    sealed class UpdateInfo { public string Version;public string Url; }
    static async Task<UpdateInfo> FindUpdate(HttpClient http) {
      const string releases="https://api.github.com/repos/japanoxx-afk/chanbogo/releases/latest";
      using(var response=await http.GetAsync(releases)) {
        if(response.IsSuccessStatusCode) {
          var json=await response.Content.ReadAsStringAsync();
          var tag=Regex.Match(json,"\\\"tag_name\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
          var asset=Regex.Match(json,"\\\"browser_download_url\\\"\\s*:\\s*\\\"([^\\\"]*ChangpogoLauncher[^\\\"]*\\.zip)\\\"",RegexOptions.IgnoreCase);
          if(tag.Success&&asset.Success)return new UpdateInfo { Version=tag.Groups[1].Value,Url=asset.Groups[1].Value.Replace("\\/","/") };
        } else if((int)response.StatusCode!=404)throw new HttpRequestException("GitHub API 응답: "+(int)response.StatusCode);
      }
      const string manifest="https://raw.githubusercontent.com/japanoxx-afk/chanbogo/main/update.json";
      var fallback=await http.GetStringAsync(manifest);
      var version=Regex.Match(fallback,"\\\"version\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
      var url=Regex.Match(fallback,"\\\"url\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
      if(!version.Success||!url.Success)throw new InvalidDataException("update.json 형식이 잘못되었습니다.");
      return new UpdateInfo { Version=version.Groups[1].Value,Url=url.Groups[1].Value.Replace("\\/","/") };
    }
    internal static string Quote(string value) {
      var result=new StringBuilder("\"");int slashes=0;
      foreach(char c in value) { if(c=='\\') { slashes++;continue; }result.Append('\\',c=='\"'?slashes*2+1:slashes);result.Append(c);slashes=0; }
      result.Append('\\',slashes*2);result.Append('\"');return result.ToString();
    }
    void WriteLog(string text) {
      var line="["+DateTime.Now.ToString("HH:mm:ss.fff")+"] "+text+Environment.NewLine;
      try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"launcher.log"),line,Encoding.UTF8); } catch { }
      if(IsDisposed||Disposing||!IsHandleCreated)return;if(InvokeRequired) { BeginInvoke(new Action<string>(AppendLog),line);return; }AppendLog(line);
    }
    void AppendLog(string line) { if(!IsDisposed&&!Disposing)log.AppendText(line); }
  }
}
