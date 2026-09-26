using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Win32;

namespace ScreenCanvas.Support;

/// <summary>Writes a plain-text report for any unexpected error to %LOCALAPPDATA%\InkIt\logs (last 20 kept).</summary>
public static class CrashLog
{
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InkIt", "logs");

    public static string? Write(Exception exception, string source)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(path,
                $"InkIt {AppInfo.Version}{Environment.NewLine}" +
                $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}" +
                $"Windows: {Environment.OSVersion.VersionString}{Environment.NewLine}" +
                $"Source: {source}{Environment.NewLine}{Environment.NewLine}{exception}");
            foreach (var old in new DirectoryInfo(Folder).GetFiles("crash-*.log").OrderByDescending(f => f.CreationTimeUtc).Skip(20))
                old.Delete();
            return path;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public static void OpenFolder()
    {
        Directory.CreateDirectory(Folder);
        Links.Open(Folder);
    }
}

/// <summary>Web pages InkIt opens in the default browser.</summary>
public static class Links
{
    public const string Repo = "https://github.com/aishsynk/InkIt";
    public const string LatestInstaller = Repo + "/releases/latest/download/InkIt_Setup.exe";
    public const string Releases = Repo + "/releases";

    /// <summary>The review form with the version already filled in.</summary>
    public static string Review => $"{Repo}/issues/new?template=review.yml&version={Uri.EscapeDataString(AppInfo.Version)}";

    /// <summary>The problem-report form with the version already filled in.</summary>
    public static string Problem => $"{Repo}/issues/new?template=bug.yml&version={Uri.EscapeDataString(AppInfo.Version)}";

    public static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }
}

/// <summary>Adds or removes InkIt from the current user's "run at sign-in" list.</summary>
public static class StartWithWindows
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "InkIt";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static bool TrySet(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) return false;
                key.SetValue(ValueName, $"\"{exe}\" --startup");
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName);
            }
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return false;
        }
    }
}

/// <summary>Asks GitHub for the latest release (at most once a day, and only when the user allows it).</summary>
public static class UpdateChecker
{
    public sealed record Update(string Version, string PageUrl);

    public static async Task<Update?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"InkIt/{AppInfo.Version}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        try
        {
            using var response = await http.GetAsync("https://api.github.com/repos/aishsynk/InkIt/releases/latest", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var tag = json.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v', 'V');
            var page = json.RootElement.TryGetProperty("html_url", out var url) ? url.GetString() : Links.Releases;
            if (tag is null || !Version.TryParse(tag, out var latest) || !Version.TryParse(AppInfo.Version, out var current)) return null;
            return latest > current ? new Update(tag, page ?? Links.Releases) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
