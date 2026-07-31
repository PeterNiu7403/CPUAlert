using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using WinMoe.Models;
using WinMoe.Services;

namespace WinMoe.ViewModels;

public partial class UninstallViewModel : ViewModelBase
{
    private const string AppsTabUninstall = "uninstall";
    private const string AppsTabUpdates = "updates";
    private const string AppsTabStartup = "startup";

    private readonly IMoleEngineService _moleEngineService;
    private readonly IInstalledApplicationService _installedApplicationService;
    private readonly IOperationHistoryService _operationHistoryService;
    private readonly IWindowsStartupItemService _startupItemService;
    private readonly List<InstalledApplication> _allApplications = [];

    public UninstallViewModel(
        IMoleEngineService moleEngineService,
        IInstalledApplicationService installedApplicationService,
        IOperationHistoryService operationHistoryService,
        IWindowsStartupItemService startupItemService)
    {
        _moleEngineService = moleEngineService;
        _installedApplicationService = installedApplicationService;
        _operationHistoryService = operationHistoryService;
        _startupItemService = startupItemService;
    }

    public ObservableCollection<ApplicationRowViewModel> Applications { get; } = new();

    public ObservableCollection<LeftoverCandidate> Leftovers { get; } = new();

    public ObservableCollection<StartupItem> StartupItems { get; } = new();

    public ObservableCollection<string> OutputLines { get; } = new();

    [ObservableProperty]
    private string summary = "正在准备软件清单";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private string sortKey = "size";

    [ObservableProperty]
    private bool sortDescending = true;

    [ObservableProperty]
    private InstalledApplication? selectedApplication;

    [ObservableProperty]
    private ApplicationRowViewModel? selectedApplicationRow;

    [ObservableProperty]
    private string leftoverSummary = "尚未选择软件";

    [ObservableProperty]
    private string appsTab = AppsTabUninstall;

    public string OutputText => string.Join(Environment.NewLine, OutputLines);

    public bool CanPreviewLeftovers => SelectedApplication is not null && !IsBusy;

    public bool CanLaunchUninstaller =>
        !IsBusy &&
        (Applications.Any(row => row.IsSelected && !string.IsNullOrWhiteSpace(row.Application.UninstallString)) ||
         (SelectedApplication is not null && !string.IsNullOrWhiteSpace(SelectedApplication.UninstallString)));

    public bool CanRemoveSelectedLeftovers => Leftovers.Any(leftover => leftover.IsSelected) && !IsBusy;

    public bool CanRemoveSelectedApps => Applications.Any(row => row.IsSelected) && !IsBusy;

    public string SortSummary => $"按 {SortKey} {(SortDescending ? "降序" : "升序")}";

    /// <summary>Mole sort links: Name ↕ / Size ↕ / Last Used ↕ / Installed ↕ with active arrow.</summary>
    public string NameSortLabel => FormatSortLabel("名称", "name");

    public string SizeSortLabel => FormatSortLabel("大小", "size");

    public string SourceSortLabel => FormatSortLabel("上次使用", "lastused");

    public string InstalledSortLabel => FormatSortLabel("已安装", "installed");

    public string AppsCountText
    {
        get
        {
            var selected = Applications.Where(row => row.IsSelected).ToArray();
            if (selected.Length == 0)
            {
                return $"{Applications.Count} 个软件";
            }

            var bytes = selected.Sum(row => row.Application.SizeBytes);
            // Mole footer: "3 apps · 9.61 GB"
            return $"已选 {selected.Length} 个 · {SystemTelemetryFormatter.Bytes(bytes)}";
        }
    }

    public string RemoveButtonLabel
    {
        get
        {
            var selected = Applications.Count(row => row.IsSelected);
            return selected <= 0 ? "移除" : $"移除 {selected}";
        }
    }

    public Visibility ClearSelectionVisibility =>
        Applications.Any(row => row.IsSelected) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SelectedIconsVisibility =>
        Applications.Any(row => row.IsSelected) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Up to 5 selected app chips for the sticky bottom bar (Mole icon stack).</summary>
    public ObservableCollection<ApplicationRowViewModel> SelectedFooterApps { get; } = new();

    public string LoadedCountText => _allApplications.Count == 0
        ? "正在读取已安装软件"
        : $"已载入 {_allApplications.Count} 个软件";

    public bool IsUninstallTab => string.Equals(AppsTab, AppsTabUninstall, StringComparison.OrdinalIgnoreCase);

    public bool IsUpdatesTab => string.Equals(AppsTab, AppsTabUpdates, StringComparison.OrdinalIgnoreCase);

    public bool IsStartupTab => string.Equals(AppsTab, AppsTabStartup, StringComparison.OrdinalIgnoreCase);

    public bool HasSelectedApplication => SelectedApplication is not null;

    public bool HasLeftovers => Leftovers.Count > 0;

    public string LeftoverSelectionText => Leftovers.Count == 0
        ? "已选 0/0"
        : $"已选 {Leftovers.Count(leftover => leftover.IsSelected)}/{Leftovers.Count}";

    public string UpdatesSummary =>
        "未发现可静默安装的安全更新源。WinMoe 不会从未知通道自动更新软件。";

    public string UpdatesHeadline => "0 个可用更新";

    public string StartupSummary =>
        StartupItems.Count == 0
            ? "未发现启动项（或无权限读取）。列表只读，不会修改注册表或服务。"
            : $"只读清单 · {StartupItems.Count} 项 · 不会修改注册表 / 计划任务 / 服务";

    public string StartupHeadline => StartupItems.Count == 0
        ? "启动项"
        : $"{StartupItems.Count} 个启动项";

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        Summary = "正在读取已安装软件…";
        OutputLines.Clear();
        Leftovers.Clear();
        OnPropertyChanged(nameof(OutputText));

        try
        {
            var appsTask = _installedApplicationService.GetInstalledApplicationsAsync();
            var startupTask = _startupItemService.GetStartupItemsAsync();
            await Task.WhenAll(appsTask, startupTask).ConfigureAwait(false);

            var apps = await appsTask.ConfigureAwait(false);
            var startups = await startupTask.ConfigureAwait(false);
            var runningHints = AppActivityHintResolver.ResolveForApps(apps.Select(app => app.Name));
            RunOnUiThread(() =>
            {
                _allApplications.Clear();
                _allApplications.AddRange(apps);
                ApplyFilter(runningHints);

                StartupItems.Clear();
                foreach (var item in startups)
                {
                    StartupItems.Add(item);
                }

                Summary = $"已载入 {_allApplications.Count} 个软件 · {StartupItems.Count} 个启动项";
                OnPropertyChanged(nameof(LoadedCountText));
                OnPropertyChanged(nameof(StartupSummary));
                OnPropertyChanged(nameof(StartupHeadline));
                OnPropertyChanged(nameof(UpdatesHeadline));
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Summary = ex.Message;
            AppendOutput(ex.Message);
        }
        finally
        {
            // Continuation runs on a thread-pool thread after ConfigureAwait(false);
            // IsBusy triggers OnIsBusyChanged -> NotifyCanExecuteChanged, which must
            // be raised on the UI thread (RPC_E_WRONG_THREAD otherwise).
            RunOnUiThread(() =>
            {
                IsBusy = false;
                PreviewLeftoversCommand.NotifyCanExecuteChanged();
                LaunchUninstallerCommand.NotifyCanExecuteChanged();
                RemoveSelectedLeftoversCommand.NotifyCanExecuteChanged();
            });
        }
    }

    [RelayCommand]
    public void Sort(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (string.Equals(SortKey, key, StringComparison.OrdinalIgnoreCase))
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortKey = key;
            SortDescending = !string.Equals(key, "name", StringComparison.OrdinalIgnoreCase);
        }

        ApplyFilter();
        NotifySortLabels();
    }

    [RelayCommand]
    public void SelectAppsTab(string tab)
    {
        if (string.IsNullOrWhiteSpace(tab))
        {
            return;
        }

        var normalized = tab.Trim().ToLowerInvariant();
        if (normalized is not (AppsTabUninstall or AppsTabUpdates or AppsTabStartup))
        {
            return;
        }

        AppsTab = normalized;
    }

    [RelayCommand]
    public void SelectAllLeftovers()
    {
        foreach (var leftover in Leftovers)
        {
            leftover.IsSelected = true;
        }

        NotifyLeftoverSelectionState();
    }

    [RelayCommand]
    public void ClearSelection()
    {
        foreach (var row in Applications)
        {
            row.IsSelected = false;
        }

        NotifyAppSelectionState();
    }

    [RelayCommand]
    public void ToggleAppSelection(ApplicationRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        row.IsSelected = !row.IsSelected;
        NotifyAppSelectionState();
    }

    [RelayCommand]
    public async Task ToggleExpandAsync(ApplicationRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var expanding = !row.IsExpanded;
        foreach (var other in Applications)
        {
            other.IsExpanded = false;
        }

        row.IsExpanded = expanding;
        if (expanding)
        {
            SelectedApplicationRow = row;
            SelectedApplication = row.Application;
            if (Leftovers.Count == 0 ||
                !string.Equals(LeftoverSummary, $"已选择 {row.Name}", StringComparison.Ordinal) &&
                !LeftoverSummary.Contains(row.Name, StringComparison.Ordinal))
            {
                await PreviewLeftoversAsync();
            }

            var leftoverCount = Leftovers.Count;
            var leftoverBytes = Leftovers.Sum(item => item.SizeBytes);
            row.SelectionHint = leftoverCount == 0
                ? $"残留 · {row.SizeText}"
                : $"{leftoverCount} 项 · {SystemTelemetryFormatter.Bytes(leftoverBytes)} 残留";
        }
    }

    [RelayCommand(CanExecute = nameof(CanPreviewLeftovers))]
    public async Task PreviewLeftoversAsync()
    {
        if (SelectedApplication is null)
        {
            return;
        }

        IsBusy = true;
        Leftovers.Clear();
        NotifyLeftoverSelectionState();

        try
        {
            var leftovers = await _installedApplicationService.PreviewLeftoversAsync(SelectedApplication);
            RunOnUiThread(() =>
            {
                foreach (var leftover in leftovers)
                {
                    Leftovers.Add(leftover);
                    TrackLeftover(leftover);
                }

                var totalBytes = leftovers.Sum(leftover => leftover.SizeBytes);
                LeftoverSummary = leftovers.Count == 0
                    ? "未发现残留候选项"
                    : $"{leftovers.Count} 个残留候选项 · {SystemTelemetryFormatter.Bytes(totalBytes)}";
                NotifyLeftoverSelectionState();
            });
        }
        finally
        {
            IsBusy = false;
            PreviewLeftoversCommand.NotifyCanExecuteChanged();
            LaunchUninstallerCommand.NotifyCanExecuteChanged();
            RemoveSelectedLeftoversCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanLaunchUninstaller))]
    public async Task LaunchUninstallerAsync()
    {
        var queue = Applications.Where(row => row.IsSelected).Select(row => row.Application).ToList();
        if (queue.Count == 0 && SelectedApplication is not null)
        {
            queue.Add(SelectedApplication);
        }

        if (queue.Count == 0)
        {
            return;
        }

        IsBusy = true;
        OutputLines.Clear();
        OnPropertyChanged(nameof(OutputText));

        try
        {
            var started = 0;
            foreach (var app in queue)
            {
                // After the first Task.Delay(400).ConfigureAwait(false) below, later
                // iterations run on a thread-pool thread; OnSelectedApplicationChanged
                // clears Leftovers and touches commands, so marshal the assignment.
                RunOnUiThread(() => SelectedApplication = app);
                var result = await _installedApplicationService.LaunchUninstallerAsync(app);
                AppendOutput(result.Succeeded
                    ? $"{app.Name}: {result.StandardOutput}"
                    : $"{app.Name}: {result.StandardError}");
                if (result.Succeeded)
                {
                    started++;
                }

                // Brief gap so multiple vendor UIs don't thrash focus (Mole batch style).
                if (queue.Count > 1)
                {
                    await Task.Delay(400).ConfigureAwait(false);
                }
            }

            Summary = started == queue.Count
                ? $"已启动 {started} 个卸载程序"
                : $"已启动 {started}/{queue.Count} 个卸载程序";

            if (started > 0)
            {
                try
                {
                    await _operationHistoryService.RecordAsync(new OperationHistoryEntry(
                        DateTimeOffset.UtcNow,
                        "ui",
                        "uninstall",
                        "launch_uninstaller",
                        0,
                        true,
                        0,
                        $"Started {started} uninstaller(s)")).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        finally
        {
            // May resume on a thread-pool thread (Task.Delay / RecordAsync with
            // ConfigureAwait(false)); marshal the busy reset and command notifies.
            RunOnUiThread(() =>
            {
                IsBusy = false;
                LaunchUninstallerCommand.NotifyCanExecuteChanged();
                RemoveSelectedLeftoversCommand.NotifyCanExecuteChanged();
            });
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedLeftovers))]
    public async Task RemoveSelectedLeftoversAsync()
    {
        var selected = Leftovers.Where(leftover => leftover.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            return;
        }

        IsBusy = true;
        OutputLines.Clear();
        OnPropertyChanged(nameof(OutputText));

        try
        {
            var results = await _installedApplicationService.RemoveLeftoversAsync(selected);
            RunOnUiThread(() =>
            {
                foreach (var result in results)
                {
                    OutputLines.Add($"{(result.Succeeded ? "OK" : "FAILED")} {result.Path} - {result.Message}");
                }

                foreach (var removed in results.Where(result => result.Succeeded).Select(result => result.Path).ToHashSet(StringComparer.OrdinalIgnoreCase))
                {
                    var item = Leftovers.FirstOrDefault(leftover => string.Equals(leftover.Path, removed, StringComparison.OrdinalIgnoreCase));
                    if (item is not null)
                    {
                        Leftovers.Remove(item);
                    }
                }

                var removedBytes = results.Where(result => result.Succeeded).Sum(result => result.SizeBytes);
                LeftoverSummary = $"已将 {results.Count(result => result.Succeeded)}/{results.Count} 项移入回收站 · {SystemTelemetryFormatter.Bytes(removedBytes)}";
                OnPropertyChanged(nameof(OutputText));
                NotifyLeftoverSelectionState();
            });
        }
        finally
        {
            IsBusy = false;
            RemoveSelectedLeftoversCommand.NotifyCanExecuteChanged();
        }
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
                ? "Mole engine is present; this page uses native inventory and safe leftover preview because Mole uninstall is an interactive TUI"
                : $"Mole engine check failed with exit code {result.ExitCode}; native inventory remains available";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSortKeyChanged(string value) => NotifySortLabels();

    partial void OnSortDescendingChanged(bool value) => NotifySortLabels();

    private void NotifySortLabels()
    {
        OnPropertyChanged(nameof(SortSummary));
        OnPropertyChanged(nameof(NameSortLabel));
        OnPropertyChanged(nameof(SizeSortLabel));
        OnPropertyChanged(nameof(SourceSortLabel));
        OnPropertyChanged(nameof(InstalledSortLabel));
    }

    // SourceSortLabel is bound to "上次使用" / lastused for Mole parity.

    private string FormatSortLabel(string title, string key)
    {
        if (!string.Equals(SortKey, key, StringComparison.OrdinalIgnoreCase))
        {
            return $"{title} ↕";
        }

        return SortDescending ? $"{title} ↓" : $"{title} ↑";
    }

    partial void OnSelectedApplicationChanged(InstalledApplication? value)
    {
        Leftovers.Clear();
        LeftoverSummary = value is null ? "尚未选择软件" : $"已选择 {value.Name}";
        SyncExpandedRows();
        NotifySelectedApplicationState();
        NotifyLeftoverSelectionState();
    }

    partial void OnSelectedApplicationRowChanged(ApplicationRowViewModel? value)
    {
        SelectedApplication = value?.Application;
    }

    partial void OnAppsTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsUninstallTab));
        OnPropertyChanged(nameof(IsUpdatesTab));
        OnPropertyChanged(nameof(IsStartupTab));
    }

    private void NotifySelectedApplicationState()
    {
        OnPropertyChanged(nameof(HasSelectedApplication));
        OnPropertyChanged(nameof(CanPreviewLeftovers));
        OnPropertyChanged(nameof(CanLaunchUninstaller));
        OnPropertyChanged(nameof(CanRemoveSelectedLeftovers));
        PreviewLeftoversCommand.NotifyCanExecuteChanged();
        LaunchUninstallerCommand.NotifyCanExecuteChanged();
        RemoveSelectedLeftoversCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifySelectedApplicationState();
        NotifyLeftoverSelectionState();
        OnPropertyChanged(nameof(CanRemoveSelectedApps));
        PreviewLeftoversCommand.NotifyCanExecuteChanged();
        LaunchUninstallerCommand.NotifyCanExecuteChanged();
        RemoveSelectedLeftoversCommand.NotifyCanExecuteChanged();
    }

    private void ApplyFilter(IReadOnlyDictionary<string, string>? activityHints = null)
    {
        var query = SearchQuery.Trim();
        IEnumerable<InstalledApplication> filtered = string.IsNullOrWhiteSpace(query)
            ? _allApplications
            : _allApplications
                .Where(app =>
                    app.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    app.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    app.Source.Contains(query, StringComparison.OrdinalIgnoreCase));

        filtered = SortApplications(filtered);

        var selectedId = SelectedApplication?.Id;
        ApplicationRowViewModel? selectedRow = null;

        var selectedIds = Applications
            .Where(row => row.IsSelected)
            .Select(row => row.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var previousActivity = Applications
            .Where(row => !string.IsNullOrWhiteSpace(row.ActivityText))
            .ToDictionary(row => row.Id, row => row.ActivityText, StringComparer.OrdinalIgnoreCase);

        Applications.Clear();
        foreach (var app in filtered.Take(500))
        {
            var row = new ApplicationRowViewModel(app)
            {
                IsExpanded = !string.IsNullOrWhiteSpace(selectedId) &&
                             string.Equals(app.Id, selectedId, StringComparison.OrdinalIgnoreCase),
                IsSelected = selectedIds.Contains(app.Id)
            };

            var isRunning = activityHints is not null && activityHints.ContainsKey(app.Name);
            var activity = AppActivityFormatter.Format(app.LastActivityUtc, isRunning);
            if (!string.IsNullOrWhiteSpace(activity))
            {
                row.ActivityText = activity;
            }
            else if (previousActivity.TryGetValue(app.Id, out var prior))
            {
                row.ActivityText = prior;
            }

            TrackAppRow(row);
            Applications.Add(row);
            if (row.IsExpanded)
            {
                selectedRow = row;
            }
        }

        SelectedApplicationRow = selectedRow;
        NotifyAppSelectionState();
        OnPropertyChanged(nameof(LoadedCountText));
    }

    private void TrackAppRow(ApplicationRowViewModel row)
    {
        row.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ApplicationRowViewModel.IsSelected))
            {
                NotifyAppSelectionState();
            }
        };
    }

    private void NotifyAppSelectionState()
    {
        RebuildSelectedFooterApps();
        OnPropertyChanged(nameof(AppsCountText));
        OnPropertyChanged(nameof(RemoveButtonLabel));
        OnPropertyChanged(nameof(ClearSelectionVisibility));
        OnPropertyChanged(nameof(SelectedIconsVisibility));
        OnPropertyChanged(nameof(CanRemoveSelectedApps));
        OnPropertyChanged(nameof(CanLaunchUninstaller));
        LaunchUninstallerCommand.NotifyCanExecuteChanged();
    }

    private void RebuildSelectedFooterApps()
    {
        SelectedFooterApps.Clear();
        foreach (var row in Applications.Where(item => item.IsSelected).Take(5))
        {
            SelectedFooterApps.Add(row);
        }
    }

    private IEnumerable<InstalledApplication> SortApplications(IEnumerable<InstalledApplication> apps)
    {
        return SortKey.ToLowerInvariant() switch
        {
            "name" => SortDescending
                ? apps.OrderByDescending(app => app.Name, StringComparer.OrdinalIgnoreCase)
                : apps.OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase),
            "source" or "lastused" => SortDescending
                ? apps.OrderByDescending(app => app.LastActivityUtc ?? DateTimeOffset.MinValue)
                    .ThenBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
                : apps.OrderBy(app => app.LastActivityUtc ?? DateTimeOffset.MaxValue)
                    .ThenBy(app => app.Name, StringComparer.OrdinalIgnoreCase),
            // InstallDateRaw is yyyyMMdd, so ordinal string order is chronological; empty = unknown/oldest.
            "installed" => SortDescending
                ? apps.OrderByDescending(app => app.InstallDateRaw, StringComparer.Ordinal)
                    .ThenBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
                : apps.OrderBy(app => app.InstallDateRaw, StringComparer.Ordinal)
                    .ThenBy(app => app.Name, StringComparer.OrdinalIgnoreCase),
            _ => SortDescending
                ? apps.OrderByDescending(app => app.SizeBytes).ThenBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
                : apps.OrderBy(app => app.SizeBytes).ThenBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
        };
    }

    private void AppendOutput(string line)
    {
        RunOnUiThread(() =>
        {
            OutputLines.Add(line);
            OnPropertyChanged(nameof(OutputText));
        });
    }

    private void TrackLeftover(LeftoverCandidate leftover)
    {
        leftover.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LeftoverCandidate.IsSelected))
            {
                NotifyLeftoverSelectionState();
                RemoveSelectedLeftoversCommand.NotifyCanExecuteChanged();
            }
        };
    }

    private void SyncExpandedRows()
    {
        foreach (var row in Applications)
        {
            row.IsExpanded = SelectedApplication is not null &&
                string.Equals(row.Application.Id, SelectedApplication.Id, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void NotifyLeftoverSelectionState()
    {
        OnPropertyChanged(nameof(HasLeftovers));
        OnPropertyChanged(nameof(LeftoverSelectionText));
        OnPropertyChanged(nameof(CanRemoveSelectedLeftovers));
        RemoveSelectedLeftoversCommand.NotifyCanExecuteChanged();
    }
}
