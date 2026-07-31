using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinMoe.Models;
using WinMoe.Services;

namespace WinMoe.ViewModels;

public partial class InstallerViewModel : ViewModelBase
{
    private readonly IInstallerCleanupService _installerCleanupService;
    private readonly IMoleEngineService _moleEngineService;
    private readonly IOperationHistoryService _operationHistoryService;

    public InstallerViewModel(
        IInstallerCleanupService installerCleanupService,
        IMoleEngineService moleEngineService,
        IOperationHistoryService operationHistoryService)
    {
        _installerCleanupService = installerCleanupService;
        _moleEngineService = moleEngineService;
        _operationHistoryService = operationHistoryService;
    }

    public ObservableCollection<InstallerCleanupCandidate> Items { get; } = new();

    public ObservableCollection<string> OutputLines { get; } = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool canRemove;

    [ObservableProperty]
    private string summary = "准备扫描下载目录旧安装包";

    [ObservableProperty]
    private string selectedSummary = "0 个文件";

    [ObservableProperty]
    private string engineSummary = "WinMoe 以与 Mole 相同的规则识别下载目录中的旧安装包与镜像文件。";

    public string OutputText => string.Join(Environment.NewLine, OutputLines);

    [RelayCommand]
    public async Task ScanAsync()
    {
        var startedAt = Stopwatch.GetTimestamp();
        var succeeded = false;
        var historySummary = "安装包预览未完成";

        IsBusy = true;
        CanRemove = false;
        ClearItems();
        OutputLines.Clear();
        OnPropertyChanged(nameof(OutputText));
        Summary = "正在扫描旧安装包…";

        try
        {
            var availability = _moleEngineService.GetAvailability();
            var items = await _installerCleanupService.PreviewAsync().ConfigureAwait(false);
            succeeded = true;
            historySummary = BuildPreviewSummary(items);

            RunOnUiThread(() =>
            {
                EngineSummary = availability.IsAvailable
                    ? $"Mole 引擎可用（{availability.Path}）；安装包预览使用与 Mole 兼容的下载目录规则。"
                    : $"{availability.Message} Installer preview uses local Windows Downloads rules.";

                ClearItems();
                foreach (var item in items)
                {
                    item.PropertyChanged += Item_PropertyChanged;
                    Items.Add(item);
                }

                Summary = historySummary;
                UpdateSelectionState();
            });
        }
        finally
        {
            await RecordHistoryAsync(
                "installer-preview",
                "old Downloads installers",
                succeeded,
                Stopwatch.GetElapsedTime(startedAt),
                historySummary).ConfigureAwait(false);

            RunOnUiThread(() =>
            {
                IsBusy = false;
                UpdateSelectionState();
            });
        }
    }

    public async Task RemoveAsync()
    {
        var selected = Items.Where(item => item.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        var succeeded = false;
        var historySummary = "安装包移除未完成";

        IsBusy = true;
        CanRemove = false;
        OutputLines.Clear();
        OnPropertyChanged(nameof(OutputText));
        Summary = "正在移除所选安装包…";

        try
        {
            var results = await _installerCleanupService.RemoveAsync(selected).ConfigureAwait(false);
            var removedBytes = results.Where(result => result.Succeeded).Sum(result => result.SizeBytes);
            var failedCount = results.Count(result => !result.Succeeded);
            succeeded = failedCount == 0;
            historySummary = failedCount == 0
                ? $"已移除 {results.Count} 个文件，释放 {SystemTelemetryFormatter.Bytes(removedBytes)}"
                : $"已移除 {results.Count - failedCount} 个文件；{failedCount} 个失败";

            RunOnUiThread(() =>
            {
                foreach (var result in results)
                {
                    var prefix = result.Succeeded ? "removed" : "failed";
                    OutputLines.Add($"{prefix}: {result.Path} ({SystemTelemetryFormatter.Bytes(result.SizeBytes)}) {result.Message}");
                }

                Summary = historySummary;
                OnPropertyChanged(nameof(OutputText));
            });
        }
        finally
        {
            await RecordHistoryAsync(
                "installer-remove",
                $"{selected.Count} selected old Downloads installers",
                succeeded,
                Stopwatch.GetElapsedTime(startedAt),
                historySummary).ConfigureAwait(false);

            RunOnUiThread(() =>
            {
                IsBusy = false;
                UpdateSelectionState();
            });
        }
    }

    [RelayCommand]
    public void SelectAll()
    {
        foreach (var item in Items)
        {
            item.IsSelected = true;
        }

        UpdateSelectionState();
    }

    [RelayCommand]
    public void ClearSelection()
    {
        foreach (var item in Items)
        {
            item.IsSelected = false;
        }

        UpdateSelectionState();
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstallerCleanupCandidate.IsSelected))
        {
            UpdateSelectionState();
        }
    }

    private void UpdateSelectionState()
    {
        var selected = Items.Where(item => item.IsSelected).ToList();
        var selectedBytes = selected.Sum(item => item.SizeBytes);
        SelectedSummary = $"{selected.Count} 个文件 · {SystemTelemetryFormatter.Bytes(selectedBytes)}";
        CanRemove = selected.Count > 0 && !IsBusy;
    }

    private void ClearItems()
    {
        foreach (var item in Items)
        {
            item.PropertyChanged -= Item_PropertyChanged;
        }

        Items.Clear();
        UpdateSelectionState();
    }

    private static string BuildPreviewSummary(IReadOnlyList<InstallerCleanupCandidate> items)
    {
        if (items.Count == 0)
        {
            return "No old installers found";
        }

        var totalBytes = items.Sum(item => item.SizeBytes);
        return $"{items.Count} 个文件 · {SystemTelemetryFormatter.Bytes(totalBytes)}";
    }

    private async Task RecordHistoryAsync(
        string operation,
        string arguments,
        bool succeeded,
        TimeSpan duration,
        string historySummary)
    {
        var entry = new OperationHistoryEntry(
            DateTimeOffset.UtcNow,
            "winmoe",
            operation,
            arguments,
            succeeded ? 0 : 1,
            succeeded,
            (long)duration.TotalMilliseconds,
            historySummary);

        try
        {
            await _operationHistoryService.RecordAsync(entry).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
