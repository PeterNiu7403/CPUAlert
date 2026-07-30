using Microsoft.UI.Xaml.Controls;
using WinMoe.ViewModels;

namespace WinMoe.Pages;

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
