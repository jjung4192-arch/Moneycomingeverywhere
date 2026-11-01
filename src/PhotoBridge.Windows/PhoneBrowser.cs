using PhotoBridge.Core;
using System.Security.Cryptography;
using System.Text;
namespace PhotoBridge.Windows;
public record BrowserEntry(string Id,string Name);
public record PreviewReference(byte[]? IdList,string? Path);
public interface IPhoneBrowser : IDisposable
{
    void Connect();
    IEnumerable<BrowserEntry> BrowseAll(CancellationToken cancellation);
    PreviewReference? Preview(string id);
    IReadOnlyList<IMedia> Selected(IReadOnlyList<string> ids,CancellationToken cancellation);
}
public sealed partial class PhoneSession : IPhoneBrowser
{
    private sealed record Item(object Native,string Device,string Relative,string Name);
    private readonly Dictionary<string,Item> files=new();private object? root;private string devicePath="";
    public void Connect()
    {
        dynamic computer=shell.NameSpace(17);var devices=new List<object>();
        foreach(dynamic entry in computer.Items())if(((string)entry.Name).Contains("iPhone",StringComparison.OrdinalIgnoreCase))devices.Add(entry);
        if(devices.Count!=1)throw new IOException(devices.Count==0?"아이폰을 찾지 못했습니다. USB 연결·잠금 해제·신뢰 승인을 확인해주세요.":"아이폰을 한 대만 연결해주세요.");
        dynamic device=devices[0];devicePath=Convert.ToString(device.Path)??"iPhone";files.Clear();root=(object)device.GetFolder;
    }
    public IEnumerable<BrowserEntry> BrowseAll(CancellationToken cancellation)
    {
        if(root==null)throw new InvalidOperationException("아이폰을 먼저 연결해주세요.");
        return Walk(root,"",0,cancellation);
    }
    private IEnumerable<BrowserEntry> Walk(object native,string relative,int depth,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();if(depth>20)throw new IOException("사진 폴더 구조를 확인하지 못했습니다.");
        dynamic folder=native;
        // Enumerate once, without Count, repeated Items(), size/date lookups or a page limit.
        foreach(dynamic item in folder.Items())
        {
            cancellation.ThrowIfCancellationRequested();bool isFolder=(bool)item.IsFolder;string name=Convert.ToString(item.Name)??"";
            if(isFolder){foreach(var entry in Walk((object)item.GetFolder,relative+"/"+name,depth+1,cancellation))yield return entry;continue;}
            if(string.IsNullOrEmpty(Path.GetExtension(name)))
                try{string full=Convert.ToString(item.ExtendedProperty("System.FileName"))??"";if(!string.IsNullOrEmpty(full))name=full;}catch{}
            if(!Extensions.Contains(Path.GetExtension(name)))continue;
            var path=relative+"/"+name;var id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(devicePath+path)));
            files[id]=new((object)item,devicePath,path,name);yield return new(id,name);
        }
    }
    public PreviewReference? Preview(string id)=>files.TryGetValue(id,out var entry)?Thumbnails.Reference(entry.Native):null;
    public IReadOnlyList<IMedia> Selected(IReadOnlyList<string> ids,CancellationToken cancellation)
    {
        var selected=new List<IMedia>();foreach(var id in ids)
        {
            cancellation.ThrowIfCancellationRequested();if(!files.TryGetValue(id,out var entry))throw new IOException("선택한 사진을 다시 확인해주세요.");dynamic item=entry.Native;
            long size;try{size=Convert.ToInt64(item.ExtendedProperty("System.Size"));}catch{size=0;}
            if(size<=0){try{size=Convert.ToInt64(item.Size);}catch{size=0;}}
            string modified;try{modified=Convert.ToString(item.ModifyDate,System.Globalization.CultureInfo.InvariantCulture)??"";}catch{modified="";}
            selected.Add(new ShellMedia(shell,item,entry.Name,size,entry.Device+entry.Relative+"|"+size+"|"+modified));
        }
        return selected;
    }
}
