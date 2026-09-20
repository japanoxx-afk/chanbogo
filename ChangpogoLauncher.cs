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
[assembly: AssemblyVersion("1.4.4.0")]
[assembly: AssemblyFileVersion("1.4.4.0")]

namespace ChangpogoLauncher {
  static class Program {
    [STAThread] static void Main() {
      ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
      MouseCapture.InitializeDpi();
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.Run(new LauncherForm());
    }
  }

  sealed class LauncherForm : Form {
    const string VersionText="1.4.4";
    const string DefaultGame=@"C:\Users\seo\Downloads\DGGL\Games\Changpogo_Win_260708\Changpogo.exe";
    readonly TextBox gamePath=new TextBox();
    readonly TextBox log=new TextBox();
    readonly CheckBox lowLatency=new CheckBox { Text="실행 우선순위 보조",AutoSize=true };
    readonly CheckBox commandLatency=new CheckBox { Text="싱글 명령 지연 줄이기 (시험 적용)",AutoSize=true };
    readonly CheckBox multiplayerLatency=new CheckBox { Text="멀티 명령 대기 3→2 (시험 · 참가자 전원 동일 설정 필요)",AutoSize=true };
    readonly Button play=new Button();
    readonly Button update=new Button();
    readonly ComboBox display=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList };
    readonly CheckBox captureMouse=new CheckBox { Text="마우스 가두기 (F8: 해제/다시 가두기)",AutoSize=true };
    readonly CheckBox widescreen=new CheckBox { Text="16:9 표시 (원본 화면 가로 확장)",AutoSize=true };
    MouseCapture mouseSession;
    GameDiagnostics diagnostics;
    readonly CheckBox compatibility=new CheckBox { Text="미니맵 정상 설정 복원·유지 (화면 강제 설정 안 함)",AutoSize=true };
    readonly CheckBox diagnosticMode=new CheckBox { Text="충돌·멀티 진단 기록 (로컬 메모리 덤프 · 자동 전송 없음)",AutoSize=true };

    public LauncherForm() {
      Text="해상왕 장보고 런처";ClientSize=new Size(720,670);MinimumSize=new Size(740,710);
      Font=new Font("맑은 고딕",10F);StartPosition=FormStartPosition.CenterScreen;
      var title=new Label { Text="해상왕 장보고",Font=new Font("맑은 고딕",22F,FontStyle.Bold),AutoSize=true,Location=new Point(22,18) };
      var version=new Label { Text="Launcher v"+VersionText,AutoSize=true,ForeColor=Color.SteelBlue,Location=new Point(250,39) };
      var subtitle=new Label { Text="원본 게임을 보존하는 저지연 실행 및 자동 업데이트",AutoSize=true,ForeColor=Color.DimGray,Location=new Point(25,65) };
      var pathLabel=new Label { Text="게임 실행 파일",AutoSize=true,Location=new Point(20,103) };
      gamePath.Text=LoadSetting("game-path.txt",DefaultGame);gamePath.Location=new Point(20,128);gamePath.Width=585;
      var browse=new Button { Text="찾기",Location=new Point(615,125),Size=new Size(80,31) };browse.Click+=Browse;
      lowLatency.Checked=LoadSetting("low-latency.txt","true")=="true";lowLatency.Location=new Point(20,177);
      var lowHint=new Label { Text="기존 보조 설정 · 명령 지연 패치는 아래 옵션",AutoSize=true,ForeColor=Color.DimGray,Location=new Point(210,179) };
      commandLatency.Checked=LoadSetting("command-latency.txt","true")=="true";commandLatency.Location=new Point(20,212);
      Controls.Add(commandLatency);
      display.Items.AddRange(new object[]{"창모드","테두리 없는 전체화면"});
      display.SelectedIndex=LoadSetting("display-mode.txt","0")=="1"?1:0;display.Location=new Point(20,247);display.Width=250;
      captureMouse.Checked=LoadSetting("capture-mouse.txt","true")=="true";captureMouse.Location=new Point(290,250);
      Controls.AddRange(new Control[]{display,captureMouse});
      widescreen.Checked=LoadSetting("widescreen.txt","true")=="true";widescreen.Location=new Point(20,280);Controls.Add(widescreen);
      compatibility.Checked=LoadSetting("preserve-display-profile.txt","true")=="true";compatibility.Location=new Point(20,313);
      compatibility.CheckedChanged+=(s,e)=>{display.Enabled=!compatibility.Checked;widescreen.Enabled=!compatibility.Checked;};
      display.Enabled=!compatibility.Checked;widescreen.Enabled=!compatibility.Checked;
      diagnosticMode.Checked=LoadSetting("diagnostics.txt","true")=="true";diagnosticMode.Location=new Point(20,346);
      Controls.AddRange(new Control[]{compatibility,diagnosticMode});
      multiplayerLatency.Checked=LoadSetting("multiplayer-latency.txt","false")=="true";multiplayerLatency.Location=new Point(20,380);Controls.Add(multiplayerLatency);
      Controls.Add(new Label { Text="문제 발생 시 F9: 시점 기록 · 종료 후 진단 폴더 확인",AutoSize=true,Location=new Point(20,415),ForeColor=Color.DimGray });
      FormClosed+=(s,e)=>{if(mouseSession!=null)mouseSession.Dispose();if(diagnostics!=null)diagnostics.Dispose();};
      play.Text="게임 실행";play.Font=new Font(Font,FontStyle.Bold);play.Location=new Point(20,460);play.Size=new Size(180,44);
      update.Text="런처 업데이트";update.Location=new Point(215,460);update.Size=new Size(180,44);
      var reports=new Button { Text="진단 폴더 열기",Location=new Point(410,460),Size=new Size(180,44) };
      reports.Click+=(s,e)=>{try {Directory.CreateDirectory(GameDiagnostics.Root);Process.Start(new ProcessStartInfo { FileName=GameDiagnostics.Root,UseShellExecute=true });}catch(Exception ex){WriteLog(ex.Message);}};Controls.Add(reports);
      play.Click+=StartGame;update.Click+=async (s,e)=>await CheckUpdate();
      log.Location=new Point(20,525);log.Size=new Size(675,125);log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;
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
      play.Enabled=false;update.Enabled=false;LowLatencySession session=null;Process process=null;
      try {
        var source=Path.GetFullPath(gamePath.Text.Trim());
        if(!File.Exists(source))throw new FileNotFoundException("Changpogo.exe를 찾을 수 없습니다.",source);
        if(!string.Equals(Path.GetFileName(source),"Changpogo.exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Changpogo.exe를 선택하세요.");
        SaveSetting("game-path.txt",source);SaveSetting("low-latency.txt",lowLatency.Checked?"true":"false");
        SaveSetting("command-latency.txt",commandLatency.Checked?"true":"false");
        if(Process.GetProcessesByName("Changpogo").Length>0)throw new InvalidOperationException("실행 중인 게임을 종료한 뒤 화면 설정을 적용하세요.");
        if(multiplayerLatency.Checked&&MessageBox.Show(this,"참가자 전원이 v1.4.4 이상에서 같은 멀티 대기 옵션을 켜고 게임을 재시작해야 합니다.\n\n통신 대기 여유가 줄어 끊김이 늘 수 있는 시험 기능입니다. 문제가 생기면 전원이 옵션을 끄고 재시작하세요.\n계속할까요?","멀티 명령 대기 시험",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        SaveSetting("multiplayer-latency.txt",multiplayerLatency.Checked?"true":"false");
        DisplayOptions.PrepareCompatible(source,display.SelectedIndex==1,widescreen.Checked,compatibility.Checked);
        SaveSetting("preserve-display-profile.txt",compatibility.Checked?"true":"false");SaveSetting("diagnostics.txt",diagnosticMode.Checked?"true":"false");
        SaveSetting("widescreen.txt",widescreen.Checked?"true":"false");
        SaveSetting("display-mode.txt",display.SelectedIndex.ToString());SaveSetting("capture-mouse.txt",captureMouse.Checked?"true":"false");
        if(diagnosticMode.Checked) {
          diagnostics=new GameDiagnostics(source,"latency="+commandLatency.Checked+", multiplayerLead="+(multiplayerLatency.Checked?2:3)+", borderless="+(display.SelectedIndex==1)+", wide="+widescreen.Checked+", compatibility="+compatibility.Checked);
          WriteLog("진단 저장 위치: "+diagnostics.DirectoryPath);
        }
        process=SinglePlayerPatch.StartObserved(source,commandLatency.Checked,multiplayerLatency.Checked,diagnostics==null?(Action<Process>)null:diagnostics.Attach);
        if(process==null)throw new InvalidOperationException("게임 프로세스를 시작하지 못했습니다.");
        mouseSession=new MouseCapture(process,captureMouse.Checked,WriteLog);
        WriteLog("화면: "+(compatibility.Checked?"원본 설정 유지":display.Text)+" / F8: 마우스 가두기 전환 / Alt+Tab: 자동 해제");
        if(!compatibility.Checked)WriteLog(widescreen.Checked?"16:9 가로 확장 표시 (네이티브 와이드 아님)":"4:3 원본 비율");
        if(compatibility.Checked)WriteLog("최초 화면 설정 백업이 있으면 복원, 없으면 현재 설정 유지. 전체화면·16:9·해상도·색상 강제 변경 없음.");
        WriteLog("Alt+Enter: dgVoodoo 화면 전환 비활성화 적용.");
        session=new LowLatencySession();if(lowLatency.Checked)try { session.Begin(process); }catch(Exception ex) { WriteLog("실행 보조 설정 실패 (게임은 계속 실행): "+ex.Message); }
        WriteLog("게임 시작 PID="+process.Id+" / 싱글 명령 패치="+commandLatency.Checked);
        if(commandLatency.Checked)WriteLog("싱글 명령 묶음 200→50ms / 시뮬레이션 진행량 보정 / 멀티는 기존 경로");
        WriteLog(multiplayerLatency.Checked?"멀티 초기 선행 명령 묶음 3→2 적용 / 200ms 처리 간격 유지 / 실제 체감 개선은 양쪽 테스트 필요":"멀티 명령 대기: 원본 3 유지");
        await Task.Run(()=>process.WaitForExit());WriteLog("게임 종료 / 코드 0x"+unchecked((uint)process.ExitCode).ToString("X8"));
      } catch(Exception ex) { WriteLog("실행 실패: "+ex.Message);MessageBox.Show(this,ex.Message,"게임 실행",MessageBoxButtons.OK,MessageBoxIcon.Error); }
      finally { if(mouseSession!=null){mouseSession.Dispose();mouseSession=null;}if(diagnostics!=null){diagnostics.Dispose();diagnostics=null;}if(session!=null)session.Dispose();if(process!=null)process.Dispose();if(!IsDisposed){play.Enabled=true;update.Enabled=true;} }
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
      if(diagnostics!=null)diagnostics.Write(text);
      var line="["+DateTime.Now.ToString("HH:mm:ss.fff")+"] "+text+Environment.NewLine;
      try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"launcher.log"),line,Encoding.UTF8); } catch { }
      if(IsDisposed||Disposing||!IsHandleCreated)return;if(InvokeRequired) { BeginInvoke(new Action<string>(AppendLog),line);return; }AppendLog(line);
    }
    void AppendLog(string line) { if(!IsDisposed&&!Disposing)log.AppendText(line); }
  }
}
