using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Xml;
using System.Xml.Linq;

namespace LANPUT.Core;

public sealed record NetworkChoice(string Name, IPAddress Address, IPAddress Mask)
{
    public override string ToString() => $"{Name} · {Address}";
    public bool Contains(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        var a = Address.GetAddressBytes(); var b = address.GetAddressBytes(); var m = Mask.GetAddressBytes();
        return Enumerable.Range(0, 4).All(i => (a[i] & m[i]) == (b[i] & m[i]));
    }
    public static IReadOnlyList<NetworkChoice> Enumerate() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up && n.SupportsMulticast && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .SelectMany(n => n.GetIPProperties().UnicastAddresses
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && IsPrivate(a.Address))
            .Select(a => new NetworkChoice(n.Name, a.Address, a.IPv4Mask)))
        .ToArray();
    public static bool IsPrivate(IPAddress ip)
    {
        var a = ip.GetAddressBytes();
        return a.Length == 4 && (a[0] == 10 || a[0] == 192 && a[1] == 168 || a[0] == 172 && a[1] is >= 16 and <= 31);
    }
}

public sealed record PlaybackSnapshot(string State = "NO_MEDIA_PRESENT", string Status = "OK", string Uri = "", string Metadata = "", string Title = "等待影片", long PositionMs = 0, long DurationMs = 0, int Volume = 70, bool Muted = false);
public interface IPlayback
{
    bool AllowsTransitionPause => false;
    PlaybackSnapshot Snapshot { get; }
    void Load(string uri, string metadata, string title);
    void Play();
    void Pause();
    void Stop();
    void Seek(long milliseconds);
    void SetVolume(int volume);
    void SetMute(bool muted);
}

public static class XmlSafe
{
    public static XDocument Parse(string text)
    {
        using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 262144 });
        return XDocument.Load(reader);
    }
    public static string Time(long ms)
    {
        var t = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
    }
    public static long ParseTime(string value)
    {
        var parts = value.Split(':');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m) ||
            !double.TryParse(parts[2], System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var s) ||
            h < 0 || h > 9999 || m is < 0 or > 59 || !double.IsFinite(s) || s < 0 || s >= 60)
            throw new UpnpException(402, "Invalid Args");
        return checked((long)((h * 3600d + m * 60d + s) * 1000));
    }
}

public sealed class UpnpException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}
