using MoleWindows.Models;

namespace MoleWindows.Services;

public interface IApplicationSettingsService
{
    string SettingsFilePath { get; }

    MoleWindowsSettings Current { get; }

    event EventHandler<MoleWindowsSettings>? SettingsChanged;

    Task<MoleWindowsSettings> SaveAsync(MoleWindowsSettings settings, CancellationToken cancellationToken = default);

    MoleWindowsSettings Reload();
}
