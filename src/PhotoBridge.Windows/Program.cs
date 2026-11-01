using System.Collections.Concurrent;
using System.Diagnostics;
using PhotoBridge.Core;
namespace PhotoBridge.Windows;
internal static class Program
{
    [STAThread]static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if(!args.Contains("--self-test")){Application.Run(new MainWindow());return;}
        var fake=new DemoPhone();var window=new MainWindow(()=>fake,r=>fake.LoadPreview(r));
        window.Shown+=async(_,_)=>{try{await window.CheckSelectionFlow(fake);File.WriteAllText("ui-checks.txt","PASS: all-photo streaming, visible-only previews, bounded cache, selection and selected-only copy");Environment.ExitCode=0;}catch(Exception ex){File.WriteAllText("ui-checks.txt",ex.ToString());Environment.ExitCode=1;}finally{window.FinishSelfTest();}};
        Application.Run(window);
    }
}
internal sealed partial class MainWindow : Form
{
    private readonly BlockingCollection<Action> work=new(),thumbnailWork=new();
    private readonly ConcurrentQueue<(string Id,int Version)> previewRequests=new();
    private readonly Func<IPhoneBrowser> createPhone;private readonly Func<PreviewReference,Bitmap?> loadPreview;private IPhoneBrowser? phone;
    private CancellationTokenSource? browseCancel,copyCancel;
    private readonly HashSet<string> selected=new(),attempted=new();private readonly Dictionary<string,ListViewItem> rows=new();
    private readonly Dictionary<string,int> slots=new();private readonly Dictionary<int,string> slotOwners=new();private int replaceSlot;
    private readonly List<Bitmap> ownedImages=new();private readonly object previewLock=new();private (string Id,int Version)? inFlight;
    private int generation;private bool copying,loading,listing,rendering;private readonly System.Windows.Forms.Timer viewport=new(){Interval=180};
    private readonly Button connect=new(){Text="아이폰 사진 불러오기",AutoSize=true};
    private readonly Button all=new(){Text="표시된 사진 전체 선택",AutoSize=true,Enabled=false};
    private readonly Button clear=new(){Text="선택 해제",AutoSize=true};
    private readonly Button import=new(){Text="선택한 사진 가져오기",AutoSize=true,Enabled=false,BackColor=Color.FromArgb(42,68,55),ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
    private readonly Button cancel=new(){Text="중지",AutoSize=true,Enabled=false};
    private readonly Button choose=new(){Text="저장 위치 변경",AutoSize=true};
    private readonly TextBox destination=new(){ReadOnly=true,Dock=DockStyle.Fill};
    private readonly Label location=new(){Text="사진을 계속 추가해서 표시합니다. 페이지를 넘길 필요가 없습니다.",AutoSize=true};
    private readonly Label selection=new(){Text="0개 선택",AutoSize=true,Padding=new Padding(0,7,0,0)};
    private readonly Label status=new(){Text="아이폰을 연결하고 ‘아이폰 사진 불러오기’를 누르세요.",AutoSize=true,MaximumSize=new Size(840,0)};
    private readonly TextBox errors=new(){ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill};
    private readonly ListView gallery=new(){Dock=DockStyle.Fill,View=View.LargeIcon,CheckBoxes=true,MultiSelect=true,HideSelection=false,Alignment=ListViewAlignment.Top};
    private readonly ImageList images=new(){ImageSize=new Size(144,112),ColorDepth=ColorDepth.Depth32Bit};
    public MainWindow(Func<IPhoneBrowser>? createPhone=null,Func<PreviewReference,Bitmap?>? loadPreview=null)
    {
        this.createPhone=createPhone??(()=>new PhoneSession());this.loadPreview=loadPreview??Thumbnails.Load;
        Text="PhotoBridge · 전체 사진에서 골라 가져오기";Size=new Size(980,850);MinimumSize=new Size(760,700);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(248,247,242);Font=new Font("맑은 고딕",10);AutoScaleMode=AutoScaleMode.Dpi;
        destination.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),"PhotoBridge");gallery.LargeImageList=images;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=10};for(int i=0;i<10;i++)layout.RowStyles.Add(i==4?new RowStyle(SizeType.Percent,100):i==8?new RowStyle(SizeType.Absolute,65):new RowStyle(SizeType.AutoSize));Controls.Add(layout);
        layout.Controls.Add(new Label{Text="모두 보고, 골라 가져오기.",AutoSize=true,Font=new Font("맑은 고딕",23,FontStyle.Bold)});
        layout.Controls.Add(new Label{Text="USB 연결 → 아이폰 잠금 해제·신뢰 승인 → 사진 불러오기 → 원하는 사진 체크",AutoSize=true,ForeColor=Color.DimGray,Margin=new Padding(0,8,0,15)});
        var navigation=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};navigation.Controls.AddRange(new Control[]{connect,all,clear});layout.Controls.Add(navigation);location.Margin=new Padding(0,10,0,8);layout.Controls.Add(location);layout.Controls.Add(gallery);
        var path=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Top,ColumnCount=2};path.ColumnStyles.Add(new(SizeType.Percent,100));path.ColumnStyles.Add(new(SizeType.AutoSize));path.Controls.Add(destination);path.Controls.Add(choose);layout.Controls.Add(path);
        var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};actions.Controls.AddRange(new Control[]{import,cancel,selection});layout.Controls.Add(actions);status.Margin=new Padding(0,10,0,10);layout.Controls.Add(status);layout.Controls.Add(errors);
        layout.Controls.Add(new Label{Text="화면에 보이는 사진부터 축소판을 요청합니다. 미지원 파일은 이름으로 표시합니다. 아이폰 원본은 삭제하지 않습니다.",AutoSize=true,MaximumSize=new Size(840,0),Font=new Font("맑은 고딕",8),ForeColor=Color.DimGray,Margin=new Padding(0,10,0,0)});
        StartWorker(work,()=>phone?.Dispose());StartWorker(thumbnailWork,()=>{});
        ResetImages();viewport.Tick+=(_,_)=>RequestVisiblePreview();viewport.Start();
        connect.Click+=(_,_)=>OpenPhone();all.Click+=(_,_)=>{foreach(ListViewItem row in gallery.Items)row.Checked=true;};
        clear.Click+=(_,_)=>{selected.Clear();rendering=true;foreach(ListViewItem row in gallery.Items)row.Checked=false;rendering=false;UpdateButtons();};
        gallery.ItemCheck+=(_,e)=>{if(rendering)return;var entry=(BrowserEntry)gallery.Items[e.Index].Tag!;if(e.NewValue==CheckState.Checked)selected.Add(entry.Id);else selected.Remove(entry.Id);UpdateButtons();};
        choose.Click+=(_,_)=>{using var picker=new FolderBrowserDialog{Description="저장할 폴더",SelectedPath=destination.Text,UseDescriptionForTitle=true};if(picker.ShowDialog()==DialogResult.OK)destination.Text=picker.SelectedPath;};
        import.Click+=(_,_)=>ImportSelected();cancel.Click+=(_,_)=>{browseCancel?.Cancel();copyCancel?.Cancel();status.Text="중지 요청 중 · 이미 표시된 사진과 완료된 파일은 유지됩니다.";};
        FormClosing+=(_,e)=>{if(copying||listing){e.Cancel=true;browseCancel?.Cancel();copyCancel?.Cancel();status.Text="진행 중인 작업을 중지합니다. 끝나면 창을 닫아주세요.";}else{generation++;viewport.Stop();work.CompleteAdding();thumbnailWork.CompleteAdding();}};
        FormClosed+=(_,_)=>{viewport.Dispose();foreach(var image in ownedImages)image.Dispose();images.Dispose();};
    }
    private static void StartWorker(BlockingCollection<Action> queue,Action cleanup){var thread=new Thread(()=>{foreach(var action in queue.GetConsumingEnumerable())action();cleanup();});thread.SetApartmentState(ApartmentState.STA);thread.IsBackground=true;thread.Start();}
    private void Ui(Action action){if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke(()=>{if(!IsDisposed)action();});}catch(InvalidOperationException){}}
    internal void FinishSelfTest(){browseCancel?.Cancel();copyCancel?.Cancel();copying=loading=listing=false;Close();}
    private void UpdateButtons(){selection.Text=$"{selected.Count:N0}개 선택";connect.Enabled=choose.Enabled=!loading&&!copying;gallery.Enabled=!copying;all.Enabled=rows.Count>0&&!copying;clear.Enabled=!copying;import.Enabled=selected.Count>0&&!copying;cancel.Enabled=listing||copying;}
    private void ResetImages(){images.Images.Clear();foreach(var image in ownedImages)image.Dispose();ownedImages.Clear();slots.Clear();slotOwners.Clear();replaceSlot=0;var bitmap=new Bitmap(144,112);using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.FromArgb(242,242,235));using var font=new Font("맑은 고딕",10);using var format=new StringFormat{Alignment=StringAlignment.Center};g.DrawString("미리보기 대기",font,Brushes.Gray,new RectangleF(0,40,144,50),format);}ownedImages.Add(bitmap);images.Images.Add(bitmap);}
    private void OpenPhone()
    {
        browseCancel?.Cancel();browseCancel=new();var token=browseCancel.Token;int version=++generation;selected.Clear();rows.Clear();attempted.Clear();rendering=true;gallery.Items.Clear();rendering=false;ResetImages();loading=listing=true;errors.Clear();UpdateButtons();status.Text="아이폰을 여는 중 · 원본은 복사하지 않습니다.";
        work.Add(()=>
        {
            var batch=new List<BrowserEntry>();var watch=Stopwatch.StartNew();
            void Flush(){if(batch.Count==0)return;var entries=batch.ToArray();batch.Clear();watch.Restart();Ui(()=>{if(version!=generation)return;Append(entries);loading=false;UpdateButtons();location.Text=$"전체 사진 · {rows.Count:N0}개 표시 중";status.Text="사진 목록을 계속 추가하고 있습니다. 지금 보이는 사진부터 선택할 수 있습니다.";});}
            try
            {
                phone?.Dispose();phone=createPhone();phone.Connect();
                foreach(var entry in phone.BrowseAll(token)){token.ThrowIfCancellationRequested();batch.Add(entry);if(batch.Count==32||watch.ElapsedMilliseconds>=150)Flush();DrainPreviewRequest();}
                Flush();Ui(()=>{if(version!=generation)return;location.Text=$"전체 사진 · {rows.Count:N0}개";status.Text=rows.Count==0?"사진이 없습니다. 잠금·접근 권한·iCloud 원본 상태를 확인해주세요.":"목록 읽기 완료 · 원하는 사진을 체크하세요.";});
            }
            catch(OperationCanceledException){Flush();Ui(()=>{if(version==generation)status.Text="목록 읽기를 중지했습니다. 이미 표시된 사진은 선택할 수 있습니다.";});}
            catch(Exception ex){Flush();Ui(()=>{if(version!=generation)return;status.Text="목록을 끝까지 읽지 못했습니다. 표시된 파일은 유지됩니다.";errors.Text=ex.Message;});}
            finally{Ui(()=>{if(version==generation){loading=listing=false;UpdateButtons();}if(!previewRequests.IsEmpty&&!work.IsAddingCompleted)work.Add(DrainPreviewRequest);});}
        });
    }
    private void Append(IEnumerable<BrowserEntry> entries){rendering=true;gallery.BeginUpdate();foreach(var entry in entries){if(rows.ContainsKey(entry.Id))continue;var row=new ListViewItem(entry.Name){Name=entry.Id,Tag=entry,ImageIndex=0,Checked=selected.Contains(entry.Id)};rows[entry.Id]=row;gallery.Items.Add(row);}gallery.EndUpdate();rendering=false;}
    private IEnumerable<ListViewItem> VisibleRows()
    {
        if(gallery.Items.Count==0||gallery.ClientSize.Height<=0)yield break;
        int low=0,high=gallery.Items.Count;while(low<high){int mid=(low+high)/2;if(gallery.GetItemRect(mid).Bottom<0)low=mid+1;else high=mid;}
        for(int i=Math.Max(0,low-8);i<gallery.Items.Count;i++){var rect=gallery.GetItemRect(i);if(rect.Top>gallery.ClientSize.Height)yield break;if(rect.IntersectsWith(gallery.ClientRectangle))yield return gallery.Items[i];}
    }
    private void RequestVisiblePreview()
    {
        if(copying||loading||rows.Count==0)return;lock(previewLock){if(inFlight!=null)return;}
        var row=VisibleRows().FirstOrDefault(r=>!slots.ContainsKey(r.Name)&&!attempted.Contains(r.Name));if(row==null)return;
        int version=generation;string id=row.Name;attempted.Add(id);lock(previewLock)inFlight=(id,version);previewRequests.Enqueue((id,version));
        // During scanning DrainPreviewRequest is called cooperatively; after scanning this wakes the STA.
        if(!listing)work.Add(DrainPreviewRequest);
    }
    private void FinishPreview(string id,int version,Bitmap? image)
    {
        lock(previewLock){if(inFlight==(id,version))inFlight=null;}
        if(IsDisposed||!IsHandleCreated){image?.Dispose();return;}
        Ui(()=>{if(version!=generation||copying){image?.Dispose();return;}if(image==null||!rows.TryGetValue(id,out var row)){image?.Dispose();return;}
            int slot;if(ownedImages.Count<129){slot=ownedImages.Count;ownedImages.Add(image);images.Images.Add(image);}else{slot=1+(replaceSlot++%128);if(slotOwners.TryGetValue(slot,out var oldId)){slots.Remove(oldId);attempted.Remove(oldId);if(rows.TryGetValue(oldId,out var old))old.ImageIndex=0;}ownedImages[slot].Dispose();ownedImages[slot]=image;images.Images[slot]=image;}
            slots[id]=slot;slotOwners[slot]=id;row.ImageIndex=slot;
        });
    }
    private void DrainPreviewRequest()
    {
        if(!previewRequests.TryDequeue(out var request))return;
        try{var reference=phone?.Preview(request.Id);if(reference==null){FinishPreview(request.Id,request.Version,null);return;}thumbnailWork.Add(()=>{Bitmap? image=null;try{image=loadPreview(reference);}catch{}FinishPreview(request.Id,request.Version,image);});}
        catch{FinishPreview(request.Id,request.Version,null);}
    }
    private void ImportSelected()
    {
        if(copying||selected.Count==0)return;browseCancel?.Cancel();int version=++generation;listing=loading=false;copying=true;var ids=selected.ToArray();var path=destination.Text;copyCancel?.Dispose();copyCancel=new();var token=copyCancel.Token;errors.Clear();UpdateButtons();status.Text=$"선택한 {ids.Length}개만 가져오는 중…";
        work.Add(()=>{try{var media=phone!.Selected(ids,token);var result=new Importer().Run(media,path,token,p=>Ui(()=>status.Text=$"{p.Done}/{p.Total} · 새 파일 {p.Imported} · 이미 가져옴 {p.Skipped} · 실패 {p.Failed}\n{p.Message}"));Ui(()=>{status.Text=$"{(result.Cancelled?"중지됨":"가져오기 종료")} · 새 파일 {result.Imported} · 이미 가져옴 {result.Skipped} · 실패 {result.Failed}";errors.Text=string.Join(Environment.NewLine,result.Errors);});}catch(Exception ex){Ui(()=>{status.Text=ex is OperationCanceledException?"중지했습니다.":"가져오지 못했습니다.";errors.Text=ex.Message;});}finally{Ui(()=>{if(version!=generation)return;copying=false;UpdateButtons();});}});
    }
}
