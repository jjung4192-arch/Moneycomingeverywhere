using PhotoBridge.Core;
namespace PhotoBridge.Windows;
internal static class Program
{
    [STAThread]static void Main(){ApplicationConfiguration.Initialize();Application.Run(new MainWindow());}
}
internal sealed class MainWindow : Form
{
    private readonly Label heading=new(){Text="아이폰 사진, PC로.",AutoSize=true,Font=new Font("맑은 고딕",25,FontStyle.Bold)};
    private readonly Label guide=new(){Text="아이폰을 USB로 연결하고 잠금을 풀어주세요.\n처음 연결했다면 ‘이 컴퓨터를 신뢰’를 눌러주세요.",AutoSize=true,ForeColor=Color.DimGray};
    private readonly TextBox folder=new(){ReadOnly=true,Dock=DockStyle.Fill};
    private readonly Button choose=new(){Text="폴더 변경",AutoSize=true};
    private readonly Button import=new(){Text="새 사진 가져오기",Height=52,Dock=DockStyle.Top,BackColor=Color.FromArgb(42,68,55),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
    private readonly Button cancel=new(){Text="중지",Enabled=false,AutoSize=true};
    private readonly Button open=new(){Text="저장 폴더 열기",AutoSize=true};
    private readonly Label status=new(){Text="사진과 동영상을 원본 형식으로 가져옵니다.",AutoSize=true,MaximumSize=new Size(570,0)};
    private readonly ProgressBar bar=new(){Dock=DockStyle.Top,Height=6};
    private readonly TextBox log=new(){ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,BorderStyle=BorderStyle.FixedSingle};
    private CancellationTokenSource? cts;private bool running;
    public MainWindow()
    {
        Text="PhotoBridge · 아이폰 사진 가져오기";Size=new Size(680,720);MinimumSize=new Size(560,650);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(248,247,242);Font=new Font("맑은 고딕",10);AutoScaleMode=AutoScaleMode.Dpi;
        folder.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),"PhotoBridge");
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(28),ColumnCount=1,RowCount=11};
        for(int i=0;i<11;i++)layout.RowStyles.Add(i==9?new RowStyle(SizeType.Percent,100):new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);layout.Controls.Add(heading);layout.Controls.Add(guide);
        layout.Controls.Add(new Label{Text="저장 위치",AutoSize=true,Margin=new Padding(0,24,0,8)});
        var path=new TableLayoutPanel{Dock=DockStyle.Top,ColumnCount=2,AutoSize=true};path.ColumnStyles.Add(new(SizeType.Percent,100));path.ColumnStyles.Add(new(SizeType.AutoSize));path.Controls.Add(folder);path.Controls.Add(choose);layout.Controls.Add(path);
        import.Margin=new Padding(0,22,0,12);layout.Controls.Add(import);layout.Controls.Add(bar);status.Margin=new Padding(0,15,0,12);layout.Controls.Add(status);
        var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};actions.Controls.Add(open);actions.Controls.Add(cancel);layout.Controls.Add(actions);
        layout.Controls.Add(new Label{Text="오류가 발생하면 아래에 표시합니다. 원본은 삭제하지 않습니다.",AutoSize=true,Margin=new Padding(0,16,0,8)});layout.Controls.Add(log);
        layout.Controls.Add(new Label{Text="USB 연결 시험 전 버전 · 기기에서 원본 해시를 읽지 못하므로 바이트 단위 일치를 보장하지 않습니다.",AutoSize=true,MaximumSize=new Size(570,0),Font=new Font("맑은 고딕",8),ForeColor=Color.DimGray,Margin=new Padding(0,16,0,0)});
        choose.Click+=(_,_)=>{using var picker=new FolderBrowserDialog{Description="사진을 저장할 폴더",SelectedPath=folder.Text,UseDescriptionForTitle=true};if(picker.ShowDialog()==DialogResult.OK)folder.Text=picker.SelectedPath;};
        open.Click+=(_,_)=>{try{Directory.CreateDirectory(folder.Text);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe",folder.Text){UseShellExecute=true});}catch(Exception e){MessageBox.Show(e.Message);}};
        cancel.Click+=(_,_)=>{cts?.Cancel();cancel.Enabled=false;status.Text="중지하는 중입니다. 완료된 사진은 유지됩니다.";};
        import.Click+=(_,_)=>StartImport();
        FormClosing+=(_,e)=>{if(running){e.Cancel=true;cts?.Cancel();status.Text="작업을 중지하고 있습니다. 끝나면 창을 닫아주세요.";}};
    }
    private void Ui(Action action){if(!IsDisposed&&IsHandleCreated)BeginInvoke(action);}
    private void StartImport()
    {
        if(running)return;running=true;choose.Enabled=import.Enabled=false;cancel.Enabled=true;cts=new();var token=cts.Token;var destination=folder.Text;log.Clear();bar.Style=ProgressBarStyle.Marquee;status.Text="연결된 아이폰에서 사진을 찾고 있습니다…";
        var thread=new Thread(()=>
        {
            try
            {
                using var phone=new PhoneSession();var media=phone.Find(token,text=>Ui(()=>status.Text=text));
                var result=new Importer().Run(media,destination,token,p=>Ui(()=>{bar.Style=ProgressBarStyle.Continuous;bar.Maximum=Math.Max(1,p.Total);bar.Value=Math.Min(p.Done,bar.Maximum);status.Text=$"{p.Done:N0}/{p.Total:N0} · 새 파일 {p.Imported:N0} · 이미 가져옴 {p.Skipped:N0} · 실패 {p.Failed:N0}\n{p.Message}";}));
                Ui(()=>{status.Text=$"{(result.Cancelled?"중지됨":"가져오기 종료")} · 새 파일 {result.Imported:N0} · 이미 가져옴 {result.Skipped:N0} · 실패 {result.Failed:N0}";log.Text=result.Errors.Count>0?string.Join(Environment.NewLine,result.Errors):"오류 없음. PC 파일 크기를 확인하고 로컬 SHA-256 기록을 저장했습니다. 아이폰 원본은 그대로입니다.";});
            }
            catch(OperationCanceledException){Ui(()=>status.Text="중지했습니다. 다시 가져오면 완료된 파일은 건너뜁니다.");}
            catch(Exception ex){Ui(()=>{status.Text="가져오기를 완료하지 못했습니다.";log.Text=ex.Message;});}
            finally{Ui(()=>{running=false;choose.Enabled=import.Enabled=true;cancel.Enabled=false;bar.Style=ProgressBarStyle.Continuous;cts?.Dispose();cts=null;});}
        });thread.SetApartmentState(ApartmentState.STA);thread.IsBackground=true;thread.Start();
    }
}
