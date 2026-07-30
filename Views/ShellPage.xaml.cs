using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI;
using WinMoe.Services;
using WinMoe.Ui;
using WinMoe.ViewModels;

namespace WinMoe.Views;

public sealed partial class ShellPage : Page
{
    private readonly INavigationService _navigationService;

    // Sampled from mole.fit screenshots — see mole-ui-pixel-audit.md
    private static readonly Color BgClean = Color.FromArgb(255, 14, 27, 54);
    private static readonly Color BgApps = Color.FromArgb(255, 36, 20, 22);
    private static readonly Color BgOptimize = Color.FromArgb(255, 34, 30, 20);
    private static readonly Color BgAnalyze = Color.FromArgb(255, 26, 19, 15);
    private static readonly Color BgStatus = Color.FromArgb(255, 28, 24, 16);
    private static readonly Color BgSettings = Color.FromArgb(255, 22, 22, 24);

    public ShellPage(ShellViewModel viewModel, INavigationService navigationService)
    {
        InitializeComponent();
        ViewModel = viewModel;
        _navigationService = navigationService;
        DataContext = ViewModel;
    }

    public ShellViewModel ViewModel { get; }

    public void InitializeForWindow(Window window)
    {
        window.ExtendsContentIntoTitleBar = true;
        window.SetTitleBar(AppTitleBar);
    }

    private void ShellPage_Loaded(object sender, RoutedEventArgs e)
    {
        _navigationService.Initialize(ContentFrame);
        if (ContentFrame.Content is null)
        {
            ViewModel.NavigateCommand.Execute("status");
            UpdateSelectedRoute("status");
        }

        WinMoeButtonVisualState.FreezeTree(this);
    }

    private void TopNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string route } && !string.IsNullOrWhiteSpace(route))
        {
            UpdateSelectedRoute(route);
            ViewModel.NavigateCommand.Execute(route);
            UpdateSelectedRoute(ViewModel.SelectedRoute);
        }
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        ViewModel.RefreshNavigationState();
        UpdateSelectedRoute(ViewModel.SelectedRoute);
        if (e.Content is DependencyObject content)
        {
            WinMoeButtonVisualState.FreezeTree(content);
        }
    }

    private void UpdateSelectedRoute(string route)
    {
        ApplyRouteChrome(route);
        WinMoeButtonVisualState.Freeze(BrandButton);

        foreach (var button in GetRouteButtons())
        {
            var buttonRoute = button.Tag as string;
            var isSelected = string.Equals(buttonRoute, route, StringComparison.OrdinalIgnoreCase);
            button.Style = (Style)Application.Current.Resources[isSelected ? "WinMoeTopNavButtonSelectedStyle" : "WinMoeTopNavButtonStyle"];
            WinMoeButtonVisualState.ApplyNavigationState(button, isSelected);
        }

        var settingsSelected = string.Equals(route, "settings", StringComparison.OrdinalIgnoreCase);
        SettingsButton.Style = (Style)Application.Current.Resources[settingsSelected ? "WinMoeIconButtonSelectedStyle" : "WinMoeIconButtonStyle"];
        SettingsButton.Opacity = settingsSelected ? 1.0 : 0.35;
        WinMoeButtonVisualState.ApplyNavigationState(SettingsButton, settingsSelected);
    }

    private void ApplyRouteChrome(string route)
    {
        var color = route?.ToLowerInvariant() switch
        {
            "clean" => BgClean,
            "apps" => BgApps,
            "optimize" => BgOptimize,
            "analyze" => BgAnalyze,
            "settings" => BgSettings,
            _ => BgStatus
        };

        var brush = new SolidColorBrush(color);
        Background = brush;
        ShellRoot.Background = brush;
    }

    private IEnumerable<Button> GetRouteButtons()
    {
        yield return CleanNavButton;
        yield return AppsNavButton;
        yield return OptimizeNavButton;
        yield return AnalyzeNavButton;
        yield return StatusNavButton;
    }
}
