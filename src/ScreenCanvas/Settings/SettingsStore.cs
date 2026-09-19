using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace ScreenCanvas.Settings;

public interface ISettingsStore
{
    string FilePath { get; }
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public sealed class JsonSettingsStore(string? filePath = null) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string FilePath { get; } = filePath ?? Path.Combine(
        ResolveLocalAppData(), "InkIt", "settings.json");

    private static string ResolveLocalAppData()
    {
        var folder=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if(!string.IsNullOrWhiteSpace(folder))return folder;
        folder=Environment.GetEnvironmentVariable("LOCALAPPDATA");
        return string.IsNullOrWhiteSpace(folder)?Path.Combine(AppContext.BaseDirectory,"Settings"):folder;
    }

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath)) return Task.FromResult(new AppSettings());
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json=File.ReadAllText(FilePath);
            return Task.FromResult(JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings());
        }
        catch (JsonException) { return Task.FromResult(new AppSettings()); }
        catch (IOException) { return Task.FromResult(new AppSettings()); }
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(settings,Options));
        File.Move(temporary, FilePath, true);
        return Task.CompletedTask;
    }
}
