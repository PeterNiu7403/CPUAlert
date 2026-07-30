using WinMoe.Models;

namespace WinMoe.Services;

public interface ISystemTelemetryService
{
    Task<SystemTelemetrySnapshot> CaptureAsync(CancellationToken cancellationToken = default);
}
