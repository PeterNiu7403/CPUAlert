using WinMoe.Models;

namespace WinMoe.Services;

public interface IApplicationSettingsService
{
    string SettingsFilePath { get; }

    WinMoeSettings Current { get; }

    event EventHandler<WinMoeSettings>? SettingsChanged;

    Task<WinMoeSettings> SaveAsync(WinMoeSettings settings, CancellationToken cancellationToken = default);

    WinMoeSettings Reload();
}
