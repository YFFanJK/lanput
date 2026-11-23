using Avalonia.Threading;

namespace LANPUT.Desktop;

// Keep the same synchronous command boundary as the Windows receiver, while
// native VLC events always post asynchronously to avoid interop deadlocks.
internal sealed class UiDispatcher
{
    public void Invoke(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.InvokeAsync(action).GetAwaiter().GetResult();
    }
    public void InvokeAsync(Action action) => Dispatcher.UIThread.Post(action);
    public void VerifyAccess() => Dispatcher.UIThread.VerifyAccess();
}
