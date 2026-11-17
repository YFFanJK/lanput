using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LANPUT.Core;

public sealed class EventSubscriptions : IAsyncDisposable
{
    sealed class Subscription(string sid, string service, Uri callback, IPAddress owner, int seconds)
    {
        public string Sid { get; } = sid;
        public string Service { get; } = service;
        public Uri Callback { get; } = callback;
        public IPAddress Owner { get; } = owner;
        public DateTimeOffset Expires { get; set; } = DateTimeOffset.UtcNow.AddSeconds(seconds);
        public uint Sequence { get; set; }
        public SemaphoreSlim Sending { get; } = new(1, 1);
    }
    readonly ConcurrentDictionary<string, Subscription> subscriptions = new();
    readonly HttpClient client = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(2) }) { Timeout = TimeSpan.FromSeconds(3) };
    readonly CancellationTokenSource stop = new();
    readonly object gate = new();
    public static int TimeoutSeconds(string timeout)
    {
        return timeout.StartsWith("Second-", StringComparison.OrdinalIgnoreCase) && int.TryParse(timeout[7..], out var n)
            ? Math.Clamp(n, 30, 1800) : 1800;
    }
    public string Add(string service, string callback, IPAddress owner, int seconds)
    {
        // Only a numeric callback on the requesting controller is accepted. No DNS, proxy, or redirects.
        if (!callback.StartsWith('<') || !callback.EndsWith('>') || !Uri.TryCreate(callback[1..^1], UriKind.Absolute, out var uri) ||
            uri.Scheme != "http" || uri.UserInfo.Length != 0 || !IPAddress.TryParse(uri.Host, out var ip) || !ip.Equals(owner) || uri.Port is <= 0 or > 65535)
            throw new UpnpException(412, "Invalid callback");
        lock (gate)
        {
            Prune();
            if (subscriptions.Count >= 16) throw new UpnpException(503, "Too many subscriptions");
            var sid = $"uuid:{Guid.NewGuid()}";
            subscriptions[sid] = new Subscription(sid, service, uri, owner, seconds);
            return sid;
        }
    }
    public bool Renew(string sid, string service, IPAddress owner, int seconds)
    {
        if (!subscriptions.TryGetValue(sid, out var s) || s.Service != service || !s.Owner.Equals(owner) || s.Expires <= DateTimeOffset.UtcNow) return false;
        s.Expires = DateTimeOffset.UtcNow.AddSeconds(seconds); return true;
    }
    public bool Remove(string sid, string service, IPAddress owner)
    {
        return subscriptions.TryGetValue(sid, out var s) && s.Service == service && s.Owner.Equals(owner) && subscriptions.TryRemove(sid, out _);
    }
    void Prune() { foreach (var s in subscriptions.Values) if (s.Expires <= DateTimeOffset.UtcNow) subscriptions.TryRemove(s.Sid, out _); }
    public async Task NotifyAsync(PlaybackSnapshot snapshot, string? onlySid = null)
    {
        Prune();
        await Task.WhenAll(subscriptions.Values.Where(s => onlySid is null || s.Sid == onlySid).Select(s => SendAsync(s, snapshot)));
    }
    async Task SendAsync(Subscription s, PlaybackSnapshot snapshot)
    {
        var acquired = false;
        try
        {
            await s.Sending.WaitAsync(stop.Token);
            acquired = true;
            using var request = new HttpRequestMessage(new HttpMethod("NOTIFY"), s.Callback);
            request.Headers.TryAddWithoutValidation("NT", "upnp:event"); request.Headers.TryAddWithoutValidation("NTS", "upnp:propchange");
            request.Headers.TryAddWithoutValidation("SID", s.Sid); request.Headers.TryAddWithoutValidation("SEQ", s.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
            request.Content = new StringContent(UpnpProtocol.EventBody(s.Service, snapshot), Encoding.UTF8, "text/xml");
            using var response = await client.SendAsync(request, stop.Token);
            if (response.IsSuccessStatusCode) s.Sequence = s.Sequence == uint.MaxValue ? 1 : s.Sequence + 1;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException) { }
        finally { if (acquired) s.Sending.Release(); }
    }
    public ValueTask DisposeAsync() { stop.Cancel(); client.Dispose(); subscriptions.Clear(); stop.Dispose(); return ValueTask.CompletedTask; }
}
