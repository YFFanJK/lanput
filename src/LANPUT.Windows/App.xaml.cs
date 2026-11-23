using System.Windows;

namespace LANPUT.Windows;
public partial class App : Application
{
    SingleInstance? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        instance = new SingleInstance();
        if (!instance.Acquire(Dispatcher, () => (MainWindow as LANPUT.Windows.MainWindow)?.RestoreFromTray()))
        { Shutdown(); return; }
        AppStorage.MigrateLegacySettings();
        AppDiagnostics.Record("应用启动 · LANPUT 0.9.5");
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppDiagnostics.Record("未处理的线程异常", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => AppDiagnostics.Record("未观察的任务异常", args.Exception);
        DispatcherUnhandledException += (_, args) =>
        {
            AppDiagnostics.Record("界面未处理异常，应用即将退出", args.Exception);
            MessageBox.Show("程序出现未处理错误。\n\n错误类型：" + args.Exception.GetType().Name + "\n已在日志目录记录 application 开头的诊断文件。",
                "LANPUT", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1); args.Handled = true;
        };
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        AppDiagnostics.Record("应用退出 · ExitCode " + e.ApplicationExitCode);
        (MainWindow as LANPUT.Windows.MainWindow)?.DisposeDesktopFeatures();
        instance?.Dispose();
        base.OnExit(e);
    }
}
