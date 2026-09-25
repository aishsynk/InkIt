using System.Drawing;
using System.Windows;

namespace ScreenCanvas.Capture;

public interface ICaptureService
{
    void CopyDesktopToClipboard();
    void CopyCurrentMonitorToClipboard();
    void SaveDesktop(string path, bool jpeg);

    Bitmap Capture(CaptureRequest request);
    Bitmap? CaptureInteractiveRegion(Window? owner = null);
    Rectangle? SelectRegion(Window? owner = null, string? hint = null);
    System.Windows.Media.Imaging.BitmapSource ToImage(Bitmap bitmap);
    void Save(Bitmap bitmap, string path, CaptureImageFormat format, long jpegQuality = 92);
    void CopyToClipboard(Bitmap bitmap);
}
