using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
namespace LANPUT.Windows;

internal sealed class PlaybackPower : IDisposable
{
    [DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint flags);
    bool active;
    public void Update(bool playing)
    {
        if (active == playing) return;
        if (SetThreadExecutionState(playing ? 0x80000003u : 0x80000000u) == 0)
        { AppDiagnostics.Record("防熄屏请求失败"); return; }
        active = playing;
    }
    public void Dispose() => Update(false);
}

internal sealed class SingleInstance : IDisposable
{
    readonly Mutex mutex = new(false, @"Local\LANPUT.Instance");
    readonly EventWaitHandle signal = new(false, EventResetMode.AutoReset, @"Local\LANPUT.Activate");
    RegisteredWaitHandle? registration;
    bool owns;
    public bool Acquire(Dispatcher dispatcher, Action activate)
    {
        try { owns = mutex.WaitOne(0); } catch (AbandonedMutexException) { owns = true; }
        if (!owns) { signal.Set(); return false; }
        registration = ThreadPool.RegisterWaitForSingleObject(signal, (_, _) =>
            dispatcher.BeginInvoke(activate), null, Timeout.Infinite, false);
        return true;
    }
    public void Dispose()
    {
        registration?.Unregister(null);
        if (owns) mutex.ReleaseMutex();
        signal.Dispose(); mutex.Dispose();
    }
}
