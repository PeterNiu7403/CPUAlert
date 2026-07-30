using Microsoft.UI.Xaml.Controls;
using MoleWindows.ViewModels;

namespace MoleWindows.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<SettingsViewModel>();
        DataContext = ViewModel;
    }

    public SettingsViewModel ViewModel { get; }
}
