namespace ScreenCanvas.Webcam;

public enum WebcamOverlayShape { Circle, RoundedRectangle, Rectangle }
public sealed record WebcamDevice(string Id, string Name);
public sealed record WebcamCapability(bool CanEnumerate, bool CanCapture, string? Limitation = null);
