using System.Runtime.InteropServices;
using System.Text;

namespace ScreenCanvas.Audio;

/// <summary>Dependency-free WinMM discovery and state shared with a recording backend.</summary>
public sealed class WindowsAudioCaptureService : IAudioCaptureService
{
    private readonly List<AudioDevice> _devices = [];
    public AudioCapability Capability { get; private set; } = new(true, false, false, "Encoding/capture requires the recording backend; device selection and level state are ready.");
    public IReadOnlyList<AudioDevice> Devices => _devices;
    public AudioSourceKind Source { get; set; } = AudioSourceKind.Microphone;
    public string? SelectedMicrophoneId { get; set; }
    public string? SelectedOutputId { get; set; }
    public bool MicrophoneMuted { get; set; }
    public bool SystemAudioMuted { get; set; }
    public AudioLevels Levels { get; private set; } = AudioLevels.Silent;
    public event EventHandler<AudioLevels>? LevelsChanged;

    public Task RefreshDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); _devices.Clear();
        for (uint i = 0; i < waveInGetNumDevs(); i++)
        {
            var caps = new WaveInCaps(); caps.Name = new string('\0', 32);
            if (waveInGetDevCaps(i, ref caps, (uint)Marshal.SizeOf<WaveInCaps>()) == 0)
                _devices.Add(new AudioDevice($"wavein:{i}", caps.Name.TrimEnd('\0'), AudioSourceKind.Microphone, i == 0));
        }
        for (uint i = 0; i < waveOutGetNumDevs(); i++)
        {
            var caps = new WaveOutCaps(); caps.Name = new string('\0', 32);
            if (waveOutGetDevCaps(i, ref caps, (uint)Marshal.SizeOf<WaveOutCaps>()) == 0)
                _devices.Add(new AudioDevice($"waveout:{i}", caps.Name.TrimEnd('\0'), AudioSourceKind.System, i == 0));
        }
        SelectedMicrophoneId ??= _devices.FirstOrDefault(x => x.Kind == AudioSourceKind.Microphone)?.Id;
        SelectedOutputId ??= _devices.FirstOrDefault(x => x.Kind == AudioSourceKind.System)?.Id;
        return Task.CompletedTask;
    }

    /// <summary>Called by the recorder's audio callback; values are normalized to 0..1.</summary>
    public void ReportLevels(float left, float right)
    {
        Levels = new AudioLevels(Math.Clamp(left, 0, 1), Math.Clamp(right, 0, 1));
        LevelsChanged?.Invoke(this, Levels);
    }

    public void Dispose() { Levels = AudioLevels.Silent; LevelsChanged = null; }

    [DllImport("winmm.dll")] private static extern uint waveInGetNumDevs();
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)] private static extern uint waveInGetDevCaps(uint id, ref WaveInCaps caps, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutGetNumDevs();
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)] private static extern uint waveOutGetDevCaps(uint id, ref WaveOutCaps caps, uint size);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WaveInCaps { public ushort ManufacturerId, ProductId; public uint DriverVersion; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name; public uint Formats; public ushort Channels, Reserved; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WaveOutCaps { public ushort ManufacturerId, ProductId; public uint DriverVersion; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name; public uint Formats; public ushort Channels, Reserved; public uint Support; }
}
