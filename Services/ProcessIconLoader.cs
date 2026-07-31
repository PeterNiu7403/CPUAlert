using System.Diagnostics;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace WinMoe.Services;

/// <summary>
/// Resolves process icons to cached PNG files using the WinRT thumbnail
/// extractor, reusing the same on-disk cache as the Apps page icon resolver.
/// All methods are safe to call off the UI thread and never throw.
/// </summary>
public static class ProcessIconLoader
{
    public static string? TryGetExecutablePath(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var path = process.MainModule?.FileName;
            return string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? null : path;
        }
        catch
        {
            // Protected/system processes deny MainModule; the row keeps its initials tile.
            return null;
        }
    }

    /// <summary>Returns a cached PNG path for the executable, extracting it when needed.</summary>
    public static async Task<string?> EnsurePngAsync(string executablePath)
    {
        var cached = AppIconResolver.TryGetFreshCachePath(executablePath);
        if (cached is not null)
        {
            return cached;
        }

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(executablePath);
            using var thumbnail = await file.GetThumbnailAsync(
                ThumbnailMode.SingleItem,
                48,
                ThumbnailOptions.ResizeThumbnail);
            if (thumbnail is null || thumbnail.Size == 0)
            {
                return null;
            }

            AppIconResolver.EnsureCacheDirectory();
            var cachePath = AppIconResolver.GetCachePath(executablePath);
            using (var output = File.Open(cachePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                var reader = new DataReader(thumbnail.GetInputStreamAt(0));
                var loaded = await reader.LoadAsync((uint)thumbnail.Size);
                var buffer = new byte[loaded];
                reader.ReadBytes(buffer);
                await output.WriteAsync(buffer);
            }

            return cachePath;
        }
        catch
        {
            return null;
        }
    }
}
