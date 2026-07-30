using CommunityToolkit.Mvvm.ComponentModel;
using WinMoe.Services;

namespace WinMoe.Models;

public partial class InstalledApplication : ObservableObject
{
    public InstalledApplication(
        string id,
        string name,
        string? publisher,
        string? version,
        string? installLocation,
        string? uninstallString,
        string source,
        long sizeBytes,
        string? iconPath = null,
        DateTimeOffset? lastActivityUtc = null,
        string? installDateRaw = null)
    {
        Id = id;
        Name = name;
        Publisher = publisher ?? string.Empty;
        Version = version ?? string.Empty;
        InstallLocation = installLocation ?? string.Empty;
        UninstallString = uninstallString ?? string.Empty;
        Source = source;
        SizeBytes = sizeBytes;
        IconPath = iconPath ?? string.Empty;
        LastActivityUtc = lastActivityUtc;
        InstallDateRaw = installDateRaw ?? string.Empty;
    }

    public string Id { get; }

    public string Name { get; }

    public string Publisher { get; }

    public string Version { get; }

    public string InstallLocation { get; }

    public string UninstallString { get; }

    public string Source { get; }

    public long SizeBytes { get; }

    /// <summary>Raw DisplayIcon / install-path hint (may include ",0" suffix).</summary>
    public string IconPath { get; }

    /// <summary>Best-effort last activity (exe/dir mtime or registry InstallDate).</summary>
    public DateTimeOffset? LastActivityUtc { get; }

    /// <summary>Registry InstallDate raw (yyyyMMdd) when present.</summary>
    public string InstallDateRaw { get; }

    public string SizeText => SizeBytes <= 0 ? "Unknown" : SystemTelemetryFormatter.Bytes(SizeBytes);

    public string Initials => string.IsNullOrWhiteSpace(Name) ? "?" : Name[..1].ToUpperInvariant();

    public string DetailLine
    {
        get
        {
            var location = string.IsNullOrWhiteSpace(InstallLocation) ? "No install path" : InstallLocation;
            return $"{SizeText} - {Source} - {location}";
        }
    }

    [ObservableProperty]
    private bool isSelected;
}
