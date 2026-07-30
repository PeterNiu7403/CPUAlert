using System.Reflection;

namespace WinMoe.Services;

/// Build/run-time configuration for the Windows telemetry pipeline.
///
/// Crash reporting goes to a separate Windows-only Sentry project; product
/// analytics reuses the macOS PostHog project (free-plan 1-project limit),
/// discriminated by the `platform: "windows"` event property. The macOS
/// pipeline is untouched.
///
/// Keys are **baked into the assembly at build time** via MSBuild
/// `AssemblyMetadata` (see WinMoe.csproj), sourced from the
/// `WINMOE_*` environment variables a release build sets — exactly the way
/// the macOS build bakes `SENTRY_DSN`/`POSTHOG_API_KEY` into Info.plist. A
/// runtime environment variable is honored as a fallback so a developer can
/// point a local build at real keys without rebaking; it is NOT how shipped
/// builds get their keys (end users have no such variables set). Legacy
/// `MOLEWINDOWS_*` variables remain a final fallback during the rename.
///
/// When a value is absent — every local/dev build — the corresponding SDK is
/// never started, so telemetry is completely inert outside signed releases.
public static class TelemetryConfig
{
    public static string SentryDsn =>
        Resolve("WinMoeSentryDsn", "WINMOE_SENTRY_DSN", "MOLEWINDOWS_SENTRY_DSN");

    public static string PostHogApiKey =>
        Resolve("WinMoePostHogApiKey", "WINMOE_POSTHOG_API_KEY", "MOLEWINDOWS_POSTHOG_API_KEY");

    public static string PostHogHost
    {
        get
        {
            var value = Resolve("WinMoePostHogHost", "WINMOE_POSTHOG_HOST", "MOLEWINDOWS_POSTHOG_HOST");
            return string.IsNullOrWhiteSpace(value) ? "https://us.i.posthog.com" : value;
        }
    }

    public static bool IsSentryConfigured => !string.IsNullOrWhiteSpace(SentryDsn);

    public static bool IsPostHogConfigured => !string.IsNullOrWhiteSpace(PostHogApiKey);

    /// Prefer the value baked into the assembly at build time (how releases get
    /// their keys); fall back to an environment variable so a local dev build
    /// can use real keys without rebaking.
    private static string Resolve(
        string metadataKey,
        string environmentVariable,
        string legacyEnvironmentVariable)
    {
        var baked = Metadata(metadataKey);
        if (!string.IsNullOrWhiteSpace(baked))
        {
            return baked;
        }

        return Environment.GetEnvironmentVariable(environmentVariable)
               ?? Environment.GetEnvironmentVariable(legacyEnvironmentVariable)
               ?? string.Empty;
    }

    private static readonly Dictionary<string, string> BakedMetadata = LoadBakedMetadata();

    private static Dictionary<string, string> LoadBakedMetadata()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var attribute in Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (attribute.Key is { Length: > 0 } key && attribute.Value is { } value)
            {
                map[key] = value;
            }
        }

        return map;
    }

    private static string Metadata(string key) =>
        BakedMetadata.TryGetValue(key, out var value) ? value : string.Empty;
}
