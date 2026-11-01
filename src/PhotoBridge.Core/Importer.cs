using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace PhotoBridge.Core;
public interface IMedia
{
    string Identity { get; }
    string Name { get; }
    long Size { get; }
    void CopyTo(string stagingDirectory, CancellationToken cancellation);
}
public record Entry(string File, long Size, string Hash);
public record Progress(int Done, int Total, int Imported, int Skipped, int Failed, string Message);
public record ImportResult(int Imported, int Skipped, int Failed, bool Cancelled, IReadOnlyList<string> Errors);
public sealed class Importer
{
    private static string Hash(string path) { using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f)); }
    private static string Key(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static bool Within(string root,string file)=>Path.GetFullPath(file).StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    public ImportResult Run(IReadOnlyList<IMedia> media,string destination,CancellationToken cancellation,Action<Progress> progress)
    {
        Directory.CreateDirectory(destination);
        var root=Path.GetFullPath(destination);
        var state=Path.Combine(root,".photo-bridge");Directory.CreateDirectory(state);
        // A second import must not modify this destination concurrently.
        using var gate=new FileStream(Path.Combine(state,"import.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var manifest=Path.Combine(state,"manifest.json");
        var entries=File.Exists(manifest)?JsonSerializer.Deserialize<Dictionary<string,Entry>>(File.ReadAllText(manifest))??throw new InvalidDataException("가져오기 기록을 읽을 수 없습니다."):new Dictionary<string,Entry>();
        int imported=0,skipped=0,failed=0,done=0;var errors=new List<string>();bool cancelled=false;
        foreach(var source in media)
        {
            if(cancellation.IsCancellationRequested){cancelled=true;break;}
            string? stage=null;
            try
            {
                progress(new(done,media.Count,imported,skipped,failed,$"확인 중 · {source.Name}"));
                if(source.Size<=0)throw new IOException("원본 크기를 확인할 수 없습니다. 아이폰에서 원본을 다운로드한 뒤 다시 시도하세요.");
                var key=Key(source.Identity);
                if(entries.TryGetValue(key,out var saved))
                {
                    var file=Path.Combine(root,saved.File);
                    if(!Within(root,file))throw new InvalidDataException("저장 기록에 잘못된 경로가 있습니다.");
                    if(File.Exists(file)&&new FileInfo(file).Length==source.Size&&saved.Size==source.Size&&Hash(file)==saved.Hash){skipped++;continue;}
                }
                stage=Path.Combine(state,"staging",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
                source.CopyTo(stage,cancellation);cancellation.ThrowIfCancellationRequested();
                var files=Directory.GetFiles(stage);
                if(files.Length!=1)throw new IOException("복사된 파일을 확인할 수 없습니다.");
                var staged=files[0];
                using(var check=new FileStream(staged,FileMode.Open,FileAccess.Read,FileShare.None))
                    if(check.Length!=source.Size)throw new IOException("원본과 복사본의 크기가 다릅니다. 다음 가져오기에서 다시 시도합니다.");
                var hash=Hash(staged);
                var safeName=Path.GetFileName(source.Name);
                if(string.IsNullOrWhiteSpace(safeName)||safeName is "." or ".."||safeName.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new IOException("저장할 수 없는 파일 이름입니다.");
                var target=Path.Combine(root,safeName);
                if(File.Exists(target))
                {
                    // Recover a completed copy if a prior run stopped before writing its manifest.
                    if(new FileInfo(target).Length==source.Size&&Hash(target)==hash){File.Delete(staged);}
                    else
                    {
                        var stem=Path.GetFileNameWithoutExtension(safeName);var ext=Path.GetExtension(safeName);int suffix=0;
                        do{target=Path.Combine(root,$"{stem}_{key[..8]}{(suffix==0?"":"_"+suffix)}{ext}");suffix++;}while(File.Exists(target)&&!(new FileInfo(target).Length==source.Size&&Hash(target)==hash));
                        if(File.Exists(target))File.Delete(staged);else File.Move(staged,target,false);
                    }
                }
                else File.Move(staged,target,false);
                entries[key]=new(Path.GetRelativePath(root,target),source.Size,hash);
                var temp=Path.Combine(state,"manifest.new");File.WriteAllText(temp,JsonSerializer.Serialize(entries));File.Move(temp,manifest,true);
                imported++;
            }
            catch(OperationCanceledException){cancelled=true;break;}
            catch(Exception ex){failed++;errors.Add($"{source.Name}: {ex.Message}");}
            finally
            {
                // A COM copy can still be running after cancellation: preserve staging for diagnosis.
                if(stage!=null&&!cancellation.IsCancellationRequested){try{Directory.Delete(stage,true);}catch{}}
                done++;progress(new(done,media.Count,imported,skipped,failed,source.Name));
            }
        }
        return new(imported,skipped,failed,cancelled,errors);
    }
}
