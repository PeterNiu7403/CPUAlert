using System.Diagnostics;

namespace WinMoe.Services;

/// <summary>
/// Safe open / recycle helpers for Analyze (Mole: Right-click Open / Trash).
/// </summary>
public static class ShellPathActions
{
    public static bool CanOpen(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
            return File.Exists(full) || Directory.Exists(full);
        }
        catch
        {
            return false;
        }
    }

    public static bool CanSendToRecycleBin(string? path)
        => !string.IsNullOrWhiteSpace(path) && OperationPlanValidator.IsConcreteDeletablePath(path);

    public static string Normalize(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        return Path.GetFullPath(expanded);
    }

    public static bool TryOpenInExplorer(string path, out string message)
    {
        message = string.Empty;
        try
        {
            var full = Normalize(path);
            if (!File.Exists(full) && !Directory.Exists(full))
            {
                message = "路径不存在";
                return false;
            }

            if (Directory.Exists(full))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = full,
                    UseShellExecute = true
                });
            }
            else
            {
                // Reveal file in Explorer.
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{full}\"",
                    UseShellExecute = true
                });
            }

            message = "已在资源管理器中打开";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            message = ex.Message;
            return false;
        }
    }

    public static long TryMeasureSize(string path)
    {
        try
        {
            var full = Normalize(path);
            if (File.Exists(full))
            {
                return new FileInfo(full).Length;
            }

            if (Directory.Exists(full))
            {
                return Directory.EnumerateFiles(full, "*", SearchOption.TopDirectoryOnly)
                    .Select(file =>
                    {
                        try
                        {
                            return new FileInfo(file).Length;
                        }
                        catch
                        {
                            return 0L;
                        }
                    })
                    .Sum();
            }
        }
        catch
        {
        }

        return 0;
    }
}
