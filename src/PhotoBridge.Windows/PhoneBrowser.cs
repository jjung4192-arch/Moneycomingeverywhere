using PhotoBridge.Core;
using System.Security.Cryptography;
using System.Text;
namespace PhotoBridge.Windows;
public record BrowserEntry(string Id,string Name,bool IsFolder);
public record BrowserPage(string FolderId,int Offset,int NextOffset,bool HasMore,IReadOnlyList<BrowserEntry> Entries);
public interface IPhoneBrowser : IDisposable
{
    string Connect();
    BrowserPage ReadPage(string folderId,int offset,CancellationToken cancellation);
    Bitmap? Thumbnail(string id,CancellationToken cancellation);
    IReadOnlyList<IMedia> Selected(IReadOnlyList<string> ids,CancellationToken cancellation);
}
public sealed partial class PhoneSession : IPhoneBrowser
{
    private sealed record Folder(object Native,string Device,string Relative);
    private sealed record Item(object Native,string Device,string Relative,string Name);
    private readonly Dictionary<string,Folder> folders=new();
    private readonly Dictionary<string,Item> files=new();
    private string? rootId;
    public string Connect()
    {
        dynamic computer=shell.NameSpace(17);var devices=new List<object>();
        foreach(dynamic entry in computer.Items())if(((string)entry.Name).Contains("iPhone",StringComparison.OrdinalIgnoreCase))devices.Add(entry);
        if(devices.Count!=1)throw new IOException(devices.Count==0?"아이폰을 찾지 못했습니다. USB 연결·잠금 해제·신뢰 승인을 확인해주세요.":"아이폰을 한 대만 연결해주세요.");
        dynamic device=devices[0];string devicePath=Convert.ToString(device.Path)??"iPhone";
        folders.Clear();files.Clear();rootId=Guid.NewGuid().ToString("N");folders.Add(rootId,new((object)device.GetFolder,devicePath,""));return rootId;
    }
    public BrowserPage ReadPage(string folderId,int offset,CancellationToken cancellation)
    {
        if(offset<0)throw new ArgumentOutOfRangeException(nameof(offset));
        var folder=folders[folderId];dynamic native=folder.Native;dynamic items=native.Items();int count=Convert.ToInt32(items.Count);var result=new List<BrowserEntry>();int index=offset;
        // No recursive scan; only twenty entries from the folder the user opened.
        for(;index<Math.Min(count,offset+20);index++)
        {
            cancellation.ThrowIfCancellationRequested();dynamic item=items.Item(index);bool isFolder=(bool)item.IsFolder;string name=Convert.ToString(item.Name)??"";
            // Fetch only the filename needed for display. Size, dates and hashes are deferred until selection.
            if(!isFolder){try{string fullName=Convert.ToString(item.ExtendedProperty("System.FileName"))??"";if(!string.IsNullOrWhiteSpace(fullName))name=fullName;}catch{}}
            var relative=folder.Relative+"/"+name;var id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(folder.Device+relative)));
            if(isFolder){folders[id]=new((object)item.GetFolder,folder.Device,relative);result.Add(new(id,name,true));}
            else if(Extensions.Contains(Path.GetExtension(name))){files[id]=new((object)item,folder.Device,relative,name);result.Add(new(id,name,false));}
        }
        return new(folderId,offset,index,index<count,result);
    }
    public Bitmap? Thumbnail(string id,CancellationToken cancellation){cancellation.ThrowIfCancellationRequested();return files.TryGetValue(id,out var entry)?Thumbnails.Load(entry.Native):null;}
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
