namespace WinMoe.Services;

/// <summary>
/// Resolves files in WinMoe's local application-data directory and carries
/// pre-release data forward from the former MoleWindows directory.
/// </summary>
public static class ApplicationDataPaths
{
    private const string CurrentDirectoryName = "WinMoe";
    private const string LegacyDirectoryName = "MoleWindows";

    public static string ResolveFile(string fileName)
    {
        return ResolveFile(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            fileName);
    }

    public static string CurrentFile(string fileName)
    {
        return CurrentFile(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            fileName);
    }

    internal static string ResolveFile(string localApplicationData, string fileName)
    {
        var currentPath = CurrentFile(localApplicationData, fileName);
        if (File.Exists(currentPath))
        {
            return currentPath;
        }

        var legacyPath = Path.Combine(localApplicationData, LegacyDirectoryName, fileName);
        if (!File.Exists(legacyPath))
        {
            return currentPath;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(currentPath)!);
            File.Copy(legacyPath, currentPath, overwrite: false);
            return currentPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Another process may have won the migration race. Otherwise keep
            // using the readable legacy file instead of silently resetting data.
            return File.Exists(currentPath) ? currentPath : legacyPath;
        }
    }

    internal static string CurrentFile(string localApplicationData, string fileName)
    {
        Validate(localApplicationData, fileName);
        return Path.Combine(localApplicationData, CurrentDirectoryName, fileName);
    }

    private static void Validate(string localApplicationData, string fileName)
    {
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new ArgumentException("A local application-data root is required.", nameof(localApplicationData));
        }

        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("The application-data file name must be a single path segment.", nameof(fileName));
        }
    }
}
