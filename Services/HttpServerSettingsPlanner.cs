using WinMoe.Models;

namespace WinMoe.Services;

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
        WinMoeSettings settings)
    {
        var normalized = WinMoeSettings.Normalize(settings);
        if (!activeHttpEnabled)
        {
            return HttpServerSettingsAction.Start;
        }

        return activePort == normalized.HttpServerPort
            ? HttpServerSettingsAction.None
            : HttpServerSettingsAction.Restart;
    }
}
