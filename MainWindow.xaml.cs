using Microsoft.UI.Windowing;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinMoe.Ui;
using WinMoe.Views;
using WinRT.Interop;

namespace WinMoe;

public sealed partial class MainWindow : Window
{
    private const int InitialWidthInDips = 1194;
    private const int InitialHeightInDips = 768;
    private const uint DefaultDpi = 96;
    private static readonly Windows.UI.Color TitleBarColor = Windows.UI.Color.FromArgb(255, 28, 24, 16);

    public MainWindow(ShellPage shellPage)
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new MicaBackdrop();
        Content = shellPage;
        shellPage.InitializeForWindow(this);
        // Mole chrome is compact; Tall title bar wasted vertical air above the capsule.
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        AppWindow.TitleBar.BackgroundColor = TitleBarColor;
        AppWindow.TitleBar.InactiveBackgroundColor = TitleBarColor;
        AppWindow.TitleBar.ButtonBackgroundColor = TitleBarColor;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = TitleBarColor;
        AppWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(48, 255, 255, 255);
        AppWindow.TitleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(64, 255, 255, 255);
        AppWindow.TitleBar.ButtonForegroundColor = Colors.White;
        AppWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Gray;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        var windowHandle = WindowNative.GetWindowHandle(this);
        var dpi = DisplayDpi.GetDpiForWindow(windowHandle);
        var physicalSize = WindowSizing.ToPhysicalPixels(
            InitialWidthInDips,
            InitialHeightInDips,
            dpi == 0 ? DefaultDpi : dpi);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(physicalSize.Width, physicalSize.Height));
    }
}
