using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using WinMoe.Ui;
using WinMoe.ViewModels;
using System.ComponentModel;

namespace WinMoe.Pages;

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
        WinMoeButtonVisualState.FreezeTree(this);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UninstallViewModel.AppsTab))
        {
            UpdateAppsSurface();
        }

        if (e.PropertyName is nameof(UninstallViewModel.SortKey)
            or nameof(UninstallViewModel.SortDescending)
            or nameof(UninstallViewModel.NameSortLabel))
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
        var selected = ViewModel.Applications.Where(row => row.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            return;
        }

        // Prefer explicit row selection as the uninstall target (Mole: Remove N).
        if (selected.Length == 1)
        {
            ViewModel.SelectedApplicationRow = selected[0];
        }
        else if (ViewModel.SelectedApplication is null ||
                 selected.All(row => row.Application.Id != ViewModel.SelectedApplication.Id))
        {
            ViewModel.SelectedApplicationRow = selected[0];
        }

        var names = string.Join("、", selected.Take(3).Select(row => row.Name));
        if (selected.Length > 3)
        {
            names += $" 等 {selected.Length} 个";
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = selected.Length == 1 ? "启动卸载程序" : $"移除 {selected.Length} 个软件",
            Content = selected.Length == 1
                ? $"即将启动 {names} 注册的 Windows 卸载程序。请先完成厂商卸载流程，再预览残留。"
                : $"将依次引导卸载：{names}。当前版本每次启动一个厂商卸载程序，并保持可恢复策略。",
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
            Title = "没有可用更新",
            Content = "WinMoe 不会从未知通道静默安装软件更新。当前仅展示只读空态；未来只会接入可审计的安全更新源。",
            CloseButtonText = "知道了",
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync();
    }

    private void UpdateAppsSurface()
    {
        SetNavSegment(UninstallTabButton, ViewModel.IsUninstallTab);
        SetNavSegment(UpdatesTabButton, ViewModel.IsUpdatesTab);
        SetNavSegment(StartupTabButton, ViewModel.IsStartupTab);

        UninstallContent.Visibility = ViewModel.IsUninstallTab ? Visibility.Visible : Visibility.Collapsed;
        UpdatesContent.Visibility = ViewModel.IsUpdatesTab ? Visibility.Visible : Visibility.Collapsed;
        StartupContent.Visibility = ViewModel.IsStartupTab ? Visibility.Visible : Visibility.Collapsed;

        UpdateSortVisuals();
    }

    private void UpdateSortVisuals()
    {
        SetTextLink(NameSortButton, string.Equals(ViewModel.SortKey, "name", StringComparison.OrdinalIgnoreCase));
        SetTextLink(SizeSortButton, string.Equals(ViewModel.SortKey, "size", StringComparison.OrdinalIgnoreCase));
        SetTextLink(
            SourceSortButton,
            string.Equals(ViewModel.SortKey, "lastused", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ViewModel.SortKey, "source", StringComparison.OrdinalIgnoreCase));
        SetTextLink(InstalledSortButton, string.Equals(ViewModel.SortKey, "installed", StringComparison.OrdinalIgnoreCase));
    }

    private void SetNavSegment(Button button, bool isSelected)
    {
        // Mole apps sub-tabs: selected = warm brown pill (page resources), not the white top-nav pill.
        button.Style = (Style)Resources[isSelected ? "AppsSubTabSelectedStyle" : "AppsSubTabStyle"];
        if (isSelected)
        {
            WinMoeButtonVisualState.Freeze(button);
        }
        else
        {
            WinMoeButtonVisualState.ApplyCapsuleIdleHover(button);
        }
    }

    private static void SetTextLink(Button button, bool isSelected)
    {
        button.Style = (Style)Application.Current.Resources[
            isSelected ? "WinMoeTextLinkActiveStyle" : "WinMoeTextLinkStyle"];

        // Mole: active sort link is quiet orange text, never a white pill.
        // Clear local brushes left by earlier states so the style rules again.
        button.ClearValue(Control.BackgroundProperty);
        button.ClearValue(Control.ForegroundProperty);
        button.ClearValue(Control.BorderBrushProperty);

        if (isSelected)
        {
            WinMoeButtonVisualState.Freeze(button);
        }
        else
        {
            WinMoeButtonVisualState.ApplyCapsuleIdleHover(button);
        }
    }
}

