using System.Diagnostics;
using System.IO;
using LANPUT.Core;

namespace LANPUT.Windows;

internal static class AppDiagnostics
{
    static readonly object Gate = new();
    static readonly string Session = $"application-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log";
    static readonly RotatingLog Writer = new(Path.Combine(AppStorage.DataDirectory, "logs", Session));
    public static string LastEvent { get; private set; } = "尚无记录";
    public static void Record(string kind, Exception? error = null)
    {
        // Exception.Message / ToString may contain signed URLs. Record types and method names only.
        var details = error is null ? "" : $" · {error.GetType().FullName} · HRESULT 0x{error.HResult:X8}";
        if (error is not null)
        {
            var methods = new StackTrace(error, false).GetFrames()?.Take(8)
                .Select(f => f.GetMethod()).Where(m => m is not null)
                .Select(m => $"{m!.DeclaringType?.FullName}.{m.Name}") ?? Enumerable.Empty<string>();
            details += Environment.NewLine + string.Join(Environment.NewLine, methods);
        }
        lock (Gate)
        {
            LastEvent = kind + details;
            try
            {
                var folder = Path.Combine(AppStorage.DataDirectory, "logs");
                Directory.CreateDirectory(folder);
                Writer.Append($"{DateTime.Now:HH:mm:ss} · {kind}{details}{Environment.NewLine}");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
