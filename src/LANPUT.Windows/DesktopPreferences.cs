using System.IO;
using System.Text.Json;
namespace LANPUT.Windows;
internal sealed record DesktopPreferences(double Left = 0, double Top = 0, double Width = 1180,
    double Height = 760, bool HasBounds = false, bool Maximized = false, int Volume = 70,
    bool CloseToTray = false, bool KeepAwake = true, bool InterfaceAnimations = true,
    bool AutoReceive = true, bool CompatibilityRendering = false)
{
    static string FilePath => Path.Combine(AppStorage.DataDirectory, "desktop.json");
    public static DesktopPreferences Load()
    {
        try { return JsonSerializer.Deserialize<DesktopPreferences>(File.ReadAllText(FilePath)) ?? new(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(AppStorage.DataDirectory);
            File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(this));
            File.Move(FilePath + ".tmp", FilePath, true); return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }
}
