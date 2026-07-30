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
}
