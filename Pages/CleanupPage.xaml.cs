using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinMoe.Services;
using WinMoe.Ui;
using WinMoe.ViewModels;

namespace WinMoe.Pages;

public sealed partial class CleanupPage : Page
{
    private bool _autoScanStarted;
    private readonly IStartupDiagnosticsService _diagnostics;

    public CleanupPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<CleanupViewModel>();
        _diagnostics = App.GetService<IStartupDiagnosticsService>();
        DataContext = ViewModel;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        UpdateActionButtons();
    }

    public CleanupViewModel ViewModel { get; }

    private async void CleanupPage_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateActionButtons();
        PlanetMotion.StartSlowSpin(PlanetVisual, secondsPerRevolution: 120);

        if (_autoScanStarted)
        {
            return;
        }

        var autoScan = Environment.GetEnvironmentVariable("WINMOE_CLEAN_AUTOSCAN")
                       ?? Environment.GetEnvironmentVariable("MOLEWINDOWS_CLEAN_AUTOSCAN");
        if (!string.Equals(autoScan, "1", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(autoScan, "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _autoScanStarted = true;
        _diagnostics.Record("clean", "Starting clean autoscan.");
        await ViewModel.ScanAsync();
        _diagnostics.Record("clean", $"Clean autoscan finished: {ViewModel.Summary}");
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CleanupViewModel.SurfaceState)
            or nameof(CleanupViewModel.PrimaryActionVisibility))
        {
            UpdateActionButtons();
        }
    }

    private void UpdateActionButtons()
    {
        var complete = ViewModel.SurfaceState == CleanupSurfaceState.Complete;
        var scanning = ViewModel.SurfaceState == CleanupSurfaceState.Scanning;

        ScanButton.Visibility = complete || scanning ? Visibility.Collapsed : Visibility.Visible;
        ReturnButton.Visibility = complete ? Visibility.Visible : Visibility.Collapsed;

        if (complete)
        {
            ReturnButton.Content = "返回地球";
        }
    }

    private void CategoryExpand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: CleanupCategoryGroupViewModel group })
        {
            group.ToggleExpanded();
        }
    }

    private void CategoryCheck_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: CleanupCategoryGroupViewModel group })
        {
            return;
        }

        // After click, IsChecked reflects the intermediate UI state; invert from prior group model.
        var selectAll = group.IsGroupSelected != true;
        group.ApplyGroupSelection(selectAll);
        ViewModel.RefreshSelectionFromUi();
    }

    private async void CleanButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "确认清理",
            Content = "所选目标将经过计划指纹校验，并默认移入 Windows 回收站。系统目录、盘符根与重解析点会被拒绝。是否继续？",
            PrimaryButtonText = "移入回收站",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.CleanAsync();
            UpdateActionButtons();
        }
    }
}

