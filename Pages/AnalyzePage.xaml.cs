using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinMoe.Services;
using WinMoe.Ui;
using WinMoe.ViewModels;

namespace WinMoe.Pages;

public sealed partial class AnalyzePage : Page
{
    private const double ShellChromeHeight = 56;
    private const double AnalyzeHeaderHeight = 52;
    private bool _autoScanStarted;
    private readonly IStartupDiagnosticsService _diagnostics;

    public AnalyzePage()
    {
        InitializeComponent();
        ViewModel = App.GetService<AnalyzeViewModel>();
        _diagnostics = App.GetService<IStartupDiagnosticsService>();
        DataContext = ViewModel;
    }

    public AnalyzeViewModel ViewModel { get; }

    private async void AnalyzePage_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateAnalyzeRootSize(ActualWidth, ActualHeight);
        PlanetMotion.StartSlowSpin(PlanetVisual, secondsPerRevolution: 140);

        if (_autoScanStarted || ViewModel.HasScanResult || ViewModel.IsBusy)
        {
            return;
        }

        // Mole opens Analyze with content; auto-scan user profile once per page instance.
        _autoScanStarted = true;
        _diagnostics.Record("analyze", "Starting analyze default scan.");
        UpdateTreemapViewport();
        await ViewModel.ScanAsync();
        UpdateTreemapViewport();
        _diagnostics.Record("analyze", $"Analyze scan finished: {ViewModel.Summary}");
    }

    private void AnalyzePage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateAnalyzeRootSize(e.NewSize.Width, e.NewSize.Height);
    }

    private void TreemapViewport_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateTreemapViewport();
    }

    private void TreemapViewport_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTreemapViewport();
    }

    private async void SidebarList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AnalyzeSidebarItemViewModel item)
        {
            await ViewModel.DrillIntoPathAsync(item.Path);
            UpdateTreemapViewport();
        }
    }

    private async void GoUpButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.GoUpAsync();
        UpdateTreemapViewport();
    }

    private async void TreemapTile_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string path } && Directory.Exists(path))
        {
            e.Handled = true;
            await ViewModel.DrillIntoPathAsync(path);
            UpdateTreemapViewport();
        }
    }

    private void AnalyzeTarget_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string path || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        e.Handled = true;
        ShowPathFlyout(element, path);
    }

    private void ShowPathFlyout(FrameworkElement target, string path)
    {
        var flyout = new MenuFlyout();

        var open = new MenuFlyoutItem { Text = "在资源管理器中打开" };
        open.Click += async (_, _) => await ViewModel.OpenPathAsync(path);
        flyout.Items.Add(open);

        if (Directory.Exists(path))
        {
            var drill = new MenuFlyoutItem { Text = "分析此文件夹" };
            drill.Click += async (_, _) =>
            {
                await ViewModel.DrillIntoPathAsync(path);
                UpdateTreemapViewport();
            };
            flyout.Items.Add(drill);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());

        var trash = new MenuFlyoutItem { Text = "移入回收站" };
        trash.IsEnabled = ShellPathActions.CanSendToRecycleBin(path);
        trash.Click += async (_, _) =>
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "移入回收站",
                Content = $"将把以下路径移入 Windows 回收站（受保护系统目录会被拒绝）：\n\n{path}",
                PrimaryButtonText = "移入回收站",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await ViewModel.SendToRecycleBinAsync(path);
                UpdateTreemapViewport();
            }
        };
        flyout.Items.Add(trash);

        flyout.ShowAt(target, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions
        {
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom
        });
    }

    private void UpdateTreemapViewport()
    {
        var padding = TreemapViewport.Padding;
        var width = Math.Max(0, TreemapViewport.ActualWidth - padding.Left - padding.Right);
        var height = Math.Max(0, TreemapViewport.ActualHeight - padding.Top - padding.Bottom);
        ViewModel.UpdateTreemapViewport(width, height);
    }

    private void UpdateAnalyzeRootSize(double width, double height)
    {
        if (Parent is FrameworkElement parent)
        {
            width = Math.Max(width, parent.ActualWidth);
            height = Math.Max(height, parent.ActualHeight);
        }

        if (XamlRoot is not null)
        {
            width = Math.Max(width, XamlRoot.Size.Width);
            height = Math.Max(height, XamlRoot.Size.Height - ShellChromeHeight);
        }

        if (width > 0)
        {
            AnalyzeRoot.Width = width;
        }

        if (height > 0)
        {
            AnalyzeRoot.Height = height;
            TreemapViewport.Height = Math.Max(0, height - AnalyzeHeaderHeight);
        }

        UpdateTreemapViewport();
        DispatcherQueue.TryEnqueue(UpdateTreemapViewport);
    }
}
