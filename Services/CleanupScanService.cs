using WinMoe.Models;

namespace WinMoe.Services;

/// <inheritdoc cref="ICleanupScanService"/>
public sealed class CleanupScanService : ICleanupScanService
{
    /// <summary>Skip user-temp entries touched within this window (likely in use).</summary>
    private static readonly TimeSpan TempEntryMinAge = TimeSpan.FromHours(24);

    /// <summary>Bound the number of user-temp top-level entries offered for review.</summary>
    private const int MaxTempEntries = 40;

    private const string CategoryTemp = "用户临时文件";
    private const string CategoryBrowser = "浏览器缓存";
    private const string CategoryApps = "应用缓存";
    private const string CategoryDev = "开发工具缓存";
    private const string CategoryDumps = "崩溃转储与日志";

    public Task<IReadOnlyList<CleanupPreviewItem>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<CleanupPreviewItem>();

        ScanUserTemp(items, cancellationToken);
        ScanVolumeTempRoots(items, cancellationToken);
        foreach (var (category, path) in EnumerateWellKnownTargets())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(path) && !File.Exists(path))
            {
                continue;
            }

            var (sizeBytes, fileCount) = Measure(path, cancellationToken);
            if (sizeBytes <= 0)
            {
                continue;
            }

            items.Add(new CleanupPreviewItem(
                category,
                path,
                SystemTelemetryFormatter.Bytes(sizeBytes),
                sizeBytes,
                fileCount));
        }

        IReadOnlyList<CleanupPreviewItem> result = items
            .OrderBy(item => CategoryOrder(item.Category))
            .ThenByDescending(item => item.SizeBytes)
            .ToArray();
        return Task.FromResult(result);
    }

    private static int CategoryOrder(string category) => category switch
    {
        CategoryTemp => 0,
        CategoryBrowser => 1,
        CategoryApps => 2,
        CategoryDev => 3,
        CategoryDumps => 4,
        _ => 9
    };

    private void ScanUserTemp(List<CleanupPreviewItem> items, CancellationToken cancellationToken)
    {
        var tempRoot = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var (path, sizeBytes, fileCount) in SelectTempEntries(
                     tempRoot,
                     TempEntryMinAge,
                     MaxTempEntries,
                     cancellationToken))
        {
            items.Add(new CleanupPreviewItem(
                CategoryTemp,
                path,
                SystemTelemetryFormatter.Bytes(sizeBytes),
                sizeBytes,
                fileCount));
        }
    }

    /// <summary>
    /// Junk is not confined to the system drive: scan the conventional Temp/tmp
    /// folder at the root of every fixed volume with the same age/size rules.
    /// </summary>
    private void ScanVolumeTempRoots(List<CleanupPreviewItem> items, CancellationToken cancellationToken)
    {
        foreach (var tempRoot in SelectVolumeTempRoots(FixedVolumeRoots(), Path.GetTempPath()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var (path, sizeBytes, fileCount) in SelectTempEntries(
                         tempRoot,
                         TempEntryMinAge,
                         MaxTempEntries,
                         cancellationToken))
            {
                items.Add(new CleanupPreviewItem(
                    CategoryTemp,
                    path,
                    SystemTelemetryFormatter.Bytes(sizeBytes),
                    sizeBytes,
                    fileCount));
            }
        }
    }

    /// <summary>
    /// Existing root-level Temp/tmp folders across the given volumes, normalized and
    /// deduplicated against the user temp root (in case TEMP points at another drive).
    /// </summary>
    internal static IReadOnlyList<string> SelectVolumeTempRoots(
        IEnumerable<string> volumeRoots,
        string userTempRoot)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            NormalizeTempRoot(userTempRoot)
        };
        var roots = new List<string>();
        foreach (var volumeRoot in volumeRoots)
        {
            foreach (var name in new[] { "Temp", "tmp" })
            {
                var candidate = Path.Combine(volumeRoot, name);
                if (!seen.Add(NormalizeTempRoot(candidate)) || !Directory.Exists(candidate))
                {
                    continue;
                }

                roots.Add(candidate);
            }
        }

        return roots;
    }

    private static string NormalizeTempRoot(string path)
    {
        return Path.GetFullPath(path.Trim())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static IEnumerable<string> FixedVolumeRoots()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            string? root = null;
            try
            {
                if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                {
                    root = drive.RootDirectory.FullName;
                }
            }
            catch
            {
                // A drive can vanish (USB/eject) between enumeration and query.
            }

            if (root is not null)
            {
                yield return root;
            }
        }
    }

    /// <summary>
    /// Top-level temp entries older than <paramref name="minAge"/>, largest first,
    /// capped at <paramref name="maxEntries"/>. Reparse points are never offered.
    /// </summary>
    internal static IReadOnlyList<(string Path, long SizeBytes, int FileCount)> SelectTempEntries(
        string tempRoot,
        TimeSpan minAge,
        int maxEntries,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(tempRoot))
        {
            return [];
        }

        var cutoff = DateTimeOffset.Now - minAge;
        var candidates = new List<(string Path, long SizeBytes, int FileCount)>();
        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(tempRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Junctions/symlinks in temp are never followed or offered.
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                var lastWrite = File.GetLastWriteTime(entry);
                if (lastWrite > cutoff.DateTime)
                {
                    continue;
                }

                var (sizeBytes, fileCount) = Measure(entry, cancellationToken);
                if (sizeBytes > 0)
                {
                    candidates.Add((entry, sizeBytes, fileCount));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Entry vanished or is locked by another process mid-scan.
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.SizeBytes)
            .Take(maxEntries)
            .ToArray();
    }

    /// <summary>
    /// Well-known safe-to-clean locations, mirroring Mole's lib/clean knowledge
    /// (browser/app/dev caches, crash dumps). Only user-profile paths are listed.
    /// </summary>
    private static IEnumerable<(string Category, string Path)> EnumerateWellKnownTargets()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        foreach (var path in ChromiumCaches(Path.Combine(local, @"Google\Chrome\User Data")))
        {
            yield return (CategoryBrowser, path);
        }

        foreach (var path in ChromiumCaches(Path.Combine(local, @"Microsoft\Edge\User Data")))
        {
            yield return (CategoryBrowser, path);
        }

        yield return (CategoryBrowser, Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data\Default\Cache"));
        yield return (CategoryBrowser, Path.Combine(roaming, @"Opera Software\Opera Stable\Cache"));

        var firefoxProfiles = Path.Combine(roaming, @"Mozilla\Firefox\Profiles");
        if (Directory.Exists(firefoxProfiles))
        {
            IEnumerable<string> profiles;
            try
            {
                profiles = Directory.EnumerateDirectories(firefoxProfiles);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                profiles = [];
            }

            foreach (var profileDir in profiles)
            {
                yield return (CategoryBrowser, Path.Combine(profileDir, "cache2"));
            }
        }

        foreach (var path in new[]
                 {
                     Path.Combine(roaming, @"Code\Cache"),
                     Path.Combine(roaming, @"Code\CachedData"),
                     Path.Combine(roaming, @"Code\CachedExtensions"),
                     Path.Combine(roaming, @"Code\CachedExtensionVSIXs"),
                     Path.Combine(roaming, @"Code\Code Cache"),
                     Path.Combine(roaming, @"Code\GPUCache"),
                     Path.Combine(roaming, @"discord\Cache"),
                     Path.Combine(roaming, @"discord\Code Cache"),
                     Path.Combine(roaming, @"discord\GPUCache"),
                     Path.Combine(roaming, @"Slack\Cache"),
                     Path.Combine(roaming, @"Slack\Code Cache"),
                     Path.Combine(roaming, @"Slack\GPUCache"),
                     Path.Combine(roaming, @"Slack\Service Worker\CacheStorage"),
                     Path.Combine(roaming, @"Microsoft\Teams\Cache"),
                     Path.Combine(roaming, @"Microsoft\Teams\GPUCache"),
                     Path.Combine(roaming, @"Microsoft\Teams\tmp"),
                     Path.Combine(local, @"Spotify\Data"),
                     Path.Combine(roaming, @"Zoom\data")
                 })
        {
            yield return (CategoryApps, path);
        }

        foreach (var path in new[]
                 {
                     Path.Combine(roaming, "npm-cache"),
                     Path.Combine(local, @"pnpm\store"),
                     Path.Combine(local, @"Yarn\Cache"),
                     Path.Combine(profile, @".bun\install\cache"),
                     Path.Combine(local, @"node-gyp\Cache"),
                     Path.Combine(local, @"electron\Cache"),
                     Path.Combine(local, "TypeScript"),
                     Path.Combine(local, @"pip\Cache")
                 })
        {
            yield return (CategoryDev, path);
        }

        yield return (CategoryDumps, Path.Combine(local, "CrashDumps"));
        yield return (CategoryDumps, Path.Combine(local, @"Microsoft\Windows\WER"));
    }

    private static IEnumerable<string> ChromiumCaches(string userDataRoot)
    {
        yield return Path.Combine(userDataRoot, @"Default\Cache");
        yield return Path.Combine(userDataRoot, @"Default\Code Cache");
        yield return Path.Combine(userDataRoot, @"Default\GPUCache");
        yield return Path.Combine(userDataRoot, @"Default\Service Worker\CacheStorage");
        yield return Path.Combine(userDataRoot, "ShaderCache");
        yield return Path.Combine(userDataRoot, "GrShaderCache");
    }

    /// <summary>
    /// Recursive size/count with the same safety contract as the disk analyzer:
    /// reparse points are never followed; locked or denied subtrees are skipped.
    /// </summary>
    internal static (long SizeBytes, int FileCount) Measure(string path, CancellationToken cancellationToken = default)
    {
        long size = 0;
        var count = 0;

        try
        {
            if (File.Exists(path))
            {
                return (new FileInfo(path).Length, 1);
            }

            if (!Directory.Exists(path))
            {
                return (0, 0);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (0, 0);
        }

        var pending = new Stack<string>();
        pending.Push(path);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                try
                {
                    var info = new FileInfo(file);
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    size += info.Length;
                    count++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }

            IEnumerable<string> subdirectories;
            try
            {
                subdirectories = Directory.EnumerateDirectories(current);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var subdirectory in subdirectories)
            {
                try
                {
                    if ((File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                pending.Push(subdirectory);
            }
        }

        return (size, count);
    }
}
