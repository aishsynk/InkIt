using Forms = System.Windows.Forms;

namespace ScreenCanvas.Displays;

public sealed class DisplayManager
{
    public IReadOnlyList<DisplayInfo> GetDisplays() => Forms.Screen.AllScreens
        .Select(screen => new DisplayInfo(
            screen.DeviceName,
            screen.Bounds.Left,
            screen.Bounds.Top,
            screen.Bounds.Width,
            screen.Bounds.Height,
            screen.Primary))
        .ToArray();

    public DisplayInfo? GetDisplayAtCursor()
    {
        var cursor = Forms.Cursor.Position;
        var screen = Forms.Screen.FromPoint(cursor);
        return new DisplayInfo(
            screen.DeviceName,
            screen.Bounds.Left,
            screen.Bounds.Top,
            screen.Bounds.Width,
            screen.Bounds.Height,
            screen.Primary);
    }
}
