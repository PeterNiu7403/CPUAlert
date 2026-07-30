using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoleWindows.Services;
using MoleWindows.Ui;
using MoleWindows.ViewModels;

namespace MoleWindows.Views;

public sealed partial class ShellPage : Page
{
    private readonly INavigationService _navigationService;

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

        MoleWindowsButtonVisualState.FreezeTree(this);
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
            MoleWindowsButtonVisualState.FreezeTree(content);
        }
    }

    private void UpdateSelectedRoute(string route)
    {
        MoleWindowsButtonVisualState.Freeze(BrandButton);

        foreach (var button in GetRouteButtons())
        {
            var buttonRoute = button.Tag as string;
            var isSelected = string.Equals(buttonRoute, route, StringComparison.OrdinalIgnoreCase);
            button.Style = (Style)Application.Current.Resources[isSelected ? "MoleWindowsTopNavButtonSelectedStyle" : "MoleWindowsTopNavButtonStyle"];
            MoleWindowsButtonVisualState.ApplyNavigationState(button, isSelected);
        }

        foreach (var button in GetUtilityButtons())
        {
            var buttonRoute = button.Tag as string;
            var isSelected = string.Equals(buttonRoute, route, StringComparison.OrdinalIgnoreCase);
            button.Style = (Style)Application.Current.Resources[isSelected ? "MoleWindowsIconButtonSelectedStyle" : "MoleWindowsIconButtonStyle"];
            MoleWindowsButtonVisualState.ApplyNavigationState(button, isSelected);
        }
    }

    private IEnumerable<Button> GetRouteButtons()
    {
        yield return CleanNavButton;
        yield return OptimizeNavButton;
        yield return AppsNavButton;
        yield return AnalyzeNavButton;
        yield return StatusNavButton;
    }

    private IEnumerable<Button> GetUtilityButtons()
    {
        yield return SettingsButton;
    }
}
