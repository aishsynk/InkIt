using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
using ScreenCanvas.UI.Theme;
using Color = System.Windows.Media.Color;

namespace ScreenCanvas.Settings;

/// <summary>
/// Applies the InkIt design palette. Dark (slate) is the design default; Light is a matching variant.
/// Semantic brushes use the "Ink.*" keys and are consumed as DynamicResource so a theme switch updates live.
/// </summary>
public static class ThemeManager
{
    private static AppTheme _preference = AppTheme.Dark;
    private static bool _listening;

    public static bool IsDark { get; private set; } = true;
    public static AppTheme Preference => _preference;
    public static event EventHandler? ThemeChanged;

    public static void Initialize(AppTheme preference)
    {
        _preference = preference;
        Apply();
        if (_listening) return;
        SystemEvents.UserPreferenceChanged += OnChanged;
        _listening = true;
    }

    public static void SetPreference(AppTheme preference)
    {
        _preference = preference;
        Apply();
    }

    private static void OnChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_preference == AppTheme.System) System.Windows.Application.Current?.Dispatcher.BeginInvoke(Apply);
    }

    private static bool IsWindowsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 1)) != 0;
        }
        catch { return true; }
    }

    private static void Apply()
    {
        var r = System.Windows.Application.Current?.Resources;
        if (r is null) return;
        var dark = _preference == AppTheme.Dark || (_preference == AppTheme.System && !IsWindowsLight());
        IsDark = dark;

        void Set(string key, Color color, double alpha = 1) => r[key] = Tw.B(color, alpha);
        void SetD(string key, Color darkColor, double darkAlpha, Color lightColor, double lightAlpha) =>
            r[key] = dark ? Tw.B(darkColor, darkAlpha) : Tw.B(lightColor, lightAlpha);

        SetD("Ink.Surface", Tw.Slate900, 0.95, Colors.White, 0.97);
        SetD("Ink.SurfaceSolid", Tw.Slate900, 1, Colors.White, 1);
        SetD("Ink.Popover", Tw.Slate900, 0.98, Colors.White, 0.99);
        SetD("Ink.Sunken", Tw.Slate950, 0.6, Tw.Slate100, 1);
        SetD("Ink.Footer", Tw.Slate950, 0.8, Tw.Slate100, 1);
        SetD("Ink.Raised", Tw.Slate800, 0.6, Tw.Slate100, 1);
        SetD("Ink.Raised40", Tw.Slate800, 0.4, Tw.Slate50, 1);
        SetD("Ink.Raised70", Tw.Slate800, 0.7, Tw.Slate100, 1);
        SetD("Ink.RaisedHover", Tw.Slate800, 0.9, Tw.Slate200, 1);
        SetD("Ink.Control", Tw.Slate800, 1, Tw.Slate100, 1);
        SetD("Ink.ControlHover", Tw.Slate700, 1, Tw.Slate200, 1);
        SetD("Ink.Hover", Tw.Slate800, 1, Tw.Slate200, 1);
        SetD("Ink.Border", Tw.Slate700, 0.8, Tw.Slate200, 1);
        SetD("Ink.BorderStrong", Tw.Slate700, 1, Tw.Slate300, 1);
        SetD("Ink.Divider", Tw.Slate800, 1, Tw.Slate200, 1);
        SetD("Ink.Text", Colors.White, 1, Tw.Slate900, 1);
        SetD("Ink.Text200", Tw.Slate200, 1, Tw.Slate800, 1);
        SetD("Ink.Text300", Tw.Slate300, 1, Tw.Slate700, 1);
        SetD("Ink.Text400", Tw.Slate400, 1, Tw.Slate500, 1);
        SetD("Ink.Text500", Tw.Slate500, 1, Tw.Slate400, 1);
        SetD("Ink.Text600", Tw.Slate600, 1, Tw.Slate300, 1);
        SetD("Ink.Track", Tw.Slate700, 1, Tw.Slate300, 1);
        SetD("Ink.Backdrop", Colors.Black, 0.6, Tw.Slate900, 0.25);
        SetD("Ink.BackdropLight", Colors.Black, 0.4, Tw.Slate900, 0.15);
        SetD("Ink.Kbd", Tw.Slate900, 1, Colors.White, 1);
        SetD("Ink.Kbd950", Tw.Slate950, 1, Tw.Slate100, 1);
        SetD("Ink.Selected", Tw.Blue950, 0.4, Tw.Hex("#EFF6FF"), 1);
        SetD("Ink.Code", Tw.Blue400, 1, Tw.Blue600, 1);
        SetD("Ink.ToolbarText", Tw.Slate300, 1, Tw.Slate600, 1);
        SetD("Ink.ToolbarMuted", Tw.Slate400, 1, Tw.Slate500, 1);
        SetD("Ink.GripText", Tw.Slate500, 1, Tw.Slate400, 1);

        // Legacy keys kept for windows that predate the design system.
        SetD("PanelBrush", Tw.Slate900, 0.95, Colors.White, 0.97);
        SetD("Level2Brush", Tw.Slate800, 1, Colors.White, 1);
        Set("AccentBrush", Tw.Blue600);
        SetD("PrimaryTextBrush", Colors.White, 1, Tw.Slate900, 1);
        SetD("SecondaryTextBrush", Tw.Slate400, 1, Tw.Slate500, 1);
        SetD("MutedTextBrush", Tw.Slate500, 1, Tw.Slate400, 1);
        SetD("HoverBrush", Tw.Slate800, 1, Tw.Slate200, 1);
        SetD("PressedBrush", Tw.Slate700, 1, Tw.Slate300, 1);
        SetD("SelectedBrush", Tw.Blue600, 1, Tw.Blue600, 1);
        SetD("BorderBrush", Tw.Slate700, 0.8, Tw.Slate200, 1);
        SetD("SeparatorBrush", Tw.Slate800, 1, Tw.Slate200, 1);
        SetD("DragHandleBrush", Tw.Slate500, 1, Tw.Slate400, 1);
        SetD("TrackBrush", Tw.Slate700, 1, Tw.Slate300, 1);
        SetD("ThumbBrush", Colors.White, 1, Colors.White, 1);
        r["GlassSpecularBrush"] = Tw.B(Colors.Transparent);

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }
}
