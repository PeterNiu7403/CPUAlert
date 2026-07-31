using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinMoe.Ui;
using WinMoe.ViewModels;

namespace WinMoe.Pages;

public sealed partial class DashboardPage : Page
{
    private readonly DispatcherTimer _refreshTimer = new();

    public DashboardPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<DashboardViewModel>();
        DataContext = ViewModel;
        _refreshTimer.Interval = TimeSpan.FromSeconds(15);
        _refreshTimer.Tick += RefreshTimer_Tick;
    }

    public DashboardViewModel ViewModel { get; }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        WinMoeButtonVisualState.FreezeTree(this);

        if (!ViewModel.IsBusy)
        {
            await ViewModel.RefreshAsync();
        }

        _refreshTimer.Start();
    }

    private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
    }

    private async void RefreshTimer_Tick(object? sender, object e)
    {
        if (!ViewModel.IsBusy)
        {
            await ViewModel.RefreshAsync();
        }
    }

    private void ProcessMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not ProcessRowViewModel row)
        {
            return;
        }

        var process = row.Process;

        var flyout = new MenuFlyout();
        var pinItem = new MenuFlyoutItem
        {
            Text = process.IsPinned ? "取消固定" : "固定到顶部"
        };
        pinItem.Click += async (_, _) =>
        {
            var message = await ViewModel.TogglePinProcessAsync(process);
            await ShowToastAsync(message);
            if (!ViewModel.IsBusy)
            {
                await ViewModel.RefreshAsync();
            }
        };

        var copyItem = new MenuFlyoutItem { Text = "复制可执行路径" };
        copyItem.Click += async (_, _) =>
        {
            var message = await ViewModel.CopyProcessPathAsync(process);
            await ShowToastAsync(message);
        };
        var killItem = new MenuFlyoutItem { Text = "结束进程" };
        killItem.Click += async (_, _) =>
        {
            var confirm = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "结束进程",
                Content = $"确定结束 {process.Name} (PID {process.ProcessId})？系统关键进程可能无法结束或会立刻重启。",
                PrimaryButtonText = "结束",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            var message = await ViewModel.TerminateProcessAsync(process);
            await ShowToastAsync(message);
            if (!ViewModel.IsBusy)
            {
                await ViewModel.RefreshAsync();
            }
        };

        flyout.Items.Add(pinItem);
        flyout.Items.Add(copyItem);
        flyout.Items.Add(killItem);
        flyout.ShowAt(button);
    }

    private async Task ShowToastAsync(string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "进程",
            Content = message,
            CloseButtonText = "好的",
            DefaultButton = ContentDialogButton.Close
        };
        await dialog.ShowAsync();
    }
}
