using System.Text.Json;
using MoleWindows.Models;

namespace MoleWindows.Services;

public sealed class JsonApplicationSettingsService : IApplicationSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _sync = new();

    public JsonApplicationSettingsService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MoleWindows",
            "settings.json"))
    {
    }

    public JsonApplicationSettingsService(string settingsFilePath)
    {
        SettingsFilePath = settingsFilePath;
        Current = ReadFromDisk();
    }

    public string SettingsFilePath { get; }

    public MoleWindowsSettings Current { get; private set; }

    public event EventHandler<MoleWindowsSettings>? SettingsChanged;

    public async Task<MoleWindowsSettings> SaveAsync(
        MoleWindowsSettings settings,
        CancellationToken cancellationToken = default)
    {
        var normalized = MoleWindowsSettings.Normalize(settings);
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

    public MoleWindowsSettings Reload()
    {
        var settings = ReadFromDisk();
        lock (_sync)
        {
            Current = settings;
        }

        SettingsChanged?.Invoke(this, settings);
        return settings;
    }

    private MoleWindowsSettings ReadFromDisk()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return MoleWindowsSettings.Normalize(null);
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            return MoleWindowsSettings.Normalize(JsonSerializer.Deserialize<MoleWindowsSettings>(json, SerializerOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return MoleWindowsSettings.Normalize(null);
        }
    }
}
