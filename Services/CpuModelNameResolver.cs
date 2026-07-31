namespace WinMoe.Services;

/// <summary>
/// Short CPU marketing name for the device chip (Mole shows "M5 Pro · 48 GB").
/// Reads the registry once and caches the result.
/// </summary>
public static class CpuModelNameResolver
{
    private static string s_cached = string.Empty;

    public static string Get()
    {
        if (!string.IsNullOrWhiteSpace(s_cached))
        {
            return s_cached;
        }

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            var name = key?.GetValue("ProcessorNameString") as string;
            if (!string.IsNullOrWhiteSpace(name))
            {
                // "Intel(R) Core(TM) i9-14900HX" → "i9-14900HX"; keep short like Mole's "M5 Pro".
                var cleaned = name.Trim();
                cleaned = cleaned.Replace("Intel(R)", string.Empty, StringComparison.OrdinalIgnoreCase);
                cleaned = cleaned.Replace("Core(TM)", string.Empty, StringComparison.OrdinalIgnoreCase);
                cleaned = cleaned.Replace("AMD", string.Empty, StringComparison.OrdinalIgnoreCase);
                cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s*CPU.*$|@\s*[\d.]+GHz.*$", string.Empty);
                cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();
                s_cached = cleaned.Length == 0 ? name.Trim() : cleaned;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }

        if (string.IsNullOrWhiteSpace(s_cached))
        {
            s_cached = "Windows";
        }

        return s_cached;
    }
}
