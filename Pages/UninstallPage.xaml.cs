using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using MoleWindows.Ui;
using MoleWindows.ViewModels;
using System.ComponentModel;

namespace MoleWindows.Pages;

public sealed partial class UninstallPage : Page
{
    private bool _loadStarted;

    public UninstallPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<UninstallViewModel>();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    public UninstallViewModel ViewModel { get; }

    private async void UninstallPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loadStarted)
        {
            return;
        }

        _loadStarted = true;
        await ViewModel.LoadAsync();
        UpdateAppsSurface();
        MoleWindowsButtonVisualState.FreezeTree(this);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UninstallViewModel.AppsTab))
        {
            UpdateAppsSurface();
        }

        if (e.PropertyName is nameof(UninstallViewModel.SortKey))
        {
            UpdateSortVisuals();
        }
    }

    private void AppsTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tab)
        {
            return;
        }

        ViewModel.SelectAppsTabCommand.Execute(tab);
        UpdateAppsSurface();
    }

    private async void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        var appName = ViewModel.SelectedApplication?.Name ?? "所选软件";
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "启动卸载程序",
            Content = $"即将启动 {appName} 注册的 Windows 卸载程序。请先完成厂商卸载流程，再预览残留。",
            PrimaryButtonText = "启动",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.LaunchUninstallerAsync();
        }
    }

    private async void RemoveLeftoversButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "将所选残留移入回收站",
            Content = "系统会阻止受保护目录，并把通过校验的路径移入 Windows 回收站。仍请在确认前逐项检查。",
            PrimaryButtonText = "移入回收站",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.RemoveSelectedLeftoversAsync();
        }
    }

    private async void UnsupportedAppsFeatureButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "功能正在接入",
            Content = "安全的非交互 Windows 软件更新源尚未完成；当前页面不会静默安装或更新软件。",
            CloseButtonText = "知道了",
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync();
    }

    private void UpdateAppsSurface()
    {
        SetSegmentButton(UninstallTabButton, ViewModel.IsUninstallTab);
        SetSegmentButton(UpdatesTabButton, ViewModel.IsUpdatesTab);
        SetSegmentButton(StartupTabButton, ViewModel.IsStartupTab);

        UninstallContent.Visibility = ViewModel.IsUninstallTab ? Visibility.Visible : Visibility.Collapsed;
        UpdatesContent.Visibility = ViewModel.IsUpdatesTab ? Visibility.Visible : Visibility.Collapsed;
        StartupContent.Visibility = ViewModel.IsStartupTab ? Visibility.Visible : Visibility.Collapsed;

        UpdateSortVisuals();
    }

    private void UpdateSortVisuals()
    {
        SetSortButton(NameSortButton, "name");
        SetSortButton(SizeSortButton, "size");
        SetSortButton(SourceSortButton, "source");
    }

    private void SetSortButton(Button button, string key)
    {
        var isSelected = string.Equals(ViewModel.SortKey, key, StringComparison.OrdinalIgnoreCase);
        SetSegmentButton(button, isSelected);
    }

    private static void SetSegmentButton(Button button, bool isSelected)
    {
        button.Style = (Style)Application.Current.Resources[
            isSelected ? "MoleWindowsTopNavButtonSelectedStyle" : "MoleWindowsTopNavButtonStyle"];
        MoleWindowsButtonVisualState.ApplyNavigationState(button, isSelected);
    }
}
