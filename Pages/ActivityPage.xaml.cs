using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MoleWindows.ViewModels;

namespace MoleWindows.Pages;

public sealed partial class ActivityPage : Page
{
    public ActivityPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<ActivityViewModel>();
        DataContext = ViewModel;
    }

    public ActivityViewModel ViewModel { get; }

    private async void ActivityPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
    }
}
