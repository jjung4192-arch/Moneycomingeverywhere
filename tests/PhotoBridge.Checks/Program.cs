using PhotoBridge.Core;
static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
var root=Path.Combine(Path.GetTempPath(),"PhotoBridge-test-"+Guid.NewGuid());Directory.CreateDirectory(root);
try{
 var importer=new Importer();var first=new Fake("folder/1","IMG_0001.HEIC",new byte[]{1,2,3});var second=new Fake("folder/2","IMG_0001.HEIC",new byte[]{4,5,6});
 var batch=new IMedia[]{first,second};var result=importer.Run(batch,root,CancellationToken.None,_=>{});Check(result.Imported==2&&result.Failed==0,"initial import");Check(Directory.GetFiles(root).Length==2,"same-name files preserved");
 result=importer.Run(batch,root,CancellationToken.None,_=>{});Check(result.Skipped==2,"deduplication");
 File.WriteAllBytes(Path.Combine(root,"IMG_0001.HEIC"),new byte[]{9,9,9});result=importer.Run(batch,root,CancellationToken.None,_=>{});Check(result.Imported==1&&result.Skipped==1,"tamper detected and repaired");Check(File.ReadAllBytes(Path.Combine(root,"IMG_0001.HEIC")).SequenceEqual(new byte[]{9,9,9}),"existing file not overwritten");
 var partial=new Fake("broken","broken.MOV",new byte[]{7,8,9},true);result=importer.Run(new[]{partial},root,CancellationToken.None,_=>{});Check(result.Failed==1&&!File.Exists(Path.Combine(root,"broken.MOV")),"partial copy rejected");
 using var cancelled=new CancellationTokenSource();cancelled.Cancel();result=importer.Run(batch,root,cancelled.Token,_=>{});Check(result.Cancelled&&result.Imported==0,"cancellation");
 result=importer.Run(new IMedia[]{partial,new Fake("retry","new.JPG",new byte[]{3,2,1})},root,CancellationToken.None,_=>{});Check(result.Failed==1&&result.Imported==1,"continue after failed file");
 using(var midCancel=new CancellationTokenSource()){result=importer.Run(new IMedia[]{new Interrupt(midCancel)},root,midCancel.Token,_=>{});Check(result.Cancelled&&!File.Exists(Path.Combine(root,"interrupted.JPG")),"cancel mid-copy without publishing partial");}
 result=importer.Run(new[]{new Fake("interrupted","interrupted.JPG",new byte[]{1,4,9})},root,CancellationToken.None,_=>{});Check(result.Imported==1,"retry interrupted file");
 using(var held=new FileStream(Path.Combine(root,".photo-bridge","import.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)){bool locked=false;try{importer.Run(batch,root,CancellationToken.None,_=>{});}catch(IOException){locked=true;}Check(locked,"parallel import lock");}
 // Simulate a crash after moving a complete file but before persisting its entry.
 File.WriteAllBytes(Path.Combine(root,"orphan.JPG"),new byte[]{5,6});result=importer.Run(new[]{new Fake("orphan","orphan.JPG",new byte[]{5,6})},root,CancellationToken.None,_=>{});Check(result.Imported==1&&Directory.GetFiles(root,"orphan*").Length==1,"recover interrupted manifest write");
 File.WriteAllText(Path.Combine(root,".photo-bridge","manifest.json"),"broken json");bool threw=false;try{importer.Run(batch,root,CancellationToken.None,_=>{});}catch(System.Text.Json.JsonException){threw=true;}Check(threw,"corrupt manifest must not be silently reset");
 Console.WriteLine("PASS: original copy, same-name collision, deduplication, tamper detection, partial rejection, cancellation, failure isolation, mid-copy cancellation/retry, parallel lock, crash recovery, corrupt manifest protection.");
}finally{Directory.Delete(root,true);}
sealed class Fake(string identity,string name,byte[] bytes,bool partial=false):IMedia
{
 public string Identity=>identity;public string Name=>name;public long Size=>bytes.Length;
 public void CopyTo(string stagingDirectory,CancellationToken cancellation){File.WriteAllBytes(Path.Combine(stagingDirectory,name),partial?bytes[..1]:bytes);}
}

sealed class Interrupt(CancellationTokenSource cancellation):IMedia
{
 public string Identity=>"interrupted";public string Name=>"interrupted.JPG";public long Size=>3;
 public void CopyTo(string directory,CancellationToken token){File.WriteAllBytes(Path.Combine(directory,Name),new byte[]{1});cancellation.Cancel();token.ThrowIfCancellationRequested();}
}
