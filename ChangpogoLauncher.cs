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
#if STABLE_RELEASE
[assembly: AssemblyVersion("1.6.5.0")]
[assembly: AssemblyFileVersion("1.6.5.0")]
#elif MULTIPLAYER_75MS
[assembly: AssemblyVersion("1.5.2.0")]
[assembly: AssemblyFileVersion("1.5.2.0")]
#elif MULTIPLAYER_EXPERIMENT
[assembly: AssemblyVersion("1.5.0.0")]
[assembly: AssemblyFileVersion("1.5.0.0")]
#else
[assembly: AssemblyVersion("1.4.6.0")]
[assembly: AssemblyFileVersion("1.4.6.0")]
#endif

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
#if STABLE_RELEASE
    const string VersionText="1.6.5";
    const string ExperimentPeriod="75";
#elif MULTIPLAYER_75MS
    const string VersionText="1.5.2";
    const string ExperimentPeriod="75";
#elif MULTIPLAYER_EXPERIMENT
    const string VersionText="1.5.0";
    const string ExperimentPeriod="100";
#else
    const string VersionText="1.4.6";
#endif
    const string DefaultGame=@"C:\Users\seo\Downloads\DGGL\Games\Changpogo_Win_260708\Changpogo.exe";
    readonly TextBox gamePath=new TextBox();
    readonly TextBox log=new TextBox();
    readonly CheckBox lowLatency=new CheckBox { Text="실행 우선순위 보조",AutoSize=true };
    readonly CheckBox commandLatency=new CheckBox { Text="싱글 명령 지연 줄이기",AutoSize=true };
    readonly CheckBox multiplayerLatency=new CheckBox { Text="멀티 대기 원본 3 유지 (3→2 시험 중단 · 양쪽 업데이트 필요)",AutoSize=true };
    readonly Button play=new Button();
    readonly Button update=new Button();
    readonly ComboBox display=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList };
    readonly CheckBox captureMouse=new CheckBox { Text="마우스 가두기 (F8: 해제/다시 가두기)",AutoSize=true };
    readonly CheckBox widescreen=new CheckBox { Text="16:9 표시 (원본 화면 가로 확장)",AutoSize=true };
    MouseCapture mouseSession;
    GameDiagnostics diagnostics;
    GameClockOverlay gameClock;
    readonly CheckBox showGameClock=new CheckBox {Text="게임 시간 표시",AutoSize=true};
    readonly NumericUpDown cameraPercent=new NumericUpDown {Minimum=100,Maximum=150,Increment=5,DecimalPlaces=0,Value=100};
    readonly CheckBox compatibility=new CheckBox { Text="미니맵 정상 설정 복원·유지 (화면 강제 설정 안 함)",AutoSize=true };
    readonly CheckBox diagnosticMode=new CheckBox { Text="충돌·멀티 진단 기록 (로컬 메모리 덤프 · 자동 전송 없음)",AutoSize=true };

    public LauncherForm() {
      SuspendLayout();AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
      Text="해상왕 장보고 런처";ClientSize=new Size(720,735);MinimumSize=new Size(740,775);
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
      showGameClock.Checked=LoadSetting("game-clock.txt","true")=="true";showGameClock.Location=new Point(420,212);Controls.Add(showGameClock);
      showGameClock.CheckedChanged+=(s,e)=>{SaveSetting("game-clock.txt",showGameClock.Checked?"true":"false");if(!showGameClock.Checked&&gameClock!=null){gameClock.Dispose();gameClock=null;}};
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
      multiplayerLatency.Checked=false;multiplayerLatency.Enabled=false;multiplayerLatency.Location=new Point(20,380);Controls.Add(multiplayerLatency);
#if MULTIPLAYER_EXPERIMENT
#if STABLE_RELEASE
      Text+=" [정식 버전]";
      multiplayerLatency.Text="멀티 응답 개선 적용 (75ms) · 참가자 전원 같은 버전 사용";
#else
      Text+=" [멀티 "+ExperimentPeriod+"ms 시험판]";
      multiplayerLatency.Text="멀티 "+ExperimentPeriod+"ms 시험판 · 대기 3 유지 · 전원 이 시험판 사용 필수";
#endif
      commandLatency.Checked=true;commandLatency.Enabled=false;
#endif
      Controls.Add(new Label { Text="문제 발생 시 F9: 시점 기록 · 종료 후 진단 폴더 확인",AutoSize=true,Location=new Point(20,415),ForeColor=Color.DimGray });
      var multiplayerTest=new Button { Text="멀티 응답 테스트",Location=new Point(490,408),Size=new Size(205,35) };
      multiplayerTest.Click+=(s,e)=>{using(var dialog=new MultiplayerCaptureForm())dialog.ShowDialog(this);};Controls.Add(multiplayerTest);
      FormClosed+=(s,e)=>{if(gameClock!=null)gameClock.Dispose();if(mouseSession!=null)mouseSession.Dispose();if(diagnostics!=null)diagnostics.Dispose();};
      Controls.Add(new Label {Text="카메라 거리 (시험)",AutoSize=true,Location=new Point(20,464)});
      int savedCamera;
      if(!int.TryParse(LoadSetting("camera-distance-percent.txt","100"),out savedCamera)||savedCamera<100||savedCamera>150)savedCamera=100;
      cameraPercent.Value=savedCamera;cameraPercent.Location=new Point(165,460);cameraPercent.Size=new Size(75,28);
      cameraPercent.ValueChanged+=(s,e)=>SaveSetting("camera-distance-percent.txt",cameraPercent.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
      var resetCamera=new Button {Text="100% 복원",Location=new Point(450,458),Size=new Size(110,30)};
      resetCamera.Click+=(s,e)=>{cameraPercent.Value=100;};
      Controls.AddRange(new Control[]{cameraPercent,resetCamera,new Label {Text="% · 기본 100 / 최대 150",AutoSize=true,Location=new Point(250,464)},new Label {Text="다음 실행부터 적용 · 새 싱글 게임에서 먼저 확인 · 이상 시 100%로 복원",AutoSize=true,ForeColor=Color.DimGray,Location=new Point(20,495)}});
      play.Text="게임 실행";play.Font=new Font(Font,FontStyle.Bold);play.Location=new Point(20,525);play.Size=new Size(180,44);
      update.Text="런처 업데이트";update.Location=new Point(215,525);update.Size=new Size(180,44);
      var reports=new Button { Text="진단 폴더 열기",Location=new Point(410,525),Size=new Size(180,44) };
      reports.Click+=(s,e)=>{try {Directory.CreateDirectory(GameDiagnostics.Root);Process.Start(new ProcessStartInfo { FileName=GameDiagnostics.Root,UseShellExecute=true });}catch(Exception ex){WriteLog(ex.Message);}};Controls.Add(reports);
      play.Click+=StartGame;update.Click+=async (s,e)=>await CheckUpdate();
      log.Location=new Point(20,590);log.Size=new Size(675,125);log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;
      log.BackColor=Color.FromArgb(25,25,28);log.ForeColor=Color.Gainsboro;log.Font=new Font("Consolas",9F);
      Controls.AddRange(new Control[]{title,version,subtitle,pathLabel,gamePath,browse,lowLatency,lowHint,play,update,log});
      // Size rows from their contents rather than mixing DPI-scaled fonts with
      // fixed pixel coordinates. Narrow windows wrap; short displays scroll.
      Controls.Clear();AutoScroll=true;
      var layout=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,Padding=new Padding(14)};
      layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
      Action<Control[]> row=items=>{
        var panel=new FlowLayoutPanel {AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Fill,WrapContents=true,Margin=new Padding(0,3,0,3)};
        foreach(var item in items) {item.Margin=new Padding(5);if(item is Button){item.AutoSize=true;((Button)item).AutoSizeMode=AutoSizeMode.GrowAndShrink;}panel.Controls.Add(item);}
        layout.Controls.Add(panel,0,layout.RowCount++);
      };
      row(new Control[]{title,version});row(new Control[]{subtitle});row(new Control[]{pathLabel});
      var pathRow=new TableLayoutPanel {AutoSize=true,Dock=DockStyle.Fill,ColumnCount=2};
      pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      gamePath.Dock=DockStyle.Fill;browse.AutoSize=true;pathRow.Controls.Add(gamePath,0,0);pathRow.Controls.Add(browse,1,0);layout.Controls.Add(pathRow,0,layout.RowCount++);
      row(new Control[]{lowLatency});row(new Control[]{lowHint});row(new Control[]{commandLatency,showGameClock});
      row(new Control[]{display,captureMouse});row(new Control[]{widescreen});row(new Control[]{compatibility});row(new Control[]{diagnosticMode});row(new Control[]{multiplayerLatency});
      row(new Control[]{multiplayerTest});
      cameraPercent.MinimumSize=new Size(100,0);
      row(new Control[]{new Label {Text="카메라 시야 확대 (시험)",AutoSize=true},cameraPercent,new Label {Text="% (100~150)",AutoSize=true},resetCamera});
      row(new Control[]{new Label {Text="다음 실행부터 적용 · 이상 시 100%로 복원",AutoSize=true}});
      row(new Control[]{play,update,reports});
      log.Dock=DockStyle.Fill;log.MinimumSize=new Size(0,120);layout.Controls.Add(log,0,layout.RowCount++);
      Controls.Add(layout);ResumeLayout(true);
      WriteLog("런처 v"+VersionText+" 준비됨. EXE는 보존하며 농부 비용 데이터는 원본 백업 후 적용합니다.");
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
#if MULTIPLAYER_EXPERIMENT && !STABLE_RELEASE
        if(MessageBox.Show(this,"멀티 처리 주기 "+ExperimentPeriod+"ms 시험판입니다. 실제 두 PC 동기화·안정성은 검증 전입니다.\n참가자 전원이 이 "+VersionText+" 시험판으로 게임을 새로 실행해야 합니다. 다른 버전과 혼용하지 마세요.\n동기화 오류·끊김 발생 시 경기를 종료하고 전원이 이전 버전으로 돌아가세요.\n계속할까요?",ExperimentPeriod+"ms 시험판",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
#endif
        if(multiplayerLatency.Checked&&MessageBox.Show(this,"참가자 전원이 v1.4.4 이상에서 같은 멀티 대기 옵션을 켜고 게임을 재시작해야 합니다.\n\n통신 대기 여유가 줄어 끊김이 늘 수 있는 시험 기능입니다. 문제가 생기면 전원이 옵션을 끄고 재시작하세요.\n계속할까요?","멀티 명령 대기 시험",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        SaveSetting("multiplayer-latency.txt",multiplayerLatency.Checked?"true":"false");
        FarmerBalancePatch.Apply(source);
        WriteLog("밸런스: 농부 쌀 50 / 모든 유닛 생산·건물 건설시간 50%. 멀티 참가자 전원 1.6.5 사용 필수.");
        DisplayOptions.PrepareCompatible(source,display.SelectedIndex==1,widescreen.Checked,compatibility.Checked);
        SaveSetting("preserve-display-profile.txt",compatibility.Checked?"true":"false");SaveSetting("diagnostics.txt",diagnosticMode.Checked?"true":"false");
        SaveSetting("widescreen.txt",widescreen.Checked?"true":"false");
        SaveSetting("display-mode.txt",display.SelectedIndex.ToString());SaveSetting("capture-mouse.txt",captureMouse.Checked?"true":"false");
        if(diagnosticMode.Checked) {
          diagnostics=new GameDiagnostics(source,"latency="+commandLatency.Checked+", multiplayerLead="+(multiplayerLatency.Checked?2:3)+", borderless="+(display.SelectedIndex==1)+", wide="+widescreen.Checked+", compatibility="+compatibility.Checked);
          WriteLog("진단 저장 위치: "+diagnostics.DirectoryPath);
        }
        int camera=(int)cameraPercent.Value;
        SaveSetting("camera-distance-percent.txt",camera.ToString(System.Globalization.CultureInfo.InvariantCulture));
        process=SinglePlayerPatch.StartConfigured(source,commandLatency.Checked,false,camera,diagnostics==null?(Action<Process>)null:diagnostics.Attach);
        WriteLog("카메라 거리·시야 동시 확대: "+camera+"% / 새 게임 권장. 저장 게임·연출에서는 별도 카메라가 사용될 수 있습니다.");
        if(process==null)throw new InvalidOperationException("게임 프로세스를 시작하지 못했습니다.");
        mouseSession=new MouseCapture(process,captureMouse.Checked,WriteLog);
        if(showGameClock.Checked&&commandLatency.Checked)try {gameClock=new GameClockOverlay(process);}catch(Exception ex){WriteLog("게임 시간 표시 실패 (게임은 계속 실행): "+ex.Message);}
        WriteLog("화면: "+(compatibility.Checked?"원본 설정 유지":display.Text)+" / F8: 마우스 가두기 전환 / Alt+Tab: 자동 해제");
        if(!compatibility.Checked)WriteLog(widescreen.Checked?"16:9 가로 확장 표시 (네이티브 와이드 아님)":"4:3 원본 비율");
        if(compatibility.Checked)WriteLog("최초 화면 설정 백업이 있으면 복원, 없으면 현재 설정 유지. 전체화면·16:9·해상도·색상 강제 변경 없음.");
        WriteLog("Alt+Enter: dgVoodoo 화면 전환 비활성화 적용.");
        session=new LowLatencySession();if(lowLatency.Checked)try { session.Begin(process); }catch(Exception ex) { WriteLog("실행 보조 설정 실패 (게임은 계속 실행): "+ex.Message); }
        WriteLog("게임 시작 PID="+process.Id+" / 싱글 명령 패치="+commandLatency.Checked);
        if(commandLatency.Checked)WriteLog("싱글 명령 묶음 200→50ms / 시뮬레이션 진행량 보정");
        WriteLog(multiplayerLatency.Checked?"멀티 초기 선행 명령 묶음 3→2 적용 / 200ms 처리 간격 유지 / 실제 체감 개선은 양쪽 테스트 필요":"멀티 명령 대기: 원본 3 유지");
#if MULTIPLAYER_EXPERIMENT
#if STABLE_RELEASE
        WriteLog("정식 패치: 멀티 처리 75ms / 진행량 보정 / 원본 준비 검사·동기화 보정 유지 / 싱글 50ms 유지");
#else
        WriteLog("시험 패치: 멀티 처리 "+ExperimentPeriod+"ms / 진행량 주기 비례 보정 / 원본 준비 검사·동기화 보정 유지 / 싱글 50ms 유지");
#endif
#endif
        await Task.Run(()=>process.WaitForExit());WriteLog("게임 종료 / 코드 0x"+unchecked((uint)process.ExitCode).ToString("X8"));
      } catch(Exception ex) { WriteLog("실행 실패: "+ex.Message);MessageBox.Show(this,ex.Message,"게임 실행",MessageBoxButtons.OK,MessageBoxIcon.Error); }
      finally { if(gameClock!=null){gameClock.Dispose();gameClock=null;}if(mouseSession!=null){mouseSession.Dispose();mouseSession=null;}if(diagnostics!=null){diagnostics.Dispose();diagnostics=null;}if(session!=null)session.Dispose();if(process!=null)process.Dispose();if(!IsDisposed){play.Enabled=true;update.Enabled=true;} }
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
      // The manifest explicitly selects the update, including experimental builds.
      const string manifest="https://raw.githubusercontent.com/japanoxx-afk/chanbogo/main/update.json";
      var fallback=await http.GetStringAsync(manifest+"?t="+DateTime.UtcNow.Ticks);
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
