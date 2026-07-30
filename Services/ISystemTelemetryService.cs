using MoleWindows.Models;

namespace MoleWindows.Services;

public interface ISystemTelemetryService
{
    Task<SystemTelemetrySnapshot> CaptureAsync(CancellationToken cancellationToken = default);
}
