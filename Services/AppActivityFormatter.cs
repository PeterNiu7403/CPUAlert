using System.Globalization;

namespace WinMoe.Services;

/// <summary>
/// Mole-style activity labels: "active now" / "active N hours ago" / install age.
/// </summary>
public static class AppActivityFormatter
{
    public static string Format(DateTimeOffset? lastActivityUtc, bool isRunningNow, DateTimeOffset? nowUtc = null)
    {
        if (isRunningNow)
        {
            return "使用中";
        }

        if (lastActivityUtc is null)
        {
            return string.Empty;
        }

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var age = now - lastActivityUtc.Value;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age.TotalMinutes < 30)
        {
            return "刚刚活跃";
        }

        if (age.TotalHours < 24)
        {
            var hours = Math.Max(1, (int)Math.Round(age.TotalHours));
            return $"{hours} 小时前";
        }

        if (age.TotalDays < 30)
        {
            var days = Math.Max(1, (int)Math.Round(age.TotalDays));
            return $"{days} 天前";
        }

        if (age.TotalDays < 365)
        {
            var months = Math.Max(1, (int)Math.Round(age.TotalDays / 30d));
            return $"{months} 个月前";
        }

        var years = Math.Max(1, (int)Math.Round(age.TotalDays / 365d));
        return $"{years} 年前";
    }

    public static DateTimeOffset? TryParseInstallDate(string? installDateRaw)
    {
        if (string.IsNullOrWhiteSpace(installDateRaw))
        {
            return null;
        }

        var digits = new string(installDateRaw.Where(char.IsDigit).ToArray());
        if (digits.Length < 8)
        {
            return null;
        }

        if (DateTime.TryParseExact(
                digits[..8],
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var date))
        {
            return new DateTimeOffset(date, TimeSpan.Zero);
        }

        return null;
    }

    public static DateTimeOffset? ResolveLastActivityUtc(
        string? installLocation,
        string? iconPath,
        string? installDateRaw)
    {
        DateTimeOffset? best = null;

        foreach (var candidate in EnumerateCandidatePaths(installLocation, iconPath))
        {
            try
            {
                if (File.Exists(candidate))
                {
                    var write = File.GetLastWriteTimeUtc(candidate);
                    if (write.Year > 2000)
                    {
                        best = Max(best, new DateTimeOffset(write, TimeSpan.Zero));
                    }
                }
                else if (Directory.Exists(candidate))
                {
                    var write = Directory.GetLastWriteTimeUtc(candidate);
                    if (write.Year > 2000)
                    {
                        best = Max(best, new DateTimeOffset(write, TimeSpan.Zero));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
            }
        }

        var installDate = TryParseInstallDate(installDateRaw);
        return best ?? installDate;
    }

    private static IEnumerable<string> EnumerateCandidatePaths(string? installLocation, string? iconPath)
    {
        var paths = new List<string>();
        var normalizedIcon = AppIconResolver.NormalizeIconPath(iconPath);
        if (!string.IsNullOrWhiteSpace(normalizedIcon))
        {
            paths.Add(normalizedIcon);
        }

        if (string.IsNullOrWhiteSpace(installLocation))
        {
            return paths;
        }

        string expanded;
        try
        {
            expanded = Environment.ExpandEnvironmentVariables(installLocation.Trim());
        }
        catch
        {
            return paths;
        }

        paths.Add(expanded);

        try
        {
            if (Directory.Exists(expanded))
            {
                paths.AddRange(Directory.EnumerateFiles(expanded, "*.exe", SearchOption.TopDirectoryOnly).Take(3));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return paths;
    }

    private static DateTimeOffset? Max(DateTimeOffset? left, DateTimeOffset right)
        => left is null || right > left ? right : left;
}
