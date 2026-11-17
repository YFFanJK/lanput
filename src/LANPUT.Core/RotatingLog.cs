using System.Text;
namespace LANPUT.Core;

// Each writer owns one basename; cleanup is deliberately limited to LANPUT log patterns.
public sealed class RotatingLog(string path, long maxBytes = 1048576, int backups = 3)
{
    readonly object gate = new();
    public void Append(string line)
    {
        lock (gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (File.Exists(path) && new FileInfo(path).Length + Encoding.UTF8.GetByteCount(line) > maxBytes)
                {
                    for (int i = backups; i >= 1; i--)
                    {
                        var from = i == 1 ? path : path + "." + (i - 1);
                        if (File.Exists(from)) File.Move(from, path + "." + i, overwrite: true);
                    }
                }
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
    public static void Cleanup(string folder, DateTime utcNow)
    {
        try
        {
            var files = Directory.EnumerateFiles(folder).Select(p => new FileInfo(p))
                .Where(f => System.Text.RegularExpressions.Regex.IsMatch(f.Name,
                    @"^(application-|receiver-)?\d{8}-\d{6}(-\d+)?\.log(\.[1-3])?$"))
                .OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
            long kept = 0;
            foreach (var file in files)
            {
                kept += file.Length;
                if (utcNow - file.LastWriteTimeUtc > TimeSpan.FromDays(14) || kept > 32 * 1048576L)
                    try { file.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
