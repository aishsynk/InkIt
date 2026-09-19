using ScreenCanvas.Hotkeys;

namespace ScreenCanvas.Settings;

public enum AppTheme { System, Light, Dark }

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public AppearanceSettings Appearance { get; set; } = new();
    public ToolbarSettings Toolbar { get; set; } = new();
    public HotkeyConfiguration Hotkeys { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();
    public BlurSettings Blur { get; set; } = new();
    public SpotlightSettings Spotlight { get; set; } = new();
    public EyedropperSettings Eyedropper { get; set; } = new();
    public Dictionary<string, ToolProfile> ToolProfiles { get; set; } = [];
}

public sealed class ToolProfile
{
    public string Color { get; set; } = "#FF2563EB";
    public double StrokeWidth { get; set; } = 4;
    public byte Opacity { get; set; } = 255;
    public bool PressureEnabled { get; set; }
    public string? FadeDuration { get; set; }
    public bool FillEnabled { get; set; }
    public byte FillOpacity { get; set; } = 40;
}

public sealed class AppearanceSettings { public AppTheme Theme { get; set; } = AppTheme.System; public string AccentColor { get; set; } = "#FF2196F3"; public double UiScale { get; set; } = 1; }
public sealed class ToolbarSettings { public bool AutoCollapse { get; set; } public bool ShowTooltips { get; set; } = true; public double Opacity { get; set; } = 1; public double? FloatingX { get; set; } public double? FloatingY { get; set; } public bool Horizontal { get; set; } }
public sealed class AdvancedSettings { public bool HardwareAcceleration { get; set; } = true; public int HistoryLimit { get; set; } = 100; public bool DiagnosticLogging { get; set; } }

public sealed class BlurSettings { public double Radius { get; set; } = 10; public int PixelationLevel { get; set; } = 10; }
public sealed class SpotlightSettings { public double Radius { get; set; } = 110; public double OverlayOpacity { get; set; } = 0.5; }
public sealed class EyedropperSettings { public bool AutoSwitchBack { get; set; } = true; }
