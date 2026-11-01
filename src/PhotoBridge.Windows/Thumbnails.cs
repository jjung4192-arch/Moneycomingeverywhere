using System.Runtime.InteropServices;
namespace PhotoBridge.Windows;
internal static class Thumbnails
{
    [StructLayout(LayoutKind.Sequential)] private struct NativeSize { public int Width,Height; }
    [ComImport,Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageFactory { [PreserveSig] int GetImage(NativeSize size,int flags,out IntPtr bitmap); }
    [DllImport("shell32.dll",PreserveSig=true)] private static extern int SHGetIDListFromObject([MarshalAs(UnmanagedType.IUnknown)] object item,out IntPtr pidl);
    [DllImport("shell32.dll",PreserveSig=true)] private static extern int SHCreateItemFromIDList(IntPtr pidl,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IImageFactory factory);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=true)] private static extern int SHCreateItemFromParsingName(string path,IntPtr bind,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IImageFactory factory);
    [DllImport("gdi32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr bitmap);
    public static Bitmap? Load(object item)
    {
        IntPtr pidl=IntPtr.Zero,bitmap=IntPtr.Zero;IImageFactory? factory=null;
        try
        {
            var iid=typeof(IImageFactory).GUID;
            if(SHGetIDListFromObject(item,out pidl)==0)
            {if(SHCreateItemFromIDList(pidl,ref iid,out factory)!=0)return null;}
            else
            {dynamic entry=item;string path=Convert.ToString(entry.Path)??"";if(string.IsNullOrEmpty(path)||SHCreateItemFromParsingName(path,IntPtr.Zero,ref iid,out factory)!=0)return null;}
            // Thumbnail-only: do not substitute a generic file icon for a real preview.
            if(factory.GetImage(new(){Width=144,Height=112},0x8,out bitmap)!=0||bitmap==IntPtr.Zero)return null;
            using var image=Image.FromHbitmap(bitmap);return new Bitmap(image);
        }
        catch{return null;}
        finally{if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(pidl!=IntPtr.Zero)Marshal.FreeCoTaskMem(pidl);if(factory!=null)Marshal.ReleaseComObject(factory);}
    }
}
