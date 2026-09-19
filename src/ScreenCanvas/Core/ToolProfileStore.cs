using System.Windows.Media;
using ScreenCanvas.Settings;
using MediaColor = System.Windows.Media.Color;

namespace ScreenCanvas.Core;

/// <summary>Keeps tool changes from leaking visual settings into unrelated tools.</summary>
public sealed class ToolProfileStore
{
    private readonly Dictionary<ToolProfileKey, ToolStyle> _styles = [];
    private readonly AppSettings _settings;
    private readonly ISettingsStore _store;

    public ToolProfileStore(AppSettings settings, ISettingsStore store)
    {
        _settings = settings;
        _store = store;
        LoadProfiles();
        SeedDefaults();
    }

    private void LoadProfiles()
    {
        if (_settings.ToolProfiles is null) return;
        foreach (var (key, profile) in _settings.ToolProfiles)
        {
            if (StringToKey(key) is { } profileKey)
                _styles[profileKey] = ProfileToStyle(profile);
        }
    }

    private void SeedDefaults()
    {
        void Default(ToolKind tool, PenMode? pm, ShapeKind? sk, ToolStyle style)
        {
            var key = new ToolProfileKey(tool, pm, sk);
            if (!_styles.ContainsKey(key)) _styles[key] = style;
        }

        Default(ToolKind.Pen, PenMode.Ballpoint, null, new(ToolSettings.TeachingBlue, 4, 255, false, null, false, 40));
        Default(ToolKind.Highlighter, PenMode.Highlighter, null, new(ToolSettings.TeachingAmber, 18, 115, false, null, false, 40));
        Default(ToolKind.Highlighter, PenMode.StraightHighlighter, null, new(ToolSettings.TeachingAmber, 18, 115, false, null, false, 40));
        Default(ToolKind.Shape, null, ShapeKind.Arrow, new(ToolSettings.TeachingRed, 6, 255, false, null, false, 40));
        Default(ToolKind.Shape, null, ShapeKind.DoubleArrow, new(ToolSettings.TeachingRed, 6, 255, false, null, false, 40));
        Default(ToolKind.Shape, null, ShapeKind.CurvedArrow, new(ToolSettings.TeachingRed, 6, 255, false, null, false, 40));
        Default(ToolKind.Shape, null, ShapeKind.Line, new(ToolSettings.TeachingRed, 4, 255, false, null, false, 40));
        Default(ToolKind.Shape, null, ShapeKind.Rectangle, new(ToolSettings.TeachingBlue, 4, 255, false, null, false, 40));
        Default(ToolKind.Shape, null, ShapeKind.Circle, new(ToolSettings.TeachingBlue, 4, 255, false, null, false, 40));
        Default(ToolKind.Shape, null, ShapeKind.Triangle, new(ToolSettings.TeachingBlue, 4, 255, false, null, false, 40));
        Default(ToolKind.Laser, null, null, new(ToolSettings.TeachingRed, 8, 255, false, null, false, 40));
        Default(ToolKind.Text, null, null, new(ToolSettings.TeachingBlue, 4, 255, false, null, false, 40));
    }

    public void Save(ToolSettings settings)
    {
        var key = ToolProfileKey.From(settings.Tool, settings.PenMode, settings.Shape);
        var style = ToolStyle.Capture(settings);
        _styles[key] = style;
        PersistProfile(key, style);
    }

    public bool Restore(ToolSettings settings, ToolKind tool, PenMode penMode, ShapeKind shape)
    {
        if (!_styles.TryGetValue(ToolProfileKey.From(tool, penMode, shape), out var style)) return false;
        style.Apply(settings);
        return true;
    }

    private void PersistProfile(ToolProfileKey key, ToolStyle style)
    {
        _settings.ToolProfiles ??= [];
        _settings.ToolProfiles[KeyToString(key)] = StyleToProfile(style);
        _ = _store.SaveAsync(_settings);
    }

    private static ToolProfile StyleToProfile(ToolStyle s) => new()
    {
        Color = ColorToString(s.Color),
        StrokeWidth = s.Thickness,
        Opacity = s.Opacity,
        PressureEnabled = s.PressureEnabled,
        FadeDuration = s.FadeDuration?.ToString(),
        FillEnabled = s.FillEnabled,
        FillOpacity = s.FillOpacity
    };

    private static ToolStyle ProfileToStyle(ToolProfile p) => new(
        StringToColor(p.Color),
        Math.Clamp(p.StrokeWidth, 1, 64),
        p.Opacity,
        p.PressureEnabled,
        string.IsNullOrEmpty(p.FadeDuration) ? null : TimeSpan.Parse(p.FadeDuration),
        p.FillEnabled,
        p.FillOpacity
    );

    private static string ColorToString(MediaColor c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
    private static MediaColor StringToColor(string s) => (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(s);

    private static string KeyToString(ToolProfileKey key)
    {
        if (key.PenMode.HasValue) return $"{key.Tool}_{key.PenMode}";
        if (key.Shape.HasValue) return $"{key.Tool}_{key.Shape}";
        return key.Tool.ToString();
    }

    private static ToolProfileKey? StringToKey(string s)
    {
        var parts = s.Split('_', 2);
        if (!Enum.TryParse<ToolKind>(parts[0], out var tool)) return null;
        if (parts.Length == 1) return new(tool, null, null);
        if (Enum.TryParse<PenMode>(parts[1], out var pm)) return new(tool, pm, null);
        if (Enum.TryParse<ShapeKind>(parts[1], out var sk)) return new(tool, null, sk);
        return new(tool, null, null);
    }

    private readonly record struct ToolProfileKey(ToolKind Tool, PenMode? PenMode, ShapeKind? Shape)
    {
        public static ToolProfileKey From(ToolKind tool, PenMode penMode, ShapeKind shape) =>
            new(tool,
                tool is ToolKind.Pen or ToolKind.Highlighter ? penMode : null,
                tool is ToolKind.Shape ? shape : null);
    }
}
