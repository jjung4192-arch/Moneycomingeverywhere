using System.Diagnostics;
using System.Runtime.InteropServices;
using PhotoBridge.Core;
namespace PhotoBridge.Windows;
public sealed partial class PhoneSession : IDisposable
{
    private readonly dynamic shell;
    public PhoneSession(){var type=Type.GetTypeFromProgID("Shell.Application")??throw new IOException("Windows 사진 접근 기능을 사용할 수 없습니다.");shell=Activator.CreateInstance(type)!;}
    private static readonly HashSet<string> Extensions=new(StringComparer.OrdinalIgnoreCase){".jpg",".jpeg",".heic",".heif",".png",".gif",".tif",".tiff",".dng",".mov",".mp4",".m4v",".aae"};
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
