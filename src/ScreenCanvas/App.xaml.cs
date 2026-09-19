using System.Windows;
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
        var settingsStore = new JsonSettingsStore();
        var startupSettings = settingsStore.LoadAsync().GetAwaiter().GetResult();
        ThemeManager.Initialize(startupSettings.Appearance.Theme);
        _overlays = new OverlayManager(startupSettings, settingsStore);
        _toolbar = new ToolbarWindow(_overlays, startupSettings, settingsStore);
        _hotkeys = new HotkeyManager(_toolbar);
        _hotkeys.EmergencyStop += (_, _) => EmergencyStop();
        _hotkeys.ToggleDrawing += (_, _) => _overlays.ToggleDrawing();
        _hotkeys.Undo += (_, _) => _overlays.Undo();
        _hotkeys.Clear += (_, _) => _overlays.Clear();
        _hotkeys.EscapePressed += (_, _) =>
        {
            if (_toolbar?.IsPaletteOpen == true && _overlays.CurrentBoardColor == null && !_toolbar.IsZoomActive && !_temporaryModeActive)
            {
                _toolbar.CloseMenus();
                UpdateEscapeState();
                return;
            }

            EndCurrentTool();
        };
        _toolbar.HotkeysChanged = config => _hotkeys.Reconfigure(config);
        _hotkeys.RegisterDefaults(startupSettings.Hotkeys);
        _overlays.ToolChanged += (_, _) => UpdateEscapeState();
        _overlays.BoardChanged += (_, _) => UpdateEscapeState();
        _toolbar.TemporaryModeChanged += (_, active) => { _temporaryModeActive = active; UpdateEscapeState(); };
        _toolbar.PaletteStateChanged += (_, _) => UpdateEscapeState();
        UpdateEscapeState();

        _tray = new TrayService(
            showToolbar: () => ShowToolbar(),
            annotate: () => _overlays.ToggleDrawing(),
            clear: () => _overlays.Clear(),
            exit: () => Shutdown());

        _toolbar.Show();

        if (e.Args.Length > 0 && e.Args[0] == "--qa-capture")
        {
            RunQaCapture(_toolbar, _overlays);
            return;
        }
    }

    private void RunQaCapture(ToolbarWindow toolbar, OverlayManager overlays)
    {
        var outDir = System.IO.Path.GetFullPath(@"artifacts/screenshots");
        System.IO.Directory.CreateDirectory(outDir);

        System.Drawing.Bitmap CaptureElement(FrameworkElement elem)
        {
            elem.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            elem.Arrange(new Rect(elem.DesiredSize));
            elem.UpdateLayout();

            int w = Math.Max(1, (int)Math.Ceiling(elem.ActualWidth > 0 ? elem.ActualWidth : elem.DesiredSize.Width));
            int h = Math.Max(1, (int)Math.Ceiling(elem.ActualHeight > 0 ? elem.ActualHeight : elem.DesiredSize.Height));

            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(elem);

            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            using var ms = new System.IO.MemoryStream();
            enc.Save(ms);
            ms.Position = 0;
            return new System.Drawing.Bitmap(ms);
        }

        void SaveBitmap(System.Drawing.Bitmap bmp, string filename)
        {
            bmp.Save(System.IO.Path.Combine(outDir, filename), System.Drawing.Imaging.ImageFormat.Png);
        }

        // 1. Horizontal Toolbar
        toolbar.ApplyOrientation(true);
        toolbar.UpdateLayout();
        using var bmpHoriz = CaptureElement(toolbar.ToolbarChrome);
        SaveBitmap(bmpHoriz, "qa_horizontal_toolbar.png");

        // 2. Vertical Toolbar
        toolbar.ApplyOrientation(false);
        toolbar.UpdateLayout();
        using var bmpVert = CaptureElement(toolbar.ToolbarChrome);
        SaveBitmap(bmpVert, "qa_vertical_toolbar.png");

        // Restore Horizontal
        toolbar.ApplyOrientation(true);
        toolbar.UpdateLayout();

        // 3. Composite Toolbar + Pen Inspector
        var inspector = new InspectorWindow(overlays, toolbar);
        inspector.ShowCategory("pen", toolbar.PenButton);
        inspector.UpdateLayout();

        void SaveComposite(string filename)
        {
            using var bmpTool = CaptureElement(toolbar.ToolbarChrome);
            using var bmpInsp = CaptureElement(inspector.InspectorChrome);
            int compW = Math.Max(bmpTool.Width, bmpInsp.Width);
            int compH = bmpTool.Height + 8 + bmpInsp.Height;
            using var comp = new System.Drawing.Bitmap(compW, compH);
            using (var g = System.Drawing.Graphics.FromImage(comp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.DrawImage(bmpTool, (compW - bmpTool.Width) / 2, 0);
                g.DrawImage(bmpInsp, (compW - bmpInsp.Width) / 2, bmpTool.Height + 8);
            }
            SaveBitmap(comp, filename);
        }

        SaveComposite("qa_pen_inspector_teaching.png");

        // 4. Composite Toolbar + Shapes Inspector
        inspector.ShowCategory("shapes", toolbar.ShapesButton);
        inspector.UpdateLayout();
        SaveComposite("qa_shapes_inspector_markers.png");

        // 5. Composite Toolbar + Highlighter Inspector
        inspector.ShowCategory("highlighter", toolbar.HighlighterButton);
        inspector.UpdateLayout();
        SaveComposite("qa_highlighter_inspector.png");

        // 6. Composite Toolbar + Zoom Inspector
        inspector.ShowCategory("zoom", toolbar.ZoomButton);
        inspector.UpdateLayout();
        SaveComposite("qa_zoom_inspector.png");

        // 7. Vertical Docked Companions (Side-by-Side)
        toolbar.ApplyOrientation(false);
        toolbar.UpdateLayout();

        void SaveSideBySide(string filename)
        {
            using var bmpTool = CaptureElement(toolbar.ToolbarChrome);
            using var bmpInsp = CaptureElement(inspector.InspectorChrome);
            int compW = bmpTool.Width + 8 + bmpInsp.Width;
            int compH = Math.Max(bmpTool.Height, bmpInsp.Height);
            using var comp = new System.Drawing.Bitmap(compW, compH);
            using (var g = System.Drawing.Graphics.FromImage(comp))
            {
                g.Clear(System.Drawing.Color.Transparent);
                g.DrawImage(bmpTool, 0, (compH - bmpTool.Height) / 2);
                g.DrawImage(bmpInsp, bmpTool.Width + 8, (compH - bmpInsp.Height) / 2);
            }
            SaveBitmap(comp, filename);
        }

        inspector.ShowCategory("pen", toolbar.PenButton);
        inspector.UpdateLayout();
        SaveSideBySide("qa_vertical_pen_docked.png");

        inspector.ShowCategory("shapes", toolbar.ShapesButton);
        inspector.UpdateLayout();
        SaveSideBySide("qa_vertical_shapes_docked.png");

        inspector.ShowCategory("more", toolbar.MoreButton);
        inspector.UpdateLayout();
        SaveSideBySide("qa_vertical_more_docked.png");

        inspector.Close();
        Shutdown();
    }

    private void ShowToolbar()
    {
        if (_toolbar is null) return;
        _toolbar.Show();
        _toolbar.Activate();
    }

    private void EmergencyStop()
    {
        _toolbar?.EndCurrentTool();
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
