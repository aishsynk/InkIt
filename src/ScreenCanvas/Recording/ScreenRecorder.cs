using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace ScreenCanvas.Recording;

/// <summary>What to record.</summary>
public sealed record RecordingOptions(Rectangle Area, bool Microphone, int FramesPerSecond = 15, bool ShowCursor = true);

/// <summary>
/// Records part of the screen (drawings and pointer included) and optionally the microphone to an MP4 file,
/// using only what ships with Windows: GDI screen copies, the waveIn microphone API and the Media Foundation
/// H.264/AAC encoder behind Windows.Media.Transcoding.
/// </summary>
public sealed class ScreenRecorder : IAsyncDisposable
{
    private const int SampleRate = 48000;
    private const int Channels = 1;
    private readonly RecordingOptions _options;
    private readonly string _path;
    private readonly BlockingCollection<(byte[] Pixels, TimeSpan Time)> _frames = new(boundedCapacity: 24);
    private readonly BlockingCollection<(byte[] Pcm, TimeSpan Time, TimeSpan Duration)> _audio = new(boundedCapacity: 400);
    private readonly Stopwatch _clock = new();
    private readonly CancellationTokenSource _stop = new();
    private TimeSpan _pausedTotal;
    private TimeSpan? _pausedAt;
    private Task? _transcode;
    private Thread? _captureThread;
    private MicrophoneCapture? _microphone;
    private long _audioSamplesWritten;

    public ScreenRecorder(string path, RecordingOptions options)
    {
        _path = path;
        // H.264 needs even dimensions.
        var area = options.Area;
        _options = options with { Area = new Rectangle(area.X, area.Y, area.Width & ~1, area.Height & ~1) };
    }

    public bool IsPaused => _pausedAt is not null;
    public TimeSpan Elapsed => (_pausedAt ?? _clock.Elapsed) - _pausedTotal;
    public string? MicrophoneProblem { get; private set; }

    /// <summary>Recording time, excluding pauses.</summary>
    private TimeSpan Now => _clock.Elapsed - _pausedTotal;

    public async Task StartAsync()
    {
        var area = _options.Area;
        if (area.Width < 16 || area.Height < 16) throw new ArgumentException("The recording area is too small.");

        var video = new VideoStreamDescriptor(VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.Bgra8, (uint)area.Width, (uint)area.Height));
        video.EncodingProperties.FrameRate.Numerator = (uint)_options.FramesPerSecond;
        video.EncodingProperties.FrameRate.Denominator = 1;
        MediaStreamSource source;
        AudioStreamDescriptor? audio = null;
        if (_options.Microphone)
        {
            _microphone = MicrophoneCapture.TryOpen(SampleRate, Channels, out var problem);
            MicrophoneProblem = problem;
        }
        if (_microphone is not null)
        {
            audio = new AudioStreamDescriptor(AudioEncodingProperties.CreatePcm(SampleRate, Channels, 16));
            source = new MediaStreamSource(video, audio);
        }
        else source = new MediaStreamSource(video);
        source.BufferTime = TimeSpan.Zero;
        source.SampleRequested += (_, args) =>
        {
            var request = args.Request;
            var deferral = request.GetDeferral();
            var isVideo = ReferenceEquals(request.StreamDescriptor, video);
            Task.Run(() =>
            {
                try
                {
                    if (isVideo)
                    {
                        if (Take(_frames, out var frame))
                        {
                            var sample = MediaStreamSample.CreateFromBuffer(frame.Pixels.AsBuffer(), frame.Time);
                            sample.Duration = TimeSpan.FromSeconds(1.0 / _options.FramesPerSecond);
                            request.Sample = sample;
                        }
                    }
                    else if (Take(_audio, out var chunk))
                    {
                        var sample = MediaStreamSample.CreateFromBuffer(chunk.Pcm.AsBuffer(), chunk.Time);
                        sample.Duration = chunk.Duration;
                        request.Sample = sample;
                    }
                }
                catch (OperationCanceledException) { }
                catch (InvalidOperationException) { } // queue completed: end of stream
                finally { deferral.Complete(); }
            });
        };

        var folder = await StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetDirectoryName(_path)!);
        var file = await folder.CreateFileAsync(System.IO.Path.GetFileName(_path), CreationCollisionOption.ReplaceExisting);
        var output = await file.OpenAsync(FileAccessMode.ReadWrite);
        var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
        profile.Video.Width = (uint)area.Width;
        profile.Video.Height = (uint)area.Height;
        profile.Video.FrameRate.Numerator = (uint)_options.FramesPerSecond;
        profile.Video.FrameRate.Denominator = 1;
        profile.Video.Bitrate = (uint)Math.Clamp(area.Width * area.Height * _options.FramesPerSecond / 6, 1_500_000, 12_000_000);
        if (_microphone is null) profile.Audio = null;
        else profile.Audio = AudioEncodingProperties.CreateAac(SampleRate, Channels, 128_000);

        var transcoder = new MediaTranscoder { HardwareAccelerationEnabled = true };
        var prepared = await transcoder.PrepareMediaStreamSourceTranscodeAsync(source, output, profile);
        if (!prepared.CanTranscode)
        {
            output.Dispose();
            throw new InvalidOperationException("Windows could not prepare the video encoder: " + prepared.FailureReason);
        }

        _clock.Start();
        _captureThread = new Thread(CaptureLoop) { IsBackground = true, Name = "InkIt screen recorder", Priority = ThreadPriority.AboveNormal };
        _captureThread.Start();
        if (_microphone is not null) _microphone.Start(OnMicrophone);
        _transcode = Task.Run(async () =>
        {
            try { await prepared.TranscodeAsync(); }
            finally { output.Dispose(); }
        });
    }

    /// <summary>Waits for the next item; after Stop, drains what is left, then reports the end of the stream.</summary>
    private bool Take<T>(BlockingCollection<T> queue, out T item)
    {
        try
        {
            if (queue.TryTake(out item!, Timeout.Infinite, _stop.Token)) return true;
        }
        catch (OperationCanceledException) { }
        return queue.TryTake(out item!);
    }

    public void Pause()
    {
        if (_pausedAt is null) _pausedAt = _clock.Elapsed;
    }

    public void Resume()
    {
        if (_pausedAt is not { } at) return;
        _pausedTotal += _clock.Elapsed - at;
        _pausedAt = null;
    }

    /// <summary>Stops recording and waits until the MP4 file is complete.</summary>
    public async Task StopAsync()
    {
        _stop.Cancel();
        _microphone?.Stop();
        _captureThread?.Join(2000);
        _frames.CompleteAdding();
        _audio.CompleteAdding();
        if (_transcode is not null) await _transcode;
    }

    private void CaptureLoop()
    {
        var area = _options.Area;
        var interval = TimeSpan.FromSeconds(1.0 / _options.FramesPerSecond);
        using var bitmap = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
        var next = TimeSpan.Zero;
        while (!_stop.IsCancellationRequested)
        {
            var wait = next - _clock.Elapsed;
            if (wait > TimeSpan.Zero) Thread.Sleep(wait);
            next += interval;
            if (IsPaused) continue;
            var time = Now;
            try
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    var hdc = g.GetHdc();
                    try
                    {
                        var screen = GetDC(nint.Zero);
                        // CAPTUREBLT includes layered windows, i.e. InkIt's drawings.
                        BitBlt(hdc, 0, 0, area.Width, area.Height, screen, area.X, area.Y, 0x00CC0020 | 0x40000000);
                        ReleaseDC(nint.Zero, screen);
                        if (_options.ShowCursor) DrawCursor(hdc, area);
                    }
                    finally { g.ReleaseHdc(hdc); }
                }
                var data = bitmap.LockBits(new Rectangle(0, 0, area.Width, area.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var pixels = new byte[area.Width * area.Height * 4];
                try
                {
                    // Media Foundation reads RGB32 bottom-up: copy rows in reverse order.
                    for (var row = 0; row < area.Height; row++)
                        Marshal.Copy(data.Scan0 + row * data.Stride, pixels, (area.Height - 1 - row) * area.Width * 4, area.Width * 4);
                }
                finally { bitmap.UnlockBits(data); }
                _frames.TryAdd((pixels, time), 0); // a busy encoder drops a frame rather than falling behind
            }
            catch (ExternalException) { } // e.g. the secure desktop (UAC prompt) is showing
        }
    }

    private void OnMicrophone(byte[] pcm)
    {
        if (IsPaused || _stop.IsCancellationRequested) return;
        var samples = pcm.Length / (2 * Channels);
        var time = TimeSpan.FromSeconds((double)_audioSamplesWritten / SampleRate);
        // Keep audio in step with the picture after a pause.
        if (Now - time > TimeSpan.FromMilliseconds(300)) { _audioSamplesWritten = (long)(Now.TotalSeconds * SampleRate); time = Now; }
        _audioSamplesWritten += samples;
        _audio.TryAdd((pcm, time, TimeSpan.FromSeconds((double)samples / SampleRate)), 0);
    }

    private static void DrawCursor(nint hdc, Rectangle area)
    {
        var info = new CursorInfo { Size = Marshal.SizeOf<CursorInfo>() };
        if (!GetCursorInfo(ref info) || (info.Flags & 1) == 0) return;
        if (!GetIconInfo(info.Cursor, out var icon)) return;
        try
        {
            DrawIconEx(hdc, info.Position.X - area.X - icon.HotspotX, info.Position.Y - area.Y - icon.HotspotY, info.Cursor, 0, 0, 0, nint.Zero, 3);
        }
        finally
        {
            if (icon.Mask != nint.Zero) DeleteObject(icon.Mask);
            if (icon.Color != nint.Zero) DeleteObject(icon.Color);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_stop.IsCancellationRequested) await StopAsync();
        _stop.Dispose();
        _frames.Dispose();
        _audio.Dispose();
        _microphone?.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)] private struct PointInt { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct CursorInfo { public int Size; public int Flags; public nint Cursor; public PointInt Position; }
    [StructLayout(LayoutKind.Sequential)] private struct IconInfo { public bool IsIcon; public int HotspotX; public int HotspotY; public nint Mask; public nint Color; }
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint hdc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint dest, int x, int y, int w, int h, nint src, int sx, int sy, int rop);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint handle);
    [DllImport("user32.dll")] private static extern bool GetCursorInfo(ref CursorInfo info);
    [DllImport("user32.dll")] private static extern bool GetIconInfo(nint icon, out IconInfo info);
    [DllImport("user32.dll")] private static extern bool DrawIconEx(nint hdc, int x, int y, nint icon, int w, int h, int step, nint brush, int flags);
}
