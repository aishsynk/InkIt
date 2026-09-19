using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Vector = System.Windows.Vector;

namespace ScreenCanvas.Zoom;

public interface IZoomEngine : IDisposable
{
    event EventHandler<ZoomState>? StateChanged;
    ZoomState State { get; }
    bool IsSupported { get; }
    bool TrySetLiveZoom(double factor, Point focus);
    bool TrySetRegionZoom(Rect region);
    bool TryPan(Vector delta);
    void Reset();
}

public readonly record struct ZoomState(
    ZoomMode Mode,
    double Factor,
    Point SourceOrigin,
    string? Error = null)
{
    public static ZoomState Inactive => new(ZoomMode.None, 1, new Point());
}

public enum ZoomMode
{
    None,
    Live,
    Region
}
