using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DeckDeckDeck.App.Native;

namespace DeckDeckDeck.App.Services;

public sealed class ShellFileIconExtractor : IFileIconExtractor
{
    private const int IconPixels = 96;

    public bool TryExtractIcon(string sourcePath, string destinationPngPath)
    {
        var factory = CreateImageFactory(sourcePath);
        if (factory is null)
        {
            return false;
        }

        var hBitmap = IntPtr.Zero;

        try
        {
            var hr = factory.GetImage(
                new NativeSize { Width = IconPixels, Height = IconPixels },
                ShellImageFlags.BiggerSizeOk | ShellImageFlags.IconOnly,
                out hBitmap);
            if (hr < 0 || hBitmap == IntPtr.Zero)
            {
                return false;
            }

            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bitmapSource.Freeze();

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPngPath)!);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

            using var stream = File.Create(destinationPngPath);
            encoder.Save(stream);

            return true;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
            {
                Gdi32.DeleteObject(hBitmap);
            }

            Marshal.ReleaseComObject(factory);
        }
    }

    private static IShellItemImageFactory? CreateImageFactory(string sourcePath)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        var hr = SHCreateItemFromParsingName(sourcePath, IntPtr.Zero, ref iid, out var factory);

        return hr < 0 ? null : factory;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string pszPath,
        IntPtr pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(NativeSize size, ShellImageFlags flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;

        public int Height;
    }

    [Flags]
    private enum ShellImageFlags
    {
        BiggerSizeOk = 0x00000001,
        IconOnly = 0x00000004
    }
}
