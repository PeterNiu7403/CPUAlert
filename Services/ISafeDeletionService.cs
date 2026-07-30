using MoleWindows.Models;

namespace MoleWindows.Services;

public interface ISafeDeletionService
{
    LeftoverRemovalResult DeleteFileOrDirectory(string path, long sizeBytes);
}
