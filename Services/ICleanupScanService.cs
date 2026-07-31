using WinMoe.Models;

namespace WinMoe.Services;

/// <summary>
/// Native cleanup scanner: enumerates well-known cache/temp locations (the same
/// knowledge Mole's PowerShell clean modules carry), measures each target with
/// reparse-point and access-denied guards, and returns review items whose paths
/// can be recycled through the existing operation-plan contract.
/// </summary>
public interface ICleanupScanService
{
    Task<IReadOnlyList<CleanupPreviewItem>> ScanAsync(CancellationToken cancellationToken = default);
}
