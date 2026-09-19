using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ScreenCanvas.Presentation;

public sealed class FreezeFrameService : IDisposable
{
    private FreezeFrameWindow? _window;
    public bool IsFrozen => _window?.IsVisible == true;

    public void Freeze()
    {
        Unfreeze();
        var bounds = new Rectangle((int)SystemParameters.VirtualScreenLeft, (int)SystemParameters.VirtualScreenTop,
            (int)SystemParameters.VirtualScreenWidth, (int)SystemParameters.VirtualScreenHeight);
        using var bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
        var handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(handle, nint.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            _window = new FreezeFrameWindow(source);
        }
        finally { DeleteObject(handle); }
        _window.Closed += (_, _) => _window = null;
        _window.Show();
        _window.Activate();
    }

    public void Unfreeze() { _window?.Close(); _window = null; }
    public void Dispose() => Unfreeze();

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint handle);
}

internal sealed class FreezeFrameWindow : EmergencySafeWindow
{
    internal FreezeFrameWindow(BitmapSource source)
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Content = new System.Windows.Controls.Image
        {
            Source = source,
            Stretch = System.Windows.Media.Stretch.Fill,
            SnapsToDevicePixels = true
        };
    }
}
