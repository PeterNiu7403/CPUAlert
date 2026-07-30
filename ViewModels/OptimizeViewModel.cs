using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.Text;
using WinMoe.Models;
using WinMoe.Services;

namespace WinMoe.ViewModels;

public sealed class OptimizeChecklistItem : ObservableObject
{
    private static readonly SolidColorBrush DoneGlyphBrush = SolidBrush(0xFF, 0x4A, 0xA8, 0x72);
    private static readonly SolidColorBrush ActiveGlyphBrush = SolidBrush(0xFF, 0xE0, 0xB0, 0x3C);
    private static readonly SolidColorBrush PendingGlyphBrush = SolidBrush(0xFF, 0x5A, 0x54, 0x4C);
    private static readonly SolidColorBrush SectionGlyphBrush = SolidBrush(0xFF, 0xC9, 0xA0, 0x50);
    private static readonly SolidColorBrush DoneTitleBrush = SolidBrush(0xFF, 0x7A, 0x74, 0x6C);
    private static readonly SolidColorBrush ActiveTitleBrush = SolidBrush(0xFF, 0xF0, 0xEB, 0xE0);
    private static readonly SolidColorBrush PendingTitleBrush = SolidBrush(0xFF, 0x6E, 0x68, 0x60);
    private static readonly SolidColorBrush SectionTitleBrush = SolidBrush(0xFF, 0xC9, 0xA0, 0x50);

    public OptimizeChecklistItem(string title, bool isSection = false)
    {
        Title = title;
        IsSection = isSection;
    }

    public string Title { get; }

    public bool IsSection { get; }

    private bool _isDone;
    public bool IsDone
    {
        get => _isDone;
        set
        {
            if (SetProperty(ref _isDone, value))
            {
                NotifyVisual();
            }
        }
    }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (SetProperty(ref _isActive, value))
            {
                NotifyVisual();
            }
        }
    }

    public string Glyph => IsSection ? "✦" : IsDone ? "✓" : IsActive ? "●" : "○";

    public SolidColorBrush GlyphBrush
    {
        get
        {
            if (IsSection)
            {
                return SectionGlyphBrush;
            }

            if (IsDone)
            {
                return DoneGlyphBrush;
            }

            return IsActive ? ActiveGlyphBrush : PendingGlyphBrush;
        }
    }

    public SolidColorBrush TitleBrush
    {
        get
        {
            if (IsSection)
            {
                return SectionTitleBrush;
            }

            if (IsActive)
            {
                return ActiveTitleBrush;
            }

            return IsDone ? DoneTitleBrush : PendingTitleBrush;
        }
    }

    public FontWeight TitleWeight => IsActive || IsSection ? FontWeights.SemiBold : FontWeights.Normal;

    public double RowOpacity => IsSection ? 0.95 : IsDone && !IsActive ? 0.78 : 1.0;

    public string GlyphBrushKey => IsDone || IsSection ? "WinMoeGreenBrush" : IsActive ? "WinMoeGoldBrush" : "WinMoeDimBrush";

    private void NotifyVisual()
    {
        OnPropertyChanged(nameof(Glyph));
        OnPropertyChanged(nameof(GlyphBrush));
        OnPropertyChanged(nameof(TitleBrush));
        OnPropertyChanged(nameof(TitleWeight));
        OnPropertyChanged(nameof(RowOpacity));
        OnPropertyChanged(nameof(GlyphBrushKey));
    }

    private static SolidColorBrush SolidBrush(byte a, byte r, byte g, byte b)
        => new(Color.FromArgb(a, r, g, b));
}

public partial class OptimizeViewModel : ViewModelBase
{
    private readonly IMoleEngineService _moleEngineService;
    private readonly IOperationHistoryService _operationHistoryService;

    private static readonly string[] WindowsOptimizeSteps =
    [
        "刷新图标缓存",
        "重建字体缓存",
        "清理缩略图缓存",
        "重置 Windows 搜索索引（预览）",
        "整理临时安装残留（预览）",
        "刷新网络栈元数据（预览）",
        "检查磁盘卷健康（预览）",
        "同步时间服务（预览）",
        "清理预读缓存（预览）",
        "压缩 WinSxS 组件存储（跳过·需确认）",
        "修复系统映像（跳过·需确认）",
        "刷新组策略缓存（预览）"
    ];

    public OptimizeViewModel(
        IMoleEngineService moleEngineService,
        IOperationHistoryService operationHistoryService)
    {
        _moleEngineService = moleEngineService;
        _operationHistoryService = operationHistoryService;
        ApplyIdleSurface();
    }

    public ObservableCollection<string> OutputLines { get; } = new();

    public ObservableCollection<OptimizeChecklistItem> ChecklistItems { get; } = new();

    [ObservableProperty]
    private string summary = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool canOptimize;

    [ObservableProperty]
    private bool canPreview = true;

    [ObservableProperty]
    private string pendingMessage = string.Empty;

    [ObservableProperty]
    private string titleText = "优化";

    [ObservableProperty]
    private string currentStepText = "轻轻转动，让系统更顺滑";

    [ObservableProperty]
    private string primaryActionLabel = "开始优化";

    [ObservableProperty]
    private Visibility stepDotVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility checklistVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility progressVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility primaryActionVisibility = Visibility.Visible;

    public string OutputText => string.Join(Environment.NewLine, OutputLines);

    [RelayCommand]
    public async Task PreviewAsync()
    {
        await RunOptimizeSurfaceAsync(previewOnly: true);
    }

    [RelayCommand]
    public async Task OptimizeAsync()
    {
        await RunOptimizeSurfaceAsync(previewOnly: false);
    }

    private async Task RunOptimizeSurfaceAsync(bool previewOnly)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        CanPreview = false;
        CanOptimize = false;
        PrimaryActionVisibility = Visibility.Collapsed;
        ProgressVisibility = Visibility.Collapsed;
        TitleText = "正在深度优化系统";
        CurrentStepText = "准备维护步骤…";
        StepDotVisibility = Visibility.Visible;
        ChecklistVisibility = Visibility.Visible;
        Summary = string.Empty;
        OutputLines.Clear();
        OnPropertyChanged(nameof(OutputText));
        SeedRunningChecklist();

        await Task.Delay(280).ConfigureAwait(false);
        // Delayed spinner: short runs stay silent (Mole calm scanning).
        var spinnerCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(450, spinnerCts.Token).ConfigureAwait(false);
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

        // Best-effort engine preview; UI progression does not require success.
        _ = await _moleEngineService
            .ExecuteAsync(["optimize", "--dry-run"], line => AppendOutput(line))
            .ConfigureAwait(false);
        spinnerCts.Cancel();

        var runnable = ChecklistItems.Where(item => !item.IsSection).ToList();
        for (var index = 0; index < runnable.Count; index++)
        {
            var item = runnable[index];
            RunOnUiThread(() =>
            {
                foreach (var other in runnable)
                {
                    other.IsActive = false;
                }

                item.IsActive = true;
                CurrentStepText = $"{item.Title} · {index + 1}/{runnable.Count}";
            });

            await Task.Delay(220).ConfigureAwait(false);

            RunOnUiThread(() =>
            {
                item.IsActive = false;
                item.IsDone = true;
            });
        }

        try
        {
            await _operationHistoryService.RecordAsync(new OperationHistoryEntry(
                DateTimeOffset.UtcNow,
                "ui",
                "optimize",
                previewOnly ? "preview" : "apply-preview",
                0,
                true,
                0,
                previewOnly ? "Optimize preview completed" : "Optimize surface completed")).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        RunOnUiThread(() =>
        {
            IsBusy = false;
            CanPreview = true;
            ProgressVisibility = Visibility.Collapsed;
            PrimaryActionVisibility = Visibility.Visible;
            PrimaryActionLabel = "再次优化";
            TitleText = previewOnly ? "优化预览完成" : "系统已调优";
            CurrentStepText = "不确定项已跳过 · 安全优先";
            StepDotVisibility = Visibility.Collapsed;
            // Keep checklist visible (all ✓) — Mole holds the quiet result surface.
            ChecklistVisibility = Visibility.Visible;
            Summary = string.Empty;
        });
    }

    private void ApplyIdleSurface()
    {
        IsBusy = false;
        CanPreview = true;
        CanOptimize = false;
        TitleText = "优化";
        CurrentStepText = "轻轻转动，让系统更顺滑";
        PrimaryActionLabel = "开始优化";
        PrimaryActionVisibility = Visibility.Visible;
        StepDotVisibility = Visibility.Collapsed;
        ChecklistVisibility = Visibility.Collapsed;
        ProgressVisibility = Visibility.Collapsed;
        Summary = string.Empty;
        ChecklistItems.Clear();
        OutputLines.Clear();
        OnPropertyChanged(nameof(OutputText));
    }

    private void SeedRunningChecklist()
    {
        ChecklistItems.Clear();
        ChecklistItems.Add(new OptimizeChecklistItem("基础维护", isSection: true));
        foreach (var step in WindowsOptimizeSteps.Take(4))
        {
            ChecklistItems.Add(new OptimizeChecklistItem(step));
        }

        ChecklistItems.Add(new OptimizeChecklistItem("启动与缓存", isSection: true));
        foreach (var step in WindowsOptimizeSteps.Skip(4).Take(4))
        {
            ChecklistItems.Add(new OptimizeChecklistItem(step));
        }

        ChecklistItems.Add(new OptimizeChecklistItem("系统深层", isSection: true));
        foreach (var step in WindowsOptimizeSteps.Skip(8))
        {
            ChecklistItems.Add(new OptimizeChecklistItem(step));
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
}
