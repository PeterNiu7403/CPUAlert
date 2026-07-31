using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI;
using WinMoe.Models;
using WinMoe.Services;

namespace WinMoe.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private const int CpuBarCount = 34;
    private const int NetworkPointCount = 48;
    private const double ChartWidth = 100;
    private const double ChartHeight = 48;
    private const double ChartPadding = 4;
    private const double NetworkChartWidth = 200;
    private const double NetworkChartHeight = 48;
    private const double NetworkChartTopPadding = 6;
    private const double NetworkChartBottomPadding = 6;

    // Badge colors mirror WinMoeTheme tokens (green/gold/red, muted when no sensor reading).
    private static readonly SolidColorBrush TemperatureLowBrush = new(Color.FromArgb(0xFF, 0x5B, 0xD4, 0x8E));
    private static readonly SolidColorBrush TemperatureMidBrush = new(Color.FromArgb(0xFF, 0xE0, 0xB0, 0x3C));
    private static readonly SolidColorBrush TemperatureHighBrush = new(Color.FromArgb(0xFF, 0xF0, 0x60, 0x4E));
    private static readonly SolidColorBrush TemperatureUnknownBrush = new(Color.FromArgb(0xFF, 0xA3, 0x9C, 0x92));
    private readonly IMoleEngineService _moleEngineService;
    private readonly ISystemTelemetrySamplerService _telemetrySamplerService;
    private readonly ISystemTelemetryHistoryService _systemTelemetryHistoryService;
    private readonly IOperationHistoryService _operationHistoryService;
    private readonly IApplicationSettingsService _settingsService;
    private readonly HashSet<string> _pinnedProcessNames;
    private readonly object _pinSync = new();

    public DashboardViewModel(
        IMoleEngineService moleEngineService,
        ISystemTelemetrySamplerService telemetrySamplerService,
        ISystemTelemetryHistoryService systemTelemetryHistoryService,
        IOperationHistoryService operationHistoryService,
        IApplicationSettingsService settingsService)
    {
        _moleEngineService = moleEngineService;
        _telemetrySamplerService = telemetrySamplerService;
        _systemTelemetryHistoryService = systemTelemetryHistoryService;
        _operationHistoryService = operationHistoryService;
        _settingsService = settingsService;
        _pinnedProcessNames = new HashSet<string>(
            WinMoeSettings.NormalizePinnedProcessNames(_settingsService.Current.PinnedProcessNames),
            StringComparer.OrdinalIgnoreCase);
        TelemetryHistoryPath = _systemTelemetryHistoryService.HistoryFilePath;
        _settingsService.SettingsChanged += (_, settings) =>
        {
            lock (_pinSync)
            {
                _pinnedProcessNames.Clear();
                foreach (var name in WinMoeSettings.NormalizePinnedProcessNames(settings.PinnedProcessNames))
                {
                    _pinnedProcessNames.Add(name);
                }
            }
        };
    }

    public ObservableCollection<string> OutputLines { get; } = new();

    public ObservableCollection<DashboardBarSample> CpuBars { get; } = new();

    public ObservableCollection<ProcessRowViewModel> TopProcesses { get; } = new();

    public ObservableCollection<OperationHistoryEntry> RecentActivity { get; } = new();

    /// <summary>Per-volume partition rows under the aggregate disk card.</summary>
    public ObservableCollection<DiskVolumeTelemetry> DiskVolumes { get; } = new();

    [ObservableProperty]
    private string engineStatus = "尚未检查";

    [ObservableProperty]
    private string enginePath = string.Empty;

    [ObservableProperty]
    private string engineKindText = "Mole";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isEngineAvailable;

    [ObservableProperty]
    private string statusContract = "等待引擎检查";

    [ObservableProperty]
    private double cpuUsagePercent;

    [ObservableProperty]
    private double memoryUsagePercent;

    [ObservableProperty]
    private string memorySummary = "尚未采样";

    [ObservableProperty]
    private double diskUsagePercent;

    [ObservableProperty]
    private string diskSummary = "尚未采样";

    [ObservableProperty]
    private string networkSummary = "尚未采样";

    [ObservableProperty]
    private string gpuStatus = "尚未采样";

    [ObservableProperty]
    private string capturedAt = string.Empty;

    [ObservableProperty]
    private string telemetryHistorySummary = "尚无遥测历史";

    [ObservableProperty]
    private string telemetryHistoryPath = string.Empty;

    [ObservableProperty]
    private string activitySummary = "近期无活动";

    [ObservableProperty]
    private string deviceSummary = "Windows";

    [ObservableProperty]
    private string healthFooter = "已运行 --";

    [ObservableProperty]
    private string cpuCoresBadge = $"{Environment.ProcessorCount} cores";

    [ObservableProperty]
    private string cpuFooter = "尚未采样";

    [ObservableProperty]
    private string memoryStateBadge = "压力 —%";

    [ObservableProperty]
    private string diskTotalBadge = "-";

    [ObservableProperty]
    private string diskFreeAmountText = "-";

    [ObservableProperty]
    private string diskFreeUnitText = "可用";

    [ObservableProperty]
    private string diskFooter = "尚未采样";

    [ObservableProperty]
    private string networkRateText = "-";

    [ObservableProperty]
    private string networkRateValueText = "-";

    [ObservableProperty]
    private string networkRateUnitText = "KB/s";

    [ObservableProperty]
    private string networkFooter = "尚未采样";

    [ObservableProperty]
    private string networkBadge = "-";

    [ObservableProperty]
    private string cpuTemperatureBadge = "—°C";

    [ObservableProperty]
    private SolidColorBrush cpuTemperatureBrush = TemperatureUnknownBrush;

    [ObservableProperty]
    private string gpuTemperatureBadge = "—°C";

    [ObservableProperty]
    private SolidColorBrush gpuTemperatureBrush = TemperatureUnknownBrush;

    [ObservableProperty]
    private string gpuAdapterFooter = "GPU 信息不可用";

    [ObservableProperty]
    private string networkAdapterText = "网络 · 不可用";

    [ObservableProperty]
    private string gpuMetricText = "-";

    [ObservableProperty]
    private string gpuFooter = "Windows GPU engine";

    [ObservableProperty]
    private string batteryMetricText = "-";

    [ObservableProperty]
    private string batteryStateText = "不可用";

    [ObservableProperty]
    private string batteryFooter = string.Empty;

    [ObservableProperty]
    private Visibility batteryFooterVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private string batteryBadge = "Good";

    [ObservableProperty]
    private string batteryHealthBadge = "不可用";

    [ObservableProperty]
    private string batteryPercentText = "-";

    [ObservableProperty]
    private double batteryChargePercent;

    [ObservableProperty]
    private DoubleCollection batteryRingDash = ToDashCollection(CircularProgressGeometry.CreateDash(0, 22));

    [ObservableProperty]
    private string fanMetricText = "-";

    [ObservableProperty]
    private string fanBadge = "0 fans";

    [ObservableProperty]
    private string fanFooter = "Windows 风扇指标不可用";

    [ObservableProperty]
    private HistoryChartSeries fanStatusChart = HistoryChartSeries.Empty("0 RPM", "avg 0 RPM");

    [ObservableProperty]
    private string topProcessesTitle = "NAME (0)";

    [ObservableProperty]
    private HistoryChartSeries memoryStatusChart = HistoryChartSeries.Empty("0%", "avg 0%");

    [ObservableProperty]
    private HistoryChartSeries networkStatusChart = HistoryChartSeries.Empty("0 B/s", "avg 0 B/s");

    [ObservableProperty]
    private HistoryChartSeries networkDownloadChart = HistoryChartSeries.Empty("0 B/s", "avg 0 B/s");

    [ObservableProperty]
    private HistoryChartSeries networkUploadChart = HistoryChartSeries.Empty("0 B/s", "avg 0 B/s");

    [ObservableProperty]
    private HistoryChartSeries gpuStatusChart = HistoryChartSeries.Empty("0%", "avg 0%");

    public string McpSurfaceSummary => $"HTTP 127.0.0.1:{LocalMcpServerService.DefaultPort} | STDIO Assets\\Mcp\\winmoe-mcp-stdio.exe";

    public string CpuUsageText => SystemTelemetryFormatter.Percent(CpuUsagePercent);

    public string MemoryUsageText => SystemTelemetryFormatter.Percent(MemoryUsagePercent);

    public string DiskUsageText => SystemTelemetryFormatter.Percent(DiskUsagePercent);

    public string OutputText => string.Join(Environment.NewLine, OutputLines);

    [ObservableProperty]
    private string healthScore = "100";

    [ObservableProperty]
    private string healthStatusText = "各项指标正常";

    [ObservableProperty]
    private string healthReason = "检查项均通过";

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsBusy = true;
        OutputLines.Clear();
        OnPropertyChanged(nameof(OutputText));

        try
        {
            await RefreshTelemetryAsync();

            var availability = _moleEngineService.GetAvailability();
            IsEngineAvailable = availability.IsAvailable;
            EngineStatus = availability.Message;
            EnginePath = availability.Path ?? string.Empty;
            EngineKindText = availability.IsAvailable ? availability.Kind.ToString() : "Mole missing";

            if (!availability.IsAvailable)
            {
                StatusContract = "Mole is missing";
                AppendOutput(availability.Message);
                return;
            }

            var version = await _moleEngineService.ExecuteCommandAsync("--version", AppendOutput);
            if (version.Succeeded)
            {
                StatusContract = "Mole engine is available; Dashboard uses native polling until WinMoe exposes non-interactive status data";
            }
            else
            {
                StatusContract = "Mole version check failed";
                AppendOutput(version.StandardError);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task<string> TogglePinProcessAsync(ProcessTelemetry? process)
    {
        if (process is null || string.IsNullOrWhiteSpace(process.Name))
        {
            return "无效进程";
        }

        var name = NormalizeProcessName(process.Name);
        bool pinned;
        lock (_pinSync)
        {
            if (_pinnedProcessNames.Contains(name))
            {
                _pinnedProcessNames.Remove(name);
                pinned = false;
            }
            else
            {
                if (_pinnedProcessNames.Count >= WinMoeSettings.MaxPinnedProcessNames)
                {
                    return $"最多固定 {WinMoeSettings.MaxPinnedProcessNames} 个进程";
                }

                _pinnedProcessNames.Add(name);
                pinned = true;
            }
        }

        await PersistPinnedNamesAsync().ConfigureAwait(false);
        return pinned ? $"已固定 {name}" : $"已取消固定 {name}";
    }

    [RelayCommand]
    public Task<string> TerminateProcessAsync(ProcessTelemetry? process)
    {
        if (process is null || process.ProcessId <= 0)
        {
            return Task.FromResult("无效进程");
        }

        try
        {
            using var target = Process.GetProcessById(process.ProcessId);
            var name = target.ProcessName;
            target.Kill(entireProcessTree: false);
            return Task.FromResult($"已结束 {name} ({process.ProcessId})");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return Task.FromResult($"无法结束进程：{ex.Message}");
        }
    }

    [RelayCommand]
    public Task<string> CopyProcessPathAsync(ProcessTelemetry? process)
    {
        if (process is null || process.ProcessId <= 0)
        {
            return Task.FromResult("无效进程");
        }

        try
        {
            using var target = Process.GetProcessById(process.ProcessId);
            string path;
            try
            {
                path = target.MainModule?.FileName ?? string.Empty;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
            {
                path = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                path = $"{process.Name} · PID {process.ProcessId}";
            }

            var package = new DataPackage();
            package.SetText(path);
            Clipboard.SetContent(package);
            return Task.FromResult($"已复制：{path}");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return Task.FromResult($"无法读取路径：{ex.Message}");
        }
    }

    partial void OnCpuUsagePercentChanged(double value)
    {
        OnPropertyChanged(nameof(CpuUsageText));
    }

    partial void OnMemoryUsagePercentChanged(double value)
    {
        OnPropertyChanged(nameof(MemoryUsageText));
    }

    partial void OnDiskUsagePercentChanged(double value)
    {
        OnPropertyChanged(nameof(DiskUsageText));
    }

    private IReadOnlyList<ProcessTelemetry> BuildProcessList(IReadOnlyList<ProcessTelemetry> liveTop)
    {
        string[] pinnedNames;
        lock (_pinSync)
        {
            pinnedNames = _pinnedProcessNames.ToArray();
        }

        var byId = liveTop.ToDictionary(process => process.ProcessId);
        var presentNames = new HashSet<string>(
            liveTop.Select(process => NormalizeProcessName(process.Name)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var pinnedName in pinnedNames)
        {
            if (presentNames.Contains(pinnedName))
            {
                continue;
            }

            try
            {
                foreach (var process in Process.GetProcessesByName(pinnedName))
                {
                    try
                    {
                        if (byId.ContainsKey(process.Id))
                        {
                            continue;
                        }

                        byId[process.Id] = new ProcessTelemetry(
                            process.ProcessName,
                            process.Id,
                            process.WorkingSet64,
                            0,
                            0,
                            IsPinned: true);
                        presentNames.Add(pinnedName);
                        break;
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch
            {
                // Process may have exited between name lookup and inspection.
            }
        }

        return byId.Values
            .Select(process => process with
            {
                IsPinned = pinnedNames.Contains(NormalizeProcessName(process.Name), StringComparer.OrdinalIgnoreCase)
            })
            .OrderByDescending(process => process.IsPinned)
            .ThenByDescending(process => process.CpuUsagePercent)
            .ThenByDescending(process => process.WorkingSetBytes)
            .Take(50)
            .ToArray();
    }

    private async Task PersistPinnedNamesAsync()
    {
        List<string> names;
        lock (_pinSync)
        {
            names = _pinnedProcessNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        var current = _settingsService.Current;
        var next = WinMoeSettings.Normalize(new WinMoeSettings
        {
            SamplingIntervalSeconds = current.SamplingIntervalSeconds,
            HistoryRetentionDays = current.HistoryRetentionDays,
            HttpServerEnabled = current.HttpServerEnabled,
            HttpServerPort = current.HttpServerPort,
            TrayIconEnabled = current.TrayIconEnabled,
            McpDestructiveActionsEnabled = current.McpDestructiveActionsEnabled,
            TelemetryEnabled = current.TelemetryEnabled,
            PinnedProcessNames = names
        });

        try
        {
            await _settingsService.SaveAsync(next).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Keep in-memory pins even if disk write fails this tick.
        }
    }

    private static string NormalizeProcessName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 4)
        {
            return trimmed[..^4];
        }

        return trimmed;
    }

    private async Task RefreshTelemetryAsync()
    {
        var snapshot = await _telemetrySamplerService.SampleNowAsync();

        IReadOnlyList<SystemTelemetrySnapshot> recentSnapshots = [];
        IReadOnlyList<OperationHistoryEntry> recentActivity = [];
        try
        {
            recentSnapshots = await _systemTelemetryHistoryService.ReadRecentAsync(Math.Max(CpuBarCount, NetworkPointCount));
            recentActivity = await _operationHistoryService.ReadRecentAsync(5);
        }
        catch (IOException)
        {
            // History files can be briefly held by an external process; render the fresh
            // snapshot without charts rather than crashing the async void refresh tick.
        }

        // Resolve row icons off the UI thread (thumbnail extraction + disk cache);
        // BitmapImage creation happens inside the UI marshal below.
        var processRows = await Task.Run(async () =>
        {
            var rows = BuildProcessList(snapshot.TopProcesses)
                .Select(process => new ProcessRowViewModel(process))
                .ToArray();
            await Task.WhenAll(rows.Select(async row =>
            {
                var executablePath = ProcessIconLoader.TryGetExecutablePath(row.ProcessId);
                row.IconPngPath = executablePath is null
                    ? null
                    : await ProcessIconLoader.EnsurePngAsync(executablePath);
            }));
            return rows;
        });

        RunOnUiThread(() =>
        {
            CpuUsagePercent = snapshot.CpuUsagePercent;
            MemoryUsagePercent = snapshot.MemoryUsagePercent;
            MemorySummary = SystemTelemetryFormatter.MemorySummary(snapshot);

            // Disk: aggregate every fixed volume (Mole shows whole-disk free/total).
            var aggregateTotal = snapshot.AllDisksTotalBytes > 0 ? snapshot.AllDisksTotalBytes : snapshot.DiskTotalBytes;
            var aggregateFree = snapshot.AllDisksTotalBytes > 0
                ? snapshot.AllDisksFreeBytes
                : Math.Max(0, snapshot.DiskTotalBytes - snapshot.DiskUsedBytes);
            var aggregateUsed = Math.Max(0, aggregateTotal - aggregateFree);
            var aggregatePercent = aggregateTotal > 0 ? aggregateUsed * 100d / aggregateTotal : 0;
            DiskUsagePercent = Math.Clamp(aggregatePercent, 0, 100);
            DiskSummary = $"{SystemTelemetryFormatter.Bytes(aggregateUsed)} / {SystemTelemetryFormatter.Bytes(aggregateTotal)}";
            DiskFooter = $"已用 {SystemTelemetryFormatter.Bytes(aggregateUsed)} · {DiskUsageText}";
            DiskTotalBadge = snapshot.PhysicalDiskCount > 0
                ? $"{snapshot.PhysicalDiskCount} 盘 · {SystemTelemetryFormatter.Bytes(aggregateTotal)}"
                : SystemTelemetryFormatter.Bytes(aggregateTotal);
            var maxDriveTemperature = snapshot.Volumes
                .Select(volume => volume.TemperatureCelsius)
                .Where(temperature => temperature.HasValue)
                .Select(temperature => temperature!.Value)
                .DefaultIfEmpty()
                .Max();
            if (maxDriveTemperature > 0)
            {
                DiskTotalBadge += string.Create(CultureInfo.InvariantCulture, $" · {maxDriveTemperature:0}°C");
            }
            SetDiskFreeText(aggregateFree);

            DiskVolumes.Clear();
            foreach (var volume in snapshot.Volumes)
            {
                DiskVolumes.Add(volume);
            }

            NetworkSummary =
                $"Down {SystemTelemetryFormatter.Rate(snapshot.NetworkReceivedBytesPerSecond)} | Up {SystemTelemetryFormatter.Rate(snapshot.NetworkSentBytesPerSecond)}";
            NetworkRateText = SystemTelemetryFormatter.Rate(snapshot.NetworkReceivedBytesPerSecond + snapshot.NetworkSentBytesPerSecond);
            (NetworkRateValueText, NetworkRateUnitText) = SplitRateText(NetworkRateText);
            NetworkAdapterText = BuildNetworkEndpointText(snapshot);
            NetworkBadge = DeriveNetworkBadge(snapshot.NetworkInterfaceName);
            NetworkFooter =
                $"↑ {SystemTelemetryFormatter.Rate(snapshot.NetworkSentBytesPerSecond)} · {NetworkBadge}";

            CpuTemperatureBadge = FormatTemperatureBadge(snapshot.CpuTemperatureCelsius);
            CpuTemperatureBrush = ResolveTemperatureBrush(snapshot.CpuTemperatureCelsius);
            GpuTemperatureBadge = FormatTemperatureBadge(snapshot.GpuTemperatureCelsius);
            GpuTemperatureBrush = ResolveTemperatureBrush(snapshot.GpuTemperatureCelsius);
            UpdateGpuSurface(snapshot);
            UpdateFanSurface(snapshot);

            GpuStatus = snapshot.GpuStatus;
            CapturedAt = snapshot.CapturedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            var loadAverage = snapshot.CpuUsagePercent / 100 * Environment.ProcessorCount;
            CpuFooter = string.Create(
                CultureInfo.InvariantCulture,
                $"{CpuLoadTier(snapshot.CpuUsagePercent)} · 负载 {loadAverage:0.0} / {Environment.ProcessorCount} 核");
            CpuCoresBadge = $"{Environment.ProcessorCount} cores";
            HealthFooter = BuildUptimeText(snapshot.CapturedAt);
            MemoryStateBadge = string.Create(CultureInfo.InvariantCulture, $"压力 {snapshot.MemoryUsagePercent:0}%");
            SetBatteryText(snapshot);
            (HealthScore, HealthStatusText, HealthReason) = SystemHealthEvaluator.Evaluate(snapshot);

            TopProcesses.Clear();
            foreach (var row in processRows)
            {
                if (row.IconPngPath is { } iconPath)
                {
                    try
                    {
                        row.IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(iconPath));
                        row.HasIcon = true;
                    }
                    catch (Exception ex) when (ex is UriFormatException or IOException or ArgumentException)
                    {
                        row.HasIcon = false;
                    }
                }

                TopProcesses.Add(row);
            }

            TopProcessesTitle = $"名称 ({TopProcesses.Count})";
            // Mole device chip: "M5 Pro · 48 GB" → CPU model + RAM.
            DeviceSummary = $"{CpuModelNameResolver.Get()} · {SystemTelemetryFormatter.Bytes(snapshot.MemoryTotalBytes)}";

            var chartSamples = BuildStatusSamples(recentSnapshots, snapshot);
            RebuildCpuBars(chartSamples);
            MemoryStatusChart = BuildChart(chartSamples, sample => sample.MemoryUsagePercent, 100, SystemTelemetryFormatter.Percent);
            (NetworkDownloadChart, NetworkUploadChart) = BuildNetworkCharts(chartSamples);
            NetworkStatusChart = NetworkDownloadChart;
            GpuStatusChart = BuildChart(chartSamples, sample => ParseGpuPercent(sample.GpuStatus), 100, SystemTelemetryFormatter.Percent);
            FanStatusChart = BuildChart(
                chartSamples,
                sample => sample.Fans.Count == 0 ? 0 : sample.Fans.Max(fan => fan.Rpm),
                null,
                rpm => $"{rpm:0} RPM");

            TelemetryHistorySummary = BuildTelemetryHistorySummary(recentSnapshots);
            ActivitySummary = BuildActivitySummary(recentActivity);

            RecentActivity.Clear();
            foreach (var entry in recentActivity)
            {
                RecentActivity.Add(entry);
            }
        });
    }

    private void SetDiskFreeText(long freeBytes)
    {
        var formatted = SystemTelemetryFormatter.Bytes(Math.Max(0, freeBytes)).Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        DiskFreeAmountText = formatted.Length > 0 ? formatted[0] : "-";
        DiskFreeUnitText = formatted.Length > 1 ? $"{formatted[1]} 可用" : "可用";
    }

    private static string FormatTemperatureBadge(double? celsius)
    {
        return celsius is { } value
            ? string.Create(CultureInfo.InvariantCulture, $"{value:0}°C")
            : "—°C";
    }

    // Mole temperature colors: cool ≤60°C, warm 61–80°C, hot >80°C.
    private static SolidColorBrush ResolveTemperatureBrush(double? celsius)
    {
        if (celsius is not { } value)
        {
            return TemperatureUnknownBrush;
        }

        return value <= 60
            ? TemperatureLowBrush
            : value <= 80
                ? TemperatureMidBrush
                : TemperatureHighBrush;
    }

    private static string CpuLoadTier(double cpuPercent) => cpuPercent switch
    {
        < 30 => "低负载",
        < 70 => "中负载",
        _ => "高负载"
    };

    private void UpdateGpuSurface(SystemTelemetrySnapshot snapshot)
    {
        var discrete = snapshot.GpuAdapters.FirstOrDefault(adapter => adapter.Kind == GpuAdapterKind.Discrete);
        var integrated = snapshot.GpuAdapters.FirstOrDefault(adapter => adapter.Kind == GpuAdapterKind.Integrated);
        var primary = discrete ?? integrated ?? snapshot.GpuAdapters.FirstOrDefault();

        if (primary is null)
        {
            GpuMetricText = string.Equals(snapshot.GpuStatus, "Unavailable", StringComparison.OrdinalIgnoreCase)
                ? "-"
                : snapshot.GpuStatus;
            GpuAdapterFooter = "GPU 引擎计数不可用";
            return;
        }

        GpuMetricText = string.Create(CultureInfo.InvariantCulture, $"{primary.Engine3DPercent:0.0}%");
        GpuAdapterFooter = discrete is not null && integrated is not null
            ? $"独显 {discrete.ShortName}{TemperatureSuffix(discrete)} · 集显 {integrated.Engine3DPercent:0.0}%{TemperatureSuffix(integrated)}"
            : discrete is not null
                ? $"独显 {discrete.ShortName}{TemperatureSuffix(discrete)}"
                : $"集显 {primary.ShortName}{TemperatureSuffix(primary)}";
    }

    private static string TemperatureSuffix(GpuAdapterTelemetry adapter)
    {
        return adapter.TemperatureCelsius is { } celsius
            ? string.Create(CultureInfo.InvariantCulture, $" {celsius:0}°C")
            : string.Empty;
    }

    private void UpdateFanSurface(SystemTelemetrySnapshot snapshot)
    {
        if (snapshot.Fans.Count == 0)
        {
            FanMetricText = "—";
            FanBadge = "系统托管";
            FanFooter = "此设备未暴露风扇转速接口";
            return;
        }

        var maxRpm = snapshot.Fans.Max(fan => fan.Rpm);
        FanMetricText = maxRpm.ToString(CultureInfo.InvariantCulture);
        var detail = string.Join(" · ", snapshot.Fans.Select(fan => $"{fan.Name} {fan.Rpm}"));
        FanFooter = snapshot.FanMaxRpm is { } peak
            ? string.Create(CultureInfo.InvariantCulture, $"{detail} RPM · 峰值 {peak}")
            : $"{detail} RPM";
        FanBadge = snapshot.FanMaxRpm is { } peakRpm && peakRpm > 0
            ? $"负载 {(int)Math.Round(maxRpm * 100d / peakRpm)}%"
            : $"{snapshot.Fans.Count} fans";
    }

    private static string DeriveNetworkBadge(string interfaceName)
    {
        if (string.IsNullOrWhiteSpace(interfaceName) ||
            string.Equals(interfaceName, "network", StringComparison.OrdinalIgnoreCase))
        {
            return "-";
        }

        var name = interfaceName.Trim();
        if (name.Contains("wi-fi", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("wlan", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("wireless", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("无线", StringComparison.Ordinal))
        {
            return "Wi-Fi";
        }

        if (name.Contains("ethernet", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("以太", StringComparison.Ordinal))
        {
            return "以太网";
        }

        if (name.Contains("bluetooth", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("蓝牙", StringComparison.Ordinal))
        {
            return "蓝牙";
        }

        return name;
    }

    private void SetBatteryText(SystemTelemetrySnapshot snapshot)
    {
        if (!snapshot.HasBattery || !snapshot.BatteryChargePercent.HasValue)
        {
            BatteryMetricText = "-";
            BatteryStateText = "不可用";
            BatteryFooter = string.Empty;
            BatteryFooterVisibility = Visibility.Collapsed;
            BatteryBadge = "正常";
            BatteryHealthBadge = "不可用";
            BatteryPercentText = "-";
            BatteryChargePercent = 0;
            BatteryRingDash = ToDashCollection(CircularProgressGeometry.CreateDash(0, 22));
            return;
        }

        var percent = Math.Clamp(snapshot.BatteryChargePercent.Value, 0, 100);
        BatteryChargePercent = percent;
        BatteryMetricText = percent.ToString("0", CultureInfo.InvariantCulture);
        BatteryStateText = LocalizeBatteryStatus(snapshot.BatteryStatusText);
        BatteryFooter = BuildBatteryFooter(snapshot);
        BatteryFooterVisibility = BatteryFooter.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        var badge = BuildBatteryBadge(snapshot);
        BatteryBadge = badge;
        BatteryHealthBadge = badge;
        BatteryPercentText = string.Create(CultureInfo.InvariantCulture, $"{percent:0}%");
        BatteryRingDash = ToDashCollection(CircularProgressGeometry.CreateDash(percent, 22));
    }

    private static DoubleCollection ToDashCollection(CircularProgressGeometry.Dash dash)
        => new() { dash.Filled, dash.Gap };

    private static string BuildBatteryFooter(SystemTelemetrySnapshot snapshot)
    {
        // The status already sits next to the big percentage; the footer carries the
        // Mole-style segments: live power, cycle count, then the discharge estimate.
        return BatteryDetailFormatter.BuildFooterText(
            snapshot.BatteryRateMw,
            snapshot.BatteryCycleCount,
            LocalizeBatteryStatus(snapshot.BatteryStatusText),
            BuildBatteryRemainingText(snapshot));
    }

    private static string BuildBatteryRemainingText(SystemTelemetrySnapshot snapshot)
    {
        if (snapshot.BatteryEstimatedSecondsRemaining is > 0 &&
            string.Equals(snapshot.BatteryStatusText, "discharging", StringComparison.OrdinalIgnoreCase))
        {
            return $"预计剩余 {FormatBatteryDuration(snapshot.BatteryEstimatedSecondsRemaining.Value)}";
        }

        return string.Empty;
    }

    private static string BuildBatteryBadge(SystemTelemetrySnapshot snapshot)
    {
        // A critically low pack keeps the urgency badge; otherwise surface the
        // design-capacity health percent when the probe delivered the capacities.
        if (string.Equals(snapshot.BatteryHealthText, "Critical", StringComparison.OrdinalIgnoreCase))
        {
            return LocalizeBatteryHealth(snapshot.BatteryHealthText);
        }

        var healthPercent = BatteryDetailFormatter.ComputeHealthPercent(
            snapshot.BatteryDesignCapacityMwh,
            snapshot.BatteryFullChargeCapacityMwh);
        return BatteryDetailFormatter.BuildBadgeText(healthPercent, snapshot.HasBattery);
    }

    private static string FormatBatteryDuration(int seconds)
    {
        var duration = TimeSpan.FromSeconds(seconds);
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours} 小时";
        }

        return $"{Math.Max(1, duration.Minutes)} 分钟";
    }

    private static string LocalizeBatteryStatus(string status) => status.ToLowerInvariant() switch
    {
        "charging" => "充电中",
        "plugged in" => "已接电源",
        "discharging" => "放电中",
        "unknown" => "未知",
        _ => string.IsNullOrWhiteSpace(status) ? "电池" : status
    };

    private static string LocalizeBatteryHealth(string health) => health.ToLowerInvariant() switch
    {
        "good" => "健康",
        "low" => "电量低",
        "critical" => "严重",
        _ => string.IsNullOrWhiteSpace(health) ? "—" : health
    };

    private void RebuildCpuBars(IReadOnlyList<SystemTelemetrySnapshot> samples)
    {
        var values = BuildPaddedValues(samples, sample => sample.CpuUsagePercent, CpuBarCount);
        CpuBars.Clear();
        foreach (var value in values)
        {
            CpuBars.Add(new DashboardBarSample(10 + (Math.Clamp(value, 0, 100) / 100 * 48)));
        }
    }

    private static IReadOnlyList<SystemTelemetrySnapshot> BuildStatusSamples(
        IReadOnlyList<SystemTelemetrySnapshot> recentSnapshots,
        SystemTelemetrySnapshot latestSnapshot)
    {
        return recentSnapshots
            .Append(latestSnapshot)
            .GroupBy(sample => sample.CapturedAt)
            .Select(group => group.First())
            .OrderBy(sample => sample.CapturedAt)
            .TakeLast(Math.Max(CpuBarCount, NetworkPointCount))
            .ToArray();
    }

    private static HistoryChartSeries BuildChart(
        IReadOnlyList<SystemTelemetrySnapshot> samples,
        Func<SystemTelemetrySnapshot, double> selector,
        double? fixedMaximum,
        Func<double, string> formatter,
        int pointCount = 12)
    {
        var values = BuildPaddedValues(samples, selector, pointCount);
        var maximum = fixedMaximum ?? Math.Max(1, values.Max() * 1.15);
        return BuildChartFromValues(values, maximum, formatter);
    }

    private static (HistoryChartSeries Download, HistoryChartSeries Upload) BuildNetworkCharts(IReadOnlyList<SystemTelemetrySnapshot> samples)
    {
        var downloadValues = BuildPaddedValues(
            samples,
            sample => sample.NetworkReceivedBytesPerSecond,
            NetworkPointCount);
        var uploadValues = BuildPaddedValues(
            samples,
            sample => sample.NetworkSentBytesPerSecond,
            NetworkPointCount);
        var maximum = Math.Max(downloadValues.Max(), uploadValues.Max());
        maximum = Math.Max(1, maximum * 1.85);

        return (
            BuildChartFromValues(
                downloadValues,
                maximum,
                SystemTelemetryFormatter.Rate,
                NetworkChartWidth,
                NetworkChartHeight,
                NetworkChartTopPadding,
                NetworkChartBottomPadding),
            BuildChartFromValues(
                uploadValues,
                maximum,
                SystemTelemetryFormatter.Rate,
                NetworkChartWidth,
                NetworkChartHeight,
                NetworkChartTopPadding,
                NetworkChartBottomPadding));
    }

    private static HistoryChartSeries BuildChartFromValues(
        IReadOnlyList<double> values,
        double maximum,
        Func<double, string> formatter,
        double chartWidth = ChartWidth,
        double chartHeight = ChartHeight,
        double chartTopPadding = ChartPadding,
        double chartBottomPadding = ChartPadding)
    {
        if (values.Count == 0)
        {
            return HistoryChartSeries.Empty(formatter(0), $"avg {formatter(0)}");
        }

        if (maximum <= 0)
        {
            maximum = 1;
        }

        var xStep = values.Count == 1 ? 0 : chartWidth / (values.Count - 1);
        var usableHeight = Math.Max(1, chartHeight - chartTopPadding - chartBottomPadding);
        var points = new PointCollection();
        for (var index = 0; index < values.Count; index++)
        {
            var value = Math.Clamp(values[index], 0, maximum);
            var x = values.Count == 1 ? chartWidth / 2 : index * xStep;
            var y = chartHeight - chartBottomPadding - (value / maximum * usableHeight);
            points.Add(new Point(x, y));
        }

        // Mole gradient area: same silhouette closed along the chart baseline.
        var areaPoints = new PointCollection();
        foreach (var point in points)
        {
            areaPoints.Add(point);
        }

        areaPoints.Add(new Point(points[^1].X, chartHeight));
        areaPoints.Add(new Point(0, chartHeight));

        return new HistoryChartSeries(formatter(values[^1]), $"avg {formatter(values.Average())}", points, areaPoints);
    }

    private static string BuildNetworkEndpointText(SystemTelemetrySnapshot snapshot)
    {
        var interfaceName = string.IsNullOrWhiteSpace(snapshot.NetworkInterfaceName)
            ? "网络"
            : snapshot.NetworkInterfaceName.Trim();
        var address = string.IsNullOrWhiteSpace(snapshot.NetworkIPv4Address)
            ? "不可用"
            : snapshot.NetworkIPv4Address.Trim();

        return $"{interfaceName} · {address}";
    }

    private static (string Value, string Unit) SplitRateText(string rateText)
    {
        var parts = rateText.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return ("-", "KB/s");
        }

        return parts.Length == 1 ? (parts[0], string.Empty) : (parts[0], parts[1]);
    }

    private static IReadOnlyList<double> BuildPaddedValues(
        IReadOnlyList<SystemTelemetrySnapshot> samples,
        Func<SystemTelemetrySnapshot, double> selector,
        int count)
    {
        var values = samples
            .OrderBy(sample => sample.CapturedAt)
            .TakeLast(count)
            .Select(sample => Math.Max(0, selector(sample)))
            .ToList();

        if (values.Count == 0)
        {
            return Enumerable.Repeat(0d, count).ToArray();
        }

        while (values.Count < count)
        {
            values.Insert(0, values[0]);
        }

        return values;
    }

    private static double ParseGpuPercent(string gpuStatus)
    {
        var numeric = new string(gpuStatus.Where(character => char.IsDigit(character) || character == '.').ToArray());
        return double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static string BuildUptimeText(DateTimeOffset capturedAt)
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var since = capturedAt - uptime;
        var sinceText = string.Create(CultureInfo.InvariantCulture, $"自 {since.Month}月{since.Day}日");
        return uptime.TotalDays >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"已运行 {(int)uptime.TotalDays}d {uptime.Hours}h · {sinceText}")
            : string.Create(CultureInfo.InvariantCulture, $"已运行 {uptime.Hours} 小时 {uptime.Minutes} 分 · {sinceText}");
    }

    private static string BuildTelemetryHistorySummary(IReadOnlyList<SystemTelemetrySnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return "No telemetry history recorded yet";
        }

        var averageCpu = snapshots.Average(snapshot => snapshot.CpuUsagePercent);
        var averageMemory = snapshots.Average(snapshot => snapshot.MemoryUsagePercent);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{snapshots.Count} recent samples | avg CPU {averageCpu:0.0}% | avg memory {averageMemory:0.0}%");
    }

    private static string BuildActivitySummary(IReadOnlyList<OperationHistoryEntry> entries)
    {
        if (entries.Count == 0)
        {
            return "No recent activity";
        }

        var succeeded = entries.Count(entry => entry.Succeeded);
        return $"{entries.Count} recent operations | {succeeded} succeeded";
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
