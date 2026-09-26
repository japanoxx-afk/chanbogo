using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChangpogoLauncher {
  sealed class MultiplayerCaptureForm : Form {
    readonly ComboBox role=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList };
    readonly TextBox code=new TextBox { Text="mp-test-04" };
    readonly Button start=new Button { Text="60초 기록 시작" };
    readonly Label status=new Label { AutoSize=false };
    readonly string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ChangpogoLauncher","MultiplayerCapture","v3");
    bool running;
    public MultiplayerCaptureForm() {
      Text="멀티 응답속도 테스트";ClientSize=new Size(580,280);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterParent;
      Font=new Font("맑은 고딕",10F);
      Controls.Add(new Label { Text="멀티 경기 진입 후 시작하세요. 양쪽에서 같은 테스트 코드를 사용하세요.\n게임의 우클릭 상태·숫자 카운터만 기록합니다. 자동 업로드는 없습니다.",Location=new Point(15,15),Size=new Size(550,55) });
      role.Items.AddRange(new object[]{"방장 (host)","참가자 (client)"});role.SelectedIndex=0;role.SetBounds(15,85,180,30);code.SetBounds(210,85,230,30);
      start.SetBounds(15,130,180,35);start.Click+=async(s,e)=>await RunCapture();
      var folder=new Button { Text="테스트 로그 폴더 열기",Location=new Point(210,130),Size=new Size(230,35) };
      folder.Click+=(s,e)=>{try {Directory.CreateDirectory(Path.Combine(root,"reports"));Process.Start(new ProcessStartInfo { FileName=Path.Combine(root,"reports"),UseShellExecute=true });}catch(Exception ex){status.Text=ex.Message;}};
      status.SetBounds(15,180,550,85);status.Text="대기 중 · 기록 시작 후 Alt+Tab으로 게임에 돌아가세요.";
      Controls.AddRange(new Control[]{role,code,start,folder,status});
      FormClosing+=(s,e)=>{if(running){e.Cancel=true;MessageBox.Show(this,"기록 중입니다. 최대 60초 후 완료되면 닫아주세요.");}};
    }
    async Task RunCapture() {
      string session=code.Text.Trim();
      if(!Regex.IsMatch(session,@"\A[a-z0-9-]{1,32}\z")){status.Text="테스트 코드는 영문 소문자·숫자·하이픈 1~32자로 입력하세요.";return;}
      running=true;start.Enabled=role.Enabled=code.Enabled=false;status.Text="게임 상태 확인 중...";
      string selected=role.SelectedIndex==0?"host":"client";
      try {
        Directory.CreateDirectory(root);
        string helper=Path.Combine(root,"capture-"+Guid.NewGuid().ToString("N")+".exe");
        try {
          using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("MultiplayerCapture.exe"))
          using(var target=new FileStream(helper,FileMode.CreateNew,FileAccess.Write)) { if(resource==null)throw new IOException("내장 진단 도구가 없습니다.");resource.CopyTo(target); }
          using(var process=new Process()) {
            process.StartInfo=new ProcessStartInfo { FileName=helper,Arguments=selected+" "+session+" 60",UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
            process.Start();
            var errors=process.StandardError.ReadToEndAsync();
            string saved=null,line;
            while((line=await process.StandardOutput.ReadLineAsync())!=null) {
              if(line.StartsWith("Recording for "))status.Text="기록 중 (60초) · 게임으로 돌아가 약 3초마다 우클릭 이동 명령을 내려주세요.";
              else if(line.StartsWith("Saved "))saved=line.Substring(6);
              else if(line.StartsWith("Capture failed at "))status.Text="기록 실패: "+line+"\n멀티 경기 진입 여부와 게임/런처 실행 권한을 확인하세요.";
            }
            await Task.Run(()=>process.WaitForExit());await errors;
            if(process.ExitCode==0&&saved!=null)status.Text="저장 완료! 테스트 로그 폴더 열기로 CSV를 확인하세요.\n"+Path.GetFileName(saved);
            else if(!status.Text.StartsWith("기록 실패"))status.Text="기록 실패. 테스트 로그 폴더의 capture-error 파일을 확인하세요.";
          }
        } finally {try {File.Delete(helper);}catch { }}
      } catch(Exception ex){status.Text="진단 실행 실패: "+ex.Message;}
      finally {running=false;start.Enabled=role.Enabled=code.Enabled=true;}
    }
  }
}
