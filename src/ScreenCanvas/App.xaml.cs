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
    private Support.SingleInstance? _single;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Unexpected errors: keep InkIt running where possible and save a report the user can attach to a bug report.
        DispatcherUnhandledException += (_, ex) =>
        {
            Support.CrashLog.Write(ex.Exception, "UI");
            ex.Handled = true;
            Toast.Show("Something went wrong. InkIt saved a report (tray menu > Report a problem).");
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => { if (ex.ExceptionObject is Exception error) Support.CrashLog.Write(error, "Process"); };
        TaskScheduler.UnobservedTaskException += (_, ex) => { Support.CrashLog.Write(ex.Exception, "Background task"); ex.SetObserved(); };
        // Developer switches: --settings <file> isolates settings, --no-global-hotkeys skips RegisterHotKey
        // (used for QA while another InkIt instance owns the shortcuts); --qa-capture implies both.
        var args = e.Args;
        var qaCapture = args.Contains("--qa-capture");
        var settingsIndex = Array.IndexOf(args, "--settings");
        var settingsPath = settingsIndex >= 0 && settingsIndex + 1 < args.Length ? args[settingsIndex + 1]
            : qaCapture ? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "InkIt-QA", "settings.json") : null;
        var globalHotkeys = !qaCapture && !args.Contains("--no-global-hotkeys");
        // One InkIt per session: a second launch hands its request (inkit:// link, .inkit file, --command) to this one.
        var developerRun = qaCapture || settingsIndex >= 0 || args.Contains("--no-global-hotkeys");
        if (!developerRun)
        {
            _single = new Support.SingleInstance();
            if (!_single.IsFirst)
            {
                Support.SingleInstance.Forward(args);
                _single.Dispose();
                _single = null;
                Shutdown();
                return;
            }
            Support.ShellRegistration.EnsureRegistered();
        }
        var settingsStore = new JsonSettingsStore(settingsPath);
        var startupSettings = settingsStore.LoadAsync().GetAwaiter().GetResult();
        startupSettings.Hotkeys.EnsureDefaults();
        var migrated = startupSettings.MigrateToDesign();
        if (migrated) settingsStore.SaveAsync(startupSettings).GetAwaiter().GetResult();
        ThemeManager.Initialize(startupSettings.Appearance.Theme);
        _overlays = new OverlayManager(startupSettings, settingsStore);
        _toolbar = new ToolbarWindow(_overlays, startupSettings, settingsStore) { ShowStartupExtras = !qaCapture && !args.Contains("--no-global-hotkeys") };
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
        _hotkeys.CommandInvoked += command => _toolbar.RunNamedCommand(command);
        // Esc always ends the active tool (and closes any open palette) — see DECISIONS "Global Esc invariant".
        _hotkeys.EscapePressed += (_, _) => EndCurrentTool();
        _toolbar.HotkeysChanged = config => { if (globalHotkeys) _hotkeys.Reconfigure(config); };
        if (globalHotkeys) _hotkeys.RegisterDefaults(startupSettings.Hotkeys);
        _overlays.ToolChanged += (_, _) => UpdateEscapeState();
        _overlays.BoardChanged += (_, _) => UpdateEscapeState();
        _overlays.ZoomAreaChanged += (_, _) => UpdateEscapeState();
        _overlays.FocusBoxChanged += (_, _) => UpdateEscapeState();
        _overlays.OptionsChanged += (_, _) => UpdateEscapeState();
        _toolbar.TemporaryModeChanged += (_, active) => { _temporaryModeActive = active; UpdateEscapeState(); };
        _toolbar.PaletteStateChanged += (_, _) => UpdateEscapeState();
        UpdateEscapeState();

        _tray = new TrayService(
            showToolbar: () => ShowToolbar(),
            annotate: () => _overlays.ToggleDrawing(),
            clear: () => _overlays.Clear(),
            exit: () => Shutdown(),
            extras:
            [
                ("Quick tour", () => { ShowToolbar(); _toolbar.StartTour(); }),
                ("Send feedback", _toolbar.SendFeedback),
                ("Report a problem", _toolbar.ReportProblem),
                ("Check for updates", () => _toolbar.CheckForUpdates(manual: true)),
            ]);

        _toolbar.Show();
        if (_hotkeys.Unavailable.Count > 0)
            Toast.Show("Another app already uses this shortcut: " + string.Join(", ", _hotkeys.Unavailable));


        if (_single is not null)
        {
            _single.ArgumentsReceived += forwarded => Dispatcher.BeginInvoke(() => _toolbar.HandleExternalArguments(forwarded));
            _single.Listen();
        }
        if (!developerRun && args.Length > 0) Dispatcher.BeginInvoke(() => _toolbar.HandleExternalArguments(args), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

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
            foreach (var key in new[] { "pen", "shape", "color", "laser", "zoom", "board", "grid", "highlighter", "text", "more", "marker" })
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
                settings.ShowTab(theme == AppTheme.Dark ? "About" : "Hotkeys");
                settings.BeginAnimation(UIElement.OpacityProperty, null);
                settings.Opacity = 1;
                Save(settings, theme == AppTheme.Dark ? "qa_settings_about_dark.png" : "qa_settings_hotkeys_light.png");
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
            || _toolbar?.IsZoomActive == true || _overlays?.IsFocusBoxActive == true;

        _hotkeys?.SetEscapeEnabled(shouldEnable);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Dispose();
        _tray?.Dispose();
        _single?.Dispose();
        _overlays?.Dispose();
        base.OnExit(e);
    }
}
