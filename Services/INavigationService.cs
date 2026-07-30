using Microsoft.UI.Xaml.Controls;

namespace WinMoe.Services;

public interface INavigationService
{
    bool CanGoBack { get; }

    void Initialize(Frame frame);

    bool NavigateTo(string route);

    bool GoBack();
}
