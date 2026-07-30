using System.Text.Json;
using WinMoe.Models;

namespace WinMoe.Services;

public sealed class JsonApplicationSettingsService : IApplicationSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _sync = new();

    public JsonApplicationSettingsService()
        : this(ApplicationDataPaths.ResolveFile("settings.json"))
    {
    }

    public JsonApplicationSettingsService(string settingsFilePath)
    {
        SettingsFilePath = settingsFilePath;
        Current = ReadFromDisk();
    }

    public string SettingsFilePath { get; }

    public WinMoeSettings Current { get; private set; }

    public event EventHandler<WinMoeSettings>? SettingsChanged;

    public async Task<WinMoeSettings> SaveAsync(
        WinMoeSettings settings,
        CancellationToken cancellationToken = default)
    {
        var normalized = WinMoeSettings.Normalize(settings);
        var directory = Path.GetDirectoryName(SettingsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(normalized, SerializerOptions);
        await File.WriteAllTextAsync(SettingsFilePath, json, cancellationToken).ConfigureAwait(false);

        lock (_sync)
        {
            Current = normalized;
        }

        SettingsChanged?.Invoke(this, normalized);
        return normalized;
    }

    public WinMoeSettings Reload()
    {
        var settings = ReadFromDisk();
        lock (_sync)
        {
            Current = settings;
        }

        SettingsChanged?.Invoke(this, settings);
        return settings;
    }

    private WinMoeSettings ReadFromDisk()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return WinMoeSettings.Normalize(null);
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            return WinMoeSettings.Normalize(JsonSerializer.Deserialize<WinMoeSettings>(json, SerializerOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return WinMoeSettings.Normalize(null);
        }
    }
}
