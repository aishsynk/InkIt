using System.Drawing;

namespace ScreenCanvas.Capture;

public enum CaptureTarget { VirtualDesktop, CurrentMonitor, Monitor, Window, Region }
public enum CaptureImageFormat { Png, Jpeg }

public sealed record CaptureRequest(
    CaptureTarget Target,
    Rectangle? Bounds = null,
    nint WindowHandle = default,
    string? MonitorDeviceName = null,
    bool IncludeAnnotations = true);

public sealed record CaptureRegion(Rectangle PixelBounds)
{
    public bool IsEmpty => PixelBounds.Width < 2 || PixelBounds.Height < 2;
}
