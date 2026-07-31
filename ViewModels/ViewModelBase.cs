using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;

namespace WinMoe.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    private readonly DispatcherQueue? _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    protected void RunOnUiThread(Action action)
    {
        if (_dispatcherQueue is null || _dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _dispatcherQueue.TryEnqueue(() => action());
    }

    /// <summary>
    /// XAML compiled bindings crash with RPC_E_WRONG_THREAD when PropertyChanged is
    /// raised on a thread-pool continuation (e.g. after ConfigureAwait(false)).
    /// Marshal the event to the UI thread instead of crashing the app.
    /// </summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (_dispatcherQueue is null || _dispatcherQueue.HasThreadAccess)
        {
            base.OnPropertyChanged(e);
            return;
        }

        _dispatcherQueue.TryEnqueue(() => base.OnPropertyChanged(e));
    }
}
