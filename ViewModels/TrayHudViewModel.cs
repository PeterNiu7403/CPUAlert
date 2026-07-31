using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinMoe.Models;
using WinMoe.Services;

namespace WinMoe.ViewModels;

public partial class TrayHudViewModel : ViewModelBase
{
    private readonly ISystemTelemetrySamplerService _telemetrySamplerService;
    private readonly IOperationHistoryService _operationHistoryService;

    public TrayHudViewModel(
        ISystemTelemetrySamplerService telemetrySamplerService,
        IOperationHistoryService operationHistoryService)
    {
        _telemetrySamplerService = telemetrySamplerService;
        _operationHistoryService = operationHistoryService;
    }

    public ObservableCollection<TrayHudProcessRow> TopProcesses { get; } = new();

    [ObservableProperty]
    private string sampleText = "尚无遥测采样";

    [ObservableProperty]
    private string healthScore = "--";

    [ObservableProperty]
    private string healthLabel = "准备中";

    [ObservableProperty]
    private string deviceChipText = "Windows";

    [ObservableProperty]
    private string cpuText = "--";

    [ObservableProperty]
    private string memoryText = "--";

    [ObservableProperty]
    private string diskText = "--";

    [ObservableProperty]
    private string networkText = "--";

    [ObservableProperty]
    private string gpuText = "—";

    [ObservableProperty]
    private string fanText = "—";

    [ObservableProperty]
    private string memoryDetailText = string.Empty;

    [ObservableProperty]
    private string diskDetailText = string.Empty;

    [ObservableProperty]
    private string networkDetailText = string.Empty;

    [ObservableProperty]
    private string gpuDetailText = string.Empty;

    [ObservableProperty]
    private string fanDetailText = string.Empty;

    [ObservableProperty]
    private string cpuDetailText = string.Empty;

    [ObservableProperty]
    private string cpuTemperatureText = "—";

    [ObservableProperty]
    private string gpuTemperatureText = "—";

    [ObservableProperty]
    private double memoryUsagePercent;

    [ObservableProperty]
    private double diskUsagePercent;

    [ObservableProperty]
    private string? fanPeakToolTip;

    [ObservableProperty]
    private string batteryText = "—";

    [ObservableProperty]
    private string batteryDetailText = string.Empty;

    [ObservableProperty]
    private string batteryHealthBadgeText = "健康";

    [ObservableProperty]
    private double batteryChargePercent;

    [ObservableProperty]
    private Visibility batteryVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private string activityTitle = "暂无活动";

    [ObservableProperty]
    private string activityDetail = "WinMoe 尚未记录操作。";

    [ObservableProperty]
    private string lifetimeCleanedText = "—";

    [ObservableProperty]
    private string lifetimeUninstalledText = "—";

    [ObservableProperty]
    private string lifetimeOptimizedText = "—";

    public async Task RefreshAsync()
    {
        var snapshot = _telemetrySamplerService.LatestSnapshot;
        // Lifetime totals need a wider window than the activity headline.
        var entries = await _operationHistoryService.ReadRecentAsync(2500).ConfigureAwait(false);
        var status = TrayHudStatusFormatter.Build(
            snapshot,
            entries.FirstOrDefault(),
            entries);
        if (snapshot is not null)
        {
            // HUD-only fields the shared formatter does not carry.
            status = status with
            {
                CpuTemperatureCelsius = snapshot.CpuTemperatureCelsius,
                GpuTemperatureCelsius = snapshot.GpuTemperatureCelsius
            };
        }

        RunOnUiThread(() => ApplyStatus(status, snapshot));
    }

    private void ApplyStatus(TrayHudStatus status, SystemTelemetrySnapshot? snapshot)
    {
        SampleText = status.SampleText;
        HealthScore = status.HealthScore;
        HealthLabel = status.HealthLabel;
        DeviceChipText = status.DeviceChipText;
        CpuText = status.CpuText;
        MemoryText = status.MemoryText;
        DiskText = status.DiskText;
        NetworkText = status.NetworkText;
        GpuText = status.GpuText;
        FanText = status.FanText;
        MemoryDetailText = status.MemoryDetailText;
        DiskDetailText = status.DiskDetailText;
        NetworkDetailText = status.NetworkDetailText;
        GpuDetailText = status.GpuDetailText;
        FanDetailText = status.FanDetailText;
        CpuTemperatureText = FormatTemperature(status.CpuTemperatureCelsius);
        GpuTemperatureText = FormatTemperature(status.GpuTemperatureCelsius);
        ActivityTitle = status.ActivityTitle;
        ActivityDetail = status.ActivityDetail;
        LifetimeCleanedText = status.LifetimeCleanedText;
        LifetimeUninstalledText = status.LifetimeUninstalledText;
        LifetimeOptimizedText = status.LifetimeOptimizedText;

        ApplySnapshotSurface(snapshot);

        TopProcesses.Clear();
        foreach (var process in status.TopProcesses)
        {
            TopProcesses.Add(new TrayHudProcessRow(process));
        }
    }

    // HUD card subtitles/badges that need the raw snapshot (Mole-style load tiers,
    // short GPU/fan detail) rather than the formatter's shared strings.
    private void ApplySnapshotSurface(SystemTelemetrySnapshot? snapshot)
    {
        if (snapshot is null)
        {
            CpuDetailText = string.Empty;
            MemoryUsagePercent = 0;
            DiskUsagePercent = 0;
            FanPeakToolTip = null;
            BatteryVisibility = Visibility.Collapsed;
            BatteryChargePercent = 0;
            return;
        }

        CpuDetailText = $"{CpuLoadTier(snapshot.CpuUsagePercent)} · {Environment.ProcessorCount} 核";
        MemoryUsagePercent = snapshot.MemoryUsagePercent;
        DiskUsagePercent = SystemHealthEvaluator.AggregateDiskUsagePercent(snapshot);
        FanPeakToolTip = null;

        if (snapshot.HasBattery && snapshot.BatteryChargePercent is { } charge)
        {
            BatteryVisibility = Visibility.Visible;
            BatteryText = string.Create(CultureInfo.InvariantCulture, $"{charge:0}%");
            BatteryChargePercent = charge;
            BatteryDetailText = BuildBatteryDetail(snapshot);
            BatteryHealthBadgeText = string.Equals(snapshot.BatteryHealthText, "Critical", StringComparison.OrdinalIgnoreCase)
                ? "严重"
                : BatteryDetailFormatter.BuildBadgeText(
                    BatteryDetailFormatter.ComputeHealthPercent(snapshot.BatteryDesignCapacityMwh, snapshot.BatteryFullChargeCapacityMwh),
                    snapshot.HasBattery);
        }
        else
        {
            BatteryVisibility = Visibility.Collapsed;
            BatteryChargePercent = 0;
        }

        var discrete = snapshot.GpuAdapters.FirstOrDefault(adapter => adapter.Kind == GpuAdapterKind.Discrete);
        var integrated = snapshot.GpuAdapters.FirstOrDefault(adapter => adapter.Kind == GpuAdapterKind.Integrated);
        if (discrete is not null && integrated is not null)
        {
            GpuDetailText = string.Create(
                CultureInfo.InvariantCulture,
                $"{discrete.ShortName} · 集显 {integrated.Engine3DPercent:0}%");
        }
        else if (discrete is not null)
        {
            GpuDetailText = discrete.ShortName;
        }
        else if (integrated is not null)
        {
            GpuDetailText = integrated.ShortName;
        }

        if (snapshot.Fans.Count > 0)
        {
            FanDetailText = string.Join(" · ", snapshot.Fans.Select(fan => $"{fan.Name} {fan.Rpm}"));
            FanPeakToolTip = snapshot.FanMaxRpm is { } peak && peak > 0
                ? string.Create(CultureInfo.InvariantCulture, $"峰值 {peak} RPM")
                : null;
        }
    }

    private static string CpuLoadTier(double cpuPercent) => cpuPercent switch
    {
        < 30 => "低负载",
        < 70 => "中负载",
        _ => "高负载"
    };

    // Mirrors DashboardViewModel's battery surface so HUD and status page agree.
    private static string BuildBatteryDetail(SystemTelemetrySnapshot snapshot)
    {
        var status = snapshot.BatteryStatusText.ToLowerInvariant() switch
        {
            "charging" => "充电中",
            "plugged in" => "已接电源",
            "discharging" => "放电中",
            "unknown" => "未知",
            _ => "电池"
        };

        if (!string.Equals(snapshot.BatteryStatusText, "discharging", StringComparison.OrdinalIgnoreCase) ||
            snapshot.BatteryEstimatedSecondsRemaining is not { } seconds ||
            seconds <= 0)
        {
            return status;
        }

        var duration = TimeSpan.FromSeconds(seconds);
        var remaining = duration.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)duration.TotalHours} 小时 {duration.Minutes} 分")
            : string.Create(CultureInfo.InvariantCulture, $"{Math.Max(1, duration.Minutes)} 分钟");
        return $"{status} · 预计剩余 {remaining}";
    }

    private static string FormatTemperature(double? celsius) =>
        celsius is { } value
            ? string.Create(CultureInfo.InvariantCulture, $"{value:0}°C")
            : "—";
}

/// <summary>HUD process row: Mole renders idle rows ("0%") as a dim em dash.</summary>
public sealed record TrayHudProcessRow(ProcessTelemetry Process)
{
    private static readonly SolidColorBrush ActiveCpuBrush = new(Color.FromArgb(255, 0xE0, 0x70, 0x40));
    private static readonly SolidColorBrush IdleCpuBrush = new(Color.FromArgb(255, 0x7A, 0x74, 0x6C));

    public string Name => Process.Name;

    public string WorkingSetText => Process.WorkingSetText;

    public string CpuUsageText => IsIdle ? "—" : Process.CpuUsageText;

    public SolidColorBrush CpuUsageBrush => IsIdle ? IdleCpuBrush : ActiveCpuBrush;

    private bool IsIdle => Process.CpuUsagePercent < 0.05;
}
