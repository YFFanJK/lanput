using System.Runtime.InteropServices;

namespace LANPUT.Core;

public sealed record ClipboardCopyResult(bool Copied, int Attempts, int? ErrorCode);

public static class ClipboardCopy
{
    // The caller provides the platform clipboard operation. Await retains the caller's
    // synchronization context: WPF clipboard operations must stay on the UI/STA thread.
    public static async Task<ClipboardCopyResult> TryAsync(Action write, CancellationToken cancellationToken,
        int maxAttempts = 3, Func<CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(write);
        if (maxAttempts is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        delay ??= ct => Task.Delay(150, ct);
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                write();
                return new ClipboardCopyResult(true, attempt, null);
            }
            catch (ExternalException error)
            {
                // Handle only clipboard external/COM failures at the operation boundary.
                // Never swallow arbitrary application/rendering exceptions globally.
                if (attempt == maxAttempts) return new ClipboardCopyResult(false, attempt, error.HResult);
            }
            await delay(cancellationToken);
        }
        throw new InvalidOperationException("Unreachable retry state");
    }
}
