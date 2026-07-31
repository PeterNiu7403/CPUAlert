using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using WinRT.Interop;

namespace WinMoe.Views;

/// <summary>
/// Mole "擦屏幕" — borderless full-screen black surface so the display can be
/// wiped without smudges triggering input. Esc or click exits.
/// </summary>
public sealed partial class CleanScreenWindow : Window
{
    private readonly DispatcherTimer _hintTimer;

    public CleanScreenWindow()
    {
        InitializeComponent();

        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        if (appWindow.Presenter is OverlappedPresenter overlapped)
        {
            overlapped.SetBorderAndTitleBar(false, false);
        }

        appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        AppWindow.SetIcon("Assets/AppIcon.ico");

        var grid = (Microsoft.UI.Xaml.Controls.Grid)Content;
        grid.Focus(FocusState.Programmatic);

        _hintTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _hintTimer.Tick += (_, _) =>
        {
            _hintTimer.Stop();
            HintText.Visibility = Visibility.Collapsed;
        };
        _hintTimer.Start();

        Activated += (_, args) =>
        {
            if (args.WindowActivationState == WindowActivationState.PointerActivated ||
                args.WindowActivationState == WindowActivationState.CodeActivated)
            {
                grid.Focus(FocusState.Programmatic);
            }
        };
    }

    private void CleanScreenGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        Close();
    }

    private void CleanScreenGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Close();
    }
}
