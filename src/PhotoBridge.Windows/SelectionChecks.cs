using PhotoBridge.Core;
namespace PhotoBridge.Windows;
internal sealed partial class MainWindow
{
    internal async Task CheckSelectionFlow(DemoPhone fake)
    {
        static void Check(bool condition,string why){if(!condition)throw new Exception(why);}
        async Task Wait(Func<bool> condition){var until=DateTime.UtcNow.AddSeconds(15);while(!condition()){if(DateTime.UtcNow>until)throw new TimeoutException("UI state did not settle: "+status.Text);await Task.Delay(50);}}
        string output=Path.Combine(Path.GetTempPath(),"PhotoBridge-selection-"+Guid.NewGuid());destination.Text=output;
        try
        {
            connect.PerformClick();await Wait(()=>!loading&&current=="root");Check(gallery.Items.Count==1,"root folder");Check(fake.CopyCalls==0,"opening phone must not copy originals");
            gallery.Items[0].Checked=true;Check(selected.Count==0,"folder cannot be selected for copying");
            history.Push((current!,0,location.Text));LoadPage("photos",0,"DCIM");await Wait(()=>!loading&&current=="photos"&&gallery.Items.Count==20);
            Check(fake.CopyCalls==0,"browsing and thumbnails must not call CopyTo");gallery.Items[0].Checked=true;Check(selected.Count==1&&import.Enabled,"first photo selected");
            next.PerformClick();await Wait(()=>!loading&&offset==20);Check(gallery.Items.Count==3,"last page has three photos");Check(selected.Count==1,"selection persists across pages");
            previous.PerformClick();await Wait(()=>!loading&&offset==0);Check(gallery.Items[0].Checked,"selected row restored");
            clear.PerformClick();Check(selected.Count==0&&!import.Enabled,"clear selection");gallery.Items[1].Checked=true;
            import.PerformClick();await Wait(()=>!copying);Check(fake.CopyCalls==1,"copy selected file only");Check(fake.LastSelection.SequenceEqual(new[]{"photo-1"}),"correct selected ID");Check(File.Exists(Path.Combine(output,"IMG_0001.JPG")),"chosen file saved");Check(Directory.GetFiles(output).Length==1,"unselected originals never saved");
        }
        finally{browseCancel?.Cancel();if(Directory.Exists(output))Directory.Delete(output,true);}
    }
}
internal sealed class DemoPhone : IPhoneBrowser
{
    public int CopyCalls;public string[] LastSelection=[];
    public string Connect()=>"root";
    public BrowserPage ReadPage(string id,int offset,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if(id=="root")return new(id,0,1,false,new[]{new BrowserEntry("photos","DCIM",true)});
        return new(id,offset,Math.Min(23,offset+20),offset+20<23,Enumerable.Range(offset,Math.Min(20,23-offset)).Select(i=>new BrowserEntry("photo-"+i,$"IMG_{i:D4}.JPG",false)).ToArray());
    }
    public Bitmap? Thumbnail(string id,CancellationToken cancellation){cancellation.ThrowIfCancellationRequested();return new Bitmap(144,112);}
    public IReadOnlyList<IMedia> Selected(IReadOnlyList<string> ids,CancellationToken cancellation){LastSelection=ids.ToArray();return ids.Select(id=>(IMedia)new DemoMedia(this,id)).ToArray();}
    public void Dispose(){}
    private sealed class DemoMedia(DemoPhone owner,string id):IMedia
    {
        public string Identity=>id;public string Name=>$"IMG_{int.Parse(id[6..]):D4}.JPG";public long Size=>3;
        public void CopyTo(string directory,CancellationToken cancellation){owner.CopyCalls++;File.WriteAllBytes(Path.Combine(directory,Name),new byte[]{1,2,3});}
    }
}
