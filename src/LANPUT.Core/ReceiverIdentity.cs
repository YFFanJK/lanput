namespace LANPUT.Core;

public static class ReceiverIdentity
{
    public const string Version = "0.9.5";
    public static string Platform => OperatingSystem.IsWindows() ? "Windows"
        : OperatingSystem.IsMacOS() ? "macOS" : OperatingSystem.IsLinux() ? "Linux" : "Unknown";
    public static string Server => $"{Platform}/1.0 UPnP/1.0 LANPUT/{Version}";
    public static string Model => $"LANPUT {Platform} Receiver";
}
