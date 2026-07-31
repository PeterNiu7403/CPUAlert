using Microsoft.UI.Xaml;

namespace WinMoe.Services;

public interface ITrayIconService : IDisposable
{
    void Initialize(Window mainWindow);

    void ShowHudForDiagnostics(int x, int y);

    void ShowCleanScreenForDiagnostics();
}
