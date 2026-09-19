using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;

namespace ScreenCanvas.Zoom;

/// <summary>
/// GPU/compositor-backed full-screen zoom through the Windows Magnification API.
/// It never captures or polls screenshots and always restores the desktop on disposal.
/// </summary>
public sealed class WindowsZoomEngine : IZoomEngine
{
    private const double MinFactor = 1.0;
    private const double MaxFactor = 16.0;
    private readonly System.Windows.Threading.DispatcherTimer _panTimer;
    private bool _initialized;
    private bool _disposed;

    public event EventHandler<ZoomState>? StateChanged;
    public ZoomState State { get; private set; } = ZoomState.Inactive;
    public bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(6, 2) && _initialized;

    public WindowsZoomEngine()
    {
        _panTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _panTimer.Tick += OnPanTimerTick;

        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 2)) return;
        try
        {
            _initialized = NativeMethods.MagInitialize();
        }
        catch (DllNotFoundException)
        {
            _initialized = false;
        }
        catch (EntryPointNotFoundException)
        {
            _initialized = false;
        }
    }

    public bool TrySetLiveZoom(double factor, Point focus)
    {
        ThrowIfDisposed();
        factor = Math.Clamp(factor, MinFactor, MaxFactor);
        if (factor <= MinFactor)
        {
            Reset();
            return true;
        }

        var desktop = VirtualDesktop.Bounds;
        var sourceWidth = desktop.Width / factor;
        var sourceHeight = desktop.Height / factor;
        var origin = new Point(
            Math.Clamp(focus.X - sourceWidth / 2, desktop.Left, desktop.Right - sourceWidth),
            Math.Clamp(focus.Y - sourceHeight / 2, desktop.Top, desktop.Bottom - sourceHeight));
        return Apply(ZoomMode.Live, factor, origin);
    }

    public bool TrySetRegionZoom(Rect region)
    {
        ThrowIfDisposed();
        if (region.IsEmpty || region.Width < 1 || region.Height < 1) return Fail("Zoom region is empty.");

        var desktop = VirtualDesktop.Bounds;
        region.Intersect(desktop);
        if (region.IsEmpty) return Fail("Zoom region is outside the virtual desktop.");
        var factor = Math.Clamp(Math.Min(desktop.Width / region.Width, desktop.Height / region.Height), MinFactor, MaxFactor);
        return Apply(ZoomMode.Region, factor, region.TopLeft);
    }

    public bool TryPan(Vector delta)
    {
        ThrowIfDisposed();
        if (State.Mode == ZoomMode.None) return false;
        var desktop = VirtualDesktop.Bounds;
        var sourceWidth = desktop.Width / State.Factor;
        var sourceHeight = desktop.Height / State.Factor;
        var origin = new Point(
            Math.Clamp(State.SourceOrigin.X + delta.X, desktop.Left, desktop.Right - sourceWidth),
            Math.Clamp(State.SourceOrigin.Y + delta.Y, desktop.Top, desktop.Bottom - sourceHeight));
        return Apply(State.Mode, State.Factor, origin);
    }

    public void Reset()
    {
        _panTimer.Stop();
        if (_disposed || !_initialized)
        {
            SetState(ZoomState.Inactive);
            return;
        }

        if (!NativeMethods.MagSetFullscreenTransform(1, 0, 0))
        {
            Fail(new Win32Exception(Marshal.GetLastWin32Error()).Message);
            return;
        }
        SetState(ZoomState.Inactive);
    }

    private void OnPanTimerTick(object? sender, EventArgs e)
    {
        if (_disposed || !_initialized || State.Mode != ZoomMode.Live || State.Factor <= 1.0)
        {
            _panTimer.Stop();
            return;
        }

        var desktop = VirtualDesktop.Bounds;
        var factor = State.Factor;
        var sourceWidth = desktop.Width / factor;
        var sourceHeight = desktop.Height / factor;
        var cursor = System.Windows.Forms.Cursor.Position;

        var targetOrigin = new Point(
            Math.Clamp(cursor.X - sourceWidth / 2, desktop.Left, desktop.Right - sourceWidth),
            Math.Clamp(cursor.Y - sourceHeight / 2, desktop.Top, desktop.Bottom - sourceHeight));

        if (Math.Abs(targetOrigin.X - State.SourceOrigin.X) >= 1.0 || Math.Abs(targetOrigin.Y - State.SourceOrigin.Y) >= 1.0)
        {
            if (NativeMethods.MagSetFullscreenTransform((float)factor, Round(targetOrigin.X), Round(targetOrigin.Y)))
            {
                State = State with { SourceOrigin = targetOrigin };
            }
        }
    }

    private bool Apply(ZoomMode mode, double factor, Point origin)
    {
        if (!_initialized) return Fail("Windows Magnification API is unavailable.");
        if (!NativeMethods.MagSetFullscreenTransform((float)factor, Round(origin.X), Round(origin.Y)))
            return Fail(new Win32Exception(Marshal.GetLastWin32Error()).Message);
        SetState(new ZoomState(mode, factor, origin));

        if (mode == ZoomMode.Live && factor > 1.0)
        {
            if (!_panTimer.IsEnabled) _panTimer.Start();
        }
        else
        {
            _panTimer.Stop();
        }
        return true;
    }

    private bool Fail(string error)
    {
        SetState(State with { Error = error });
        return false;
    }

    private void SetState(ZoomState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    private static int Round(double value) => checked((int)Math.Round(value));
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _panTimer.Stop();
        if (_initialized)
        {
            _ = NativeMethods.MagSetFullscreenTransform(1, 0, 0);
            _ = NativeMethods.MagUninitialize();
            _initialized = false;
        }
        _disposed = true;
        State = ZoomState.Inactive;
        GC.SuppressFinalize(this);
    }

    private static class VirtualDesktop
    {
        public static Rect Bounds => new(
            NativeMethods.GetSystemMetrics(76),
            NativeMethods.GetSystemMetrics(77),
            NativeMethods.GetSystemMetrics(78),
            NativeMethods.GetSystemMetrics(79));
    }

    private static class NativeMethods
    {
        [DllImport("Magnification.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MagInitialize();

        [DllImport("Magnification.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MagUninitialize();

        [DllImport("Magnification.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MagSetFullscreenTransform(float magnificationLevel, int xOffset, int yOffset);

        [DllImport("user32.dll")]
        internal static extern int GetSystemMetrics(int index);
    }
}
