using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MoleWindows.ViewModels;

public partial class OptimizeViewModel : ViewModelBase
{
    private const string PendingFeatureMessage = "优化引擎正在接入可审阅、可取消的计划协议";

    public OptimizeViewModel()
    {
        Summary = PendingFeatureMessage;
    }

    public ObservableCollection<string> OutputLines { get; } = new();

    [ObservableProperty]
    private string summary = PendingFeatureMessage;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool canOptimize;

    [ObservableProperty]
    private bool canPreview;

    [ObservableProperty]
    private string pendingMessage = PendingFeatureMessage;

    public string OutputText => string.Join(Environment.NewLine, OutputLines);

    [RelayCommand]
    public async Task PreviewAsync()
    {
        await ShowPendingAsync();
    }

    [RelayCommand]
    public async Task OptimizeAsync()
    {
        await ShowPendingAsync();
    }

    private Task ShowPendingAsync()
    {
        IsBusy = false;
        CanOptimize = false;
        CanPreview = false;
        OutputLines.Clear();
        OutputLines.Add(PendingFeatureMessage);
        OutputLines.Add("当前版本只保留预览路径；在引擎具备非交互计划与事件协议前，GUI 执行保持禁用。");
        Summary = PendingFeatureMessage;
        OnPropertyChanged(nameof(OutputText));
        return Task.CompletedTask;
    }

}
