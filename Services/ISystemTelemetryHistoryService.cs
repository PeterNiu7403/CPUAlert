using WinMoe.Models;

namespace WinMoe.Services;

public interface ISystemTelemetryHistoryService
{
    string HistoryFilePath { get; }

    Task RecordAsync(SystemTelemetrySnapshot snapshot, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SystemTelemetrySnapshot>> ReadRecentAsync(int limit, CancellationToken cancellationToken = default);
}
