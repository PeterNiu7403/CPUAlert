using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoleWindows.Models;

namespace MoleWindows.ViewModels;

public partial class CleanupViewModel : ViewModelBase
{
    private const string PendingFeatureMessage = "清理引擎正在接入安全的非交互计划协议";

    public CleanupViewModel()
    {
        Summary = PendingFeatureMessage;
    }

    public ObservableCollection<CleanupPreviewItem> PreviewItems { get; } = new();

    public ObservableCollection<string> OutputLines { get; } = new();

    [ObservableProperty]
    private string summary = PendingFeatureMessage;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool canClean;

    [ObservableProperty]
    private bool canPreview;

    [ObservableProperty]
    private string pendingMessage = PendingFeatureMessage;

    public string OutputText => string.Join(Environment.NewLine, OutputLines);

    [RelayCommand]
    public async Task ScanAsync()
    {
        await ShowPendingAsync();
    }

    [RelayCommand]
    public async Task CleanAsync()
    {
        await ShowPendingAsync();
    }

    private Task ShowPendingAsync()
    {
        IsBusy = false;
        CanClean = false;
        CanPreview = false;
        PreviewItems.Clear();
        OutputLines.Clear();
        OutputLines.Add(PendingFeatureMessage);
        OutputLines.Add("当前 Mole Windows 引擎尚未提供稳定的 GUI 非交互清理计划，真实执行保持禁用。");
        Summary = PendingFeatureMessage;
        OnPropertyChanged(nameof(OutputText));
        return Task.CompletedTask;
    }

}
