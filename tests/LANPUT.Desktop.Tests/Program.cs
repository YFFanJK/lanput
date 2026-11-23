using System.Net;
using System.Net.Sockets;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LANPUT.Core;
using LANPUT.Desktop;

AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
int passed = 0, failed = 0;
void Assert(bool value, string message) { if (!value) throw new Exception(message); }
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + " · " + e); }
}

Test("Desktop window loads real XAML and controls at minimum size", () =>
{
    var window = new MainWindow();
    window.Measure(new Size(860, 600)); window.Arrange(new Rect(0, 0, 860, 600));
    Assert(window.FindControl<Button>("ReceiveButton") is not null, "Missing receiver control");
    Assert(window.FindControl<Slider>("Progress") is not null, "Missing progress slider");
    Assert(window.FindControl<Grid>("CinemaScene")?.IsVisible == true, "Cinema must be the initial scene");
    Assert(window.FindControl<Grid>("MusicScene")?.IsVisible == false, "Music must not cover idle screen");
});
Test("Device description identifies the actual operating system", () =>
{
    var xml = XmlSafe.Parse(UpnpProtocol.Description("LANPUT", Guid.NewGuid(), new Uri("http://192.168.1.2:41000/")));
    Assert(xml.Descendants().Single(x => x.Name.LocalName == "modelName").Value == ReceiverIdentity.Model, "Incorrect model identity");
    Assert(ReceiverIdentity.Server.Contains(ReceiverIdentity.Platform + "/"), "Incorrect server identity");
});
Test("Platform update rejects wrong OS, CPU, EXE and ambiguous asset names", () =>
{
    foreach (var rid in new[] { "osx-arm64", "osx-x64", "linux-x64", "linux-arm64" })
    {
        var extension = rid.StartsWith("osx") ? ".dmg" : ".deb";
        PlatformUpdatePolicy.Validate(new("0.9.4", "https://example.com/LANPUT-0.9.4-preview-" + rid + extension), rid);
    }
    foreach (var asset in new[] { "LANPUT-Setup-x64.exe", "LANPUT-osx-x64.dmg", "LANPUT-linux-arm64.deb", "LANPUT-linux-x64.exe", "LANPUT-linux-x64evil.deb" })
    {
        try { PlatformUpdatePolicy.Validate(new("0.9.4", "https://example.com/" + asset), "linux-x64"); throw new Exception("Accepted " + asset); }
        catch (FormatException) { }
    }
});
Test("Settings dialog loads without starting playback or a receiver", () =>
{
    var dialog = new SettingsWindow(new DesktopSettings());
    dialog.Measure(new Size(620, 670)); dialog.Arrange(new Rect(0, 0, 620, 670));
    Assert(dialog.Content is ScrollViewer, "Settings must remain scrollable");
});

if (args.Contains("--screenshots"))
{
    Test("Render cinema and music at normal and minimum window sizes", () =>
    {
        string output = Environment.GetEnvironmentVariable("LANPUT_SCREENSHOTS") ?? Path.Combine(Path.GetTempPath(), "lanput-desktop-previews");
        Directory.CreateDirectory(output);
        var window = new MainWindow(initializePlayback: false);
        window.Show();
        foreach (var size in new[] { new Size(1180, 780), new Size(860, 600) })
        {
            foreach (var scene in new[] { "cinema", "music" })
            {
                window.FindControl<Grid>("CinemaScene")!.IsVisible = scene == "cinema";
                window.FindControl<Grid>("MusicScene")!.IsVisible = scene == "music";
                window.FindControl<TextBlock>("SongTitle")!.Text = "LANPUT · 私人音乐厅";
                window.FindControl<TextBlock>("ArtistText")!.Text = "手机选好音乐，剩下的交给 LANPUT";
                window.FindControl<TextBlock>("CurrentLyric")!.Text = "让音乐填满这一刻。";
                window.Width = size.Width; window.Height = size.Height;
                window.Measure(size); window.Arrange(new Rect(size));
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var bitmap = window.CaptureRenderedFrame() ?? throw new Exception("No rendered frame");
                bitmap.Save(Path.Combine(output, scene + "-" + size.Width + ".png"));
            }
        }
    });
}

if (args.Contains("--native"))
{
    Test("Native VLC opens audio, reports music identity, seeks and stops", () =>
    {
        var file = Path.Combine(Path.GetTempPath(), "lanput-fixture-" + Guid.NewGuid() + ".wav");
        try
        {
            using (var output = new BinaryWriter(File.Create(file)))
            {
                const int sampleRate = 8000, count = sampleRate * 15;
                output.Write(Encoding.ASCII.GetBytes("RIFF")); output.Write(36 + count * 2); output.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                output.Write(16); output.Write((short)1); output.Write((short)1); output.Write(sampleRate); output.Write(sampleRate * 2);
                output.Write((short)2); output.Write((short)16); output.Write(Encoding.ASCII.GetBytes("data")); output.Write(count * 2);
                for (int i = 0; i < count; i++) output.Write((short)(Math.Sin(i * 2 * Math.PI * 440 / sampleRate) * 200));
            }
            using var player = new VlcPlayback(new UiDispatcher(), Console.WriteLine, engineOptions: ["--aout=dummy", "--vout=dummy"]);
            player.LoadLocal(file); player.Play();
            var wait = System.Diagnostics.Stopwatch.StartNew();
            while (wait.Elapsed.TotalSeconds < 8)
            {
                Dispatcher.UIThread.RunJobs(); player.Poll();
                if (player.IsAudioOnly && player.Snapshot.PositionMs > 100) break;
                Thread.Sleep(50);
            }
            Assert(player.IsAudioOnly, "Audio track was not recognized");
            Assert(player.Snapshot.State == "PLAYING", "Native playback did not start");
            Assert(player.Snapshot.DurationMs >= 14000, "Incorrect duration");
            Assert(player.Snapshot.Uri == "urn:lancast:local-test", "Local path leaked to UPnP");
            player.Pause(); Assert(player.Snapshot.State == "PAUSED_PLAYBACK", "Pause was not applied");
            player.Seek(3000); Assert(player.Snapshot.PositionMs == 3000, "Seek was not applied");
            player.Stop(); Assert(player.Snapshot.State == "STOPPED", "Stop was not applied");
        }
        finally { File.Delete(file); }
    });
    Test("Real HTTP SOAP receiver accepts remote playback commands and closes its port", () =>
    {
        // Run the HTTP client on a worker while pumping the UI, matching real
        // Kestrel -> UI dispatcher commands without a blocking test deadlock.
        var player = new ProtocolPlayer();
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var host = new ReceiverHost(new("loopback", IPAddress.Loopback, IPAddress.Parse("255.0.0.0")), "LANPUT Test", Guid.NewGuid(), port, player, Console.WriteLine);
        var task = Task.Run(async () =>
        {
            await host.StartAsync(discovery: false);
            try
            {
                using var http = new HttpClient();
                var origin = "http://127.0.0.1:" + port;
                var health = await http.GetStringAsync(origin + "/health");
                Assert(health.Contains("LANPUT"), "Health endpoint failed");
                string body = "<s:Envelope xmlns:s='http://schemas.xmlsoap.org/soap/envelope/'><s:Body><u:SetAVTransportURI xmlns:u='" + UpnpProtocol.Av
                    + "'><InstanceID>0</InstanceID><CurrentURI>https://example.com/test.mp3</CurrentURI><CurrentURIMetaData></CurrentURIMetaData></u:SetAVTransportURI></s:Body></s:Envelope>";
                using var request = new HttpRequestMessage(HttpMethod.Post, origin + "/AVTransport/control");
                request.Headers.Add("SOAPACTION", "\"" + UpnpProtocol.Av + "#SetAVTransportURI\"");
                request.Content = new StringContent(body, Encoding.UTF8, "text/xml");
                using var response = await http.SendAsync(request);
                Assert(response.IsSuccessStatusCode, "SOAP load failed");
                Assert(player.Snapshot.Uri == "https://example.com/test.mp3", "SOAP command did not reach player");
            }
            finally { await host.DisposeAsync(); }
        });
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!task.IsCompleted && timeout.Elapsed.TotalSeconds < 15) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
        Assert(task.IsCompleted, "Receiver did not shut down in time"); task.GetAwaiter().GetResult();
        var check = new TcpListener(IPAddress.Loopback, port); check.Start(); check.Stop();
    });
}
Console.WriteLine($"{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

sealed class ProtocolPlayer : IPlayback
{
    PlaybackSnapshot snapshot = new();
    public PlaybackSnapshot Snapshot => Volatile.Read(ref snapshot);
    public void Load(string uri, string metadata, string title) => Volatile.Write(ref snapshot, new(Uri: uri, Metadata: metadata, Title: title, State: "STOPPED"));
    public void Play() { }
    public void Pause() { }
    public void Stop() { }
    public void Seek(long milliseconds) { }
    public void SetVolume(int volume) { }
    public void SetMute(bool muted) { }
}
