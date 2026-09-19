namespace ScreenCanvas.Audio;

public enum AudioSourceKind { Microphone, System, Combined }
public sealed record AudioDevice(string Id, string Name, AudioSourceKind Kind, bool IsDefault = false);
public sealed record AudioLevels(float Left, float Right)
{
    public static AudioLevels Silent { get; } = new(0, 0);
    public float Peak => Math.Max(Left, Right);
}
public sealed record AudioCapability(bool CanEnumerate, bool CanCaptureMicrophone, bool CanCaptureSystemAudio, string? Limitation = null);
