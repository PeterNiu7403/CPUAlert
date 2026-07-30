using MoleWindows.Models;

namespace MoleWindows.Services;

public interface IDiskAnalyzerService
{
    Task<DiskUsageNode> AnalyzeAsync(
        string rootPath,
        DiskAnalysisOptions options,
        CancellationToken cancellationToken = default);
}
