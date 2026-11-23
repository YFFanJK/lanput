using Avalonia;
using System.Runtime.InteropServices;

namespace LANPUT.Desktop;

internal static class Program
{
    internal static bool SmokeTest { get; private set; }
    [DllImport("libX11.so.6")] static extern int XInitThreads();

    [STAThread]
    public static void Main(string[] args)
    {
        SmokeTest = args.Contains("--smoke-test");
        if (args.Contains("--verify-engine"))
        {
            NativeVlc.Initialize();
            using var engine = new LibVLCSharp.Shared.LibVLC("--aout=dummy", "--vout=dummy", "--no-osd");
            Console.WriteLine("LANPUT " + DesktopSettings.PlatformId + " · LibVLC " + engine.Version);
            return;
        }
        // LibVLC's X11 output uses a different thread from the UI.
        if (OperatingSystem.IsLinux()) XInitThreads();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect().LogToTrace();
}
