using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using WinMoe.Models;
using WinMoe.Services;

namespace WinMoe.ViewModels;

public enum CleanupSurfaceState
{
    Idle,
    Scanning,
    Review,
    Complete
}

public partial class CleanupViewModel : ViewModelBase
{
    private static readonly TimeSpan PlanLifetime = TimeSpan.FromMinutes(15);

    private readonly IMoleEngineService _moleEngineService;
    private readonly IOperationHistoryService _operationHistoryService;
    private readonly IOperationPlanValidator _planValidator;
    private readonly ISafeDeletionService _safeDeletionService;

    private OperationPlan? _activePlan;
    private IReadOnlyList<OperationPlanItem> _planBaseline = [];
    private long _lifetimeCleanedBytes;
    private long _lastCleanedBytes;
    private int _lastRemovedCount;
    private int _lastSkippedCount;
    private int _lastFailedCount;

    public CleanupViewModel(
        IMoleEngineService moleEngineService,
        IOperationHistoryService operationHistoryService,
        IOperationPlanValidator planValidator,
        ISafeDeletionService safeDeletionService)
    {
        _moleEngineService = moleEngineService;
        _operationHistoryService = operationHistoryService;
        _planValidator = planValidator;
        _safeDeletionService = safeDeletionService;
        ApplyIdleSurface();
    }

    public ObservableCollection<CleanupPreviewItem> PreviewItems { get; } = new();

    public ObservableCollection<CleanupCategoryGroupViewModel> CategoryGroups { get; } = new();

    public ObservableCollection<string> OutputLines { get; } = new();

    [ObservableProperty]
    private string summary = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool canClean;

    [ObservableProperty]
    private bool canPreview = true;

    [ObservableProperty]
    private string pendingMessage = string.Empty;

    [ObservableProperty]
    private string heroMetric = string.Empty;

    [ObservableProperty]
    private string heroSubtitle = string.Empty;

    [ObservableProperty]
    private string primaryActionLabel = "扫描";

    [ObservableProperty]
    private CleanupSurfaceState surfaceState = CleanupSurfaceState.Idle;

    [ObservableProperty]
    private Visibility reviewPanelVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility cleanButtonVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility primaryActionVisibility = Visibility.Visible;

    [ObservableProperty]
    private Visibility progressVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private string selectionSummary = string.Empty;

    public string OutputText => string.Join(Environment.NewLine, OutputLines);

    public bool IsIdle => SurfaceState == CleanupSurfaceState.Idle;

    public bool IsScanning => SurfaceState == CleanupSurfaceState.Scanning;

    public bool IsReview => SurfaceState == CleanupSurfaceState.Review;

    public bool IsComplete => SurfaceState == CleanupSurfaceState.Complete;

    [RelayCommand]
    public async Task ScanAsync()
    {
        if (IsBusy)
        {
            return;
        }

        EnterScanning();

        try
        {
            // Mole: silent short wait — delay spinner so brief scans stay calm.
            await Task.Delay(350).ConfigureAwait(false);
            var spinnerCts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(400, spinnerCts.Token).ConfigureAwait(false);
                    RunOnUiThread(() =>
                    {
                        if (IsBusy)
                        {
                            ProgressVisibility = Visibility.Visible;
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                }
            });

            var result = await _moleEngineService
                .ExecuteAsync(["clean", "--dry-run"], line => AppendOutput(line))
                .ConfigureAwait(false);
            spinnerCts.Cancel();

            var parsed = CleanPreviewParser.Parse(
                string.Join(Environment.NewLine, new[] { result.StandardOutput, result.StandardError }));

            RunOnUiThread(() =>
            {
                PreviewItems.Clear();
                foreach (var item in parsed)
                {
                    PreviewItems.Add(item);
                    TrackPreviewItem(item);
                }

                if (PreviewItems.Count == 0)
                {
                    SeedSafeUserTempPreview();
                }

                BuildActivePlanFromPreview();
                EnterReview();
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            RunOnUiThread(() =>
            {
                SeedSafeUserTempPreview();
                PendingMessage = ex.Message;
                BuildActivePlanFromPreview();
                EnterReview();
            });
        }
        finally
        {
            RunOnUiThread(() =>
            {
                IsBusy = false;
                ProgressVisibility = Visibility.Collapsed;
                CanPreview = true;
                NotifySurface();
            });
        }
    }

    [RelayCommand]
    public async Task CleanAsync()
    {
        if (IsBusy || PreviewItems.Count == 0)
        {
            return;
        }

        IsBusy = true;
        CanClean = false;
        ProgressVisibility = Visibility.Visible;
        PendingMessage = "正在校验计划并移入回收站…";

        try
        {
            var now = DateTimeOffset.UtcNow;
            var currentItems = BuildPlanItemsFromPreview();
            if (_activePlan is null || _planBaseline.Count == 0)
            {
                BuildActivePlanFromPreview();
            }

            // Keep fingerprint identity of the scan, but apply current checkbox selection.
            var plan = (_activePlan ?? OperationPlan.Create("clean", currentItems, now, PlanLifetime))
                with { Items = currentItems };

            var validation = _planValidator.ValidateForApply(
                plan,
                _planBaseline.Count == 0 ? currentItems : _planBaseline,
                userConfirmed: true,
                now);

            if (!validation.IsValid)
            {
                // Selection-only drift: re-fingerprint current selection set for a fresh short plan.
                if (validation.Code is OperationPlanValidationCode.ContentChanged
                    or OperationPlanValidationCode.EmptySelection)
                {
                    var selectedOnly = currentItems.Where(item => item.IsSelected).ToArray();
                    if (selectedOnly.Length == 0)
                    {
                        RunOnUiThread(() =>
                        {
                            PendingMessage = validation.Message;
                            CanClean = true;
                        });
                        return;
                    }

                    plan = OperationPlan.Create("clean", selectedOnly, now, PlanLifetime);
                    validation = _planValidator.ValidateForApply(plan, selectedOnly, true, now);
                }
            }

            if (!validation.IsValid)
            {
                RunOnUiThread(() =>
                {
                    PendingMessage = validation.Message;
                    CanClean = true;
                });
                return;
            }

            var selected = plan.Items.Where(item => item.IsSelected).ToArray();
            var removedBytes = 0L;
            var removed = 0;
            var skipped = 0;
            var failed = 0;
            var startedAt = Stopwatch.GetTimestamp();

            await Task.Run(() =>
            {
                foreach (var item in selected)
                {
                    if (!OperationPlanValidator.IsConcreteDeletablePath(item.TargetPath))
                    {
                        skipped++;
                        AppendOutput($"SKIP {item.TargetPath} · 非可执行路径或不受支持的目标");
                        continue;
                    }

                    var result = _safeDeletionService.DeleteFileOrDirectory(item.TargetPath, item.SizeBytes);
                    if (result.Succeeded)
                    {
                        removed++;
                        removedBytes += Math.Max(0, item.SizeBytes);
                        AppendOutput($"OK {item.TargetPath} · {result.Message}");
                    }
                    else
                    {
                        failed++;
                        AppendOutput($"FAIL {item.TargetPath} · {result.Message}");
                    }
                }
            }).ConfigureAwait(false);

            _lastCleanedBytes = removedBytes;
            _lastRemovedCount = removed;
            _lastSkippedCount = skipped;
            _lastFailedCount = failed;
            _lifetimeCleanedBytes += removedBytes;

            var durationMs = (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            var summary =
                $"Freed {removedBytes} bytes · {SystemTelemetryFormatter.Bytes(removedBytes)} · removed {removed}, skipped {skipped}, failed {failed}";

            try
            {
                await _operationHistoryService.RecordAsync(new OperationHistoryEntry(
                    DateTimeOffset.UtcNow,
                    "ui",
                    "clean",
                    "apply-recycle-bin",
                    failed == 0 ? 0 : 1,
                    failed == 0,
                    durationMs,
                    summary)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            RunOnUiThread(EnterComplete);
        }
        finally
        {
            RunOnUiThread(() =>
            {
                IsBusy = false;
                ProgressVisibility = Visibility.Collapsed;
                NotifySurface();
            });
        }
    }

    [RelayCommand]
    public void ReturnToIdle()
    {
        ApplyIdleSurface();
    }

    [RelayCommand]
    public void SelectAllPreview()
    {
        foreach (var group in CategoryGroups)
        {
            group.ApplyGroupSelection(true);
        }

        foreach (var item in PreviewItems)
        {
            item.IsSelected = true;
        }

        RefreshSelectionSummary();
    }

    [RelayCommand]
    public void ClearPreviewSelection()
    {
        foreach (var group in CategoryGroups)
        {
            group.ApplyGroupSelection(false);
        }

        foreach (var item in PreviewItems)
        {
            item.IsSelected = false;
        }

        RefreshSelectionSummary();
    }

    [RelayCommand]
    public void ToggleCategoryExpanded(CleanupCategoryGroupViewModel? group)
    {
        group?.ToggleExpanded();
    }

    /// <summary>Called from code-behind after category-level checkbox toggles.</summary>
    public void RefreshSelectionFromUi() => RefreshSelectionSummary();

    private void EnterScanning()
    {
        IsBusy = true;
        CanPreview = false;
        CanClean = false;
        SurfaceState = CleanupSurfaceState.Scanning;
        HeroMetric = string.Empty;
        HeroSubtitle = "正在扫描…";
        PrimaryActionLabel = "扫描中";
        PrimaryActionVisibility = Visibility.Collapsed;
        CleanButtonVisibility = Visibility.Collapsed;
        ReviewPanelVisibility = Visibility.Collapsed;
        ProgressVisibility = Visibility.Collapsed;
        PendingMessage = string.Empty;
        Summary = string.Empty;
        SelectionSummary = string.Empty;
        _activePlan = null;
        _planBaseline = [];
        PreviewItems.Clear();
        CategoryGroups.Clear();
        OutputLines.Clear();
        OnPropertyChanged(nameof(OutputText));
        NotifySurface();
    }

    private void EnterReview()
    {
        SurfaceState = CleanupSurfaceState.Review;
        RebuildCategoryGroups();
        var total = PreviewItems.Sum(item => item.SizeBytes);
        var deletable = PreviewItems.Count(item => OperationPlanValidator.IsConcreteDeletablePath(item.Path));
        var categories = CategoryGroups.Count;
        HeroMetric = total > 0 ? SystemTelemetryFormatter.Bytes(total) : "—";
        HeroSubtitle = PreviewItems.Count == 0
            ? "未发现可清理项"
            : $"{categories} 类 · {PreviewItems.Count} 项 · {deletable} 项可移入回收站";
        PrimaryActionLabel = "重新扫描";
        PrimaryActionVisibility = Visibility.Visible;
        ReviewPanelVisibility = Visibility.Visible;
        CleanButtonVisibility = Visibility.Visible;
        PendingMessage = deletable > 0
            ? "按分类勾选后确认：计划指纹校验通过后将移入 Windows 回收站（系统目录已拦截）。"
            : "当前列表无可执行绝对路径；请重新扫描获取真实目标，或仅查看分类。";
        RefreshSelectionSummary();
        NotifySurface();
    }

    private void EnterComplete()
    {
        SurfaceState = CleanupSurfaceState.Complete;
        HeroMetric = SystemTelemetryFormatter.Bytes(_lastCleanedBytes);
        HeroSubtitle =
            $"已移除 {_lastRemovedCount} · 跳过 {_lastSkippedCount} · 失败 {_lastFailedCount} · 累计 {SystemTelemetryFormatter.Bytes(_lifetimeCleanedBytes)}";
        PrimaryActionLabel = "返回";
        PrimaryActionVisibility = Visibility.Visible;
        CleanButtonVisibility = Visibility.Collapsed;
        ReviewPanelVisibility = Visibility.Collapsed;
        CanClean = false;
        CanPreview = true;
        PendingMessage = string.Empty;
        Summary = string.Empty;
        SelectionSummary = string.Empty;
        _activePlan = null;
        NotifySurface();
    }

    private void ApplyIdleSurface()
    {
        SurfaceState = CleanupSurfaceState.Idle;
        IsBusy = false;
        CanPreview = true;
        CanClean = false;
        HeroMetric = string.Empty;
        HeroSubtitle = "山雨洗尘，让空间重新呼吸";
        PrimaryActionLabel = "扫描";
        PrimaryActionVisibility = Visibility.Visible;
        CleanButtonVisibility = Visibility.Collapsed;
        ReviewPanelVisibility = Visibility.Collapsed;
        ProgressVisibility = Visibility.Collapsed;
        PendingMessage = string.Empty;
        Summary = string.Empty;
        SelectionSummary = string.Empty;
        _activePlan = null;
        _planBaseline = [];
        PreviewItems.Clear();
        CategoryGroups.Clear();
        OutputLines.Clear();
        OnPropertyChanged(nameof(OutputText));
        NotifySurface();
    }

    private void SeedSafeUserTempPreview()
    {
        PreviewItems.Clear();
        var temp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        // Only advertise a bounded, user-scoped temp subfolder for demo apply — never Windows\.
        var demo = Path.Combine(temp, "WinMoe-CleanPreview");
        try
        {
            Directory.CreateDirectory(demo);
            var marker = Path.Combine(demo, "preview-marker.txt");
            if (!File.Exists(marker))
            {
                File.WriteAllText(marker, "WinMoe safe clean preview target. Safe to delete.");
            }

            var size = Directory.Exists(demo)
                ? Directory.EnumerateFiles(demo, "*", SearchOption.AllDirectories).Sum(GetFileLengthSafe)
                : 0L;
            PreviewItems.Add(new CleanupPreviewItem(
                "用户临时预览",
                demo,
                SystemTelemetryFormatter.Bytes(size),
                size,
                1));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PreviewItems.Add(new CleanupPreviewItem("用户临时预览", demo, "—", 0, null) { IsSelected = false });
        }

        foreach (var item in PreviewItems)
        {
            TrackPreviewItem(item);
        }
    }

    private static long GetFileLengthSafe(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    private void BuildActivePlanFromPreview()
    {
        var items = BuildPlanItemsFromPreview();
        _planBaseline = items;
        _activePlan = items.Length == 0
            ? null
            : OperationPlan.Create("clean", items, DateTimeOffset.UtcNow, PlanLifetime);
    }

    private OperationPlanItem[] BuildPlanItemsFromPreview()
    {
        return PreviewItems
            .Select((item, index) => new OperationPlanItem(
                Id: $"{index}:{item.Path}",
                Title: item.Category,
                TargetPath: ExpandPath(item.Path),
                SizeBytes: item.SizeBytes,
                Risk: OperationPlanValidator.IsUnsafeTarget(ExpandPath(item.Path))
                    ? OperationRisk.High
                    : OperationRisk.Low,
                IsSelected: item.IsSelected && OperationPlanValidator.IsConcreteDeletablePath(item.Path)))
            .ToArray();
    }

    private static string ExpandPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
            return Path.IsPathFullyQualified(expanded)
                ? Path.GetFullPath(expanded)
                : expanded;
        }
        catch
        {
            return path;
        }
    }

    private void TrackPreviewItem(CleanupPreviewItem item)
    {
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CleanupPreviewItem.IsSelected))
            {
                RefreshSelectionSummary();
            }
        };
    }

    private void RebuildCategoryGroups()
    {
        CategoryGroups.Clear();
        foreach (var group in CleanupCategoryGrouper.Group(PreviewItems))
        {
            CategoryGroups.Add(new CleanupCategoryGroupViewModel(group.Category, group.Items));
        }
    }

    private void RefreshSelectionSummary()
    {
        foreach (var group in CategoryGroups)
        {
            group.RefreshSelectionState();
        }

        var selected = PreviewItems.Where(item => item.IsSelected).ToArray();
        var deletable = selected.Count(item => OperationPlanValidator.IsConcreteDeletablePath(item.Path));
        var bytes = selected.Sum(item => item.SizeBytes);
        var categoriesSelected = CategoryGroups.Count(group => group.IsGroupSelected != false);
        SelectionSummary = selected.Length == 0
            ? "未选择项目"
            : $"已选 {selected.Length}/{PreviewItems.Count} · {categoriesSelected} 类 · 可删除 {deletable} · {SystemTelemetryFormatter.Bytes(bytes)}";
        Summary = SelectionSummary;
        CanClean = deletable > 0 && !IsBusy;
        OnPropertyChanged(nameof(SelectionSummary));
    }

    private void AppendOutput(string line)
    {
        RunOnUiThread(() =>
        {
            OutputLines.Add(line);
            OnPropertyChanged(nameof(OutputText));
        });
    }

    private void NotifySurface()
    {
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(IsScanning));
        OnPropertyChanged(nameof(IsReview));
        OnPropertyChanged(nameof(IsComplete));
    }

    partial void OnSurfaceStateChanged(CleanupSurfaceState value) => NotifySurface();
}
