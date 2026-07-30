using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinMoe.Models;
using WinMoe.Services;

namespace WinMoe.ViewModels;

public partial class AnalyzeViewModel : ViewModelBase
{
    private const double DefaultTreemapWidth = 980;
    private const double DefaultTreemapHeight = 620;
    private const double MinimumTreemapWidth = 320;
    private const double MinimumTreemapHeight = 220;
    private readonly IMoleEngineService _moleEngineService;
    private readonly IDiskAnalyzerService _diskAnalyzerService;
    private readonly ISafeDeletionService _safeDeletionService;
    private readonly IOperationHistoryService _operationHistoryService;
    private CancellationTokenSource? _scanCancellationTokenSource;
    private DiskUsageNode? _lastScanResult;

    public AnalyzeViewModel(
        IMoleEngineService moleEngineService,
        IDiskAnalyzerService diskAnalyzerService,
        ISafeDeletionService safeDeletionService,
        IOperationHistoryService operationHistoryService)
    {
        _moleEngineService = moleEngineService;
        _diskAnalyzerService = diskAnalyzerService;
        _safeDeletionService = safeDeletionService;
        _operationHistoryService = operationHistoryService;
        var startupRoot = Environment.GetEnvironmentVariable("WINMOE_ANALYZE_ROOT")
                          ?? Environment.GetEnvironmentVariable("MOLEWINDOWS_ANALYZE_ROOT");
        RootPath = string.IsNullOrWhiteSpace(startupRoot)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : startupRoot;
    }

    public ObservableCollection<DiskUsageNode> Nodes { get; } = new();

    public ObservableCollection<AnalyzeSidebarItemViewModel> SidebarItems { get; } = new();

    public ObservableCollection<DiskTreemapTileViewModel> TreemapTiles { get; } = new();

    public ObservableCollection<string> OutputLines { get; } = new();

    [ObservableProperty]
    private string rootPath;

    [ObservableProperty]
    private string summary = "选择目录后开始分析";

    [ObservableProperty]
    private string breadcrumbText = "整盘 › 主目录";

    [ObservableProperty]
    private string scanStatusText = "尚未扫描";

    [ObservableProperty]
    private string totalItemCountText = "0 个项目";

    [ObservableProperty]
    private string analyzeOverviewText = "0 个项目 · 尚未扫描";

    [ObservableProperty]
    private bool hasScanResult;

    [ObservableProperty]
    private bool canShowTreemap;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string totalSize = "尚未扫描";

    [ObservableProperty]
    private string actionStatusText = string.Empty;

    /// <summary>Mole header: "Current 252.86 GB"</summary>
    [ObservableProperty]
    private string currentSizeMetric = "当前 —";

    /// <summary>Mole header: "Disk 487 / 994 GB"</summary>
    [ObservableProperty]
    private string diskVolumeMetric = "磁盘 —";

    [ObservableProperty]
    private string headerMetricsText = "当前 — · 磁盘 —";

    [ObservableProperty]
    private double diskUsagePercent;

    [ObservableProperty]
    private double treemapCanvasWidth = DefaultTreemapWidth;

    [ObservableProperty]
    private double treemapCanvasHeight = DefaultTreemapHeight;

    public bool CanCancel => IsBusy && _scanCancellationTokenSource is not null;

    public string OutputText => string.Join(Environment.NewLine, OutputLines);

    /// <summary>Width of the tiny disk-used bar (0–48 dip).</summary>
    public double DiskUsageBarWidth => Math.Clamp(DiskUsagePercent, 0, 100) / 100d * 48d;

    [RelayCommand]
    public async Task ScanAsync()
    {
        _scanCancellationTokenSource?.Cancel();
        _scanCancellationTokenSource?.Dispose();
        _scanCancellationTokenSource = new CancellationTokenSource();
        OnPropertyChanged(nameof(CanCancel));

        IsBusy = true;
        Nodes.Clear();
        SidebarItems.Clear();
        TreemapTiles.Clear();
        OutputLines.Clear();
        _lastScanResult = null;
        HasScanResult = false;
        CanShowTreemap = false;
        ScanStatusText = "正在扫描";
        TotalSize = "正在扫描";
        TotalItemCountText = "0 个项目";
        AnalyzeOverviewText = "0 个项目 · 正在扫描";
        CurrentSizeMetric = "当前 …";
        RefreshVolumeMetrics(RootPath);
        OnPropertyChanged(nameof(OutputText));

        try
        {
            var result = await _diskAnalyzerService.AnalyzeAsync(RootPath, new DiskAnalysisOptions(), _scanCancellationTokenSource.Token);
            RunOnUiThread(() =>
            {
                _lastScanResult = result;
                Nodes.Add(result);
                RebuildTreemapTiles();

                foreach (var item in result.Children
                             .OrderByDescending(child => child.SizeBytes)
                             .Select(child => new AnalyzeSidebarItemViewModel(child)))
                {
                    SidebarItems.Add(item);
                }

                var itemCount = CountNodes(result.Children);
                TotalSize = result.SizeText;
                TotalItemCountText = $"{itemCount} 个项目";
                AnalyzeOverviewText = $"{itemCount} 个项目 · {result.SizeText}";
                ScanStatusText = $"{itemCount} 个项目 · {result.SizeText}";
                BreadcrumbText = BuildBreadcrumbText(result.Path);
                CurrentSizeMetric = $"当前 {result.SizeText}";
                RefreshVolumeMetrics(result.Path);
                Summary = AnalyzeOverviewText;
                HasScanResult = true;
                CanShowTreemap = TreemapTiles.Count > 0;
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException or OperationCanceledException)
        {
            Summary = ex.Message;
            ScanStatusText = "扫描失败";
            TotalSize = "尚未扫描";
            AnalyzeOverviewText = "0 个项目 · 尚未扫描";
            CurrentSizeMetric = "当前 —";
            RefreshVolumeMetrics(RootPath);
            HasScanResult = false;
            CanShowTreemap = false;
            AppendOutput(ex.Message);
        }
        finally
        {
            _scanCancellationTokenSource?.Dispose();
            _scanCancellationTokenSource = null;
            IsBusy = false;
            OnPropertyChanged(nameof(CanCancel));
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        _scanCancellationTokenSource?.Cancel();
    }

    [RelayCommand]
    public async Task OpenPathAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !ShellPathActions.CanOpen(path))
        {
            ActionStatusText = "无法打开该路径";
            return;
        }

        await Task.Run(() =>
        {
            ShellPathActions.TryOpenInExplorer(path, out var message);
            RunOnUiThread(() => ActionStatusText = message);
        }).ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task DrillIntoPathAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path) || IsBusy)
        {
            ActionStatusText = "无法进入该文件夹";
            return;
        }

        try
        {
            RootPath = ShellPathActions.Normalize(path);
        }
        catch
        {
            ActionStatusText = "路径无效";
            return;
        }

        ActionStatusText = $"正在分析 {Path.GetFileName(RootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}…";
        await ScanAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task GoUpAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            var current = ShellPathActions.Normalize(RootPath);
            var parent = Directory.GetParent(current)?.FullName;
            if (string.IsNullOrWhiteSpace(parent))
            {
                ActionStatusText = "已在根目录";
                return;
            }

            RootPath = parent;
            await ScanAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ActionStatusText = ex.Message;
        }
    }

    [RelayCommand]
    public async Task SendToRecycleBinAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            ActionStatusText = "路径无效";
            return;
        }

        if (!ShellPathActions.CanSendToRecycleBin(path))
        {
            ActionStatusText = "该路径受保护，不能移入回收站";
            return;
        }

        try
        {
            var full = ShellPathActions.Normalize(path);
            var size = ShellPathActions.TryMeasureSize(full);
            var result = await Task.Run(() => _safeDeletionService.DeleteFileOrDirectory(full, size))
                .ConfigureAwait(false);

            if (result.Succeeded)
            {
                try
                {
                    await _operationHistoryService.RecordAsync(new OperationHistoryEntry(
                        DateTimeOffset.UtcNow,
                        "ui",
                        "clean",
                        "analyze-trash",
                        0,
                        true,
                        0,
                        $"Freed {size} bytes · analyze trash · {full}")).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }

                RunOnUiThread(() => ActionStatusText = $"已移入回收站：{Path.GetFileName(full)}");
                // Refresh current view so treemap drops the deleted node.
                await ScanAsync().ConfigureAwait(false);
            }
            else
            {
                RunOnUiThread(() => ActionStatusText = result.Message);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ActionStatusText = ex.Message;
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCancel));
    }

    partial void OnRootPathChanged(string value)
    {
        BreadcrumbText = BuildBreadcrumbText(value);
        if (!HasScanResult)
        {
            RefreshVolumeMetrics(value);
        }
    }

    partial void OnDiskUsagePercentChanged(double value)
    {
        OnPropertyChanged(nameof(DiskUsageBarWidth));
    }

    public void UpdateTreemapViewport(double width, double height)
    {
        var nextWidth = Math.Max(MinimumTreemapWidth, width);
        var nextHeight = Math.Max(MinimumTreemapHeight, height);

        if (Math.Abs(nextWidth - TreemapCanvasWidth) < 1 &&
            Math.Abs(nextHeight - TreemapCanvasHeight) < 1)
        {
            return;
        }

        TreemapCanvasWidth = nextWidth;
        TreemapCanvasHeight = nextHeight;
        RebuildTreemapTiles();
    }

    [RelayCommand]
    public async Task CheckMoleAsync()
    {
        IsBusy = true;
        OutputLines.Clear();
        OnPropertyChanged(nameof(OutputText));

        try
        {
            var result = await _moleEngineService.ExecuteCommandAsync("--version", AppendOutput);
            Summary = result.Succeeded
                ? "Mole engine is present; this page uses a native non-interactive tree fallback because Mole analyze is an interactive TUI"
                : $"Mole engine check failed with exit code {result.ExitCode}; native tree fallback remains available";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AppendOutput(string line)
    {
        RunOnUiThread(() =>
        {
            OutputLines.Add(line);
            OnPropertyChanged(nameof(OutputText));
        });
    }

    private void RebuildTreemapTiles()
    {
        TreemapTiles.Clear();
        if (_lastScanResult is null)
        {
            CanShowTreemap = false;
            return;
        }

        foreach (var tile in DiskTreemapLayout
                     .Build(_lastScanResult, TreemapCanvasWidth, TreemapCanvasHeight)
                     .Select(rect => new DiskTreemapTileViewModel(rect)))
        {
            TreemapTiles.Add(tile);
        }

        CanShowTreemap = TreemapTiles.Count > 0;
    }

    private static int CountNodes(IEnumerable<DiskUsageNode> nodes)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            count++;
            count += CountNodes(node.Children);
        }

        return count;
    }

    private void RefreshVolumeMetrics(string? path)
    {
        var volume = DiskVolumeStats.TryGetForPath(path);
        if (volume is null)
        {
            DiskVolumeMetric = "磁盘 —";
            DiskUsagePercent = 0;
        }
        else
        {
            DiskVolumeMetric = $"磁盘 {volume.UsedOverTotalText}";
            DiskUsagePercent = volume.UsagePercent;
        }

        HeaderMetricsText = DiskVolumeStats.FormatHeaderMetrics(CurrentSizeMetric, volume);
    }

    private static string BuildBreadcrumbText(string path) => DiskVolumeStats.BuildBreadcrumb(path);
}
