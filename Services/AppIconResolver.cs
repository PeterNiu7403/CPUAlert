using System.Security.Cryptography;
using System.Text;

namespace WinMoe.Services;

/// <summary>
/// Resolves Windows DisplayIcon / install-path hints to a filesystem image the UI can show.
/// Prefers direct .ico/.png; for .exe uses WinRT thumbnail extraction into a small disk cache.
/// </summary>
public static class AppIconResolver
{
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinMoe",
        "icon-cache");

    public static string? ResolveDirectImagePath(string? iconHint)
    {
        var path = NormalizeIconPath(iconHint);
        if (path is null)
        {
            return null;
        }

        var extension = Path.GetExtension(path);
        if (extension.Equals(".ico", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
        {
            return File.Exists(path) ? path : null;
        }

        return null;
    }

    public static string? NormalizeIconPath(string? iconHint)
    {
        if (string.IsNullOrWhiteSpace(iconHint))
        {
            return null;
        }

        var raw = Environment.ExpandEnvironmentVariables(iconHint.Trim().Trim('"'));
        // Registry DisplayIcon often ends with ",0" or ",-1"
        var comma = raw.LastIndexOf(',');
        if (comma > 2 && raw.AsSpan(comma + 1).Trim().IndexOfAny("0123456789-".AsSpan()) == 0)
        {
            var tail = raw[(comma + 1)..].Trim();
            if (int.TryParse(tail, out _))
            {
                raw = raw[..comma].Trim().Trim('"');
            }
        }

        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    public static string CacheKeyFor(string path)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(path.ToLowerInvariant()));
        return Convert.ToHexString(hash.AsSpan(0, 12));
    }

    public static string GetCachePath(string sourcePath) =>
        Path.Combine(CacheDirectory, CacheKeyFor(sourcePath) + ".png");

    public static void EnsureCacheDirectory() => Directory.CreateDirectory(CacheDirectory);

    /// <summary>
    /// Returns a cached PNG path if present and not older than the source file.
    /// </summary>
    public static string? TryGetFreshCachePath(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        var cachePath = GetCachePath(sourcePath);
        if (!File.Exists(cachePath))
        {
            return null;
        }

        try
        {
            var sourceWrite = File.GetLastWriteTimeUtc(sourcePath);
            var cacheWrite = File.GetLastWriteTimeUtc(cachePath);
            return cacheWrite >= sourceWrite ? cachePath : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
