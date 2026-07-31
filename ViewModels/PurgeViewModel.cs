using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinMoe.Models;
using WinMoe.Services;

namespace WinMoe.ViewModels;

public partial class PurgeViewModel : ViewModelBase
{
    private readonly IMoleEngineService _moleEngineService;
    private readonly IPurgeArtifactService _purgeArtifactService;
    private readonly IOperationHistoryService _operationHistoryService;

    public PurgeViewModel(
        IMoleEngineService moleEngineService,
        IPurgeArtifactService purgeArtifactService,
        IOperationHistoryService operationHistoryService)
    {
        _moleEngineService = moleEngineService;
        _purgeArtifactService = purgeArtifactService;
        _operationHistoryService = operationHistoryService;
    }

    public ObservableCollection<PurgeProjectCandidate> Projects { get; } = new();

    public ObservableCollection<string> OutputLines { get; } = new();

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool canRemove;

    [ObservableProperty]
    private string summary = "准备扫描项目构建垃圾";

    [ObservableProperty]
    private string selectedSummary = "0 个项目";

    [ObservableProperty]
    private string engineSummary = "WinMoe 以与 Mole 相同的 Windows 规则预览项目构建垃圾（node_modules、bin、obj 等）。";

    public string OutputText => string.Join(Environment.NewLine, OutputLines);

    [RelayCommand]
    public async Task PreviewAsync()
    {
        var startedAt = Stopwatch.GetTimestamp();
        var succeeded = false;
        var historySummary = "项目清理预览未完成";

        IsBusy = true;
        CanRemove = false;
        ClearProjects();
        OutputLines.Clear();
        OnPropertyChanged(nameof(OutputText));
        Summary = "正在扫描项目构建垃圾…";

        try
        {
            var availability = _moleEngineService.GetAvailability();
            EngineSummary = availability.IsAvailable
                ? $"Mole 引擎可用（{availability.Path}）；项目清理预览使用 Windows 原生规则。"
                : $"{availability.Message} Purge preview still uses local Windows artifact rules.";

            var projects = await _purgeArtifactService.PreviewAsync().ConfigureAwait(false);
            succeeded = true;
            historySummary = BuildPreviewSummary(projects);

            RunOnUiThread(() =>
            {
                ClearProjects();
                foreach (var project in projects)
                {
                    project.PropertyChanged += Project_PropertyChanged;
                    Projects.Add(project);
                }

                Summary = historySummary;
                UpdateSelectionState();
            });
        }
        finally
        {
            await RecordHistoryAsync(
                "purge-preview",
                "project artifacts",
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
        var selectedProjects = Projects.Where(project => project.IsSelected).ToList();
        if (selectedProjects.Count == 0)
        {
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        var succeeded = false;
        var historySummary = "项目清理移除未完成";

        IsBusy = true;
        CanRemove = false;
        OutputLines.Clear();
        OnPropertyChanged(nameof(OutputText));
        Summary = "正在移除所选构建垃圾…";

        try
        {
            var results = await _purgeArtifactService.RemoveAsync(selectedProjects).ConfigureAwait(false);
            var removedBytes = results.Where(result => result.Succeeded).Sum(result => result.SizeBytes);
            var failedCount = results.Count(result => !result.Succeeded);
            succeeded = failedCount == 0;
            historySummary = failedCount == 0
                ? $"已移除 {results.Count} 项构建垃圾，释放 {SystemTelemetryFormatter.Bytes(removedBytes)}"
                : $"已移除 {results.Count - failedCount} 项；{failedCount} 项失败";

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
                "purge-remove",
                $"{selectedProjects.Count} 个所选项目",
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
        foreach (var project in Projects)
        {
            project.IsSelected = true;
        }

        UpdateSelectionState();
    }

    [RelayCommand]
    public void ClearSelection()
    {
        foreach (var project in Projects)
        {
            project.IsSelected = false;
        }

        UpdateSelectionState();
    }

    [RelayCommand]
    public async Task CheckMoleAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _moleEngineService.ExecuteCommandAsync("purge --help", AppendOutput).ConfigureAwait(false);
            RunOnUiThread(() =>
            {
                EngineSummary = result.Succeeded
                    ? "Mole purge is present; its Windows command is interactive, so WinMoe uses a safe preview list before deleting artifacts."
                    : $"Mole purge help failed with exit code {result.ExitCode}; local preview remains available.";
            });
        }
        finally
        {
            RunOnUiThread(() => IsBusy = false);
        }
    }

    private void Project_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PurgeProjectCandidate.IsSelected))
        {
            UpdateSelectionState();
        }
    }

    private void UpdateSelectionState()
    {
        var selected = Projects.Where(project => project.IsSelected).ToList();
        var selectedBytes = selected.Sum(project => project.TotalSizeBytes);
        SelectedSummary = $"{selected.Count} 个项目 · {SystemTelemetryFormatter.Bytes(selectedBytes)}";
        CanRemove = selected.Count > 0 && !IsBusy;
    }

    private void ClearProjects()
    {
        foreach (var project in Projects)
        {
            project.PropertyChanged -= Project_PropertyChanged;
        }

        Projects.Clear();
        UpdateSelectionState();
    }

    private static string BuildPreviewSummary(IReadOnlyList<PurgeProjectCandidate> projects)
    {
        if (projects.Count == 0)
        {
            return "未发现可清理的项目构建垃圾";
        }

        var totalBytes = projects.Sum(project => project.TotalSizeBytes);
        var totalArtifacts = projects.Sum(project => project.ArtifactCount);
        return $"{projects.Count} 个项目 · {totalArtifacts} 项垃圾 · {SystemTelemetryFormatter.Bytes(totalBytes)}";
    }

    private void AppendOutput(string line)
    {
        RunOnUiThread(() =>
        {
            OutputLines.Add(line);
            OnPropertyChanged(nameof(OutputText));
        });
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
