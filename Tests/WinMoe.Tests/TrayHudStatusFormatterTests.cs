using WinMoe.Models;
using WinMoe.Services;
using Xunit;

namespace WinMoe.Tests;

public sealed class TrayHudStatusFormatterTests
{
    [Fact]
    public void Build_ReturnsWarmupStatus_WhenSnapshotAndActivityAreMissing()
    {
        var status = TrayHudStatusFormatter.Build(null, null);

        Assert.Equal("尚无遥测采样", status.SampleText);
        Assert.Equal("--", status.HealthScore);
        Assert.Equal("准备中", status.HealthLabel);
        Assert.Equal("--", status.CpuText);
        Assert.Equal("--", status.MemoryText);
        Assert.Equal("--", status.DiskText);
        Assert.Equal("--", status.NetworkText);
        Assert.Equal("暂无活动", status.ActivityTitle);
        Assert.Empty(status.TopProcesses);
    }

    [Fact]
    public void Build_FormatsTelemetryActivityAndTopProcesses()
    {
        var snapshot = new SystemTelemetrySnapshot(
            DateTimeOffset.Parse("2026-06-15T08:30:05Z"),
            24.2,
            51.8,
            4,
            8,
            70,
            3,
            4,
            2048,
            1024,
            "GPU pending",
            new[]
            {
                new ProcessTelemetry("editor", 10, 900, 5, 10),
                new ProcessTelemetry("compiler", 20, 700, 35, 40),
                new ProcessTelemetry("browser", 30, 1200, 20, 50),
                new ProcessTelemetry("terminal", 40, 500, 10, 15),
                new ProcessTelemetry("backup", 50, 300, 1, 80)
            });
        var activity = new OperationHistoryEntry(
            DateTimeOffset.Parse("2026-06-15T08:31:05Z"),
            "local",
            "clean",
            "--dry-run",
            0,
            true,
            120,
            "Previewed 4 items");

        var status = TrayHudStatusFormatter.Build(snapshot, activity);

        Assert.Equal("65", status.HealthScore);
        Assert.Equal("需关注", status.HealthLabel);
        Assert.Equal("24.2%", status.CpuText);
        Assert.Equal("51.8%", status.MemoryText);
        Assert.Equal("70%", status.DiskText);
        Assert.Equal("3 KB/s", status.NetworkText);
        Assert.Equal("clean · Succeeded (0)", status.ActivityTitle);
        Assert.Contains("Previewed 4 items", status.ActivityDetail);
        Assert.Equal(
            new[] { "compiler", "browser", "terminal", "editor", "backup" },
            status.TopProcesses.Select(process => process.Name));
        Assert.Equal("—", status.LifetimeCleanedText);
        Assert.Contains(" · ", status.DeviceChipText);
        Assert.False(string.IsNullOrWhiteSpace(status.MemoryDetailText));
        Assert.StartsWith("可用 ", status.DiskDetailText);
        Assert.Contains("↓ ", status.NetworkDetailText);
    }

    [Fact]
    public void Build_IncludesLifetimeStatsFromHistory()
    {
        var history = new[]
        {
            new OperationHistoryEntry(
                DateTimeOffset.UtcNow,
                "ui",
                "clean",
                "preview-complete",
                0,
                true,
                1,
                "Freed 2048 bytes · 2 KB"),
            new OperationHistoryEntry(
                DateTimeOffset.UtcNow,
                "local",
                "uninstall",
                "App",
                0,
                true,
                1,
                "Started"),
            new OperationHistoryEntry(
                DateTimeOffset.UtcNow,
                "local",
                "optimize",
                "--dry-run",
                0,
                true,
                1,
                "ok")
        };

        var status = TrayHudStatusFormatter.Build(null, history[0], history);

        Assert.Equal("2 KB", status.LifetimeCleanedText);
        Assert.Equal("1", status.LifetimeUninstalledText);
        Assert.Equal("1", status.LifetimeOptimizedText);
    }
}

