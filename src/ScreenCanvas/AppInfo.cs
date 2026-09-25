namespace ScreenCanvas;

/// <summary>Product facts shown to users (version comes from the release stamp, 0.0.0.N).</summary>
public static class AppInfo
{
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetName().Version?.ToString(4) ?? "0.0.0.0";

    public const string HomePage = "https://github.com/aishsynk/InkIt";
}
