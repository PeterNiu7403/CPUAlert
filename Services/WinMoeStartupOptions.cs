namespace WinMoe.Services;

public sealed record WinMoeStartupOptions(
    bool ShowTrayHudDiagnostic,
    bool DisableTray,
    string? InitialRoute,
    bool ShowCleanScreenDiagnostic = false)
{
    public static WinMoeStartupOptions FromLaunchArguments(string? arguments)
    {
        return Parse(arguments, Environment.GetEnvironmentVariable);
    }

    public static WinMoeStartupOptions Parse(
        string? arguments,
        Func<string, string?> environment)
    {
        var tokens = Tokenize(arguments);
        var showTrayHud = IsEnabled(ReadEnvironment(
                              environment,
                              "WINMOE_SHOW_TRAY_HUD",
                              "MOLEWINDOWS_SHOW_TRAY_HUD")) ||
                          tokens.Contains("--show-tray-hud", StringComparer.OrdinalIgnoreCase);
        var disableTray = IsEnabled(ReadEnvironment(
                              environment,
                              "WINMOE_DISABLE_TRAY",
                              "MOLEWINDOWS_DISABLE_TRAY")) ||
                          tokens.Contains("--no-tray", StringComparer.OrdinalIgnoreCase);
        var showCleanScreen = IsEnabled(ReadEnvironment(
                                  environment,
                                  "WINMOE_SHOW_CLEAN_SCREEN",
                                  "MOLEWINDOWS_SHOW_CLEAN_SCREEN")) ||
                              tokens.Contains("--show-clean-screen", StringComparer.OrdinalIgnoreCase);
        var route = ReadOption(tokens, "--route") ??
                    ReadEnvironment(environment, "WINMOE_START_ROUTE", "MOLEWINDOWS_START_ROUTE");

        return new WinMoeStartupOptions(showTrayHud, disableTray, NormalizeRoute(route), showCleanScreen);
    }

    private static bool IsEnabled(string? value)
    {
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadEnvironment(
        Func<string, string?> environment,
        string primaryName,
        string legacyName)
    {
        return environment(primaryName) ?? environment(legacyName);
    }

    private static HashSet<string> Tokenize(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return arguments
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string? ReadOption(HashSet<string> tokens, string name)
    {
        var prefix = name + "=";
        var match = tokens.FirstOrDefault(token => token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : match[prefix.Length..];
    }

    private static string? NormalizeRoute(string? route)
    {
        route = route?.Trim();
        if (string.IsNullOrWhiteSpace(route))
        {
            return null;
        }

        return route;
    }
}
