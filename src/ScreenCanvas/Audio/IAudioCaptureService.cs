namespace ScreenCanvas.Audio;

public interface IAudioCaptureService : IDisposable
{
    AudioCapability Capability { get; }
    IReadOnlyList<AudioDevice> Devices { get; }
    AudioSourceKind Source { get; set; }
    string? SelectedMicrophoneId { get; set; }
    string? SelectedOutputId { get; set; }
    bool MicrophoneMuted { get; set; }
    bool SystemAudioMuted { get; set; }
    AudioLevels Levels { get; }
    event EventHandler<AudioLevels>? LevelsChanged;
    Task RefreshDevicesAsync(CancellationToken cancellationToken = default);
}
