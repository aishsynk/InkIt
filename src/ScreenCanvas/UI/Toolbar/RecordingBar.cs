using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ScreenCanvas.Recording;
using ScreenCanvas.UI.Controls;
using ScreenCanvas.UI.Theme;
using Button = System.Windows.Controls.Button;
using Colors = System.Windows.Media.Colors;
using Orientation = System.Windows.Controls.Orientation;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace ScreenCanvas.UI.Toolbar;

/// <summary>
/// Lesson recording: choose the whole screen or an area (with or without the microphone), then a small bar shows
/// the time with Pause and Stop. The bar is hidden from the recording itself.
/// </summary>
public sealed class RecordingBar : FloatingCard
{
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private ScreenRecorder? _recorder;
    private string? _path;
    private TextBlock? _time;

    public RecordingBar(ToolbarWindow toolbar) : base(toolbar, double.NaN, Tw.B(Tw.Red500, 0.45), new Thickness(10, 8, 10, 8))
    {
        _tick.Tick += (_, _) => { if (_time is not null && _recorder is not null) _time.Text = Format(_recorder.Elapsed); };
    }

    public bool IsRecording => _recorder is not null;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Keep this bar out of the video (Windows 10 2004+).
        SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle, 0x11);
    }

    /// <summary>Step 1: what to record.</summary>
    public void ShowSetup()
    {
        if (IsRecording) return;
        var settings = Toolbar.AppSettings.Presentation;
        var mic = DK.Switch(settings.RecordMicrophone, v => { settings.RecordMicrophone = v; Toolbar.SaveAppSettings(); }, Tw.Red500);
        mic.VerticalAlignment = VerticalAlignment.Center;
        var row = Row(
            Dot(false),
            DK.Text("Record a lesson", 13, "Ink.Text", FontWeights.SemiBold),
            Spacer(),
            new LucideIcon("Mic", 15) { Foreground = Tw.B(Tw.Slate300), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Record your voice" },
            mic,
            Spacer(),
            Action("Crop", "Choose area", Tw.Slate700, () => Begin(area: true)),
            Action("Monitor", "Whole screen", Tw.Red600, () => Begin(area: false)),
            CloseButton());
        Card.Child = row;
        Show();
        Reposition();
    }

    private async void Begin(bool area)
    {
        Hide();
        System.Drawing.Rectangle bounds;
        if (area)
        {
            if (Toolbar.SelectArea("Drag a box around the part to record   ·   Esc to cancel") is not { } picked) return;
            bounds = picked;
        }
        else bounds = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).Bounds;

        foreach (var n in new[] { "3", "2", "1" })
        {
            Toast.Show($"Recording starts in {n}...");
            await Task.Delay(800);
        }
        var folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "InkIt");
        System.IO.Directory.CreateDirectory(folder);
        _path = System.IO.Path.Combine(folder, $"Lesson {DateTime.Now:yyyy-MM-dd HH.mm.ss}.mp4");
        _recorder = new ScreenRecorder(_path, new RecordingOptions(bounds, Toolbar.AppSettings.Presentation.RecordMicrophone));
        try
        {
            await _recorder.StartAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or COMException or UnauthorizedAccessException or System.IO.IOException)
        {
            await _recorder.DisposeAsync();
            _recorder = null;
            Toast.Show("Recording could not start: " + ex.Message);
            return;
        }
        if (_recorder.MicrophoneProblem is { } problem) Toast.Show(problem);
        ShowRecording();
        Toolbar.OnRecordingChanged();
    }

    /// <summary>Step 2: the running recording.</summary>
    private void ShowRecording()
    {
        _time = DK.Text("00:00", 13, Tw.B(Colors.White), FontWeights.SemiBold, mono: true);
        _time.VerticalAlignment = VerticalAlignment.Center;
        var paused = _recorder?.IsPaused == true;
        var pause = Action(paused ? "Play" : "Pause", paused ? "Resume" : "Pause", Tw.Slate700, TogglePause);
        var stop = Action("CircleStop", "Stop", Tw.Red600, () => _ = StopAsync());
        Card.Child = Row(Dot(!paused), DK.Text(paused ? "Paused" : "Recording", 12, Tw.B(paused ? Tw.Amber300 : Tw.Red300), FontWeights.SemiBold), _time, Spacer(), pause, stop);
        _tick.Start();
        Show();
        Reposition();
    }

    private void TogglePause()
    {
        if (_recorder is null) return;
        if (_recorder.IsPaused) _recorder.Resume(); else _recorder.Pause();
        ShowRecording();
    }

    public async Task StopAsync()
    {
        if (_recorder is null) return;
        var recorder = _recorder;
        _recorder = null;
        _tick.Stop();
        Hide();
        Toast.Show("Saving the video...");
        await recorder.StopAsync();
        await recorder.DisposeAsync();
        Toolbar.OnRecordingChanged();
        var path = _path!;
        Toolbar.ShowNotice("Video", "Recording saved", $"{System.IO.Path.GetFileName(path)} is in Videos > InkIt.",
        [
            new NoticeAction("Show in folder", () => Support.Links.Open(System.IO.Path.GetDirectoryName(path)!)),
            new NoticeAction("Play video", () => Support.Links.Open(path), Primary: true)
        ]);
    }

    public override void Reposition()
    {
        if (!IsVisible) return;
        UpdateLayout();
        var area = SystemParameters.WorkArea;
        PlaceCard(area.Left + (area.Width - Card.ActualWidth) / 2, area.Bottom - Card.ActualHeight - 24);
    }

    // ---------------------------------------------------------------- Pieces

    private static StackPanel Row(params UIElement[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var child in children)
        {
            if (child is FrameworkElement fe) { fe.VerticalAlignment = VerticalAlignment.Center; fe.Margin = new Thickness(0, 0, 8, 0); }
            row.Children.Add(child);
        }
        return row;
    }

    private static FrameworkElement Spacer() => new Border { Width = 4 };

    private static FrameworkElement Dot(bool pulsing)
    {
        var dot = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = Tw.B(Tw.Red500) };
        if (pulsing) dot.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.35, TimeSpan.FromMilliseconds(700)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        return dot;
    }

    private static Button Action(string icon, string label, System.Windows.Media.Color background, Action run)
    {
        var b = DK.Button(DK.IconLabel(icon, 14, label, 12, spacing: 6), Tw.B(background), Tw.B(Colors.White), Tw.B(background, 0.8), Tw.B(Colors.White), 8, new Thickness(10, 5, 10, 5));
        b.Click += (_, _) => run();
        return b;
    }

    private Button CloseButton()
    {
        var b = DK.IconButton("X", 14, "Ink.Text400", "Ink.Text", "Ink.Hover", 4, 4);
        b.ToolTip = "Close";
        b.Click += (_, _) => Hide();
        return b;
    }

    private static string Format(TimeSpan t) => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");

    [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(nint hwnd, uint affinity);
}
