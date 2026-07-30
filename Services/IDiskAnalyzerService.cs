using WinMoe.Models;

namespace WinMoe.Services;

public interface IDiskAnalyzerService
{
    Task<DiskUsageNode> AnalyzeAsync(
        string rootPath,
        DiskAnalysisOptions options,
        CancellationToken cancellationToken = default);
}
