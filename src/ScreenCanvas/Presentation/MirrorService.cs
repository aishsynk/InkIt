using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Image = System.Windows.Controls.Image;
using Brushes = System.Windows.Media.Brushes;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ScreenCanvas.Presentation;

/// <summary>
/// Shows the presenter's screen, an area of it, or one window full-size on another monitor (a projector),
/// so notes and other apps can stay private on the laptop.
/// Screen and area mirrors include InkIt's drawings; a window mirror uses the DWM live thumbnail and does not.
/// </summary>
public sealed class MirrorService : IDisposable
{
    private MirrorWindow? _window;
    public bool IsActive => _window is not null;
    public event EventHandler? Changed;

    /// <summary>The monitor to show on: any monitor other than the one with the mouse.</summary>
    public static Forms.Screen? TargetScreen()
    {
        var source = Forms.Screen.FromPoint(Forms.Cursor.Position);
        return Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName != source.DeviceName);
    }

    public void MirrorArea(System.Drawing.Rectangle area)
    {
        if (TargetScreen() is not { } target) return;
        Stop();
        _window = new MirrorWindow(target, area, nint.Zero);
        _window.Closed += (_, _) => { _window = null; Changed?.Invoke(this, EventArgs.Empty); };
        _window.Show();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void MirrorWindow(nint windowHandle)
    {
        if (TargetScreen() is not { } target) return;
        Stop();
        _window = new MirrorWindow(target, default, windowHandle);
        _window.Closed += (_, _) => { _window = null; Changed?.Invoke(this, EventArgs.Empty); };
        _window.Show();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Stop() => _window?.Close();

    public void Dispose() => Stop();
}

/// <summary>Full-screen, non-activating window on the target monitor that shows the mirrored picture.</summary>
internal sealed class MirrorWindow : Window
{
    private readonly Forms.Screen _target;
    private readonly System.Drawing.Rectangle _area;
    private readonly nint _source;
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly DispatcherTimer _checkSource = new() { Interval = TimeSpan.FromSeconds(1) };
    private WriteableBitmap? _bitmap;
    private Thread? _captureThread;
    private volatile bool _running;
    private nint _thumbnail;

    public MirrorWindow(Forms.Screen target, System.Drawing.Rectangle area, nint source)
    {
        _target = target;
        _area = area;
        _source = source;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Background = Brushes.Black;
        Title = "InkIt mirror";
        Content = _image;
        RenderRenderOptions();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Loaded += (_, _) => Begin();
        Closed += (_, _) => End();
    }

    private void RenderRenderOptions() => RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var b = _target.Bounds;
        SetWindowPos(handle, nint.Zero, b.Left, b.Top, b.Width, b.Height, 0x0010 /*NOACTIVATE*/);
    }

    private void Begin()
    {
        if (_source != nint.Zero) BeginThumbnail();
        else BeginCapture();
    }

    // ---------------------------------------------------------------- Window mirror (DWM live thumbnail)

    private void BeginThumbnail()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (DwmRegisterThumbnail(handle, _source, out _thumbnail) != 0) { Close(); return; }
        UpdateThumbnail();
        // Follow resizes of the mirrored window; stop if it closes.
        _checkSource.Tick += (_, _) => { if (!IsWindow(_source)) Close(); else UpdateThumbnail(); };
        _checkSource.Start();
    }

    private void UpdateThumbnail()
    {
        if (_thumbnail == nint.Zero || DwmQueryThumbnailSourceSize(_thumbnail, out var size) != 0 || size.X <= 0 || size.Y <= 0) return;
        var b = _target.Bounds;
        var scale = Math.Min((double)b.Width / size.X, (double)b.Height / size.Y);
        var w = (int)(size.X * scale);
        var h = (int)(size.Y * scale);
        var x = (b.Width - w) / 2;
        var y = (b.Height - h) / 2;
        var props = new ThumbnailProperties
        {
            Flags = 0x1 | 0x8 | 0x10, // RECTDESTINATION | VISIBLE | OPACITY
            Destination = new NativeRect { Left = x, Top = y, Right = x + w, Bottom = y + h },
            Opacity = 255,
            Visible = true
        };
        DwmUpdateThumbnailProperties(_thumbnail, ref props);
    }

    // ---------------------------------------------------------------- Screen/area mirror (copies with drawings)

    private void BeginCapture()
    {
        var w = _area.Width;
        var h = _area.Height;
        _bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        _image.Source = _bitmap;
        _running = true;
        _captureThread = new Thread(() =>
        {
            var pixels = new byte[w * h * 4];
            using var frame = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            while (_running)
            {
                var started = Environment.TickCount64;
                try
                {
                    using (var g = System.Drawing.Graphics.FromImage(frame))
                    {
                        var hdc = g.GetHdc();
                        var screen = GetDC(nint.Zero);
                        BitBlt(hdc, 0, 0, w, h, screen, _area.X, _area.Y, 0x00CC0020 | 0x40000000);
                        ReleaseDC(nint.Zero, screen);
                        g.ReleaseHdc(hdc);
                    }
                    var data = frame.LockBits(new System.Drawing.Rectangle(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.ReadOnly, frame.PixelFormat);
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                    frame.UnlockBits(data);
                    Dispatcher.Invoke(() =>
                    {
                        if (_running) _bitmap.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
                    }, DispatcherPriority.Render);
                }
                catch (Exception ex) when (ex is ExternalException or TaskCanceledException or InvalidOperationException) { }
                var spent = Environment.TickCount64 - started;
                Thread.Sleep((int)Math.Max(5, 66 - spent));
            }
        }) { IsBackground = true, Name = "InkIt mirror" };
        _captureThread.Start();
    }

    private void End()
    {
        _running = false;
        _checkSource.Stop();
        if (_thumbnail != nint.Zero) { DwmUnregisterThumbnail(_thumbnail); _thumbnail = nint.Zero; }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeSize { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ThumbnailProperties
    {
        public int Flags;
        public NativeRect Destination;
        public NativeRect Source;
        public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool SourceClientAreaOnly;
    }

    [DllImport("dwmapi.dll")] private static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmUnregisterThumbnail(nint thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref ThumbnailProperties properties);
    [DllImport("dwmapi.dll")] private static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out NativeSize size);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint hdc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint dest, int x, int y, int w, int h, nint src, int sx, int sy, int rop);
}
