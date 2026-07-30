using System.Security;
using WinMoe.Models;
using Microsoft.VisualBasic.FileIO;

namespace WinMoe.Services;

public sealed class RecycleBinDeletionService : ISafeDeletionService
{
    public LeftoverRemovalResult DeleteFileOrDirectory(string path, long sizeBytes)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Refused(path, "Deletion target is empty.", sizeBytes);
            }

            var expandedPath = Environment.ExpandEnvironmentVariables(path.Trim());
            if (expandedPath.Contains('%', StringComparison.Ordinal)
                || !Path.IsPathFullyQualified(expandedPath)
                || expandedPath.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return Refused(path, "Remote, device, unresolved, or relative deletion targets are not allowed.", sizeBytes);
            }

            var fullPath = Path.GetFullPath(expandedPath);
            var rootPath = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrWhiteSpace(rootPath)
                && string.Equals(
                    fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                return Refused(path, "Drive roots cannot be deleted.", sizeBytes);
            }

            if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
            {
                return new LeftoverRemovalResult(path, true, "Path was already absent.", sizeBytes);
            }

            var attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                return Refused(path, "Deletion target is a reparse point.", sizeBytes);
            }

            if (Directory.Exists(fullPath))
            {
                FileSystem.DeleteDirectory(
                    fullPath,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin,
                    UICancelOption.ThrowException);
                return new LeftoverRemovalResult(path, true, "Directory moved to Recycle Bin.", sizeBytes);
            }

            if (File.Exists(fullPath))
            {
                FileSystem.DeleteFile(
                    fullPath,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin,
                    UICancelOption.ThrowException);
                return new LeftoverRemovalResult(path, true, "File moved to Recycle Bin.", sizeBytes);
            }

            return new LeftoverRemovalResult(path, true, "Path was already absent.", sizeBytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or OperationCanceledException or ArgumentException or NotSupportedException)
        {
            return new LeftoverRemovalResult(path, false, ex.Message, sizeBytes);
        }
    }

    private static LeftoverRemovalResult Refused(string path, string message, long sizeBytes)
    {
        return new LeftoverRemovalResult(path, false, message, sizeBytes);
    }
}
