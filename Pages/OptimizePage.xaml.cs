using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinMoe.Services;
using WinMoe.Ui;
using WinMoe.ViewModels;

namespace WinMoe.Pages;

public sealed partial class OptimizePage : Page
{
    private bool _autoPreviewStarted;
    private readonly IStartupDiagnosticsService _diagnostics;

    public OptimizePage()
    {
        InitializeComponent();
        ViewModel = App.GetService<OptimizeViewModel>();
        _diagnostics = App.GetService<IStartupDiagnosticsService>();
        DataContext = ViewModel;
    }

    public OptimizeViewModel ViewModel { get; }

    private async void OptimizePage_Loaded(object sender, RoutedEventArgs e)
    {
        PlanetMotion.StartSlowSpin(PlanetVisual, secondsPerRevolution: 100);

        if (_autoPreviewStarted)
        {
            return;
        }

        var autoPreview = Environment.GetEnvironmentVariable("WINMOE_OPTIMIZE_AUTOSCAN")
                          ?? Environment.GetEnvironmentVariable("MOLEWINDOWS_OPTIMIZE_AUTOSCAN");
        if (!string.Equals(autoPreview, "1", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(autoPreview, "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _autoPreviewStarted = true;
        _diagnostics.Record("optimize", "Starting optimize auto-preview.");
        await ViewModel.PreviewAsync();
        _diagnostics.Record("optimize", $"Optimize auto-preview finished: {ViewModel.Summary}");
    }

    private async void OptimizeButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "执行优化",
            Content = "优化只会在计划可审阅、权限明确且用户确认后开放。",
            PrimaryButtonText = "执行",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.OptimizeAsync();
        }
    }
}
