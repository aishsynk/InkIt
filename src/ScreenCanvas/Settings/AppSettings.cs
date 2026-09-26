using ScreenCanvas.Hotkeys;

namespace ScreenCanvas.Settings;

public enum AppTheme { System, Light, Dark }

public sealed class AppSettings
{
    /// <summary>Bumped when the InkIt design defaults are applied to an older settings file.</summary>
    public const int CurrentDesignVersion = 7;

    public int SchemaVersion { get; set; } = 1;
    // Defaults to 0 so files written before this property existed are migrated.
    public int DesignVersion { get; set; }
    public AppearanceSettings Appearance { get; set; } = new();
    public ToolbarSettings Toolbar { get; set; } = new();
    public HotkeyConfiguration Hotkeys { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();
    public PresentationSettings Presentation { get; set; } = new();
    public CanvasSettings Canvas { get; set; } = new();
    public List<string> FavoriteCommands { get; set; } = [.. DefaultFavorites];
    public Dictionary<string, ToolProfile> ToolProfiles { get; set; } = [];

    public static readonly string[] DefaultFavorites =
        ["annot.pen", "annot.highlighter", "shape.arrow", "present.laser", "screen.zoom_toggle"];

    /// <summary>Moves a settings file written before the InkIt design onto the design defaults.</summary>
    public bool MigrateToDesign()
    {
        if (DesignVersion >= CurrentDesignVersion) return false;
        if (DesignVersion < 2)
        {
            Appearance.Theme = AppTheme.Dark;
            Toolbar.Horizontal = true;
            Toolbar.FloatingX = null;
            Toolbar.FloatingY = null;
            Toolbar.Items = null;
            Toolbar.Preset = "teaching";
            Presentation = new PresentationSettings();
            FavoriteCommands = [.. DefaultFavorites];
        }
        // v3: the area Screenshot button is back on the toolbar by default, next to Zoom.
        if (Toolbar.Items is { } items && items.FirstOrDefault(i => i.Id == "capture") is { Visible: false } capture)
        {
            items.Remove(capture);
            var zoom = items.FindIndex(i => i.Id == "zoom");
            capture.Visible = true;
            items.Insert(zoom >= 0 ? zoom + 1 : items.Count, capture);
        }
        // v4: buttons follow the grouped order (pointer, pen first ... search last); visibility is kept.
        if (Toolbar.Items is { } saved) Toolbar.Items = ScreenCanvas.UI.Toolbar.ToolbarCatalog.Canonicalize(saved);
        // v5: smart shapes are on by default (they can be turned off in the Pen panel).
        if (DesignVersion < 5) Canvas.AutoShapeAssist = true;
        // v7: global tool shortcuts for pen-tablet express keys (Ctrl+Alt+Shift + C/P/H/E/A/O).
        if (DesignVersion < 7) Hotkeys.AddTabletDefaults();
        DesignVersion = CurrentDesignVersion;
        return true;
    }
}

public sealed class ToolProfile
{
    public string Color { get; set; } = "#FF2563EB";
    public double StrokeWidth { get; set; } = 4;
    public byte Opacity { get; set; } = 255;
    public bool PressureEnabled { get; set; }
    public string? FadeDuration { get; set; }
    public bool FillEnabled { get; set; }
    public byte FillOpacity { get; set; } = 51;
}

public sealed class AppearanceSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public string AccentColor { get; set; } = "#FF2563EB";
    public double UiScale { get; set; } = 1;
}

public sealed class ToolbarSettings
{
    public bool AutoCollapse { get; set; }
    public bool ShowTooltips { get; set; } = true;
    public double Opacity { get; set; } = 1;
    public double? FloatingX { get; set; }
    public double? FloatingY { get; set; }
    public bool Horizontal { get; set; } = true;
    /// <summary>Ordered toolbar items with visibility; null means the design default layout.</summary>
    public List<ToolbarItemState>? Items { get; set; }
    /// <summary>Presenter preset shown in the toolbar footer ("teaching" or "techdemo").</summary>
    public string Preset { get; set; } = "teaching";
}

public sealed class ToolbarItemState
{
    public string Id { get; set; } = string.Empty;
    public bool Visible { get; set; } = true;
}

public sealed class AdvancedSettings
{
    public bool HardwareAcceleration { get; set; } = true;
    public int HistoryLimit { get; set; } = 100;
    public bool DiagnosticLogging { get; set; }
    /// <summary>Event-driven overlay monitoring (slower display polling) to keep idle CPU at zero.</summary>
    public bool IdleCpuOptimized { get; set; } = true;
    /// <summary>Look for a newer InkIt on GitHub at most once a day (the only network request InkIt makes).</summary>
    public bool CheckForUpdates { get; set; } = true;
    public DateTime? LastUpdateCheckUtc { get; set; }
    /// <summary>The first-run tour has been finished or skipped.</summary>
    public bool TourCompleted { get; set; }
}

public sealed class PresentationSettings
{
    public double SpotlightRadius { get; set; } = 180;
    public double SpotlightDimOpacity { get; set; } = 0.85;
    public double CodeFocusHeight { get; set; } = 120;
    public double CodeFocusDimOpacity { get; set; } = 0.85;
    public int BreakTimerMinutes { get; set; } = 5;
    public double ZoomFactor { get; set; } = 2.0;
    public bool ZoomFollowsMouse { get; set; }
    /// <summary>During a PowerPoint slide show, each slide keeps its own drawings.</summary>
    public bool FollowSlides { get; set; } = true;
    /// <summary>Lesson recordings include the microphone.</summary>
    public bool RecordMicrophone { get; set; } = true;
}

public sealed class CanvasSettings
{
    public bool SnapToGrid { get; set; }
    public int GridSize { get; set; } = 20;
    public bool ShowGridGuides { get; set; } = true;
    public bool SimultaneousLaser { get; set; }
    public bool SimultaneousSpotlight { get; set; }
    public bool AutoShapeAssist { get; set; } = true;
    public bool ShowStatusPill { get; set; } = true;
}
