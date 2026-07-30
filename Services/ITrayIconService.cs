using Microsoft.UI.Xaml;

namespace MoleWindows.Services;

public interface ITrayIconService : IDisposable
{
    void Initialize(Window mainWindow);

    void ShowHudForDiagnostics(int x, int y);
}
