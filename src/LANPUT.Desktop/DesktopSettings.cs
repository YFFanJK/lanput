using System.Runtime.InteropServices;
using System.Text.Json;

namespace LANPUT.Desktop;

internal sealed record DesktopSettings
{
    public Guid DeviceId { get; init; } = Guid.NewGuid();
    public string DeviceName { get; init; } = Environment.MachineName + " · LANPUT";
    public string NetworkAddress { get; init; } = "";
    public bool AutoReceive { get; init; } = true;
    public bool Animations { get; init; } = true;
    public bool OnlineLyrics { get; init; } = true;
    public int Volume { get; init; } = 70;
    // No Windows feed fallback: it could offer an EXE on macOS/Linux.
    public string UpdateFeed { get; init; } = "";
    public static string PlatformId => (OperatingSystem.IsMacOS() ? "osx" : OperatingSystem.IsLinux() ? "linux" : "win")
        + "-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LANPUT.Desktop");
    public static DesktopSettings Load()
    {
        try
        {
            var value = JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(Path.Combine(DataDirectory, "desktop.json"))) ?? new();
            return value with { DeviceId = value.DeviceId == Guid.Empty ? Guid.NewGuid() : value.DeviceId,
                DeviceName = string.IsNullOrWhiteSpace(value.DeviceName) ? Environment.MachineName + " · LANPUT" : value.DeviceName,
                Volume = Math.Clamp(value.Volume, 0, 100), UpdateFeed = value.UpdateFeed ?? "" };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        var file = Path.Combine(DataDirectory, "desktop.json");
        File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(file + ".tmp", file, true);
    }
}
