using System.Runtime.InteropServices;

namespace ScreenCanvas.Recording;

/// <summary>
/// Default-microphone capture through the classic waveIn API: 16-bit PCM in 50 ms blocks, handed to a callback
/// on a background thread. Needs Windows' "Let desktop apps access your microphone" to be on.
/// </summary>
public sealed class MicrophoneCapture : IDisposable
{
    private const int BufferCount = 8;
    private readonly nint _device;
    private readonly int _bytesPerBuffer;
    private readonly List<(GCHandle Data, nint Header)> _buffers = [];
    private Thread? _thread;
    private volatile bool _running;
    private Action<byte[]>? _callback;

    private MicrophoneCapture(nint device, int bytesPerBuffer)
    {
        _device = device;
        _bytesPerBuffer = bytesPerBuffer;
    }

    /// <summary>Opens the default microphone, or returns null with a plain-language reason.</summary>
    public static MicrophoneCapture? TryOpen(int sampleRate, int channels, out string? problem)
    {
        problem = null;
        if (waveInGetNumDevs() == 0) { problem = "No microphone was found. The recording has no sound."; return null; }
        var format = new WaveFormat
        {
            FormatTag = 1, Channels = (short)channels, SamplesPerSec = sampleRate, BitsPerSample = 16,
            BlockAlign = (short)(channels * 2), AvgBytesPerSec = sampleRate * channels * 2
        };
        var result = waveInOpen(out var device, -1, ref format, nint.Zero, nint.Zero, 0);
        if (result != 0)
        {
            problem = "InkIt could not use the microphone. Check Windows Settings > Privacy > Microphone and allow desktop apps.";
            return null;
        }
        return new MicrophoneCapture(device, format.AvgBytesPerSec / 20);
    }

    public void Start(Action<byte[]> callback)
    {
        _callback = callback;
        for (var i = 0; i < BufferCount; i++)
        {
            var data = GCHandle.Alloc(new byte[_bytesPerBuffer], GCHandleType.Pinned);
            var header = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHeader>());
            Marshal.StructureToPtr(new WaveHeader { Data = data.AddrOfPinnedObject(), BufferLength = _bytesPerBuffer }, header, false);
            waveInPrepareHeader(_device, header, Marshal.SizeOf<WaveHeader>());
            waveInAddBuffer(_device, header, Marshal.SizeOf<WaveHeader>());
            _buffers.Add((data, header));
        }
        _running = true;
        waveInStart(_device);
        _thread = new Thread(Pump) { IsBackground = true, Name = "InkIt microphone" };
        _thread.Start();
    }

    private void Pump()
    {
        while (_running)
        {
            var any = false;
            foreach (var (data, header) in _buffers)
            {
                var h = Marshal.PtrToStructure<WaveHeader>(header);
                if ((h.Flags & 1) == 0) continue; // WHDR_DONE
                any = true;
                if (h.BytesRecorded > 0)
                {
                    var copy = new byte[h.BytesRecorded];
                    Array.Copy((byte[])data.Target!, copy, h.BytesRecorded);
                    _callback?.Invoke(copy);
                }
                if (!_running) break;
                h.Flags &= ~1;
                h.BytesRecorded = 0;
                Marshal.StructureToPtr(h, header, false);
                waveInAddBuffer(_device, header, Marshal.SizeOf<WaveHeader>());
            }
            if (!any) Thread.Sleep(10);
        }
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _thread?.Join(1000);
        waveInStop(_device);
        waveInReset(_device);
    }

    public void Dispose()
    {
        Stop();
        foreach (var (data, header) in _buffers)
        {
            waveInUnprepareHeader(_device, header, Marshal.SizeOf<WaveHeader>());
            Marshal.FreeHGlobal(header);
            data.Free();
        }
        _buffers.Clear();
        waveInClose(_device);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat { public short FormatTag; public short Channels; public int SamplesPerSec; public int AvgBytesPerSec; public short BlockAlign; public short BitsPerSample; public short Size; }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader { public nint Data; public int BufferLength; public int BytesRecorded; public nint User; public int Flags; public int Loops; public nint Next; public nint Reserved; }

    [DllImport("winmm.dll")] private static extern int waveInGetNumDevs();
    [DllImport("winmm.dll")] private static extern int waveInOpen(out nint device, int deviceId, ref WaveFormat format, nint callback, nint instance, int flags);
    [DllImport("winmm.dll")] private static extern int waveInPrepareHeader(nint device, nint header, int size);
    [DllImport("winmm.dll")] private static extern int waveInUnprepareHeader(nint device, nint header, int size);
    [DllImport("winmm.dll")] private static extern int waveInAddBuffer(nint device, nint header, int size);
    [DllImport("winmm.dll")] private static extern int waveInStart(nint device);
    [DllImport("winmm.dll")] private static extern int waveInStop(nint device);
    [DllImport("winmm.dll")] private static extern int waveInReset(nint device);
    [DllImport("winmm.dll")] private static extern int waveInClose(nint device);
}
