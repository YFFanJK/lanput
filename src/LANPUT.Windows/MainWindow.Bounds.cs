using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace LANPUT.Windows;

public partial class MainWindow
{
    HwndSource? windowBoundsSource;
    bool boundsRefreshQueued;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        windowBoundsSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        windowBoundsSource?.AddHook(WindowBoundsHook);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (windowBoundsSource is { IsDisposed: false }) windowBoundsSource.RemoveHook(WindowBoundsHook);
        windowBoundsSource = null;
        base.OnClosed(e);
    }

    IntPtr WindowBoundsHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0024 && lParam != IntPtr.Zero && TryGetWindowMonitor(hwnd, out var monitor)) // WM_GETMINMAXINFO
        {
            var limits = Marshal.PtrToStructure<WindowMinMaxInfo>(lParam);
            var target = fullscreen ? monitor.Bounds : monitor.WorkArea;
            // Win32 expects physical pixels relative to this monitor's origin.
            limits.MaxPosition = new WindowPoint(target.Left - monitor.Bounds.Left, target.Top - monitor.Bounds.Top);
            limits.MaxSize = new WindowPoint(target.Right - target.Left, target.Bottom - target.Top);
            double dpiScale = GetDpiForWindow(hwnd) / 96d;
            if (dpiScale <= 0) dpiScale = 1;
            limits.MinTrackSize.X = Math.Max(limits.MinTrackSize.X, (int)Math.Ceiling(MinWidth * dpiScale));
            limits.MinTrackSize.Y = Math.Max(limits.MinTrackSize.Y, (int)Math.Ceiling(MinHeight * dpiScale));
            Marshal.StructureToPtr(limits, lParam, false);
            handled = true;
        }
        else if (message is 0x001A or 0x007E or 0x02E0) // settings, display or per-monitor DPI changed
        {
            if (!boundsRefreshQueued)
            {
                boundsRefreshQueued = true;
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    boundsRefreshQueued = false;
                    if (closing || closed || WindowState != WindowState.Maximized) return;
                    if (!TryGetWindowMonitor(hwnd, out var current)) return;
                    var target = fullscreen ? current.Bounds : current.WorkArea;
                    SetWindowPos(hwnd, IntPtr.Zero, target.Left, target.Top, target.Right - target.Left,
                        target.Bottom - target.Top, 0x0014); // keep Z-order and activation
                }));
            }
        }
        return IntPtr.Zero;
    }

    static bool TryGetWindowMonitor(IntPtr hwnd, out WindowMonitorInfo info)
    {
        info = new WindowMonitorInfo { Size = Marshal.SizeOf<WindowMonitorInfo>() };
        var monitor = MonitorFromWindow(hwnd, 2); // MONITOR_DEFAULTTONEAREST
        return monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WindowPoint(int x, int y) { public int X = x, Y = y; }
    [StructLayout(LayoutKind.Sequential)]
    struct WindowRectangle { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    struct WindowMinMaxInfo
    { public WindowPoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)]
    struct WindowMonitorInfo
    { public int Size; public WindowRectangle Bounds, WorkArea; public uint Flags; }

    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern bool GetMonitorInfo(IntPtr monitor, ref WindowMonitorInfo info);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
