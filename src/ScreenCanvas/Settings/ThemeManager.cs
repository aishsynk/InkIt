using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;
namespace ScreenCanvas.Settings;
public static class ThemeManager
{
    private static AppTheme _preference=AppTheme.System; private static bool _listening;
    public static void Initialize(AppTheme preference){_preference=preference;Apply();if(_listening)return;SystemEvents.UserPreferenceChanged+=OnChanged;_listening=true;}
    public static void SetPreference(AppTheme preference){_preference=preference;Apply();}
    private static void OnChanged(object sender,UserPreferenceChangedEventArgs e){if(_preference==AppTheme.System)System.Windows.Application.Current?.Dispatcher.BeginInvoke(Apply);}
    private static bool IsWindowsLight(){try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");return Convert.ToInt32(key?.GetValue("AppsUseLightTheme",1))!=0;}catch{return true;}}
    private static void Apply()
    {
        var r = System.Windows.Application.Current?.Resources;
        if (r is null) return;
        var light = _preference == AppTheme.Light || (_preference == AppTheme.System && IsWindowsLight());

        Set(r, "PanelBrush", light ? "#F7FBFBFC" : "#F51E1E22");
        Set(r, "Level2Brush", light ? "#FAFFFFFF" : "#F528282E");
        Set(r, "PrimaryTextBrush", light ? "#FF1D1D1F" : "#FFF5F5F7");
        Set(r, "SecondaryTextBrush", light ? "#FF86868B" : "#FF98989D");
        Set(r, "MutedTextBrush", light ? "#FFB0B0B5" : "#FF636366");
        Set(r, "HoverBrush", light ? "#0B000000" : "#14FFFFFF");
        Set(r, "PressedBrush", light ? "#14000000" : "#20FFFFFF");
        Set(r, "SelectedBrush", light ? "#E8F2FF" : "#26007AFF");
        Set(r, "BorderBrush", light ? "#0D000000" : "#1AFFFFFF");
        Set(r, "SeparatorBrush", light ? "#12000000" : "#18FFFFFF");
        Set(r, "DragHandleBrush", light ? "#FFC7C7CC" : "#FF636366");
        Set(r, "TrackBrush", light ? "#FFE5E5EA" : "#FF3A3A3C");
        Set(r, "ThumbBrush", light ? "#FFFFFFFF" : "#FFF5F5F7");
        Set(r, "AccentBrush", light ? "#FF007AFF" : "#FF0A84FF");

        var specular = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(0, 1),
            GradientStops =
            {
                new GradientStop(light ? System.Windows.Media.Color.FromArgb(40, 0, 0, 0) : System.Windows.Media.Color.FromArgb(50, 255, 255, 255), 0.0),
                new GradientStop(light ? System.Windows.Media.Color.FromArgb(10, 0, 0, 0) : System.Windows.Media.Color.FromArgb(12, 255, 255, 255), 1.0)
            }
        };
        r["GlassSpecularBrush"] = specular;
    }
    private static void Set(ResourceDictionary r, string key, string value) => r[key] = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value));
}
