using PhotoBridge.Core;
namespace PhotoBridge.Windows;
internal sealed partial class MainWindow
{
    internal async Task CheckSelectionFlow(DemoPhone fake)
    {
        static void Check(bool condition,string why){if(!condition)throw new Exception(why);}
        async Task Wait(Func<bool> condition){var until=DateTime.UtcNow.AddSeconds(20);while(!condition()){if(DateTime.UtcNow>until)throw new TimeoutException("UI state did not settle: "+status.Text);await Task.Delay(50);}}
        string output=Path.Combine(Path.GetTempPath(),"PhotoBridge-selection-"+Guid.NewGuid());destination.Text=output;
        try
        {
            connect.PerformClick();await Wait(()=>!listing&&gallery.Items.Count==fake.Total);
            Check(gallery.Items.Count==350,"all photos must appear without page controls");Check(fake.CopyCalls==0,"listing must never copy originals");
            await Wait(()=>fake.PreviewCalls>0);Check(fake.PreviewCalls<fake.Total/2,"offscreen photos must not be prefetched");
            gallery.Items[0].Checked=true;gallery.EnsureVisible(349);await Wait(()=>fake.PreviewIds.Contains("photo-349"));Check(gallery.Items[0].Checked,"selection retained after scroll");
            gallery.Items[349].Checked=true;Check(selected.Count==2&&import.Enabled,"first and last photo selected");
            clear.PerformClick();Check(selected.Count==0&&!import.Enabled,"clear all selections");gallery.Items[349].Checked=true;
            import.PerformClick();await Wait(()=>!copying);Check(fake.CopyCalls==1,"copy selected file only");Check(fake.LastSelection.SequenceEqual(new[]{"photo-349"}),"last photo copied correctly");Check(Directory.GetFiles(output).Length==1,"unselected originals not saved");
            // A deliberately blocked thumbnail must not hold the metadata/copy STA.
            fake.BlockPreviews=true;fake.Delay=1;fake.Total=2500;connect.PerformClick();
            await Wait(()=>gallery.Items.Count>0&&listing);Check(gallery.Items.Count<fake.Total,"initial results before full enumeration");
            gallery.Items[0].Checked=true;Check(import.Enabled,"copy available before listing finishes");
            await Wait(()=>fake.PreviewBlocked.IsSet);import.PerformClick();await Wait(()=>!copying);
            Check(fake.CopyCalls==2,"slow thumbnail did not block selected import");Check(ownedImages.Count<=129,"preview cache bounded");
        }
        finally{fake.PreviewRelease.Set();browseCancel?.Cancel();if(Directory.Exists(output))Directory.Delete(output,true);}
    }
}
internal sealed class DemoPhone : IPhoneBrowser
{
    public int CopyCalls,PreviewCalls,Total=350,Delay;public volatile bool BlockPreviews;public readonly ManualResetEventSlim PreviewBlocked=new(),PreviewRelease=new();
    public readonly System.Collections.Concurrent.ConcurrentBag<string> PreviewIds=new();public string[] LastSelection=[];
    public void Connect(){}
    public IEnumerable<BrowserEntry> BrowseAll(CancellationToken cancellation)
    {
        for(int i=0;i<Total;i++){cancellation.ThrowIfCancellationRequested();if(Delay>0)Thread.Sleep(Delay);yield return new("photo-"+i,$"IMG_{i:D4}.JPG");}
    }
    public PreviewReference? Preview(string id)=>new(null,id);
    public Bitmap? LoadPreview(PreviewReference reference){Interlocked.Increment(ref PreviewCalls);PreviewIds.Add(reference.Path!);if(BlockPreviews){PreviewBlocked.Set();PreviewRelease.Wait(TimeSpan.FromSeconds(25));}return new Bitmap(144,112);}
    public IReadOnlyList<IMedia> Selected(IReadOnlyList<string> ids,CancellationToken cancellation){LastSelection=ids.ToArray();return ids.Select(id=>(IMedia)new DemoMedia(this,id)).ToArray();}
    public void Dispose(){}
    private sealed class DemoMedia(DemoPhone owner,string id):IMedia
    {
        public string Identity=>id;public string Name=>$"IMG_{int.Parse(id[6..]):D4}.JPG";public long Size=>3;
        public void CopyTo(string directory,CancellationToken cancellation){owner.CopyCalls++;File.WriteAllBytes(Path.Combine(directory,Name),new byte[]{1,2,3});}
    }
}
