using WinMoe.Models;

namespace WinMoe.Services;

public interface ISafeDeletionService
{
    LeftoverRemovalResult DeleteFileOrDirectory(string path, long sizeBytes);
}
