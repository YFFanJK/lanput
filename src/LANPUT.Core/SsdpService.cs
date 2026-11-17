using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LANPUT.Core;

public sealed class SsdpService(NetworkChoice network, Guid id, Uri location, Action<string> log) : IAsyncDisposable
{
    readonly CancellationTokenSource stop = new();
    UdpClient? receiver;
    UdpClient? sender;
    Task? receiveTask;
    Task? announceTask;
    readonly List<Task> replies = [];
    readonly object gate = new();
    static readonly IPEndPoint Group = new(IPAddress.Parse("239.255.255.250"), 1900);
    public string[] Targets => ["upnp:rootdevice", $"uuid:{id}", UpnpProtocol.DeviceType, .. UpnpProtocol.Services];
    string Usn(string target) => target == $"uuid:{id}" ? target : $"uuid:{id}::{target}";
    public void Start()
    {
        receiver = new UdpClient(AddressFamily.InterNetwork);
        receiver.ExclusiveAddressUse = false;
        receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        receiver.Client.Bind(new IPEndPoint(IPAddress.Any, 1900));
        receiver.JoinMulticastGroup(Group.Address, network.Address);
        sender = new UdpClient(new IPEndPoint(network.Address, 0));
        sender.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, network.Address.GetAddressBytes());
        sender.Ttl = 2;
        receiveTask = ReceiveAsync(); announceTask = AnnounceAsync();
        log($"SSDP 已启动 · {network.Address} · UDP 1900");
    }
    public static string? SearchTarget(string packet)
    {
        var lines = packet.Split("\r\n");
        if (!lines[0].Equals("M-SEARCH * HTTP/1.1", StringComparison.OrdinalIgnoreCase)) return null;
        var headers = lines.Skip(1).Where(l => l.Contains(':')).Select(l => l.Split(':', 2))
            .GroupBy(a => a[0].Trim(), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First()[1].Trim(), StringComparer.OrdinalIgnoreCase);
        if (!headers.TryGetValue("MAN", out var man) || !man.Trim('"').Equals("ssdp:discover", StringComparison.OrdinalIgnoreCase)) return null;
        return headers.GetValueOrDefault("ST");
    }
    async Task ReceiveAsync()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var result = await receiver!.ReceiveAsync(stop.Token);
                if (!network.Contains(result.RemoteEndPoint.Address) || result.Buffer.Length > 8192) continue;
                var st = SearchTarget(Encoding.ASCII.GetString(result.Buffer));
                if (st is null || st != "ssdp:all" && !Targets.Contains(st)) continue;
                lock (gate)
                {
                    replies.RemoveAll(t => t.IsCompleted);
                    if (replies.Count >= 32) continue;
                    replies.Add(ReplyAsync(st, result.RemoteEndPoint));
                }
                log("收到设备搜索，已安排回应");
            }
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException) { }
        catch (SocketException) { if (!stop.IsCancellationRequested) log("SSDP 接收中断，请检查网卡并重新启动接收"); }
    }
    async Task ReplyAsync(string st, IPEndPoint remote)
    {
        try
        {
            await Task.Delay(Random.Shared.Next(10, 150), stop.Token);
            foreach (var target in st == "ssdp:all" ? Targets : [st])
            {
                var message = $"HTTP/1.1 200 OK\r\nCACHE-CONTROL: max-age=1800\r\nEXT:\r\nLOCATION: {location}\r\nSERVER: Windows/10.0 UPnP/1.0 LanCast/0.2\r\nST: {target}\r\nUSN: {Usn(target)}\r\n\r\n";
                await sender!.SendAsync(Encoding.ASCII.GetBytes(message), remote, stop.Token);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException) { }
    }
    async Task AnnounceAsync()
    {
        try
        {
            await AdvertiseAsync("ssdp:alive");
            await Task.Delay(500, stop.Token);
            await AdvertiseAsync("ssdp:alive");
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
            while (await timer.WaitForNextTickAsync(stop.Token)) await AdvertiseAsync("ssdp:alive");
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException) { }
        catch (SocketException) { if (!stop.IsCancellationRequested) log("设备广播失败，请检查网络"); }
    }
    async Task AdvertiseAsync(string kind)
    {
        foreach (var target in Targets)
        {
            var alive = kind == "ssdp:alive" ? $"CACHE-CONTROL: max-age=1800\r\nLOCATION: {location}\r\nSERVER: Windows/10.0 UPnP/1.0 LanCast/0.2\r\n" : "";
            var message = $"NOTIFY * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\n{alive}NT: {target}\r\nNTS: {kind}\r\nUSN: {Usn(target)}\r\n\r\n";
            await sender!.SendAsync(Encoding.ASCII.GetBytes(message), Group);
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (sender is not null) { try { await AdvertiseAsync("ssdp:byebye"); } catch (Exception e) when (e is SocketException or ObjectDisposedException) { } }
        stop.Cancel(); receiver?.Dispose();
        if (receiveTask is not null) await receiveTask;
        if (announceTask is not null) await announceTask;
        Task[] pending; lock (gate) pending = replies.ToArray();
        await Task.WhenAll(pending);
        sender?.Dispose(); stop.Dispose();
    }
}
