using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LANPUT.Core;

namespace LANPUT.Desktop;

public sealed partial class MainWindow : Window
{
    DesktopSettings settings = DesktopSettings.Load();
    VlcPlayback? playback;
    ReceiverHost? receiver;
    readonly SemaphoreSlim receiverGate = new(1);
    readonly CancellationTokenSource lifetime = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    readonly HttpClient lyricsHttp = new() { Timeout = TimeSpan.FromSeconds(12) };
    readonly LyricsClient lyricsClient;
    readonly List<string> messages = [];
    readonly RotatingLog log = new(Path.Combine(DesktopSettings.DataDirectory, "logs", $"receiver-{DateTime.Now:yyyyMMdd-HHmmss}.log"));
    readonly Stopwatch clock = Stopwatch.StartNew();
    CancellationTokenSource? lyricsRequest;
    LrcLyrics? lyrics;
    string plainLyrics = "", lyricsKey = "";
    int lyricsAdjustment;
    long revision = -1;
    bool closing, shutdownComplete, scrubbing, modal;
    string shown = "cinema", target = "cinema";
    bool changing;
    double changeStart, lastTick;

    public MainWindow() : this(true) { }
    internal MainWindow(bool initializePlayback)
    {
        InitializeComponent();
        lyricsClient = new LyricsClient(lyricsHttp);
        DeviceText.Text = settings.DeviceName + " · 跨平台预览";
        Volume.Value = settings.Volume;
        Progress.AddHandler(PointerPressedEvent, (_, _) => scrubbing = true, RoutingStrategies.Tunnel);
        Progress.AddHandler(PointerReleasedEvent, (_, _) => CommitSeek(), RoutingStrategies.Tunnel);
        Progress.AddHandler(PointerCaptureLostEvent, (_, _) => CommitSeek());
        Progress.KeyUp += (_, e) => { if (e.Key is Key.Left or Key.Right or Key.Home or Key.End) CommitSeek(); };
        Volume.PropertyChanged += (_, e) => { if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty) Safe(() => playback?.SetVolume((int)Volume.Value)); };
        KeyDown += OnKeyDown;
        if (initializePlayback) Opened += OnOpened;
        Closing += OnClosing;
        timer.Tick += (_, _) => Tick();
    }

    async void OnOpened(object? sender, EventArgs args)
    {
        try
        {
            playback = new VlcPlayback(new UiDispatcher(), Log);
            Video.MediaPlayer = playback.Player;
            Log("播放引擎已就绪 · " + DesktopSettings.PlatformId);
            playback.SetVolume(settings.Volume);
            timer.Start();
            if (settings.Animations) BeginChange("cinema", "欢迎来到你的私人影院");
            if (settings.AutoReceive) await StartReceiverAsync();
            else ServiceText.Text = "接收尚未开启";
        }
        catch (Exception e)
        {
            Log("播放引擎初始化失败 · " + e.GetType().Name);
            ServiceText.Text = "播放引擎未就绪";
            IdleDetail.Text = NativeVlc.Help;
            if (Program.SmokeTest) Environment.ExitCode = 1;
            else await ShowTextAsync("需要播放组件", NativeVlc.Help);
        }
    }

    void Log(string text)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {text}";
        if (Program.SmokeTest) Console.WriteLine(line);
        log.Append(line + Environment.NewLine);
        Dispatcher.UIThread.Post(() => { messages.Add(line); if (messages.Count > 120) messages.RemoveAt(0); });
    }

    void Safe(Action action)
    {
        if (closing) return;
        try { action(); }
        catch (Exception e) { Log("操作失败 · " + e.GetType().Name); PlaybackStatus.Text = "操作未完成，请查看诊断"; }
    }

    async Task StartReceiverAsync()
    {
        await receiverGate.WaitAsync();
        try
        {
            if (closing || receiver is not null || playback is null) return;
            var networks = NetworkChoice.Enumerate();
            var network = networks.FirstOrDefault(n => n.Address.ToString() == settings.NetworkAddress) ?? networks.FirstOrDefault();
            if (network is null) { ServiceText.Text = "未找到局域网，请连接 Wi-Fi 或网线后重试"; return; }
            ServiceText.Text = "正在开启接收";
            var candidate = new ReceiverHost(network, settings.DeviceName, settings.DeviceId, 41000, playback, Log);
            try { await candidate.StartAsync(); receiver = candidate; }
            catch { await candidate.DisposeAsync(); throw; }
            settings = settings with { NetworkAddress = network.Address.ToString() };
            SaveSettings();
            ServiceText.Text = "接收中 · " + network.Address;
            ReceiveButton.Content = "停止接收";
        }
        catch (Exception e) { Log("接收启动失败 · " + e.GetType().Name); ServiceText.Text = "接收失败，请检查网卡、端口与网络权限"; }
        finally { receiverGate.Release(); }
    }

    async Task StopReceiverAsync()
    {
        await receiverGate.WaitAsync();
        try
        {
            var previous = receiver; receiver = null;
            if (previous is not null) await previous.DisposeAsync();
            ServiceText.Text = "接收已停止";
            ReceiveButton.Content = "开始接收";
        }
        catch (Exception e) { Log("接收停止失败 · " + e.GetType().Name); }
        finally { receiverGate.Release(); }
    }

    async void Receive_Click(object? sender, RoutedEventArgs e)
    {
        ReceiveButton.IsEnabled = false;
        try { if (receiver is null) await StartReceiverAsync(); else await StopReceiverAsync(); }
        finally { ReceiveButton.IsEnabled = true; }
    }

    void Tick()
    {
        if (playback is null || closing) return;
        try
        {
            playback.Poll();
            var p = playback.Snapshot;
            var now = clock.Elapsed.TotalSeconds;
            var dt = Math.Min(.2, now - lastTick); lastTick = now;
            if (playback.MediaRevision != revision)
            {
                revision = playback.MediaRevision;
                lyricsRequest?.Cancel(); lyricsKey = ""; lyrics = null; plainLyrics = ""; lyricsAdjustment = 0;
                // A source replacement invalidates an unfinished transition.
                if (changing) { changing = false; playback.SetPresentationHold(false); }
                if (shown == "video") shown = "cinema";
            }
            var active = p.State is "PLAYING" or "PAUSED_PLAYBACK" or "TRANSITIONING";
            string next = !active || p.Status != "OK" ? "cinema" : playback.IsAudioOnly ? "music"
                : CinemaPresentation.ShouldImmerse(p, playback.HasVideoOutput, shown == "video" || changing && target == "video") ? "video" : "cinema";
            if (changing && next != target && next != "cinema") BeginChange(next);
            else if (changing && !active && target != "cinema") BeginChange("cinema");
            else if (!changing && next != shown) BeginChange(next);
            if (changing)
            {
                double progress = Math.Clamp((now - changeStart) / 2.6, 0, 1);
                double opening = Math.Clamp((progress - .62) / .38, 0, 1);
                double eased = opening * opening * (3 - 2 * opening);
                ((TranslateTransform)LeftCurtain.RenderTransform!).X = -TransitionScene.Bounds.Width * .5 * eased;
                ((TranslateTransform)RightCurtain.RenderTransform!).X = TransitionScene.Bounds.Width * .5 * eased;
                TransitionScene.Opacity = progress > .9 ? (1 - progress) / .1 : 1;
                if (progress >= 1) FinishChange();
            }
            ApplyScenes();
            MediaTitle.Text = playback.IsAudioOnly && playback.Music.Title.Length > 0 ? playback.Music.Title : p.Title;
            PlaybackStatus.Text = playback.PresentationLabel;
            PlaybackTimeline.IsVisible = !playback.IsImage;
            PlayButton.Content = p.State == "PLAYING" ? "暂停" : "播放";
            PositionText.Text = XmlSafe.Time(p.PositionMs); DurationText.Text = XmlSafe.Time(p.DurationMs);
            Progress.IsEnabled = playback.CanSeek;
            if (!scrubbing) { Progress.Maximum = Math.Max(1, p.DurationMs); Progress.Value = Math.Clamp(p.PositionMs, 0, Progress.Maximum); }
            if (!Volume.IsPointerOver) Volume.Value = p.Volume;
            MuteButton.Content = p.Muted ? "取消静音" : "静音";
            if (playback.IsAudioOnly)
            {
                SongTitle.Text = playback.Music.Title; ArtistText.Text = playback.Music.Artist; AlbumText.Text = playback.Music.Album;
                if (shown == "music" && !changing && settings.Animations && p.State == "PLAYING") Record.Angle = (Record.Angle + dt * 20) % 360;
                var key = playback.Music.Title + "\n" + playback.Music.Artist;
                if (settings.OnlineLyrics && lyricsKey != key && playback.Music.Title.Length > 0)
                { lyricsKey = key; _ = FindLyricsAsync(); }
                UpdateLyrics(p.PositionMs);
            }
        }
        catch (Exception e) { timer.Stop(); Log("播放状态刷新失败 · " + e.GetType().Name); PlaybackStatus.Text = "播放状态异常，请重新打开软件并查看诊断"; }
    }

    void BeginChange(string mode, string? caption = null)
    {
        target = mode;
        if (!settings.Animations) { FinishChange(); return; }
        changing = true; changeStart = clock.Elapsed.TotalSeconds;
        ((TranslateTransform)LeftCurtain.RenderTransform!).X = 0;
        ((TranslateTransform)RightCurtain.RenderTransform!).X = 0;
        TransitionTitle.Text = caption ?? (mode == "music" ? "换一个舞台，继续沉浸。" : mode == "video" ? "好戏马上上演" : "好座位，已经为你留好。");
        playback?.SetPresentationHold(mode != "cinema");
        // Keep the native video hidden throughout the entire transition.
        Video.IsVisible = false;
    }
    void FinishChange()
    {
        shown = target; changing = false; TransitionScene.Opacity = 1;
        playback?.SetPresentationHold(false);
        ApplyScenes();
    }
    void ApplyScenes()
    {
        Video.IsVisible = shown == "video" && !changing && !modal;
        CinemaScene.IsVisible = shown == "cinema" && !changing;
        MusicScene.IsVisible = shown == "music" && !changing;
        TransitionScene.IsVisible = changing;
    }

    async Task FindLyricsAsync()
    {
        if (playback is null || !playback.IsAudioOnly || closing) return;
        lyricsRequest?.Cancel();
        var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        lyricsRequest = request;
        var song = playback.Music; var mediaRevision = playback.MediaRevision;
        LyricsStatus.Text = "正在搜索歌词…";
        try
        {
            var records = await lyricsClient.SearchAsync(song, request.Token);
            request.Token.ThrowIfCancellationRequested();
            if (playback.MediaRevision != mediaRevision) return;
            var match = LyricsClient.PreferredMatch(records, song, playback.Snapshot.DurationMs / 1000d);
            lyrics = match?.SyncedLyrics is { Length: > 0 } lrc ? LrcLyrics.Parse(lrc) : null;
            plainLyrics = match?.PlainLyrics ?? "";
            LyricsStatus.Text = match is null ? "未找到歌词，可以导入 LRC" : match.Instrumental ? "纯音乐，请尽情聆听" : "已自动载入 · LRCLIB · " + match.ArtistName;
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Log("歌词搜索失败 · " + e.GetType().Name); if (!closing && playback.MediaRevision == mediaRevision) LyricsStatus.Text = "歌词暂不可用，可以重试或导入 LRC"; }
        finally { if (ReferenceEquals(lyricsRequest, request)) lyricsRequest = null; request.Dispose(); }
    }
    void UpdateLyrics(long position)
    {
        var index = lyrics?.IndexAt(position, lyricsAdjustment) ?? -1;
        string Line(int i) => lyrics is not null && i >= 0 && i < lyrics.Lines.Count ? lyrics.Lines[i].Text : "";
        PreviousLyric.Text = Line(index - 1);
        CurrentLyric.Text = index >= 0 ? Line(index) : plainLyrics.Length > 0 ? plainLyrics.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) : "让音乐填满这一刻。";
        NextLyric.Text = Line(index + 1);
    }
    void CommitSeek()
    {
        scrubbing = false;
        if (playback?.CanSeek == true) Safe(() => playback.Seek((long)Progress.Value));
    }
    void Play_Click(object? sender, RoutedEventArgs e) => Safe(() => { if (playback?.Snapshot.State == "PLAYING") playback.Pause(); else playback?.Play(); });
    void Stop_Click(object? sender, RoutedEventArgs e) => Safe(() => playback?.Stop());
    void Mute_Click(object? sender, RoutedEventArgs e) => Safe(() => playback?.SetMute(!playback.Snapshot.Muted));
    void Fullscreen_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
    void Skip_Click(object? sender, RoutedEventArgs e) => FinishChange();
    async void SearchLyrics_Click(object? sender, RoutedEventArgs e) => await FindLyricsAsync();
    void LyricsEarlier_Click(object? sender, RoutedEventArgs e) => lyricsAdjustment = Math.Clamp(lyricsAdjustment + 500, -600000, 600000);
    void LyricsLater_Click(object? sender, RoutedEventArgs e) => lyricsAdjustment = Math.Clamp(lyricsAdjustment - 500, -600000, 600000);

    async void Open_Click(object? sender, RoutedEventArgs e)
    {
        if (playback is null) return;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "打开视频或音乐", AllowMultiple = false });
            if (closing) return;
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (path is not null) Safe(() => { playback.LoadLocal(path); playback.Play(); });
        }
        catch (Exception ex) { Log("文件选择失败 · " + ex.GetType().Name); }
    }
    async void ImportLyrics_Click(object? sender, RoutedEventArgs e)
    {
        var original = playback?.MediaRevision;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "导入 LRC", FileTypeFilter = [new FilePickerFileType("LRC 歌词") { Patterns = ["*.lrc"] }] });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (path is null || closing || original != playback?.MediaRevision) return;
            if (new FileInfo(path).Length > 1_000_000) throw new InvalidDataException();
            var parsed = LrcLyrics.Parse(await File.ReadAllTextAsync(path, lifetime.Token));
            if (original != playback?.MediaRevision || closing) return;
            lyricsRequest?.Cancel(); lyrics = parsed; plainLyrics = "";
            LyricsStatus.Text = parsed.Lines.Count > 0 ? "已载入本地歌词" : "文件中没有带时间标记的歌词";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log("歌词导入失败 · " + ex.GetType().Name); LyricsStatus.Text = "导入失败，请选择 UTF-8 编码的 LRC 文件"; }
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { if (changing) FinishChange(); else if (WindowState == WindowState.FullScreen) WindowState = WindowState.Normal; e.Handled = true; }
        if (e.Source is TextBox or Slider || modal) return;
        if (e.Key == Key.F11) { Fullscreen_Click(null, new()); e.Handled = true; }
        if (e.Key == Key.Space) { Play_Click(null, new()); e.Handled = true; }
    }
    async void PlaybackInfo_Click(object? sender, RoutedEventArgs e)
    { if (playback is not null) await ShowTextAsync("播放信息", playback.ReadPlaybackInfo().ToDisplayText()); }
    async void Diagnostics_Click(object? sender, RoutedEventArgs e) => await ShowTextAsync("连接诊断",
        "平台：" + DesktopSettings.PlatformId + "\n设备：" + settings.DeviceName + "\n数据目录：" + DesktopSettings.DataDirectory
        + "\n\n手机与电脑需要同一局域网；允许 UDP 1900 组播与 TCP 41000 入站。\nmacOS 请允许 LANPUT 的本地网络访问及防火墙入站。\nLinux 请检查防火墙、VPN 及路由器的设备隔离。\n\n" + string.Join('\n', messages));

    async Task ShowTextAsync(string title, string text)
    {
        var close = new Button { Content = "关闭", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        var dialog = new Window { Title = title, Width = 660, Height = 480, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        close.Click += (_, _) => dialog.Close();
        var panel = new DockPanel { Margin = new Thickness(24), LastChildFill = true };
        DockPanel.SetDock(close, Dock.Bottom); panel.Children.Add(close);
        panel.Children.Add(new TextBox { Text = text, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap });
        dialog.Content = panel;
        modal = true; ApplyScenes();
        try { await dialog.ShowDialog(this); }
        finally { modal = false; ApplyScenes(); }
    }
    async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(settings);
        modal = true; ApplyScenes();
        try
        {
            var updated = await dialog.ShowDialog<DesktopSettings?>(this);
            if (updated is null || closing) return;
            bool restart = receiver is not null && (updated.DeviceName != settings.DeviceName || updated.NetworkAddress != settings.NetworkAddress);
            settings = updated; DeviceText.Text = settings.DeviceName + " · 跨平台预览"; SaveSettings();
            if (!settings.Animations && changing) FinishChange();
            if (!settings.OnlineLyrics) lyricsRequest?.Cancel();
            if (restart) { await StopReceiverAsync(); await StartReceiverAsync(); }
        }
        finally { modal = false; ApplyScenes(); }
    }
    void SaveSettings()
    {
        try { settings.Save(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log("设置未能保存 · " + e.GetType().Name); }
    }
    async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (shutdownComplete) return;
        e.Cancel = true;
        if (closing) return;
        closing = true; timer.Stop(); lifetime.Cancel(); lyricsRequest?.Cancel();
        settings = settings with { Volume = (int)Volume.Value }; SaveSettings();
        try
        {
            // Drain HTTP commands while the UI dispatcher remains alive.
            await StopReceiverAsync();
            Video.MediaPlayer = null;
            playback?.Dispose();
        }
        catch (Exception ex) { Log("退出清理失败 · " + ex.GetType().Name); }
        finally { lyricsHttp.Dispose(); shutdownComplete = true; Close(); }
    }
}
