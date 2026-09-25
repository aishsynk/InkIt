using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using Forms = System.Windows.Forms;

namespace ScreenCanvas.Capture;

public sealed class CaptureService : ICaptureService
{
    private readonly Func<IDisposable>? _suspendAnnotations;

    public CaptureService(Func<IDisposable>? suspendAnnotations = null) => _suspendAnnotations = suspendAnnotations;

    public void CopyDesktopToClipboard()
    {
        using var bitmap = Capture(new CaptureRequest(CaptureTarget.VirtualDesktop));
        System.Windows.Clipboard.SetImage(ToBitmapSource(bitmap));
    }

    public void CopyCurrentMonitorToClipboard()
    {
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        using var bitmap = Capture(new CaptureRequest(CaptureTarget.Region, screen.Bounds));
        System.Windows.Clipboard.SetImage(ToBitmapSource(bitmap));
    }

    public void SaveDesktop(string path, bool jpeg)
    {
        using var bitmap = Capture(new CaptureRequest(CaptureTarget.VirtualDesktop));
        Save(bitmap, path, jpeg ? CaptureImageFormat.Jpeg : CaptureImageFormat.Png);
    }

    public Bitmap Capture(CaptureRequest request)
    {
        using var annotationScope = !request.IncludeAnnotations ? _suspendAnnotations?.Invoke() : null;
        var windowHandle = request.Target == CaptureTarget.Window && request.WindowHandle == default
            ? WindowFromPoint(Forms.Cursor.Position) : request.WindowHandle;
        var bounds = request.Target == CaptureTarget.Window ? GetWindowBounds(windowHandle) : ResolveBounds(request);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Capture bounds must have positive dimensions.");

        var bitmap = CaptureScreen(bounds);
        if (request.Target == CaptureTarget.Window && windowHandle != default)
        {
            using var graphics = Graphics.FromImage(bitmap);
            var hdc = graphics.GetHdc();
            bool printed;
            try { printed = PrintWindow(windowHandle, hdc, 2); }
            finally { graphics.ReleaseHdc(hdc); }
            if (printed)
            {
                return bitmap;
            }
        }
        return bitmap;
    }

    public Bitmap? CaptureInteractiveRegion(Window? owner = null) =>
        SelectRegion(owner) is { } bounds ? Capture(new CaptureRequest(CaptureTarget.Region, bounds)) : null;

    /// <summary>Lets the user drag out an area and returns it in screen pixels, after the picker has left the screen.</summary>
    public Rectangle? SelectRegion(Window? owner = null, string? hint = null)
    {
        var selector = hint is null ? new RegionSelectionWindow() : new RegionSelectionWindow(hint);
        if (owner is not null) selector.Owner = owner;
        if (selector.ShowDialog() != true || selector.SelectedRegion is not { IsEmpty: false } region) return null;
        RegionSelectionWindow.WaitUntilGone();
        return region.PixelBounds;
    }

    public System.Windows.Media.Imaging.BitmapSource ToImage(Bitmap bitmap)
    {
        var image = ToBitmapSource(bitmap);
        image.Freeze();
        return image;
    }

    public void CopyToClipboard(Bitmap bitmap) => System.Windows.Clipboard.SetImage(ToBitmapSource(bitmap));

    public void Save(Bitmap bitmap, string path, CaptureImageFormat format, long jpegQuality = 92)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (format == CaptureImageFormat.Png) { bitmap.Save(path, ImageFormat.Png); return; }
        jpegQuality = Math.Clamp(jpegQuality, 1, 100);
        var codec = ImageCodecInfo.GetImageEncoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
        using var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, jpegQuality);
        bitmap.Save(path, codec, parameters);
    }

    private static Rectangle ResolveBounds(CaptureRequest request) => request.Target switch
    {
        CaptureTarget.VirtualDesktop => Forms.SystemInformation.VirtualScreen,
        CaptureTarget.CurrentMonitor => Forms.Screen.FromPoint(Forms.Cursor.Position).Bounds,
        CaptureTarget.Monitor => Forms.Screen.AllScreens.FirstOrDefault(s =>
            string.Equals(s.DeviceName, request.MonitorDeviceName, StringComparison.OrdinalIgnoreCase))?.Bounds
            ?? throw new ArgumentException("Monitor was not found.", nameof(request)),
        CaptureTarget.Region => request.Bounds ?? throw new ArgumentException("Region bounds are required.", nameof(request)),
        CaptureTarget.Window => GetWindowBounds(request.WindowHandle),
        _ => throw new ArgumentOutOfRangeException(nameof(request))
    };

    private static Rectangle GetWindowBounds(nint handle)
    {
        if (handle == default) handle = WindowFromPoint(Forms.Cursor.Position);
        if (handle == default || !GetWindowRect(handle, out var rect))
            throw new InvalidOperationException("No capturable window was found.");
        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    private static Bitmap CaptureScreen(Rectangle bounds)
    {
        var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    private static System.Windows.Media.Imaging.BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        var handle = bitmap.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(handle, nint.Zero, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        }
        finally { DeleteObject(handle); }
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint hObject);

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(System.Drawing.Point point);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(nint hwnd, nint hdc, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
}
