using MoleWindows.Models;

namespace MoleWindows.Services;

public enum HttpServerSettingsAction
{
    None,
    Start,
    Restart
}

public static class HttpServerSettingsPlanner
{
    public static HttpServerSettingsAction Plan(
        bool activeHttpEnabled,
        int activePort,
        MoleWindowsSettings settings)
    {
        var normalized = MoleWindowsSettings.Normalize(settings);
        if (!activeHttpEnabled)
        {
            return HttpServerSettingsAction.Start;
        }

        return activePort == normalized.HttpServerPort
            ? HttpServerSettingsAction.None
            : HttpServerSettingsAction.Restart;
    }
}
