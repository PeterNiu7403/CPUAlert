using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using Windows.Graphics;
using System.Runtime.InteropServices;
using WinMoe.Services;
using WinMoe.Ui;
using WinMoe.ViewModels;

namespace WinMoe.Views;

public sealed partial class TrayHudWindow : Window
{
    private const uint DefaultDpi = 96;
    private static readonly IntPtr HwndTopMost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpShowWindow = 0x0040;
    private readonly Action<string?> _navigate;
    private readonly DispatcherTimer _refreshTimer;

    public TrayHudWindow(
        ISystemTelemetrySamplerService telemetrySamplerService,
        IOperationHistoryService operationHistoryService,
        Action<string?> navigate)
    {
        InitializeComponent();
        ViewModel = new TrayHudViewModel(telemetrySamplerService, operationHistoryService);
        _navigate = navigate;
        HudRoot.DataContext = ViewModel;
        WinMoeButtonVisualState.FreezeTree(HudRoot);

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(HudTitleBar);
        ConfigureWindow();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _refreshTimer.Tick += async (_, _) => await ViewModel.RefreshAsync();
        Closed += (_, _) => _refreshTimer.Stop();
    }

    public TrayHudViewModel ViewModel { get; }

    public async Task ShowNearAsync(int x, int y)
    {
        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var layout = ResizeForCurrentDpi(windowHandle, appWindow);
        var outerSize = appWindow.Size;
        var position = layout.PositionNear(x, y, outerSize.Width, outerSize.Height);
        appWindow.MoveAndResize(new RectInt32(
            position.Left,
            position.Top,
            outerSize.Width,
            outerSize.Height));

        Activate();
        SetWindowPos(
            windowHandle,
            HwndTopMost,
            position.Left,
            position.Top,
            0,
            0,
            SwpNoSize | SwpShowWindow);
        SetForegroundWindow(windowHandle);
        _refreshTimer.Start();
        await ViewModel.RefreshAsync();
    }

    private void ConfigureWindow()
    {
        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        ResizeForCurrentDpi(windowHandle, appWindow);
        appWindow.TitleBar.BackgroundColor = Windows.UI.Color.FromArgb(255, 38, 49, 45);
        appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(255, 38, 49, 45);
        appWindow.TitleBar.ButtonForegroundColor = Colors.White;

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
        }
    }

    private static TrayHudLayoutMetrics ResizeForCurrentDpi(
        IntPtr windowHandle,
        AppWindow appWindow)
    {
        var dpi = GetDpiForWindow(windowHandle);
        var layout = TrayHudLayout.ForDpi(dpi == 0 ? DefaultDpi : dpi);
        appWindow.ResizeClient(new SizeInt32(
            layout.ClientSize.Width,
            layout.ClientSize.Height));
        return layout;
    }

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string route } && !string.IsNullOrWhiteSpace(route))
        {
            _navigate(route);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr windowHandle);
}
