using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinMoe.Models;
using WinMoe.Services;

namespace WinMoe.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IMoleEngineService _moleEngineService;
    private readonly IOperationHistoryService _operationHistoryService;
    private readonly ISystemTelemetryHistoryService _telemetryHistoryService;
    private readonly IApplicationSettingsService _settingsService;

    public SettingsViewModel(
        IMoleEngineService moleEngineService,
        IOperationHistoryService operationHistoryService,
        ISystemTelemetryHistoryService telemetryHistoryService,
        IApplicationSettingsService settingsService)
    {
        _moleEngineService = moleEngineService;
        _operationHistoryService = operationHistoryService;
        _telemetryHistoryService = telemetryHistoryService;
        _settingsService = settingsService;
        Refresh();
    }

    public ObservableCollection<OperationHistoryEntry> HistoryEntries { get; } = new();

    [ObservableProperty]
    private string engineStatus = string.Empty;

    [ObservableProperty]
    private string enginePath = string.Empty;

    [ObservableProperty]
    private string engineKind = string.Empty;

    [ObservableProperty]
    private string mcpEndpoint = string.Empty;

    [ObservableProperty]
    private string mcpStdioCommand = "Assets\\Mcp\\winmoe-mcp-stdio.exe";

    [ObservableProperty]
    private string engineInstallHint = "WinMoe 内置 Assets\\Mole\\mo.exe 引擎；也可用 Assets\\mo.exe、Assets\\Mole\\mole.ps1、Assets\\Mole\\mo.cmd 或 PATH 中的 mo 覆盖。";

    [ObservableProperty]
    private string settingsPath = string.Empty;

    [ObservableProperty]
    private string telemetryHistoryPath = string.Empty;

    [ObservableProperty]
    private string activityHistoryPath = string.Empty;

    [ObservableProperty]
    private string historySummary = "尚未载入历史";

    [ObservableProperty]
    private string settingsStatus = "本次会话尚未保存设置";

    [ObservableProperty]
    private string samplingIntervalSeconds = string.Empty;

    [ObservableProperty]
    private string historyRetentionDays = string.Empty;

    [ObservableProperty]
    private bool httpServerEnabled;

    [ObservableProperty]
    private string httpServerPort = string.Empty;

    [ObservableProperty]
    private bool trayIconEnabled;

    [ObservableProperty]
    private bool mcpDestructiveActionsEnabled;

    [ObservableProperty]
    private bool telemetryEnabled;

    [RelayCommand]
    public void Refresh()
    {
        var availability = _moleEngineService.GetAvailability();
        EngineStatus = availability.Message;
        EnginePath = availability.Path ?? "未解析到引擎";
        EngineKind = availability.Kind.ToString();

        SettingsPath = _settingsService.SettingsFilePath;
        TelemetryHistoryPath = _telemetryHistoryService.HistoryFilePath;
        ActivityHistoryPath = _operationHistoryService.HistoryFilePath;
        ApplySettings(_settingsService.Reload());
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        var current = _settingsService.Current;
        var settings = WinMoeSettings.Normalize(new WinMoeSettings
        {
            SamplingIntervalSeconds = ParseInt(SamplingIntervalSeconds, current.SamplingIntervalSeconds),
            HistoryRetentionDays = ParseInt(HistoryRetentionDays, current.HistoryRetentionDays),
            HttpServerEnabled = HttpServerEnabled,
            HttpServerPort = ParseInt(HttpServerPort, current.HttpServerPort),
            TrayIconEnabled = TrayIconEnabled,
            McpDestructiveActionsEnabled = McpDestructiveActionsEnabled,
            TelemetryEnabled = TelemetryEnabled,
            // Preserve Status process pins when editing other preferences.
            PinnedProcessNames = current.PinnedProcessNames.ToList()
        });

        var saved = await _settingsService.SaveAsync(settings).ConfigureAwait(false);
        RunOnUiThread(() =>
        {
            ApplySettings(saved);
            SettingsStatus = "设置已保存，采样、托盘、REST、MCP 与遥测开关立即生效。";
        });
    }

    [RelayCommand]
    public async Task LoadHistoryAsync()
    {
        var entries = await _operationHistoryService.ReadRecentAsync(25).ConfigureAwait(false);
        RunOnUiThread(() =>
        {
            HistoryEntries.Clear();
            foreach (var entry in entries)
            {
                HistoryEntries.Add(entry);
            }

            HistorySummary = entries.Count == 0 ? "未找到历史记录" : $"已载入 {entries.Count} 条最近记录";
        });
    }

    private void ApplySettings(WinMoeSettings settings)
    {
        SamplingIntervalSeconds = settings.SamplingIntervalSeconds.ToString();
        HistoryRetentionDays = settings.HistoryRetentionDays.ToString();
        HttpServerEnabled = settings.HttpServerEnabled;
        HttpServerPort = settings.HttpServerPort.ToString();
        TrayIconEnabled = settings.TrayIconEnabled;
        McpDestructiveActionsEnabled = settings.McpDestructiveActionsEnabled;
        TelemetryEnabled = settings.TelemetryEnabled;
        McpEndpoint = settings.HttpServerEnabled
            ? $"REST + MCP 服务 http://127.0.0.1:{settings.HttpServerPort}"
            : $"REST 已关闭；本地 MCP 桥仍在 http://127.0.0.1:{settings.HttpServerPort}/mcp";
    }

    private static int ParseInt(string value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
