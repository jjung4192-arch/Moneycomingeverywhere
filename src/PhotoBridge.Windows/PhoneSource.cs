using System.Diagnostics;
using System.Runtime.InteropServices;
using PhotoBridge.Core;
namespace PhotoBridge.Windows;
public sealed class PhoneSession : IDisposable
{
    private readonly dynamic shell;
    public PhoneSession(){var type=Type.GetTypeFromProgID("Shell.Application")??throw new IOException("Windows 사진 접근 기능을 사용할 수 없습니다.");shell=Activator.CreateInstance(type)!;}
    public IReadOnlyList<IMedia> Find(CancellationToken cancellation,Action<string> status)
    {
        dynamic computer=shell.NameSpace(17);var devices=new List<object>();
        foreach(dynamic item in computer.Items())
        {string name=Convert.ToString(item.Name)??"";if(name.Contains("iPhone",StringComparison.OrdinalIgnoreCase))devices.Add(item);}
        if(devices.Count==0)throw new IOException("아이폰을 찾지 못했습니다. 데이터 전송용 USB 케이블로 연결하고, 잠금을 푼 뒤 ‘이 컴퓨터를 신뢰’를 눌러주세요. Windows 탐색기 ‘내 PC’에 iPhone이 보여야 합니다.");
        if(devices.Count>1)throw new IOException("아이폰이 여러 대 연결되어 있습니다. 한 대만 연결해주세요.");
        dynamic device=devices[0];string devicePath=Convert.ToString(device.Path)??Convert.ToString(device.Name)??"iPhone";
        var found=new List<IMedia>();Walk(device.GetFolder,devicePath,"",found,cancellation,status,0);
        if(found.Count==0)throw new IOException("가져올 사진을 찾지 못했습니다. 아이폰 잠금과 접근 승인을 확인하세요. iCloud에만 있는 파일은 아이폰에서 원본을 먼저 내려받아야 할 수 있습니다.");
        return found;
    }
    private static readonly HashSet<string> Extensions=new(StringComparer.OrdinalIgnoreCase){".jpg",".jpeg",".heic",".heif",".png",".gif",".tif",".tiff",".dng",".mov",".mp4",".m4v",".aae"};
    private void Walk(dynamic folder,string device,string relative,List<IMedia> found,CancellationToken cancellation,Action<string> status,int depth)
    {
        cancellation.ThrowIfCancellationRequested();if(depth>20)throw new IOException("아이폰 폴더 구조를 확인할 수 없습니다.");
        foreach(dynamic item in folder.Items())
        {
            cancellation.ThrowIfCancellationRequested();string name=Convert.ToString(item.Name)??"";string path=relative+"/"+name;
            if((bool)item.IsFolder){status($"사진 찾는 중 · {name}");Walk(item.GetFolder,device,path,found,cancellation,status,depth+1);}
            else if(Extensions.Contains(Path.GetExtension(name)))
            {
                long size;try{size=Convert.ToInt64(item.ExtendedProperty("System.Size"));}catch{size=0;}
                if(size<=0){try{size=Convert.ToInt64(item.Size);}catch{size=0;}}
                string modified;try{modified=Convert.ToString(item.ModifyDate,System.Globalization.CultureInfo.InvariantCulture)??"";}catch{modified="";}
                found.Add(new ShellMedia(shell,item,name,size,device+path+"|"+size+"|"+modified));
            }
        }
    }
    public void Dispose(){try{Marshal.FinalReleaseComObject(shell);}catch{}}
}
internal sealed class ShellMedia : IMedia
{
    private readonly dynamic shell,item;
    public string Identity{get;}public string Name{get;}public long Size{get;}
    public ShellMedia(dynamic shell,dynamic item,string name,long size,string identity){this.shell=shell;this.item=item;Name=name;Size=size;Identity=identity;}
    public void CopyTo(string stagingDirectory,CancellationToken cancellation)
    {
        dynamic destination=shell.NameSpace(stagingDirectory)??throw new IOException("저장 폴더를 열 수 없습니다.");
        destination.CopyHere(item,4|16|512|1024);
        var timer=Stopwatch.StartNew();long? completeSince=null;
        while(timer.Elapsed<TimeSpan.FromMinutes(10))
        {
            cancellation.ThrowIfCancellationRequested();
            var files=Directory.GetFiles(stagingDirectory);
            if(files.Length==1)
            {
                try
                {
                    using var stream=new FileStream(files[0],FileMode.Open,FileAccess.Read,FileShare.None);
                    if(stream.Length==Size)
                    {
                        completeSince??=timer.ElapsedMilliseconds;
                        if(timer.ElapsedMilliseconds-completeSince>=1000)return;
                    }
                    else completeSince=null;
                }
                catch(IOException){completeSince=null;}
            }
            // Pump STA messages while Windows Shell completes its asynchronous copy.
            Application.DoEvents();Thread.Sleep(100);
        }
        throw new IOException("10분 동안 전송이 완료되지 않았습니다. 연결을 확인하고 다시 가져오세요. 큰 동영상은 더 오래 걸릴 수 있습니다.");
    }
}
