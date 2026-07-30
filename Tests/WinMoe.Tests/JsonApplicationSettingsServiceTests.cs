using WinMoe.Models;
using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class JsonApplicationSettingsServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "WinMoeTests", Guid.NewGuid().ToString("N"));
    private readonly string _settingsPath;

    public JsonApplicationSettingsServiceTests()
    {
        Directory.CreateDirectory(_tempRoot);
        _settingsPath = Path.Combine(_tempRoot, "settings.json");
    }

    [Fact]
    public void Constructor_UsesNormalizedDefaults_WhenFileIsMissing()
    {
        var service = new JsonApplicationSettingsService(_settingsPath);

        Assert.Equal(WinMoeSettings.DefaultSamplingIntervalSeconds, service.Current.SamplingIntervalSeconds);
        Assert.Equal(WinMoeSettings.DefaultHistoryRetentionDays, service.Current.HistoryRetentionDays);
        Assert.Equal(WinMoeSettings.DefaultHttpServerPort, service.Current.HttpServerPort);
        Assert.True(service.Current.HttpServerEnabled);
        Assert.True(service.Current.TrayIconEnabled);
        Assert.False(service.Current.McpDestructiveActionsEnabled);
    }

    [Fact]
    public async Task SaveAsync_NormalizesPersistsAndRaisesChangedEvent()
    {
        var service = new JsonApplicationSettingsService(_settingsPath);
        WinMoeSettings? changedSettings = null;
        service.SettingsChanged += (_, settings) => changedSettings = settings;

        var saved = await service.SaveAsync(new WinMoeSettings
        {
            SamplingIntervalSeconds = 1,
            HistoryRetentionDays = 1000,
            HttpServerEnabled = false,
            HttpServerPort = 10,
            TrayIconEnabled = false,
            McpDestructiveActionsEnabled = true
        });

        Assert.Equal(5, saved.SamplingIntervalSeconds);
        Assert.Equal(365, saved.HistoryRetentionDays);
        Assert.Equal(1024, saved.HttpServerPort);
        Assert.False(saved.HttpServerEnabled);
        Assert.False(saved.TrayIconEnabled);
        Assert.True(saved.McpDestructiveActionsEnabled);
        Assert.NotNull(changedSettings);
        Assert.Empty(saved.PinnedProcessNames);

        var reloaded = new JsonApplicationSettingsService(_settingsPath);
        Assert.Equal(saved.SamplingIntervalSeconds, reloaded.Current.SamplingIntervalSeconds);
        Assert.Equal(saved.HistoryRetentionDays, reloaded.Current.HistoryRetentionDays);
        Assert.Equal(saved.HttpServerPort, reloaded.Current.HttpServerPort);
        Assert.False(reloaded.Current.HttpServerEnabled);
    }

    [Fact]
    public async Task SaveAsync_PersistsPinnedProcessNames_Normalized()
    {
        var service = new JsonApplicationSettingsService(_settingsPath);

        var saved = await service.SaveAsync(new WinMoeSettings
        {
            PinnedProcessNames =
            [
                " chrome.EXE ",
                "chrome",
                "Code",
                "",
                "  ",
                "explorer"
            ]
        });

        Assert.Equal(["chrome", "Code", "explorer"], saved.PinnedProcessNames);

        var reloaded = new JsonApplicationSettingsService(_settingsPath);
        Assert.Equal(["chrome", "Code", "explorer"], reloaded.Current.PinnedProcessNames);
    }

    [Fact]
    public void NormalizePinnedProcessNames_CapsAndDedupes()
    {
        var names = Enumerable.Range(0, 40).Select(i => $"proc{i}").ToList();
        names.Insert(0, "proc0.exe");

        var normalized = WinMoeSettings.NormalizePinnedProcessNames(names);

        Assert.Equal(WinMoeSettings.MaxPinnedProcessNames, normalized.Count);
        Assert.Equal("proc0", normalized[0]);
        Assert.DoesNotContain(normalized, name => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
