using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace ScreenCanvas.Presentation;

/// <summary>
/// Notices a running PowerPoint slide show and which slide is showing, so drawings can stay with their slide.
/// Cheap by design: every 0.7 s it only looks for the slide-show window; PowerPoint is asked for the slide number
/// only while a show is running.
/// </summary>
public sealed class SlideShowWatcher : IDisposable
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(700) };
    private object? _powerPoint;
    private string? _lastKey;

    /// <summary>A slide is showing: (slide key, a point on the monitor the show is on).</summary>
    public event Action<string, System.Drawing.Point>? SlideChanged;
    public event Action? SlideShowEnded;

    public bool IsRunning => _timer.IsEnabled;

    public SlideShowWatcher() => _timer.Tick += (_, _) => Poll();

    public void Start() => _timer.Start();

    public void Stop()
    {
        _timer.Stop();
        EndShow();
    }

    private void Poll()
    {
        var window = FindWindow("screenClass", null);
        if (window == nint.Zero)
        {
            EndShow();
            return;
        }
        if (ReadSlide() is not { } key) return;
        if (key == _lastKey) return;
        _lastKey = key;
        GetWindowRect(window, out var rect);
        SlideChanged?.Invoke(key, new System.Drawing.Point((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2));
    }

    private void EndShow()
    {
        if (_lastKey is null) return;
        _lastKey = null;
        ReleasePowerPoint();
        SlideShowEnded?.Invoke();
    }

    /// <summary>"presentation path#slide number" for the running show, or null if PowerPoint cannot say.</summary>
    private string? ReadSlide()
    {
        try
        {
            _powerPoint ??= GetActive("PowerPoint.Application");
            if (_powerPoint is null) return null;
            dynamic app = _powerPoint;
            if ((int)app.SlideShowWindows.Count < 1) return null;
            dynamic show = app.SlideShowWindows[1];
            int position = show.View.CurrentShowPosition;
            string name = show.Presentation.FullName;
            return $"{name}#{position}";
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or InvalidComObjectException)
        {
            ReleasePowerPoint();
            return null;
        }
    }

    private void ReleasePowerPoint()
    {
        if (_powerPoint is not null && Marshal.IsComObject(_powerPoint)) Marshal.ReleaseComObject(_powerPoint);
        _powerPoint = null;
    }

    private static object? GetActive(string progId)
    {
        if (CLSIDFromProgID(progId, out var clsid) != 0) return null;
        return GetActiveObject(ref clsid, nint.Zero, out var instance) == 0 ? instance : null;
    }

    public void Dispose()
    {
        _timer.Stop();
        ReleasePowerPoint();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string? className, string? windowName);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("ole32.dll", CharSet = CharSet.Unicode)] private static extern int CLSIDFromProgID(string progId, out Guid clsid);
    [DllImport("oleaut32.dll")] private static extern int GetActiveObject(ref Guid clsid, nint reserved, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
}
