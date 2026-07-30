using WinMoe.Models;

namespace WinMoe.Services;

public interface IPurgeArtifactService
{
    Task<IReadOnlyList<PurgeProjectCandidate>> PreviewAsync(
        IReadOnlyList<string>? searchRoots = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeftoverRemovalResult>> RemoveAsync(
        IReadOnlyList<PurgeProjectCandidate> projects,
        CancellationToken cancellationToken = default);
}
