using System.Collections.Concurrent;
using PhotoBridge.Core;
namespace PhotoBridge.Windows;
internal static class Program
{
    [STAThread]static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if(!args.Contains("--self-test")){Application.Run(new MainWindow());return;}
        var fake=new DemoPhone();var window=new MainWindow(()=>fake);
        window.Shown+=async(_,_)=>{try{await window.CheckSelectionFlow(fake);File.WriteAllText("ui-checks.txt","PASS: folder browsing, paging, thumbnails, selection, and copy-only-after-confirmation");Environment.ExitCode=0;}catch(Exception ex){File.WriteAllText("ui-checks.txt",ex.ToString());Environment.ExitCode=1;}finally{window.Close();}};
        Application.Run(window);
    }
}
internal sealed partial class MainWindow : Form
{
    // Device objects never leave this single STA thread, including browse, thumbnail and copy calls.
    private readonly BlockingCollection<Action> work=new();private readonly Thread worker;
    private IPhoneBrowser? phone;private readonly Func<IPhoneBrowser> createPhone;private CancellationTokenSource? browseCancel,copyCancel;
    private readonly HashSet<string> selected=new();private readonly Stack<(string Folder,int Offset,string Title)> history=new();
    private string? current;private int offset,nextOffset=-1;private int generation;private bool copying,loading;
    private readonly Button connect=new(){Text="아이폰 열기",AutoSize=true};
    private readonly Button up=new(){Text="상위 폴더",AutoSize=true,Enabled=false};
    private readonly Button previous=new(){Text="이전 20개",AutoSize=true,Enabled=false};
    private readonly Button next=new(){Text="다음 20개",AutoSize=true,Enabled=false};
    private readonly Button all=new(){Text="이 페이지 선택",AutoSize=true,Enabled=false};
    private readonly Button clear=new(){Text="선택 해제",AutoSize=true};
    private readonly Button import=new(){Text="선택한 사진 가져오기",AutoSize=true,Enabled=false,BackColor=Color.FromArgb(42,68,55),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
    private readonly Button cancel=new(){Text="중지",AutoSize=true,Enabled=false};
    private readonly Button choose=new(){Text="저장 위치 변경",AutoSize=true};
    private readonly TextBox destination=new(){ReadOnly=true,Dock=DockStyle.Fill};
    private readonly Label location=new(){Text="아이폰을 연결한 뒤 ‘아이폰 열기’를 누르세요.",AutoSize=true};
    private readonly Label selection=new(){Text="0개 선택",AutoSize=true,Padding=new Padding(0,7,0,0)};
    private readonly Label status=new(){Text="원본을 미리 복사하지 않습니다. 폴더를 열어 사진을 골라주세요.",AutoSize=true,MaximumSize=new Size(840,0)};
    private readonly TextBox errors=new(){ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill};
    private readonly ListView gallery=new(){Dock=DockStyle.Fill,View=View.LargeIcon,CheckBoxes=true,MultiSelect=true,HideSelection=false};
    private readonly ImageList images=new(){ImageSize=new Size(144,112),ColorDepth=ColorDepth.Depth32Bit};
    private readonly List<Bitmap> ownedImages=new();private bool rendering;
    public MainWindow(Func<IPhoneBrowser>? createPhone=null)
    {
        this.createPhone=createPhone??(()=>new PhoneSession());
        Text="PhotoBridge · 사진 선택해서 가져오기";Size=new Size(980,850);MinimumSize=new Size(760,700);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(248,247,242);Font=new Font("맑은 고딕",10);AutoScaleMode=AutoScaleMode.Dpi;
        destination.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),"PhotoBridge");gallery.LargeImageList=images;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=10};for(int i=0;i<10;i++)layout.RowStyles.Add(i==4?new RowStyle(SizeType.Percent,100):i==8?new RowStyle(SizeType.Absolute,65):new RowStyle(SizeType.AutoSize));Controls.Add(layout);
        layout.Controls.Add(new Label{Text="보고, 고르고, 가져오기.",AutoSize=true,Font=new Font("맑은 고딕",23,FontStyle.Bold)});
        layout.Controls.Add(new Label{Text="USB 연결 → 아이폰 잠금 해제·신뢰 승인 → 폴더 열기 → 사진 체크",AutoSize=true,ForeColor=Color.DimGray,Margin=new Padding(0,8,0,15)});
        var navigation=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};navigation.Controls.AddRange(new Control[]{connect,up,previous,next,all,clear});layout.Controls.Add(navigation);location.Margin=new Padding(0,10,0,8);layout.Controls.Add(location);layout.Controls.Add(gallery);
        var path=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Top,ColumnCount=2};path.ColumnStyles.Add(new(SizeType.Percent,100));path.ColumnStyles.Add(new(SizeType.AutoSize));path.Controls.Add(destination);path.Controls.Add(choose);layout.Controls.Add(path);
        var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};actions.Controls.AddRange(new Control[]{import,cancel,selection});layout.Controls.Add(actions);status.Margin=new Padding(0,10,0,10);layout.Controls.Add(status);layout.Controls.Add(errors);
        layout.Controls.Add(new Label{Text="미리보기는 Windows 축소판 기능을 사용합니다. HEIC·동영상은 미지원 시 파일명으로 표시합니다. 아이폰 원본은 삭제하지 않습니다.",AutoSize=true,MaximumSize=new Size(840,0),Font=new Font("맑은 고딕",8),ForeColor=Color.DimGray,Margin=new Padding(0,10,0,0)});
        worker=new Thread(()=>{foreach(var action in work.GetConsumingEnumerable())action();phone?.Dispose();});worker.SetApartmentState(ApartmentState.STA);worker.IsBackground=true;worker.Start();
        connect.Click+=(_,_)=>OpenPhone();up.Click+=(_,_)=>{if(history.TryPop(out var parent))LoadPage(parent.Folder,parent.Offset,parent.Title);};
        previous.Click+=(_,_)=>{if(current!=null)LoadPage(current,Math.Max(0,offset-20),location.Text);};next.Click+=(_,_)=>{if(current!=null)LoadPage(current,nextOffset,location.Text);};
        all.Click+=(_,_)=>{foreach(ListViewItem item in gallery.Items)if(item.Tag is BrowserEntry entry&&!entry.IsFolder)item.Checked=true;};
        clear.Click+=(_,_)=>{selected.Clear();rendering=true;foreach(ListViewItem item in gallery.Items)item.Checked=false;rendering=false;UpdateSelection();};
        gallery.ItemCheck+=(_,e)=>{if(rendering)return;var entry=(BrowserEntry)gallery.Items[e.Index].Tag!;if(entry.IsFolder){e.NewValue=CheckState.Unchecked;return;}if(e.NewValue==CheckState.Checked)selected.Add(entry.Id);else selected.Remove(entry.Id);UpdateSelection();};
        gallery.ItemActivate+=(_,_)=>{if(gallery.SelectedItems.Count!=1)return;var entry=(BrowserEntry)gallery.SelectedItems[0].Tag!;if(entry.IsFolder&&current!=null){history.Push((current,offset,location.Text));LoadPage(entry.Id,0,entry.Name);}};
        choose.Click+=(_,_)=>{using var picker=new FolderBrowserDialog{Description="저장할 폴더",SelectedPath=destination.Text,UseDescriptionForTitle=true};if(picker.ShowDialog()==DialogResult.OK)destination.Text=picker.SelectedPath;};
        import.Click+=(_,_)=>ImportSelected();cancel.Click+=(_,_)=>{browseCancel?.Cancel();copyCancel?.Cancel();status.Text="중지 요청 중 · 완료된 파일은 유지됩니다.";};
        FormClosing+=(_,e)=>{if(copying||loading){e.Cancel=true;browseCancel?.Cancel();copyCancel?.Cancel();status.Text="진행 중인 작업을 중지합니다. 끝나면 창을 닫아주세요.";}else{generation++;work.CompleteAdding();}};
        FormClosed+=(_,_)=>{foreach(var image in ownedImages)image.Dispose();images.Dispose();};
    }
    private void Ui(Action action){if(!IsDisposed&&IsHandleCreated)BeginInvoke(()=>{if(!IsDisposed)action();});}
    private void UpdateSelection(){selection.Text=$"{selected.Count:N0}개 선택";import.Enabled=selected.Count>0&&!copying&&!loading;}
    private void Busy(bool value){loading=value;connect.Enabled=choose.Enabled=!value&&!copying;gallery.Enabled=!copying&&!value;all.Enabled=!value&&!copying;up.Enabled=!value&&!copying&&history.Count>0;previous.Enabled=!value&&!copying&&offset>0;next.Enabled=!value&&!copying&&nextOffset>=0;clear.Enabled=!copying&&!value;cancel.Enabled=value||copying;UpdateSelection();}
    private void OpenPhone()
    {
        browseCancel?.Cancel();var cts=browseCancel=new();var token=cts.Token;int version=++generation;selected.Clear();history.Clear();rendering=true;gallery.Items.Clear();rendering=false;current=null;offset=0;nextOffset=-1;errors.Clear();Busy(true);status.Text="아이폰을 여는 중 · 원본 복사는 하지 않습니다.";
        work.Add(()=>{try{phone?.Dispose();phone=createPhone();var root=phone.Connect();var page=phone.ReadPage(root,0,token);ShowPage(page,"iPhone",version,token);}catch(Exception ex){BrowseError(ex,version);}});
    }
    private void LoadPage(string id,int pageOffset,string title)
    {
        browseCancel?.Cancel();var cts=browseCancel=new();var token=cts.Token;int version=++generation;Busy(true);status.Text="선택한 폴더의 목록을 읽는 중 · 원본 복사는 하지 않습니다.";
        work.Add(()=>{try{var page=phone!.ReadPage(id,pageOffset,token);ShowPage(page,title,version,token);}catch(Exception ex){BrowseError(ex,version);}});
    }
    private void ShowPage(BrowserPage page,string title,int version,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();Ui(()=>{if(version!=generation)return;current=page.FolderId;offset=page.Offset;nextOffset=page.HasMore?page.NextOffset:-1;location.Text=title;Render(page);Busy(false);status.Text=$"목록 {page.Entries.Count}개 표시 · 원본은 아직 복사하지 않았습니다. 축소판을 불러오는 중…";});
        foreach(var entry in page.Entries.Where(e=>!e.IsFolder))
        {
            if(token.IsCancellationRequested)break;var image=phone!.Thumbnail(entry.Id,token);if(image==null)continue;
            Ui(()=>{if(version!=generation){image.Dispose();return;}ownedImages.Add(image);images.Images.Add(entry.Id,image);var row=gallery.Items[entry.Id];if(row!=null)row.ImageKey=entry.Id;});
        }
        Ui(()=>{if(version!=generation)return;Busy(false);status.Text=token.IsCancellationRequested?"미리보기를 중지했습니다. 표시된 사진은 선택할 수 있습니다.":"폴더는 두 번 클릭, 사진은 체크하세요. 다음 20개로 더 볼 수 있습니다.";});
    }
    private void BrowseError(Exception ex,int version){Ui(()=>{if(version!=generation)return;Busy(false);status.Text=ex is OperationCanceledException?"목록 읽기를 중지했습니다.":"아이폰 목록을 열지 못했습니다.";if(ex is not OperationCanceledException)errors.Text=ex.Message;});}
    private Bitmap Placeholder(bool folder)
    {
        var bitmap=new Bitmap(144,112);using var g=Graphics.FromImage(bitmap);g.Clear(Color.FromArgb(242,242,235));using var brush=new SolidBrush(Color.FromArgb(111,125,103));using var font=new Font("맑은 고딕",folder?26:11,FontStyle.Regular);g.DrawString(folder?"▱":"미리보기 없음",font,brush,new RectangleF(0,30,144,70),new StringFormat{Alignment=StringAlignment.Center});return bitmap;
    }
    private void Render(BrowserPage page)
    {
        rendering=true;gallery.BeginUpdate();gallery.Items.Clear();images.Images.Clear();foreach(var image in ownedImages)image.Dispose();ownedImages.Clear();
        foreach(bool folder in new[]{true,false}){var icon=Placeholder(folder);ownedImages.Add(icon);images.Images.Add(folder?"folder":"file",icon);}
        foreach(var entry in page.Entries){var item=new ListViewItem(entry.Name){Name=entry.Id,Tag=entry,ImageKey=entry.IsFolder?"folder":"file",Checked=!entry.IsFolder&&selected.Contains(entry.Id)};gallery.Items.Add(item);}
        gallery.EndUpdate();rendering=false;UpdateSelection();
    }
    private void ImportSelected()
    {
        if(copying||loading||selected.Count==0)return;browseCancel?.Cancel();generation++;var ids=selected.ToArray();var path=destination.Text;copying=true;Busy(false);copyCancel?.Dispose();copyCancel=new();var token=copyCancel.Token;errors.Clear();status.Text=$"선택한 {ids.Length}개만 가져오는 중…";
        work.Add(()=>{try{var media=phone!.Selected(ids,token);var result=new Importer().Run(media,path,token,p=>Ui(()=>status.Text=$"{p.Done}/{p.Total} · 새 파일 {p.Imported} · 이미 가져옴 {p.Skipped} · 실패 {p.Failed}\n{p.Message}"));Ui(()=>{status.Text=$"{(result.Cancelled?"중지됨":"가져오기 종료")} · 새 파일 {result.Imported} · 이미 가져옴 {result.Skipped} · 실패 {result.Failed}";errors.Text=string.Join(Environment.NewLine,result.Errors);});}catch(Exception ex){Ui(()=>{status.Text=ex is OperationCanceledException?"중지했습니다.":"가져오지 못했습니다.";errors.Text=ex.Message;});}finally{Ui(()=>{copying=false;Busy(false);});}});
    }
}
