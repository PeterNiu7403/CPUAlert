using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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

    public ObservableCollection<ProcessTelemetry> TopProcesses { get; } = new();

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

        RunOnUiThread(() => ApplyStatus(status));
    }

    private void ApplyStatus(TrayHudStatus status)
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
        ActivityTitle = status.ActivityTitle;
        ActivityDetail = status.ActivityDetail;
        LifetimeCleanedText = status.LifetimeCleanedText;
        LifetimeUninstalledText = status.LifetimeUninstalledText;
        LifetimeOptimizedText = status.LifetimeOptimizedText;

        TopProcesses.Clear();
        foreach (var process in status.TopProcesses)
        {
            TopProcesses.Add(process);
        }
    }
}
