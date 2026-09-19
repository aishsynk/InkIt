using System.Windows.Media.Imaging;

namespace ScreenCanvas.Webcam;

public interface IWebcamService : IDisposable
{
    WebcamCapability Capability { get; }
    IReadOnlyList<WebcamDevice> Devices { get; }
    string? SelectedDeviceId { get; set; }
    bool IsRunning { get; }
    event EventHandler<BitmapSource>? FrameReady;
    Task RefreshDevicesAsync(CancellationToken cancellationToken = default);
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}
