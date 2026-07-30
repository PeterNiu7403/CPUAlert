using Microsoft.Win32;
using WinMoe.Models;

namespace WinMoe.Services;

public sealed record StartupItem(
    string Name,
    string Command,
    string Location,
    string Source)
{
    public string ShortCommand
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Command))
            {
                return "—";
            }

            var trimmed = Command.Trim().Trim('"');
            try
            {
                if (File.Exists(trimmed))
                {
                    return Path.GetFileName(trimmed);
                }
            }
            catch
            {
            }

            return trimmed.Length > 64 ? trimmed[..61] + "…" : trimmed;
        }
    }
}

public interface IWindowsStartupItemService
{
    Task<IReadOnlyList<StartupItem>> GetStartupItemsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only Windows startup inventory (Run keys + Startup folder). No registry writes.
/// </summary>
public sealed class WindowsStartupItemService : IWindowsStartupItemService
{
    public Task<IReadOnlyList<StartupItem>> GetStartupItemsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<StartupItem>>(() =>
        {
            var items = new List<StartupItem>();
            ReadRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "当前用户 · 运行", items, cancellationToken);
            ReadRunKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "本机 · 运行", items, cancellationToken);
            ReadRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "当前用户 · 运行一次", items, cancellationToken);
            ReadStartupFolder(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                "当前用户 · 启动文件夹",
                items,
                cancellationToken);
            ReadStartupFolder(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                "本机 · 启动文件夹",
                items,
                cancellationToken);

            return items
                .GroupBy(item => item.Name + "|" + item.Command, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }, cancellationToken);
    }

    private static void ReadRunKey(
        RegistryKey hive,
        string subKeyPath,
        string source,
        List<StartupItem> items,
        CancellationToken cancellationToken)
    {
        try
        {
            using var key = hive.OpenSubKey(subKeyPath);
            if (key is null)
            {
                return;
            }

            foreach (var name in key.GetValueNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var value = key.GetValue(name)?.ToString() ?? string.Empty;
                items.Add(new StartupItem(name.Trim(), value.Trim(), subKeyPath, source));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }
    }

    private static void ReadStartupFolder(
        string folder,
        string source,
        List<StartupItem> items,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        try
        {
            foreach (var path in Directory.EnumerateFiles(folder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                items.Add(new StartupItem(name, path, folder, source));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
