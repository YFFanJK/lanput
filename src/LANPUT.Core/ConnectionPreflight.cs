using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LANPUT.Core;

public enum ConnectionCheckLevel { Info, Pass, Warning, Block }
public sealed record ConnectionCheck(string Name, ConnectionCheckLevel Level, string Message);
public sealed record ConnectionReadiness(IReadOnlyList<ConnectionCheck> Checks)
{
    public bool CanStart => Checks.All(check => check.Level != ConnectionCheckLevel.Block);
    public bool HasWarnings => Checks.Any(check => check.Level == ConnectionCheckLevel.Warning);
    public string Summary => string.Join(Environment.NewLine, Checks.Select(check =>
        $"{check.Level switch { ConnectionCheckLevel.Pass => "通过", ConnectionCheckLevel.Warning => "提醒", ConnectionCheckLevel.Block => "需处理", _ => "说明" }} · {check.Name}：{check.Message}"));
}

public sealed record NetworkReadinessFacts(bool Present, bool Up, bool Multicast, bool PrivateAddress,
    bool SuspectedVirtual, bool HasGateway, bool? TcpAvailable, bool ReceiverRunning);

public static class ConnectionPreflight
{
    public static ConnectionReadiness Evaluate(NetworkReadinessFacts facts)
    {
        var checks = new List<ConnectionCheck>();
        checks.Add(new("所选网卡", facts.Present && facts.Up ? ConnectionCheckLevel.Pass : ConnectionCheckLevel.Block,
            facts.Present && facts.Up ? "网卡已连接，所选地址仍属于此网卡" : "所选网卡或地址已失效，请刷新后重新选择"));
        checks.Add(new("局域网地址", facts.PrivateAddress ? ConnectionCheckLevel.Pass : ConnectionCheckLevel.Block,
            facts.PrivateAddress ? "使用私人 IPv4 地址" : "请选择手机所在局域网的私人 IPv4 地址"));
        checks.Add(new("设备发现", facts.Multicast ? ConnectionCheckLevel.Pass : ConnectionCheckLevel.Block,
            facts.Multicast ? "网卡报告支持组播；实际发现仍取决于防火墙和路由器" : "所选网卡不支持设备发现所需的组播"));
        if (facts.SuspectedVirtual) checks.Add(new("网卡类型", ConnectionCheckLevel.Warning,
            "名称或类型提示可能为 VPN/虚拟网卡，请确认手机能够访问；该判断仅作提醒"));
        if (!facts.HasGateway) checks.Add(new("网关", ConnectionCheckLevel.Warning,
            "未发现 IPv4 网关；隔离局域网仍可能正常使用，不要求连接互联网"));
        checks.Add(facts.ReceiverRunning
            ? new("接收服务", ConnectionCheckLevel.Pass, "本机接收服务已启动；不代表手机已发现设备")
            : facts.TcpAvailable switch
            {
                true => new("TCP 41000", ConnectionCheckLevel.Pass, "当前未发现监听冲突，启动时仍以实际绑定结果为准"),
                false => new("TCP 41000", ConnectionCheckLevel.Block, "接收地址的端口已被占用，请关闭占用程序后重试"),
                _ => new("TCP 41000", ConnectionCheckLevel.Warning, "无法确定端口是否可用，以实际启动结果为准")
            });
        checks.Add(new("手机连接", ConnectionCheckLevel.Info,
            "手机与电脑需位于可互访的局域网；访客网络、AP 隔离及防火墙可能阻止发现。此检查不修改网络设置。"));
        return new(checks);
    }

    public static bool SuspectsVirtualInterface(string name, string description, NetworkInterfaceType type)
    {
        string label = (name + " " + description).ToLowerInvariant();
        return type == NetworkInterfaceType.Tunnel || new[]
        { "vpn", "wintun", "wireguard", "tailscale", "zerotier", "clash", "tun", "tap-", "virtual", "vethernet", "vmware", "virtualbox", "docker", "hyper-v" }
            .Any(label.Contains);
    }

    public static bool? TcpAvailable(IPAddress address, IEnumerable<IPEndPoint> listeners, int port = 41000)
    {
        var matching = listeners.Where(endpoint => endpoint.Port == port).ToArray();
        if (matching.Any(endpoint => endpoint.Address.Equals(IPAddress.Any) || endpoint.Address.Equals(address))) return false;
        // An IPv6-any listener may be dual-stack; endpoints alone do not expose that option.
        return matching.Any(endpoint => endpoint.Address.Equals(IPAddress.IPv6Any)) ? null : true;
    }

    public static ConnectionReadiness Scan(NetworkChoice choice, bool receiverRunning)
    {
        var adapter = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(network => network.Name == choice.Name
            && network.GetIPProperties().UnicastAddresses.Any(address => address.Address.Equals(choice.Address)));
        var properties = adapter?.GetIPProperties();
        bool? available;
        try { available = TcpAvailable(choice.Address, IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()); }
        catch (Exception error) when (error is NetworkInformationException or SocketException or PlatformNotSupportedException)
        { available = null; }
        return Evaluate(new(adapter is not null, adapter?.OperationalStatus == OperationalStatus.Up,
            adapter?.SupportsMulticast == true, NetworkChoice.IsPrivate(choice.Address),
            adapter is not null && SuspectsVirtualInterface(adapter.Name, adapter.Description, adapter.NetworkInterfaceType),
            properties?.GatewayAddresses.Any(gateway => gateway.Address.AddressFamily == AddressFamily.InterNetwork
                && !gateway.Address.Equals(IPAddress.Any)) == true,
            available, receiverRunning));
    }
}
