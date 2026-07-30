namespace WinMoe.Services;

/// <summary>
/// Volume usage + breadcrumb helpers for Analyze header (Mole: Current · Disk / Whole Disk › user).
/// </summary>
public static class DiskVolumeStats
{
    public sealed record VolumeUsage(long UsedBytes, long TotalBytes, double UsagePercent)
    {
        public string UsedText => SystemTelemetryFormatter.Bytes(UsedBytes);

        public string TotalText => SystemTelemetryFormatter.Bytes(TotalBytes);

        /// <summary>e.g. "487 GB / 994 GB"</summary>
        public string UsedOverTotalText => $"{UsedText} / {TotalText}";
    }

    public static VolumeUsage? TryGetForPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
            var full = Path.GetFullPath(expanded);
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize <= 0)
            {
                return null;
            }

            var total = drive.TotalSize;
            var free = Math.Clamp(drive.AvailableFreeSpace, 0, total);
            var used = total - free;
            var percent = total > 0 ? used * 100d / total : 0d;
            return new VolumeUsage(used, total, percent);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Mole-style trail: Whole Disk › user › folder… (capped depth).
    /// </summary>
    public static string BuildBreadcrumb(
        string? path,
        string? userProfilePath = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "整盘 › 主目录";
        }

        try
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(path.Trim());
            var profilePath = string.IsNullOrWhiteSpace(userProfilePath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : userProfilePath;
            var fullPath = Path.GetFullPath(expandedPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullProfilePath = Path.GetFullPath(profilePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var userName = Path.GetFileName(fullProfilePath);
            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = "主目录";
            }

            if (string.Equals(fullPath, fullProfilePath, StringComparison.OrdinalIgnoreCase))
            {
                return $"整盘 › {userName}";
            }

            if (fullPath.StartsWith(fullProfilePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(fullProfilePath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                var relative = fullPath[(fullProfilePath.Length + 1)..];
                var parts = relative.Split(
                    [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                {
                    return $"整盘 › {userName}";
                }

                var tail = parts.Length <= 3
                    ? string.Join(" › ", parts)
                    : string.Join(" › ", parts.TakeLast(3));
                return $"整盘 › {userName} › {tail}";
            }

            var root = Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var leaf = Path.GetFileName(fullPath);
            if (string.IsNullOrWhiteSpace(leaf) || string.Equals(leaf, root, StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(root) ? "整盘" : $"整盘 › {root}";
            }

            return string.IsNullOrWhiteSpace(root) ? leaf : $"整盘 › {root} › {leaf}";
        }
        catch
        {
            return "整盘 › 主目录";
        }
    }

    public static string FormatHeaderMetrics(string currentSizeText, VolumeUsage? volume)
    {
        var current = string.IsNullOrWhiteSpace(currentSizeText) ? "当前 —" : currentSizeText.Trim();
        if (!current.StartsWith("当前", StringComparison.Ordinal))
        {
            current = $"当前 {current}";
        }

        if (volume is null)
        {
            return $"{current} · 磁盘 —";
        }

        return $"{current} · 磁盘 {volume.UsedOverTotalText}";
    }
}
