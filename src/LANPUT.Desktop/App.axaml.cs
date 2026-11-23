using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace LANPUT.Desktop;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            if (Program.SmokeTest)
                desktop.MainWindow.Opened += (_, _) => DispatcherTimer.RunOnce(() => desktop.MainWindow.Close(), TimeSpan.FromSeconds(6));
        }
        base.OnFrameworkInitializationCompleted();
    }
}
