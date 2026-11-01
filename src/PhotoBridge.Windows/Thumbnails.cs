using System.Runtime.InteropServices;
namespace PhotoBridge.Windows;
internal static class Thumbnails
{
    [StructLayout(LayoutKind.Sequential)] private struct NativeSize { public int Width,Height; }
    [ComImport,Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageFactory { [PreserveSig] int GetImage(NativeSize size,int flags,out IntPtr bitmap); }
    [DllImport("shell32.dll",PreserveSig=true)] private static extern int SHGetIDListFromObject([MarshalAs(UnmanagedType.IUnknown)] object item,out IntPtr pidl);
    [DllImport("shell32.dll")] private static extern uint ILGetSize(IntPtr pidl);
    [DllImport("shell32.dll",PreserveSig=true)] private static extern int SHCreateItemFromIDList(IntPtr pidl,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IImageFactory factory);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=true)] private static extern int SHCreateItemFromParsingName(string path,IntPtr bind,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IImageFactory factory);
    [DllImport("gdi32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr bitmap);
    // Run on the device STA. Only immutable PIDL bytes/path leave this apartment, never an RCW.
    public static PreviewReference? Reference(object item)
    {
        IntPtr pidl=IntPtr.Zero;
        try{if(SHGetIDListFromObject(item,out pidl)==0){uint size=ILGetSize(pidl);if(size>0&&size<1024*1024){var bytes=new byte[size];Marshal.Copy(pidl,bytes,0,bytes.Length);return new(bytes,null);}}}catch{}
        finally{if(pidl!=IntPtr.Zero)Marshal.FreeCoTaskMem(pidl);}
        try{dynamic entry=item;string path=Convert.ToString(entry.Path)??"";return string.IsNullOrEmpty(path)?null:new(null,path);}catch{return null;}
    }
    // Run on a separate thumbnail STA so slow GetImage cannot block enumeration or the copy queue.
    public static Bitmap? Load(PreviewReference reference)
    {
        IntPtr pidl=IntPtr.Zero,bitmap=IntPtr.Zero;IImageFactory? factory=null;
        try
        {
            var iid=typeof(IImageFactory).GUID;
            if(reference.IdList is {} bytes){pidl=Marshal.AllocCoTaskMem(bytes.Length);Marshal.Copy(bytes,0,pidl,bytes.Length);if(SHCreateItemFromIDList(pidl,ref iid,out factory)!=0)return null;}
            else if(reference.Path==null||SHCreateItemFromParsingName(reference.Path,IntPtr.Zero,ref iid,out factory)!=0)return null;
            if(factory.GetImage(new(){Width=144,Height=112},0x8,out bitmap)!=0||bitmap==IntPtr.Zero)return null;
            using var image=Image.FromHbitmap(bitmap);return new Bitmap(image);
        }
        catch{return null;}
        finally{if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(pidl!=IntPtr.Zero)Marshal.FreeCoTaskMem(pidl);if(factory!=null)Marshal.ReleaseComObject(factory);}
    }
}
