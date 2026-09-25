using System.Windows;
using System.Windows.Controls;
using ScreenCanvas.Hotkeys;
using ScreenCanvas.Overlay;
using ScreenCanvas.UI;
using ScreenCanvas.Settings;

namespace ScreenCanvas;

public partial class App : System.Windows.Application
{
    private OverlayManager? _overlays;
    private HotkeyManager? _hotkeys;
    private TrayService? _tray;
    private ToolbarWindow? _toolbar;
    private bool _temporaryModeActive;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Developer switches: --settings <file> isolates settings, --no-global-hotkeys skips RegisterHotKey
        // (used for QA while another InkIt instance owns the shortcuts); --qa-capture implies both.
        var args = e.Args;
        var qaCapture = args.Contains("--qa-capture");
        var settingsIndex = Array.IndexOf(args, "--settings");
        var settingsPath = settingsIndex >= 0 && settingsIndex + 1 < args.Length ? args[settingsIndex + 1]
            : qaCapture ? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "InkIt-QA", "settings.json") : null;
        var globalHotkeys = !qaCapture && !args.Contains("--no-global-hotkeys");
        var settingsStore = new JsonSettingsStore(settingsPath);
        var startupSettings = settingsStore.LoadAsync().GetAwaiter().GetResult();
        startupSettings.Hotkeys.EnsureDefaults();
        var migrated = startupSettings.MigrateToDesign();
        if (migrated) settingsStore.SaveAsync(startupSettings).GetAwaiter().GetResult();
        ThemeManager.Initialize(startupSettings.Appearance.Theme);
        _overlays = new OverlayManager(startupSettings, settingsStore);
        _toolbar = new ToolbarWindow(_overlays, startupSettings, settingsStore);
        MainWindow = _toolbar;
        ToolTipService.SetIsEnabled(_toolbar, startupSettings.Toolbar.ShowTooltips);
        _hotkeys = new HotkeyManager(_toolbar);
        _hotkeys.EmergencyStop += (_, _) => EmergencyStop();
        _hotkeys.ToggleDrawing += (_, _) => _overlays.ToggleDrawing();
        _hotkeys.Undo += (_, _) => _overlays.Undo();
        _hotkeys.Clear += (_, _) => { _overlays.Clear(); Toast.Show("All drawings cleared (Undo brings them back)"); };
        _hotkeys.ToggleSnap += (_, _) => _toolbar.Registry.Find("canvas.snap_toggle")?.Execute();
        _hotkeys.CaptureRegion += (_, _) => _toolbar.CaptureRegionWithPreview();
        _hotkeys.ToggleZoom += (_, _) => _toolbar.ToggleZoom();
        // Esc always ends the active tool (and closes any open palette) — see DECISIONS "Global Esc invariant".
        _hotkeys.EscapePressed += (_, _) => EndCurrentTool();
        _toolbar.HotkeysChanged = config => { if (globalHotkeys) _hotkeys.Reconfigure(config); };
        if (globalHotkeys) _hotkeys.RegisterDefaults(startupSettings.Hotkeys);
        _overlays.ToolChanged += (_, _) => UpdateEscapeState();
        _overlays.BoardChanged += (_, _) => UpdateEscapeState();
        _overlays.ZoomAreaChanged += (_, _) => UpdateEscapeState();
        _overlays.OptionsChanged += (_, _) => UpdateEscapeState();
        _toolbar.TemporaryModeChanged += (_, active) => { _temporaryModeActive = active; UpdateEscapeState(); };
        _toolbar.PaletteStateChanged += (_, _) => UpdateEscapeState();
        UpdateEscapeState();

        _tray = new TrayService(
            showToolbar: () => ShowToolbar(),
            annotate: () => _overlays.ToggleDrawing(),
            clear: () => _overlays.Clear(),
            exit: () => Shutdown());

        _toolbar.Show();
        if (_hotkeys.Unavailable.Count > 0)
            Toast.Show("Another app already uses this shortcut: " + string.Join(", ", _hotkeys.Unavailable));
        else if (migrated && !qaCapture)
            Toast.Show("Welcome to InkIt! Hover over any button to see what it does. Esc stops drawing, Ctrl+Shift+5 zooms, Ctrl+Shift+4 takes a screenshot.");

        if (qaCapture) RunQaCapture(_toolbar);
    }

    /// <summary>Renders the toolbar and each inspector to artifacts/screenshots for visual QA.</summary>
    private void RunQaCapture(ToolbarWindow toolbar)
    {
        var outDir = System.IO.Path.GetFullPath(@"artifacts/screenshots");
        System.IO.Directory.CreateDirectory(outDir);

        void Save(Window window, string name)
        {
            window.UpdateLayout();
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
            var w = Math.Max(1, (int)Math.Ceiling(window.ActualWidth * dpi.DpiScaleX));
            var h = Math.Max(1, (int)Math.Ceiling(window.ActualHeight * dpi.DpiScaleY));
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(window);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            using var file = System.IO.File.Create(System.IO.Path.Combine(outDir, name));
            encoder.Save(file);
        }

        Dispatcher.BeginInvoke(() =>
        {
            toolbar.ApplyOrientation(true);
            Save(toolbar, "qa_toolbar_horizontal.png");
            toolbar.ApplyOrientation(false);
            Save(toolbar, "qa_toolbar_vertical.png");
            toolbar.ApplyOrientation(true);
            foreach (var key in new[] { "pen", "shape", "color", "laser", "zoom", "board", "grid", "highlighter", "text", "more" })
            {
                toolbar.ShowInspector(key);
                if (toolbar.OwnedWindows.OfType<InspectorWindow>().FirstOrDefault() is { } inspector)
                {
                    inspector.BeginAnimation(UIElement.OpacityProperty, null);
                    inspector.Opacity = 1;
                    Save(inspector, $"qa_inspector_{key}.png");
                }
            }
            toolbar.CloseMenus();
            foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light })
            {
                ThemeManager.SetPreference(theme);
                var settings = toolbar.CreateSettingsWindow();
                settings.Show();
                settings.ShowHotkeysTab();
                settings.BeginAnimation(UIElement.OpacityProperty, null);
                settings.Opacity = 1;
                Save(settings, $"qa_settings_hotkeys_{theme.ToString().ToLowerInvariant()}.png");
                settings.Close();
            }
            Shutdown();
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void ShowToolbar()
    {
        if (_toolbar is null) return;
        _toolbar.Show();
        _toolbar.Activate();
    }

    private void EmergencyStop()
    {
        _toolbar?.EmergencyRelease();
        _overlays?.EmergencyStop();
        UpdateEscapeState();
    }

    private void EndCurrentTool()
    {
        _toolbar?.EndCurrentTool();
        UpdateEscapeState();
    }

    private void UpdateEscapeState()
    {
        bool shouldEnable = _temporaryModeActive
            || _overlays?.IsDrawing == true
            || _overlays?.CurrentBoardColor != null
            || _overlays?.Settings.CurtainProgress > 0
            || _toolbar?.IsPaletteOpen == true
            || _toolbar?.IsZoomActive == true;

        _hotkeys?.SetEscapeEnabled(shouldEnable);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Dispose();
        _tray?.Dispose();
        _overlays?.Dispose();
        base.OnExit(e);
    }
}
