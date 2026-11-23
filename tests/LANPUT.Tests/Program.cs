using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Linq;
using LANPUT.Core;

var passed = 0;
var failed = 0;
void Assert(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
void Test(string name, Action body)
{
    try { body(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + " · " + ex.Message); }
}
void Fault(int expected, Action body)
{
    try { body(); throw new Exception("Expected UPnP fault " + expected); }
    catch (UpnpException ex) { Assert(ex.Code == expected, $"Expected {expected}, got {ex.Code}"); }
}
string Body(string service, string action, params string[] args)
{
    XNamespace s = "http://schemas.xmlsoap.org/soap/envelope/";
    var a = new XElement(XName.Get(action, service));
    for (int i = 0; i < args.Length; i += 2) a.Add(new XElement(args[i], args[i + 1]));
    return new XElement(s + "Envelope", new XElement(s + "Body", a)).ToString();
}
var player = new FakePlayer();
var router = new SoapRouter(player);
string Call(string service, string action, params string[] args) => router.Execute(service, $"\"{service}#{action}\"", Body(service, action, args));
string Av(string action, params string[] args) => Call(UpnpProtocol.Av, action, ["InstanceID", "0", .. args]);
string Rc(string action, params string[] args) => Call(UpnpProtocol.Rc, action, ["InstanceID", "0", "Channel", "Master", .. args]);

Test("Clock supports long durations and rejects invalid values", () =>
{
    Assert(XmlSafe.Time(90061000) == "25:01:01"); Assert(XmlSafe.ParseTime("25:01:01") == 90061000);
    foreach (var time in new[] { "-1:00:00", "00:60:00", "00:00:60", "00:00:NaN", "abc", "00:00:-1" }) Fault(402, () => XmlSafe.ParseTime(time));
});
Test("Device description is escaped and exposes renderer services", () =>
{
    var xml = XmlSafe.Parse(UpnpProtocol.Description("MateBook & 手机 <test>", Guid.NewGuid(), new Uri("http://192.168.1.8:41000/")));
    Assert(xml.Descendants().Single(x => x.Name.LocalName == "friendlyName").Value == "MateBook & 手机 <test>");
    Assert(xml.Descendants().Count(x => x.Name.LocalName == "service") == 3);
    Assert(xml.Descendants().Single(x => x.Name.LocalName == "deviceType").Value == UpnpProtocol.DeviceType);
});
Test("All advertised SCPD arguments refer to state variables", () =>
{
    foreach (var service in UpnpProtocol.Services)
    {
        var vars = Scpd.Variables(service).Select(v => v.Name).ToHashSet();
        Assert(Scpd.Actions(service).SelectMany(a => a.Args).All(a => vars.Contains(a.State)));
        XmlSafe.Parse(Scpd.Document(service));
    }
});
Test("No media cannot be played", () => Fault(701, () => Av("Play", "Speed", "1")));
Test("Load signed HTTP URI preserves metadata and title", () =>
{
    var meta = "<DIDL-Lite xmlns='urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/' xmlns:dc='http://purl.org/dc/elements/1.1/'><item><dc:title>影片 &amp; 测试</dc:title></item></DIDL-Lite>";
    Av("SetAVTransportURI", "CurrentURI", "https://example.invalid/video.mp4?a=1&token=secret", "CurrentURIMetaData", meta);
    Assert(player.Snapshot.Title == "影片 & 测试"); Assert(player.Snapshot.Metadata == meta);
    var result = XmlSafe.Parse(Av("GetMediaInfo"));
    Assert(result.Descendants("CurrentURI").Single().Value == player.Snapshot.Uri);
});
Test("Playback pause seek and stop", () =>
{
    Av("Play", "Speed", "1"); Assert(player.Snapshot.State == "PLAYING");
    Av("Pause"); Assert(player.Snapshot.State == "PAUSED_PLAYBACK");
    Av("Seek", "Unit", "REL_TIME", "Target", "00:01:03"); Assert(player.Snapshot.PositionMs == 63000);
    var position = XmlSafe.Parse(Av("GetPositionInfo")); Assert(position.Descendants("RelTime").Single().Value == "00:01:03");
    Av("Stop"); Assert(player.Snapshot.State == "STOPPED");
});
Test("Unsupported seek and speed give semantic UPnP faults", () =>
{
    Fault(710, () => Av("Seek", "Unit", "TRACK_NR", "Target", "1"));
    Fault(711, () => Av("Seek", "Unit", "REL_TIME", "Target", "10:00:00"));
    Fault(717, () => Av("Play", "Speed", "2"));
});
Test("Volume and mute are validated and round trip", () =>
{
    Rc("SetVolume", "DesiredVolume", "35"); Assert(XmlSafe.Parse(Rc("GetVolume")).Descendants("CurrentVolume").Single().Value == "35");
    Rc("SetMute", "DesiredMute", "1"); Assert(player.Snapshot.Muted);
    Fault(402, () => Rc("SetVolume", "DesiredVolume", "101"));
    Fault(402, () => Rc("SetMute", "DesiredMute", "maybe"));
    Fault(402, () => Call(UpnpProtocol.Rc, "GetVolume", "InstanceID", "0", "Channel", "Other"));
});
Test("Invalid instance, action, missing and duplicate arguments rejected", () =>
{
    Fault(718, () => Call(UpnpProtocol.Av, "Stop", "InstanceID", "1"));
    Fault(401, () => Av("DeleteEverything"));
    Fault(402, () => Av("Play"));
    Fault(402, () => Call(UpnpProtocol.Av, "Stop", "InstanceID", "0", "InstanceID", "0"));
    Fault(401, () => router.Execute(UpnpProtocol.Av, $"{UpnpProtocol.Rc}#Stop", Body(UpnpProtocol.Av, "Stop", "InstanceID", "0")));
});
Test("Dangerous URI schemes and DTD are rejected", () =>
{
    foreach (var uri in new[] { "file:///C:/test.mp4", "ftp://example.invalid/a", "https://user:pass@example.invalid/a", "not-a-url" })
        Fault(716, () => Av("SetAVTransportURI", "CurrentURI", uri, "CurrentURIMetaData", ""));
    Fault(402, () => router.Execute(UpnpProtocol.Av, $"{UpnpProtocol.Av}#Stop", "<!DOCTYPE a [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><a>&x;</a>"));
});
Test("Repeated Play is idempotent for playing and opening media", () =>
{
    player.Stop(); Av("Play", "Speed", "1"); var before = player.PlayCalls;
    Av("Play", "Speed", "1"); Assert(player.PlayCalls == before);
    player.MarkOpening(); Av("Play", "Speed", "1"); Assert(player.PlayCalls == before);
    player.Stop(); Av("Play", "Speed", "1"); Assert(player.PlayCalls == before + 1);
    player.Stop();
});
Test("Diagnostic labels never expose raw signed URLs or arbitrary messages", () =>
{
    var label = PlaybackDiagnostics.Classify("HTTP 403 Forbidden https://private.invalid/secret.mp4?token=SUPERSECRET Authorization: secret");
    Assert(label is not null && label.Contains("403") && !label.Contains("SUPERSECRET") && !label.Contains("private.invalid"));
    Assert(PlaybackDiagnostics.Classify("unclassified secret text") is null);
    Assert(PlaybackDiagnostics.Classify("direct3d11 output failed")!.Contains("Direct3D"));
    Assert(PlaybackDiagnostics.Classify("connection timed out")!.Contains("超时"));
});
Test("Source summary keeps only transport, known format and query presence", () =>
{
    var summary = PlaybackDiagnostics.DescribeSource("https://private.invalid/secret.m3u8?token=SUPERSECRET");
    Assert(summary.Contains("HLS") && !summary.Contains("SUPERSECRET") && !summary.Contains("private.invalid") && !summary.Contains("secret.m3u8"));
    Assert(PlaybackDiagnostics.ContentType("text/html") == "HTML 网页");
    Assert(PlaybackDiagnostics.ContentType("secret/unknown") == "其他或未声明");
});

Test("403 followed by Playing and Ended without output is an error", () =>
{
    Assert(PlaybackDiagnostics.EndIsFailure(true, false, 0, 403));
    Assert(PlaybackDiagnostics.EndIsFailure(true, false, 0, 401));
    Assert(!PlaybackDiagnostics.EndIsFailure(true, true, 5000, 403));
    Assert(!PlaybackDiagnostics.EndIsFailure(true, false, 5000, 403));
    Assert(!PlaybackDiagnostics.EndIsFailure(true, true, 5000, 0));
    Assert(PlaybackDiagnostics.EndIsFailure(false, false, 0, 0));
});
Test("Source scope distinguishes local services without exposing paths", () =>
{
    var summary = PlaybackDiagnostics.DescribeSource("http://192.168.3.9:5678/private?token=SECRET");
    Assert(summary.Contains("局域网服务 192.168.3.9:5678"));
    Assert(!summary.Contains("private") && !summary.Contains("SECRET"));
    Assert(PlaybackDiagnostics.SourceScope(new Uri("http://127.0.0.1/a")) == "电脑本机服务");
    Assert(PlaybackDiagnostics.AccessErrorCode("HTTP 403 Forbidden") == 403);
    Assert(PlaybackDiagnostics.AccessErrorCode("unclassified secret") is null);
});

Test("GENA LastChange escapes XML exactly once", () =>
{
    var body = XmlSafe.Parse(UpnpProtocol.EventBody(UpnpProtocol.Av, player.Snapshot));
    var change = XmlSafe.Parse(body.Descendants("LastChange").Single().Value);
    Assert(change.Descendants().Single(x => x.Name.LocalName == "AVTransportURI").Attribute("val")!.Value == player.Snapshot.Uri);
    var cm = XmlSafe.Parse(UpnpProtocol.EventBody(UpnpProtocol.Cm, player.Snapshot));
    Assert(cm.Descendants().Count(x => x.Name.LocalName == "property") == 3);
});
Test("SSDP parses valid M-SEARCH and ignores notifications", () =>
{
    Assert(SsdpService.SearchTarget("M-SEARCH * HTTP/1.1\r\nMAN: \"ssdp:discover\"\r\nST: ssdp:all\r\n\r\n") == "ssdp:all");
    Assert(SsdpService.SearchTarget("NOTIFY * HTTP/1.1\r\nST: ssdp:all\r\n\r\n") is null);
    Assert(SsdpService.SearchTarget("M-SEARCH * HTTP/1.1\r\nST: ssdp:all\r\n\r\n") is null);
});
Test("Subnet filtering does not accept foreign or IPv6 controllers", () =>
{
    var n = new NetworkChoice("test", IPAddress.Parse("192.168.1.10"), IPAddress.Parse("255.255.255.0"));
    Assert(n.Contains(IPAddress.Parse("192.168.1.40"))); Assert(!n.Contains(IPAddress.Parse("192.168.2.40"))); Assert(!n.Contains(IPAddress.IPv6Loopback));
    Assert(NetworkChoice.IsPrivate(IPAddress.Parse("172.16.0.1"))); Assert(!NetworkChoice.IsPrivate(IPAddress.Parse("172.32.0.1")));
});
Test("Subscription owner, service, timeout and callback restrictions", () =>
{
    var subscriptions = new EventSubscriptions();
    var owner = IPAddress.Parse("192.168.1.20");
    var sid = subscriptions.Add(UpnpProtocol.Av, "<http://192.168.1.20:1234/events>", owner, 60);
    Assert(subscriptions.Renew(sid, UpnpProtocol.Av, owner, 60));
    Assert(!subscriptions.Renew(sid, UpnpProtocol.Rc, owner, 60));
    Assert(!subscriptions.Remove(sid, UpnpProtocol.Av, IPAddress.Parse("192.168.1.21")));
    Fault(412, () => subscriptions.Add(UpnpProtocol.Av, "<http://127.0.0.1/events>", owner, 60));
    Fault(412, () => subscriptions.Add(UpnpProtocol.Av, "<http://example.invalid/events>", owner, 60));
    Assert(subscriptions.Remove(sid, UpnpProtocol.Av, owner));
    Assert(EventSubscriptions.TimeoutSeconds("Second-999999") == 1800);
    Assert(EventSubscriptions.TimeoutSeconds("Second-1") == 30);
    subscriptions.DisposeAsync().GetAwaiter().GetResult();
});

try
{
    // Exercise the real HTTP host with a fake player. No Internet or copyrighted media involved.
    using var reserve = new TcpListener(IPAddress.Loopback, 0); reserve.Start();
    var port = ((IPEndPoint)reserve.LocalEndpoint).Port; reserve.Stop();
    await using var host = new ReceiverHost(new NetworkChoice("loopback", IPAddress.Loopback, IPAddress.Parse("255.0.0.0")), "Test receiver", Guid.NewGuid(), port, player, _ => { });
    await host.StartAsync(discovery: false);
    using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = host.Origin };
    Assert((await client.GetAsync("description.xml")).IsSuccessStatusCode);
    using var request = new HttpRequestMessage(HttpMethod.Post, "/AVTransport/control");
    request.Headers.TryAddWithoutValidation("SOAPACTION", $"\"{UpnpProtocol.Av}#GetPositionInfo\"");
    request.Content = new StringContent(Body(UpnpProtocol.Av, "GetPositionInfo", "InstanceID", "0"), Encoding.UTF8, "text/xml");
    using var response = await client.SendAsync(request);
    Assert(response.StatusCode == HttpStatusCode.OK);
    XmlSafe.Parse(await response.Content.ReadAsStringAsync());
    using var bad = new HttpRequestMessage(HttpMethod.Post, "/AVTransport/control");
    bad.Headers.TryAddWithoutValidation("SOAPACTION", $"\"{UpnpProtocol.Av}#Stop\"");
    bad.Content = new StringContent(Body(UpnpProtocol.Av, "Stop", "InstanceID", "99"));
    using var badResponse = await client.SendAsync(bad);
    Assert(badResponse.StatusCode == HttpStatusCode.InternalServerError);
    Assert(XmlSafe.Parse(await badResponse.Content.ReadAsStringAsync()).Descendants().Any(x => x.Name.LocalName == "errorCode" && x.Value == "718"));
    await host.DisposeAsync(); await host.DisposeAsync();
    Assert(!System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
        .Any(endpoint => endpoint.Port == port && endpoint.Address.Equals(IPAddress.Loopback)));
    passed++; Console.WriteLine("PASS Real HTTP description, SOAP round trip, fault response and repeat-safe cleanup");
}
catch (Exception ex) { failed++; Console.WriteLine("FAIL HTTP integration · " + ex); }

try
{
    int requests = 0;
    using var handler = new FakeProbeHandler(request =>
    {
        requests++;
        Assert(request.Headers.Range?.ToString() == "bytes=0-1023");
        Assert(request.Headers.Authorization is null);
        Assert(request.Headers.Contains("getcontentFeatures.dlna.org") == (requests == 2));
        if (requests == 2)
        {
            Assert(request.Headers.UserAgent.ToString().Contains("LanCast/0.2.3"));
            Assert(request.Headers.GetValues("transferMode.dlna.org").Single() == "Streaming");
        }
        return requests == 1
            ? new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("sensitive error body") }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("#EXTM3U\n#EXTINF:10,\nhttps://secret.invalid/seg.ts?token=SECRET") };
    });
    using var client = new HttpClient(handler);
    var a = await MediaProbe.RunAsync(client, "http://192.168.3.9/video?token=SECRET", false, CancellationToken.None);
    var b = await MediaProbe.RunAsync(client, "http://192.168.3.9/video?token=SECRET", true, CancellationToken.None);
    Assert(a.StatusCode == 403 && !a.IsSuccess && !a.BodyKind.Contains("sensitive"));
    Assert(b.IsSuccess && b.BodyKind.Contains("HLS") && !b.BodyKind.Contains("SECRET"));
    Assert(requests == 2);
    passed++; Console.WriteLine("PASS Independent probe profiles and response redaction");
}
catch (Exception ex) { failed++; Console.WriteLine("FAIL Media probe profiles · " + ex.Message); }

try
{
    const int busyCode = unchecked((int)0x800401D0);
    int writes = 0, delays = 0;
    var retry = await ClipboardCopy.TryAsync(() =>
    {
        writes++;
        if (writes < 3) throw new System.Runtime.InteropServices.COMException("Clipboard busy", busyCode);
    }, CancellationToken.None, delay: _ => { delays++; return Task.CompletedTask; });
    Assert(retry.Copied && retry.Attempts == 3 && retry.ErrorCode is null && writes == 3 && delays == 2);
    writes = 0;
    var failedCopy = await ClipboardCopy.TryAsync(() =>
    {
        writes++; throw new System.Runtime.InteropServices.COMException("Clipboard busy", busyCode);
    }, CancellationToken.None, delay: _ => Task.CompletedTask);
    Assert(!failedCopy.Copied && failedCopy.Attempts == 3 && failedCopy.ErrorCode == busyCode && writes == 3);
    passed++; Console.WriteLine("PASS Clipboard retries recover or return failure for HRESULT 0x800401D0 without throwing");
}
catch (Exception ex) { failed++; Console.WriteLine("FAIL Clipboard retry · " + ex.Message); }
try
{
    using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
    int writes = 0;
    try
    {
        await ClipboardCopy.TryAsync(() => writes++, cancellation.Token);
        throw new Exception("Expected cancellation");
    }
    catch (OperationCanceledException) { Assert(writes == 0); }
    try
    {
        await ClipboardCopy.TryAsync(() => throw new InvalidOperationException("Unexpected failure"), CancellationToken.None);
        throw new Exception("Unexpected exception was swallowed");
    }
    catch (InvalidOperationException ex) { Assert(ex.Message == "Unexpected failure"); }
    passed++; Console.WriteLine("PASS Clipboard cancellation and unexpected errors are not swallowed");
}
catch (Exception ex) { failed++; Console.WriteLine("FAIL Clipboard cancellation · " + ex.Message); }

Test("HLS parser resolves relative references and quoted commas without propagating parent query", () =>
{
    var origin = new Uri("https://media.invalid/dir/master.m3u8?auth=PARENT");
    var list = "#EXTM3U\n#EXT-X-MEDIA:TYPE=AUDIO,NAME=\"a,b\",URI=\"audio/list.m3u8?token=AUDIO\"\n#EXT-X-STREAM-INF:BANDWIDTH=800000\nvideo/list.m3u8?token=VIDEO\n";
    var parsed = HlsProbe.Inspect(list, origin);
    Assert(parsed.IsPlaylist && parsed.References.Count == 2);
    Assert(parsed.References.Single(r => r.Kind == "音频列表").Address.AbsoluteUri == "https://media.invalid/dir/audio/list.m3u8?token=AUDIO");
    Assert(parsed.References.Single(r => r.Kind == "子列表").Address.AbsoluteUri == "https://media.invalid/dir/video/list.m3u8?token=VIDEO");
    var attributes = HlsProbe.ParseAttributes("#EXT-X-KEY:METHOD=AES-128,URI=\"key?a=1,b=2\",KEYFORMAT=\"identity\"");
    Assert(attributes["URI"] == "key?a=1,b=2");
    var mediaList = HlsProbe.Inspect("#EXTM3U\n#EXT-X-BYTERANGE:100@20\nsegment.ts\n#EXT-X-KEY:METHOD=SAMPLE-AES,KEYFORMAT=\"private\",URI=\"skd://private\"\n", origin);
    Assert(mediaList.ByteRanges && mediaList.Protection.Contains("非 identity") && mediaList.References.Count == 1);
    Assert(!mediaList.References[0].Address.Query.Contains("PARENT"));
});
try
{
    var logs = new List<string>(); int calls = 0;
    using var handler = new FakeProbeHandler(request =>
    {
        calls++;
        Assert(request.Headers.UserAgent.ToString() == HlsProbe.Agent);
        Assert(request.Headers.Authorization is null);
        var path = request.RequestUri!.AbsolutePath;
        if (path == "/entry")
        {
            var redirect = new HttpResponseMessage(HttpStatusCode.Found);
            redirect.Headers.Location = new Uri("/dir/master?token=TOPSECRET", UriKind.Relative); return redirect;
        }
        if (path == "/dir/master") return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=10\nvideo?token=CHILDSECRET\n") };
        if (path == "/dir/video") return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("#EXTM3U\n#EXT-X-KEY:METHOD=AES-128,URI=\"key?token=KEYSECRET\"\n#EXT-X-MAP:URI=\"init.mp4\"\n#EXTINF:10,\nseg.ts?token=SEGSECRET\n") };
        if (path == "/dir/key") return new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("private error") };
        return new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new StringContent("media") };
    });
    using var client = new HttpClient(handler);
    await HlsProbe.TraceAsync(client, "http://media.invalid/entry?token=SECRET", logs.Add, CancellationToken.None);
    Assert(logs.Any(s => s.Contains("密钥") && s.Contains("403")));
    Assert(logs.Any(s => s.Contains("视频分片") && s.Contains("206")));
    Assert(logs.Any(s => s.Contains("初始化片段") && s.Contains("206")));
    Assert(logs.All(s => !s.Contains("SECRET") && !s.Contains("private error") && !s.Contains("media.invalid")));
    Assert(calls == 6);
    passed++; Console.WriteLine("PASS HLS redirect-relative resolution, resource-role failures and redacted logs");
}
catch (Exception ex) { failed++; Console.WriteLine("FAIL HLS chain · " + ex.Message); }
try
{
    int calls = 0; var logs = new List<string>();
    using var handler = new FakeProbeHandler(_ =>
    {
        calls++; return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("#EXTM3U\n" + new string('x', 270000)) };
    });
    using var client = new HttpClient(handler);
    await HlsProbe.TraceAsync(client, "http://media.invalid/entry", logs.Add, CancellationToken.None);
    Assert(calls == 1 && logs.Any(s => s.Contains("超过上限")));
    passed++; Console.WriteLine("PASS HLS oversized playlist is not traversed");
}
catch (Exception ex) { failed++; Console.WriteLine("FAIL HLS limits · " + ex.Message); }

Test("LANPUT legacy settings migration preserves identity and never overwrites current settings", () =>
{
    var root = Path.Combine(Path.GetTempPath(), "LANPUT-migration-test-" + Guid.NewGuid().ToString("N"));
    var legacy = Path.Combine(root, "LanCast"); var current = Path.Combine(root, "LANPUT");
    try
    {
        Directory.CreateDirectory(legacy);
        var original = "{\"Id\":\"58c4f958-640e-4329-b7be-cec916b53c82\",\"Name\":\"My receiver\",\"Address\":\"192.168.3.44\"}";
        File.WriteAllText(Path.Combine(legacy, "settings.json"), original);
        Assert(SettingsMigration.CopyIfNeeded(current, legacy).Status == "copied");
        Assert(File.ReadAllText(Path.Combine(current, "settings.json")) == original);
        Assert(File.ReadAllText(Path.Combine(legacy, "settings.json")) == original);
        File.WriteAllText(Path.Combine(current, "settings.json"), "new settings");
        Assert(SettingsMigration.CopyIfNeeded(current, legacy).Status == "already-present");
        Assert(File.ReadAllText(Path.Combine(current, "settings.json")) == "new settings");
        Assert(SettingsMigration.CopyIfNeeded(Path.Combine(root, "fresh"), Path.Combine(root, "missing")).Status == "no-legacy-settings");
        var blocker = Path.Combine(root, "file-not-folder"); File.WriteAllText(blocker, "keep");
        Assert(SettingsMigration.CopyIfNeeded(blocker, legacy).Status == "failed");
        Assert(File.ReadAllText(blocker) == "keep");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
});

Test("Recovery retains interface identity and rejects ambiguous replacements", () =>
{
    var wifi = new NetworkChoice("Wi-Fi", IPAddress.Parse("192.168.3.44"), IPAddress.Parse("255.255.255.0"));
    var changed = wifi with { Address = IPAddress.Parse("192.168.3.45") };
    var vpn = wifi with { Name = "VPN", Address = IPAddress.Parse("10.0.0.2") };
    Assert(ReceiverRecovery.SelectNetwork([wifi, vpn], "Wi-Fi", "192.168.3.44") == wifi);
    Assert(ReceiverRecovery.SelectNetwork([changed, vpn], "Wi-Fi", "192.168.3.44") == changed);
    Assert(ReceiverRecovery.SelectNetwork([vpn], "Wi-Fi", "192.168.3.44") is null);
    Assert(ReceiverRecovery.SelectNetwork([wifi, changed], "Wi-Fi", "192.168.3.46") is null);
});
Test("Log rotation bounds backups and cleanup preserves unrelated files", () =>
{
    var folder = Path.Combine(Path.GetTempPath(), "LANPUT-log-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    try
    {
        var path = Path.Combine(folder, "receiver-20261116-080000-1.log");
        var log = new RotatingLog(path, 32, 2);
        for (int i = 0; i < 20; i++) log.Append("entry-" + i + new string('x', 16) + "\n");
        Assert(File.Exists(path + ".1") && File.Exists(path + ".2") && !File.Exists(path + ".3"));
        Assert(File.ReadAllText(path).Contains("entry-19"));
        var old = Path.Combine(folder, "application-20250101-000000-2.log");
        File.WriteAllText(old, "old"); File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-20));
        var unrelated = Path.Combine(folder, "my-notes.log"); File.WriteAllText(unrelated, "keep");
        File.SetLastWriteTimeUtc(unrelated, DateTime.UtcNow.AddDays(-20));
        RotatingLog.Cleanup(folder, DateTime.UtcNow);
        Assert(!File.Exists(old) && File.Exists(unrelated) && File.Exists(path));
        // A blocked log location must not crash playback or diagnostics.
        new RotatingLog(Path.Combine(unrelated, "blocked.log")).Append("ignored");
    }
    finally { Directory.Delete(folder, true); }
});

Test("Buffering phase reports only engine percentages and distinguishes opening from video output", () =>
{
    var opening = new PlaybackSnapshot(State: "TRANSITIONING", Uri: "http://media.invalid/video");
    Assert(PlaybackActivity.Resolve(opening, "Opening", false, null).Phase == PlaybackPhase.Opening);
    var buffering = PlaybackActivity.Resolve(opening, "Buffering", false, 37.25);
    Assert(buffering.Phase == PlaybackPhase.Buffering && buffering.BufferPercent == 37.25);
    Assert(buffering.Label.Contains("37%"));
    foreach (double? invalid in new double?[] { null, double.NaN, double.PositiveInfinity, -1, 100.1 })
        Assert(PlaybackActivity.Resolve(opening, "Buffering", false, invalid).BufferPercent is null);
    Assert(PlaybackActivity.ValidPercent(0) == 0 && PlaybackActivity.ValidPercent(100) == 100);
    var playing = opening with { State = "PLAYING" };
    Assert(PlaybackActivity.Resolve(playing, "Playing", false, 30).Phase == PlaybackPhase.WaitingForVideo);
    Assert(PlaybackActivity.Resolve(playing, "Playing", true, 30) == new PlaybackActivity(PlaybackPhase.Playing));
});
Test("Late buffering cannot overwrite paused stopped or failed presentation", () =>
{
    var media = new PlaybackSnapshot(State: "PAUSED_PLAYBACK", Uri: "http://media.invalid/video");
    Assert(PlaybackActivity.Resolve(media, "Buffering", true, 12).Phase == PlaybackPhase.Paused);
    Assert(PlaybackActivity.Resolve(media with { State = "STOPPED" }, "Buffering", true, 12).Phase == PlaybackPhase.Idle);
    Assert(PlaybackActivity.Resolve(media with { Status = "ERROR_OCCURRED" }, "Playing", true, 100).Phase == PlaybackPhase.Error);
    Assert(PlaybackActivity.Resolve(new(), "Buffering", false, 20).Phase == PlaybackPhase.Idle);
});
Test("Relative seek clamps boundaries supports repeated commands and avoids overflow", () =>
{
    Assert(PlaybackNavigation.RelativeSeek(3000, 60000, -10000) == 0);
    Assert(PlaybackNavigation.RelativeSeek(57000, 60000, 10000) == 60000);
    var first = PlaybackNavigation.RelativeSeek(10000, 60000, 10000)!.Value;
    Assert(PlaybackNavigation.RelativeSeek(first, 60000, 10000) == 30000);
    Assert(PlaybackNavigation.RelativeSeek(long.MaxValue, long.MaxValue, 10000) == long.MaxValue);
    Assert(PlaybackNavigation.RelativeSeek(long.MinValue, 60000, -10000) == 0);
    Assert(PlaybackNavigation.RelativeSeek(100, 0, 10000) is null);
});
Test("Connection checks block known failures while allowing virtual gateway and unknown-port warnings", () =>
{
    var ready = new NetworkReadinessFacts(true, true, true, true, false, true, true, false);
    Assert(ConnectionPreflight.Evaluate(ready).CanStart && !ConnectionPreflight.Evaluate(ready).HasWarnings);
    Assert(!ConnectionPreflight.Evaluate(ready with { Present = false }).CanStart);
    Assert(!ConnectionPreflight.Evaluate(ready with { Up = false }).CanStart);
    Assert(!ConnectionPreflight.Evaluate(ready with { Multicast = false }).CanStart);
    Assert(!ConnectionPreflight.Evaluate(ready with { PrivateAddress = false }).CanStart);
    Assert(!ConnectionPreflight.Evaluate(ready with { TcpAvailable = false }).CanStart);
    var warnings = ConnectionPreflight.Evaluate(ready with { SuspectedVirtual = true, HasGateway = false, TcpAvailable = null });
    Assert(warnings.CanStart && warnings.HasWarnings);
    Assert(warnings.Checks.Count(check => check.Level == ConnectionCheckLevel.Warning) == 3);
    Assert(ConnectionPreflight.Evaluate(ready with { TcpAvailable = false, ReceiverRunning = true }).CanStart);
    Assert(ConnectionPreflight.SuspectsVirtualInterface("Office", "WireGuard Tunnel", System.Net.NetworkInformation.NetworkInterfaceType.Tunnel));
    Assert(!ConnectionPreflight.SuspectsVirtualInterface("Wi-Fi", "Intel Wi-Fi", System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211));
});
Test("TCP preflight distinguishes selected-address conflicts other adapters and IPv6 uncertainty", () =>
{
    var selected = IPAddress.Parse("192.168.3.44");
    Assert(ConnectionPreflight.TcpAvailable(selected, [new(selected, 41000)]) == false);
    Assert(ConnectionPreflight.TcpAvailable(selected, [new(IPAddress.Any, 41000)]) == false);
    Assert(ConnectionPreflight.TcpAvailable(selected, [new(IPAddress.Parse("10.0.0.2"), 41000)]) == true);
    Assert(ConnectionPreflight.TcpAvailable(selected, [new(selected, 41001)]) == true);
    Assert(ConnectionPreflight.TcpAvailable(selected, [new(IPAddress.IPv6Any, 41000)]) is null);
});
Test("Automatic receive requires a saved interface and never falls back to a different adapter", () =>
{
    var wifi = new NetworkChoice("Wi-Fi", IPAddress.Parse("192.168.3.44"), IPAddress.Parse("255.255.255.0"));
    var vpn = wifi with { Name = "VPN" };
    Assert(ReceiverRecovery.SelectForStartup([wifi, vpn], null, null) is null);
    Assert(ReceiverRecovery.SelectForStartup([wifi, vpn], "", "192.168.3.44") is null);
    Assert(ReceiverRecovery.SelectForStartup([vpn], "Wi-Fi", "192.168.3.44") is null);
    Assert(ReceiverRecovery.SelectForStartup([wifi, vpn], "Wi-Fi", "192.168.3.44") == wifi);
    var changed = wifi with { Address = IPAddress.Parse("192.168.3.45") };
    Assert(ReceiverRecovery.SelectForStartup([changed], "Wi-Fi", "192.168.3.44") == changed);
    Assert(ReceiverRecovery.SelectForStartup([wifi, changed], "Wi-Fi", "192.168.3.46") is null);
});
Test("Old desktop preferences retain defaults while new startup and compatibility options round trip", () =>
{
    var old = System.Text.Json.JsonSerializer.Deserialize<LANPUT.Windows.DesktopPreferences>("{\"Volume\":35,\"CloseToTray\":true}")!;
    Assert(old.Volume == 35 && old.CloseToTray && old.KeepAwake && old.InterfaceAnimations);
    Assert(old.AutoReceive && !old.CompatibilityRendering);
    var disabled = System.Text.Json.JsonSerializer.Deserialize<LANPUT.Windows.DesktopPreferences>("{\"AutoReceive\":false}")!;
    Assert(!disabled.AutoReceive);
    var updated = old with { AutoReceive = true, CompatibilityRendering = true };
    Assert(System.Text.Json.JsonSerializer.Deserialize<LANPUT.Windows.DesktopPreferences>(System.Text.Json.JsonSerializer.Serialize(updated)) == updated);
});

Test("Update versions compare numerically and normalize omitted zero components", () =>
{
    Assert(SoftwareUpdates.IsNewer(new("0.10.0", "https://example.com/download"), "0.6.1"));
    Assert(!SoftwareUpdates.IsNewer(new("0.6.1.0", "https://example.com/download"), "0.6.1"));
    Assert(!SoftwareUpdates.IsNewer(new("0.6.0", "https://example.com/download"), "0.6.1"));
});
Test("Update manifest rejects missing fields unsafe links and unstable version labels", () =>
{
    foreach (var json in new[] {
        "{}", "null", "{\"version\":\"0.6.2\",\"downloadUrl\":\"file:///bad.exe\"}",
        "{\"version\":\"0.6.2-beta\",\"downloadUrl\":\"https://example.com\"}",
        "{\"version\":\"0.6.2\",\"downloadUrl\":\"https://user:pass@example.com\"}" })
    {
        bool rejected = false;
        try { SoftwareUpdates.Parse(json); } catch (Exception e) when (e is FormatException or System.Text.Json.JsonException) { rejected = true; }
        Assert(rejected);
    }
});
Test("Update transport follows HTTPS redirects and parses case insensitive JSON", () =>
{
    var handler = new UpdateTestTransport(request => request.RequestUri!.AbsolutePath == "/latest"
        ? new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("/manifest", UriKind.Relative) } }
        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"version\":\"0.6.2\",\"downloadUrl\":\"https://example.com/setup.exe\",\"notes\":\"test\"}") });
    var result = SoftwareUpdates.CheckAsync("https://example.com/latest", CancellationToken.None, handler).GetAwaiter().GetResult();
    Assert(result.Version == "0.6.2" && result.Notes == "test" && handler.Requests == 2);
});
Test("Update transport rejects HTTP downgrades oversized manifests and non-success responses", () =>
{
    foreach (var mode in new[] { "downgrade", "large", "notfound", "loop" })
    {
        var handler = new UpdateTestTransport(_ => mode switch {
            "downgrade" => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("http://example.com") } },
            "loop" => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri("https://example.com") } },
            "large" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 65537)) },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        bool rejected = false;
        try { SoftwareUpdates.CheckAsync("https://example.com", CancellationToken.None, handler).GetAwaiter().GetResult(); }
        catch (Exception e) when (e is FormatException or HttpRequestException) { rejected = true; }
        Assert(rejected && handler.Requests <= 6);
    }
});
Test("Update checks propagate cancellation", () =>
{
    using var cancel = new CancellationTokenSource(); cancel.Cancel();
    bool cancelled = false;
    try { SoftwareUpdates.CheckAsync("https://example.com", cancel.Token, new UpdateTestTransport(_ => new HttpResponseMessage(HttpStatusCode.OK))).GetAwaiter().GetResult(); }
    catch (OperationCanceledException) { cancelled = true; }
    Assert(cancelled);
});

Test("Playback information keeps unknown values distinct from measured zero", () =>
{
    var empty = new PlaybackInfo().ToDisplayText();
    Assert(empty.Contains("尚无影片") && empty.Contains("暂无") && !empty.Contains("NaN"));
    var measured = new PlaybackInfo(HasMedia: true, LostFrames: 0, DisplayedFrames: 120, BufferPercent: 0).ToDisplayText();
    Assert(measured.Contains("丢失视频帧   0") && measured.Contains("已显示视频帧 120") && measured.Contains("缓冲进度     0 %"));
    var invalid = new PlaybackInfo(HasMedia: true, BufferPercent: double.PositiveInfinity, LostFrames: -1).ToDisplayText();
    Assert(invalid.Contains("缓冲进度     当前无缓冲百分比") && invalid.Contains("丢失视频帧   引擎未提供"));
});
Test("Playback information formats rational frame rates and track bitrate units", () =>
{
    Assert(PlaybackInfo.FrameRate(30000, 1001) == "29.97 fps");
    Assert(PlaybackInfo.FrameRate(25, 1) == "25 fps");
    Assert(PlaybackInfo.FrameRate(25, 0) == "暂无" && PlaybackInfo.FrameRate(0, 1) == "暂无");
    Assert(PlaybackInfo.Bitrate(0) == "暂无" && PlaybackInfo.Bitrate(128000) == "128 kbps");
    Assert(PlaybackInfo.Bitrate(8000000) == "8 Mbps");
    Assert(new PlaybackInfo(HasMedia: true, Width: 1920, Height: 1080).ToDisplayText().Contains("1920 × 1080"));
    Assert(new PlaybackInfo(HasMedia: true, Width: 1920, Height: 0).ToDisplayText().Contains("分辨率       引擎未提供"));
});

Test("Still image presentation preserves stopped paused and error states", () =>
{
    Assert(new PlaybackActivity(PlaybackPhase.Playing).LabelFor(true) == "正在展示图片");
    foreach (var phase in new[] { PlaybackPhase.Idle, PlaybackPhase.Opening, PlaybackPhase.Buffering, PlaybackPhase.Paused, PlaybackPhase.Error })
    {
        var activity = new PlaybackActivity(phase);
        Assert(activity.LabelFor(true) == activity.Label);
    }
    Assert(new PlaybackActivity(PlaybackPhase.Playing).LabelFor(false) == "正在播放");
});
Test("Still image presentation does not imply degraded video playback", () =>
{
    var image = new PlaybackInfo(HasMedia: true, IsImage: true, VideoTracks: 1, AudioTracks: 0,
        VideoCodec: "24 bits RGB", Width: 1920, Height: 1080, FrameRateNum: 10, FrameRateDen: 1,
        DisplayedFrames: 168, LostFrames: 0).ToDisplayText();
    Assert(image.Contains("媒体类型     图片") && image.Contains("1920 × 1080"));
    Assert(!image.Contains("10 fps") && !image.Contains("丢失视频帧") && !image.Contains("音频编码"));
    var video = new PlaybackInfo(HasMedia: true, VideoTracks: 1, AudioTracks: 0, VideoSelected: true,
        VideoCodec: "24 bits RGB", FrameRateNum: 60, FrameRateDen: 1).ToDisplayText();
    Assert(video.Contains("媒体类型     视频") && video.Contains("60 fps（片源参数）"));
    Assert(video.Contains("实际输出帧率 未测量") && video.Contains("未确认（由引擎自动选择）"));
});
Test("Track absence, track selection and metadata availability are distinct", () =>
{
    var audio = new PlaybackInfo(HasMedia: true, VideoTracks: 0, AudioTracks: 2, AudioSelected: true).ToDisplayText();
    Assert(audio.Contains("媒体类型     纯音频") && audio.Contains("共 2 条 · 已选择播放轨道"));
    Assert(!audio.Contains("标称帧率") && audio.Contains("音频编码     引擎未提供"));
    var noAudio = new PlaybackInfo(HasMedia: true, VideoTracks: 1, AudioTracks: 0).ToDisplayText();
    Assert(noAudio.Contains("音频轨道     未识别到轨道") && !noAudio.Contains("音频编码"));
    Assert(new PlaybackInfo(HasMedia: true).ToDisplayText().Contains("等待引擎识别"));
    Assert(!new PlaybackInfo().ToDisplayText().Contains("丢失视频帧"));
});
Test("Local media titles omit directories and image detection does not guess from RGB or URL suffix", () =>
{
    Assert(MediaPresentation.LocalTitle(Path.Combine("private", "folder", "我的影片.mp4")) == "我的影片.mp4");
    Assert(MediaPresentation.IsImage("photo.JPG", "", true));
    Assert(!MediaPresentation.IsImage("https://example.invalid/cover.jpg", "", false));
    var photo = "<DIDL-Lite xmlns='urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/' xmlns:upnp='urn:schemas-upnp-org:metadata-1-0/upnp/'><item><upnp:class>object.item.imageItem.photo</upnp:class></item></DIDL-Lite>";
    Assert(MediaPresentation.IsImage("http://example.invalid/resource", photo, false));
    Assert(!MediaPresentation.IsImage("http://example.invalid/resource", photo.Replace("imageItem.photo", "videoItem"), false));
    Assert(!MediaPresentation.IsImage("http://example.invalid/resource", "<broken", false));
});

Test("Cinema entry waits for video instead of a URI or opening event", () =>
{
    var video = new PlaybackSnapshot(Uri: "https://example.test/film", State: "PLAYING");
    Assert(!CinemaPresentation.ShouldImmerse(video, false, false));
    Assert(CinemaPresentation.ShouldImmerse(video, true, false));
    Assert(!CinemaPresentation.ShouldImmerse(video with { State = "TRANSITIONING" }, true, false));
    Assert(!CinemaPresentation.ShouldImmerse(new(), true, true));
});
Test("Cinema retains its screen on pause and buffering but returns on stop end or error", () =>
{
    var video = new PlaybackSnapshot(Uri: "https://example.test/film", State: "PLAYING");
    Assert(CinemaPresentation.ShouldImmerse(video with { State = "PAUSED_PLAYBACK" }, false, true));
    Assert(CinemaPresentation.ShouldImmerse(video with { State = "TRANSITIONING" }, false, true));
    Assert(!CinemaPresentation.ShouldImmerse(video with { State = "STOPPED" }, true, true));
    Assert(!CinemaPresentation.ShouldImmerse(video with { Status = "ERROR_OCCURRED" }, true, true));
    Assert(!CinemaPresentation.ShouldImmerse(video with { State = "NO_MEDIA_PRESENT" }, false, true));
    Assert(!CinemaPresentation.ShouldImmerse(video with { State = "PAUSED_PLAYBACK" }, false, false));
});

Test("A presentation hold accepts user pause without relaxing ordinary opening or stopped states", () =>
{
    var held = new FakePlayer();
    held.Load("https://example.test/film", "", "test");
    held.MarkOpening();
    var control = new SoapRouter(held);
    void PauseHeld() => control.Execute(UpnpProtocol.Av, $"\"{UpnpProtocol.Av}#Pause\"", Body(UpnpProtocol.Av, "Pause", "InstanceID", "0"));
    Fault(701, PauseHeld);
    held.AllowsTransitionPause = true;
    PauseHeld();
    Assert(held.Snapshot.State == "PAUSED_PLAYBACK");
    held.Stop();
    Fault(701, PauseHeld);
});


Test("Music metadata handles DIDL namespaces, missing artist and hostile XML", () =>
{
    var song = MusicIdentity.FromDidl("<DIDL-Lite xmlns:dc='urn:dc' xmlns:upnp='urn:upnp'><item><dc:title> Test Song </dc:title><upnp:artist>anonymous</upnp:artist><dc:creator>Test Artist</dc:creator><upnp:album>Album</upnp:album></item></DIDL-Lite>");
    Assert(song == new MusicIdentity("Test Song", "Test Artist", "Album"));
    Assert(MusicIdentity.FromDidl("<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///test'>]><x>&e;</x>").Title == "");
});
Test("LRC supports repeated timestamps, fractions, offset and backward seeking", () =>
{
    var lyrics = LrcLyrics.Parse("[ti:Original fixture]\n[offset:+250]\n[00:01.2][00:03.450]First\n[00:02.00]Second\n[00:04.000]\n[invalid]Ignored");
    Assert(lyrics.Lines.Count == 4);
    Assert(lyrics.IndexAt(949) == -1 && lyrics.IndexAt(950) == 0);
    Assert(lyrics.IndexAt(2000) == 1 && lyrics.IndexAt(3300) == 2);
    Assert(lyrics.IndexAt(1000) == 0 && lyrics.IndexAt(500, 500) == 0);
    Assert(lyrics.IndexAt(4000) == 3 && lyrics.Lines[3].Text == "");
});
Test("Automatic lyric matching rejects anonymous, ambiguous and different recordings", () =>
{
    var record = new LyricsRecord(1, "Test Song", "Artist", "Album", 60, false, null, "[00:01]Original line");
    var song = new MusicIdentity("test song", "Artist");
    Assert(LyricsClient.ExactMatch([record], song, 61) == record);
    Assert(LyricsClient.ExactMatch([record], song with { Artist = "" }, 60) is null);
    Assert(LyricsClient.ExactMatch([record, record with { Id = 2 }], song, 60) is null);
    Assert(LyricsClient.ExactMatch([record], song, 64) is null);
    Assert(LyricsClient.ExactMatch([record], song with { Title = "Test Song (Live)" }, 60) is null);
});
Test("Lyrics transport caches queries and honors server retry delay", () =>
{
    var transport = new UpdateTestTransport(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[{\"id\":1,\"trackName\":\"Test\",\"artistName\":\"Artist\",\"duration\":60,\"syncedLyrics\":\"[00:01]Original\"}]") });
    using var http = new HttpClient(transport);
    var client = new LyricsClient(http);
    var song = new MusicIdentity("Test", "Artist");
    Assert(client.SearchAsync(song, default).GetAwaiter().GetResult().Length == 1);
    Assert(client.SearchAsync(song, default).GetAwaiter().GetResult().Length == 1 && transport.Requests == 1);
    var limited = new UpdateTestTransport(_ => {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30)); return response;
    });
    using var limitedHttp = new HttpClient(limited);
    var limitedClient = new LyricsClient(limitedHttp);
    for (int i = 0; i < 2; i++)
        try { limitedClient.SearchAsync(song, default).GetAwaiter().GetResult(); Assert(false); } catch (HttpRequestException) { }
    Assert(limited.Requests == 1);
});


Test("Lyric search applies usable candidates automatically despite incomplete metadata", () =>
{
    var plain = new LyricsRecord(1, "Test Song", "Artist", "Album", 60, false, "Original fixture", null);
    var synced = plain with { Id = 2, ArtistName = "Artist / Guest", SyncedLyrics = "[00:00]Original timed fixture" };
    var empty = plain with { Id = 3, PlainLyrics = null };
    Assert(LyricsClient.PreferredMatch([empty, plain, synced], new("Test Song"), 60) == synced);
    Assert(LyricsClient.PreferredMatch([plain], new("Test Song", "Different formatting"), 60) == plain);
    Assert(LyricsClient.PreferredMatch([empty], new("Test Song"), 60) is null);
    Assert(LyricsClient.PreferredMatch([], new("Test Song"), 60) is null);
});
Test("Automatic lyric choice prefers the recording identity and closest duration", () =>
{
    var original = new LyricsRecord(1, "Test Song", "Artist", "Album", 60, false, null, "[00:00]Original fixture");
    var live = original with { Id = 2, TrackName = "Test Song (Live)", Duration = 90 };
    var other = original with { Id = 3, Duration = 160 };
    Assert(LyricsClient.PreferredMatch([live, other, original], new("Test Song", "Artist"), 60) == original);
    Assert(LyricsClient.PreferredMatch([other, original], new("Test Song"), 60) == original);
    Assert(LyricsClient.PreferredMatch([original with { Id = 4 }, original], new("Test Song", "Artist"), 60) is not null);
});

Test("Photographic cinema aligns with its screen and zooms uniformly across window shapes", () =>
{
    foreach (var size in new[] { (900d, 318d), (1144d, 536d), (1920d, 1080d), (3440d, 900d) })
    {
        var screen = CinemaArtworkLayout.ScreenIn(size.Item1, size.Item2);
        var rest = CinemaArtworkLayout.FrameAt(size.Item1, size.Item2, 0);
        Assert(Math.Abs(screen.X - rest.X) < .001 && Math.Abs(screen.Y - rest.Y) < .001);
        Assert(screen.X >= 0 && screen.Y >= 0 && screen.X + screen.Width <= size.Item1 && screen.Y + screen.Height <= size.Item2);
        foreach (double progress in new[] { 0, .25, .5, .75, 1 })
        {
            var frame = CinemaArtworkLayout.FrameAt(size.Item1, size.Item2, progress);
            Assert(Math.Abs(frame.Width / frame.Height - 1416d / 694) < .001, "Image or welcome content was stretched");
        }
        var end = CinemaArtworkLayout.FrameAt(size.Item1, size.Item2, 1);
        Assert(end.X <= .001 && end.Y <= .001 && end.Width >= size.Item1 - .001 && end.Height >= size.Item2 - .001);
    }
});

Console.WriteLine($"\n{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

sealed class UpdateTestTransport(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public int Requests { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests++;
        return Task.FromResult(respond(request));
    }
}

sealed class FakePlayer : IPlayback
{
    public bool AllowsTransitionPause { get; set; }
    public PlaybackSnapshot Snapshot { get; private set; } = new();
    public void Load(string uri, string metadata, string title) => Snapshot = Snapshot with { Uri = uri, Metadata = metadata, Title = title, State = "STOPPED", DurationMs = 3600000, PositionMs = 0 };
    public int PlayCalls { get; private set; }
    public void MarkOpening() => Snapshot = Snapshot with { State = "TRANSITIONING" };
    public void Play() { PlayCalls++; Snapshot = Snapshot with { State = "PLAYING" }; }
    public void Pause() => Snapshot = Snapshot with { State = "PAUSED_PLAYBACK" };
    public void Stop() => Snapshot = Snapshot with { State = "STOPPED", PositionMs = 0 };
    public void Seek(long milliseconds) => Snapshot = Snapshot with { PositionMs = milliseconds };
    public void SetVolume(int volume) => Snapshot = Snapshot with { Volume = volume };
    public void SetMute(bool muted) => Snapshot = Snapshot with { Muted = muted };
}

sealed class FakeProbeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(respond(request));
}
