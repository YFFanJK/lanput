using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows.Threading;
using LANPUT.Core;
using LibVLCSharp.Shared;

namespace LANPUT.Windows;

public sealed partial class VlcPlayback : IPlayback, IDisposable
{
    readonly Dispatcher dispatcher;
    readonly Action<string> log;
    readonly object nativeLogGate = new();
    readonly HashSet<string> nativeLabels = [];
    LibVLC vlc = null!;
    public MediaPlayer Player { get; private set; } = null!;
    Media? media;
    PlaybackSnapshot snapshot = new();
    string source = "";
    bool local, compatibility, disposed, requested, everPlayed, warnedSlow, warnedNoVideo, pauseRequested;
    long generation;
    long mediaRevision;
    bool presentationHeld, presentationPauseIssued;
    int videoOutputs;
    int imageInput;
    int accessErrorCode;
    bool hadVideoOutput;
    long furthestPositionMs;
    readonly Stopwatch attempt = new();
    string previousNativeState = "", activityNativeState = "Stopped";
    double? lastBufferPercent;
    public PlaybackSnapshot Snapshot => Volatile.Read(ref snapshot);
    public bool AllowsTransitionPause => Volatile.Read(ref presentationHeld);
    public event Action? PresentationStateChanged;
    public long MediaRevision => Interlocked.Read(ref mediaRevision);
    public bool IsLocal => local;
    public bool IsImage => media is not null && Volatile.Read(ref imageInput) != 0;
    public string PresentationLabel => Activity.LabelFor(IsImage);
    public bool HasVideoOutput => videoOutputs > 0;
    public bool CanSeek => !IsImage && snapshot.Uri.Length > 0 && Player.IsSeekable && Player.Length > 0;
    public PlaybackActivity Activity => PlaybackActivity.Resolve(Snapshot, activityNativeState, HasVideoOutput || IsAudioOnly, lastBufferPercent);
    public VlcPlayback(Dispatcher dispatcher, Action<string> log, bool compatibilityRendering = false)
    {
        this.dispatcher = dispatcher; this.log = log; compatibility = compatibilityRendering;
        LibVLCSharp.Shared.Core.Initialize(); CreateEngine();
    }
    void CreateEngine()
    {
        var options = new List<string> { "--no-video-title-show", "--no-osd", "--no-sub-autodetect-file", "--network-caching=1500" };
        if (compatibility) options.AddRange(["--avcodec-hw=none", "--vout=direct3d9"]);
        vlc = new LibVLC(options.ToArray());
        // Use one truthful, explicit identity for VLC and resource-chain diagnostics.
        vlc.SetUserAgent("LANPUT " + ReceiverIdentity.Version, HlsProbe.Agent);
        vlc.Log += (_, e) =>
        {
            if (Volatile.Read(ref requested) && PlaybackDiagnostics.AccessErrorCode(e.Message) is { } code)
                Interlocked.Exchange(ref accessErrorCode, code);
            if (e.Message.Contains("using demux module \"image\"", StringComparison.Ordinal))
                Interlocked.Exchange(ref imageInput, 1);
            var label = PlaybackDiagnostics.Classify(e.Message);
            if (label is null) return;
            lock (nativeLogGate) { if (!nativeLabels.Add(label)) return; }
            log("引擎诊断 · " + label);
        };
        Player = new MediaPlayer(vlc) { Volume = snapshot.Volume, Mute = snapshot.Muted };
        var activePlayer = Player;
        void Queue(Action action)
        {
            // Native callbacks must not synchronously call WPF or call back into LibVLC.
            var epoch = Interlocked.Read(ref generation);
            dispatcher.InvokeAsync(() =>
            {
                if (!disposed && ReferenceEquals(activePlayer, Player) && epoch == generation) action();
            });
        }
        activePlayer.Opening += (_, _) => Queue(() =>
        {
            if (!requested || snapshot.State == "PAUSED_PLAYBACK") return;
            activityNativeState = "Opening"; lastBufferPercent = null;
            log("引擎事件 · 正在打开媒体");
        });
        activePlayer.Buffering += (_, args) =>
        {
            var cache = args.Cache;
            Queue(() =>
            {
                if (!requested || snapshot.State == "PAUSED_PLAYBACK") return;
                activityNativeState = "Buffering";
                lastBufferPercent = PlaybackActivity.ValidPercent(cache);
            });
        };
        activePlayer.Playing += (_, _) => Queue(() =>
        {
            if (!requested || pauseRequested) return;
            everPlayed = true; activityNativeState = "Playing"; lastBufferPercent = null;
            Volatile.Write(ref snapshot, snapshot with { State = "PLAYING", Status = "OK" });
            log("引擎事件 · 已进入播放状态（正在识别媒体轨道）");
        });
        activePlayer.EncounteredError += (_, _) => Queue(() =>
        {
            if (requested) Fail("引擎事件 · 打开或播放失败，请查看引擎诊断及媒体连通性检测。");
        });
        activePlayer.EndReached += (_, _) => Queue(() =>
        {
            if (!requested) return;
            FinishAttempt();
        });
        activePlayer.Vout += (_, e) =>
        {
            var count = e.Count;
            Queue(() => { videoOutputs = (int)count; if (count > 0) { hadVideoOutput = true; log("引擎事件 · 视频输出已建立"); } });
        };
    }
    void OnUi(Action action) => dispatcher.Invoke(action);
    public void ConfigureCompatibility(bool enabled) => OnUi(() =>
    {
        if (compatibility == enabled) return;
        generation++; Interlocked.Increment(ref mediaRevision); requested = false; pauseRequested = false;
        presentationHeld = presentationPauseIssued = false;
        Player.Stop(); Player.Dispose(); media?.Dispose(); media = null; vlc.Dispose();
        source = ""; local = false; compatibility = enabled;
        activityNativeState = "Stopped"; lastBufferPercent = null;
        Volatile.Write(ref snapshot, new PlaybackSnapshot(Volume:snapshot.Volume, Muted:snapshot.Muted));
        CreateEngine();
        log(enabled ? "已启用兼容渲染：软件解码 / Direct3D 9，请重新投屏或选择本地视频。" : "已恢复默认渲染，请重新投屏或选择本地视频。");
    });
    public void Load(string uri, string metadata, string title) => OnUi(() => LoadCore(uri, metadata, title, false));
    public void LoadLocal(string path) => OnUi(() => LoadCore(path, "", MediaPresentation.LocalTitle(path), true));
    void LoadCore(string input, string metadata, string title, bool isLocal)
    {
        Interlocked.Increment(ref mediaRevision);
        presentationHeld = presentationPauseIssued = false;
        requested = false; pauseRequested = false;
        Player.Stop(); generation++; media?.Dispose(); media = null;
        lock (nativeLogGate) nativeLabels.Clear();
        Interlocked.Exchange(ref imageInput, MediaPresentation.IsImage(input, metadata, isLocal) ? 1 : 0);
        source = input; local = isLocal; everPlayed = warnedSlow = warnedNoVideo = false; videoOutputs = 0;
        hadVideoOutput = false; furthestPositionMs = 0; Interlocked.Exchange(ref accessErrorCode, 0);
        previousNativeState = ""; activityNativeState = "Stopped"; lastBufferPercent = null; attempt.Reset();
        // Keep signed query strings unchanged. Phone metadata never becomes engine options.
        media = new Media(vlc, input, isLocal ? FromType.FromPath : FromType.FromLocation);
        media.AddOption(":network-caching=1500");
        // Only the image demuxer consumes this option; videos and music still end normally.
        // Apply before opening, including images without extensions or UPnP metadata.
        media.AddOption(":image-duration=-1");
        if (!isLocal) media.AddOption(":http-user-agent=" + HlsProbe.Agent);
        if (compatibility) media.AddOption(":avcodec-hw=none");
        Player.Media = media;
        // Do not expose local file paths to network controllers.
        Volatile.Write(ref snapshot, snapshot with { Uri = isLocal ? "urn:lancast:local-test" : input, Metadata = metadata, Title = title,
            State = "STOPPED", Status = "OK", PositionMs = 0, DurationMs = 0 });
        log("已加载媒体 · " + (isLocal ? "本地文件（路径已隐藏）" : PlaybackDiagnostics.DescribeSource(input)));
        if (!isLocal) log("播放 HTTP 客户端标识 · " + HlsProbe.Agent);
        PresentationStateChanged?.Invoke();
    }
    public void Play() => OnUi(() =>
    {
        if (media is null) throw new UpnpException(701, "Transition not available");
        if (presentationHeld && requested)
        {
            pauseRequested = false;
            presentationPauseIssued |= requested && Player.CanPause;
            Volatile.Write(ref snapshot, snapshot with { State = "TRANSITIONING", Status = "OK" });
            return;
        }
        presentationHeld = presentationPauseIssued = false;
        if (requested && (snapshot.State is "PLAYING" or "TRANSITIONING")) { log("重复 Play 已忽略，保留当前打开/播放过程。"); return; }
        everPlayed = snapshot.State == "PAUSED_PLAYBACK";
        if (!everPlayed)
        {
            hadVideoOutput = false; furthestPositionMs = 0; Interlocked.Exchange(ref accessErrorCode, 0);
            lock (nativeLogGate) nativeLabels.Clear();
        }
        pauseRequested = false;
        requested = true; warnedSlow = warnedNoVideo = false; attempt.Restart();
        activityNativeState = "Opening"; lastBufferPercent = null;
        Volatile.Write(ref snapshot, snapshot with { State = "TRANSITIONING", Status = "OK" });
        log($"开始播放 · 视频窗口{(Player.Hwnd == IntPtr.Zero ? "未绑定" : "已绑定")} · {(compatibility ? "兼容渲染" : "默认渲染")}");
        if (!Player.Play()) { Fail("引擎未接受播放请求。"); throw new UpnpException(704, "Format not supported for playback"); }
    });
    public void Pause() => OnUi(() => { pauseRequested = true; Player.SetPause(true); activityNativeState = "Paused"; lastBufferPercent = null; Volatile.Write(ref snapshot, snapshot with { State = "PAUSED_PLAYBACK" }); });
    public void Stop() => OnUi(() =>
    {
        presentationHeld = presentationPauseIssued = false;
        requested = false; pauseRequested = false; attempt.Reset(); Player.Stop(); generation++;
        activityNativeState = "Stopped"; lastBufferPercent = null;
        Volatile.Write(ref snapshot, snapshot with { State = snapshot.Uri.Length == 0 ? "NO_MEDIA_PRESENT" : "STOPPED", PositionMs = 0 });
        PresentationStateChanged?.Invoke();
    });
    public void Seek(long milliseconds) => OnUi(() =>
    {
        if (!CanSeek) throw new UpnpException(710, "Seek mode not supported");
        Player.Time = milliseconds;
        Volatile.Write(ref snapshot, snapshot with { PositionMs = milliseconds });
    });
    public void SetVolume(int volume) => OnUi(() => { Player.Volume = Math.Clamp(volume, 0, 100); Volatile.Write(ref snapshot, snapshot with { Volume = Math.Clamp(volume,0,100) }); });
    public void SetMute(bool muted) => OnUi(() => { Player.Mute = muted; Volatile.Write(ref snapshot, snapshot with { Muted = muted }); });
    public void Poll()
    {
        if (disposed || snapshot.Uri.Length == 0) return;
        videoOutputs = (int)Player.VoutCount;
        RefreshMusic();
        if (videoOutputs > 0) hadVideoOutput = true;
        if (requested) furthestPositionMs = Math.Max(furthestPositionMs, Math.Max(0, Player.Time));
        var state = Player.State;
        activityNativeState = state.ToString();
        if (state != VLCState.Buffering) lastBufferPercent = null;
        if (state.ToString() != previousNativeState) { previousNativeState = state.ToString(); log("引擎状态 · " + previousNativeState); }
        if (state == VLCState.Error && requested) { Fail("引擎状态为 Error，请查看诊断分类。"); return; }
        string transport = snapshot.State;
        if (requested)
        {
            transport = pauseRequested ? "PAUSED_PLAYBACK" : presentationHeld ? "TRANSITIONING" : state switch
            {
                VLCState.Playing => "PLAYING", VLCState.Paused => "PAUSED_PLAYBACK",
                VLCState.Opening or VLCState.Buffering => "TRANSITIONING", _ => snapshot.State
            };
            if (state == VLCState.Playing) everPlayed = true;
            if ((state is VLCState.Stopped or VLCState.Ended) && attempt.Elapsed.TotalSeconds > 2)
            {
                FinishAttempt(); return;
            }
            if (!everPlayed && !warnedSlow && attempt.Elapsed.TotalSeconds > 15)
            { warnedSlow = true; log("15 秒仍未进入播放状态，请点击“检测当前媒体”；暂未证实 DRM 问题。"); }
            if (everPlayed && !IsAudioOnly && videoOutputs == 0 && !warnedNoVideo && attempt.Elapsed.TotalSeconds > 10 && state == VLCState.Playing)
            {
                warnedNoVideo = true;
                log($"播放中但无视频输出 · 视频轨数 {Player.VideoTrackCount} · 窗口{(Player.Hwnd == IntPtr.Zero ? "未绑定" : "已绑定")}；请对照测试本地视频及兼容渲染。");
            }
        }
        Volatile.Write(ref snapshot, snapshot with { State = transport, PositionMs = IsImage ? 0 : Math.Max(0, Player.Time), DurationMs = IsImage ? 0 : Math.Max(0, Player.Length) });
    }
    public void SetPresentationHold(bool hold)
    {
        dispatcher.VerifyAccess();
        if (disposed || presentationHeld == hold) return;
        presentationHeld = hold;
        if (hold)
        {
            presentationPauseIssued = requested && !pauseRequested && Player.CanPause;
            if (presentationPauseIssued) Player.SetPause(true);
            if (requested && !pauseRequested)
                Volatile.Write(ref snapshot, snapshot with { State = "TRANSITIONING" });
        }
        else
        {
            bool resume = presentationPauseIssued && requested && !pauseRequested;
            presentationPauseIssued = false;
            if (resume) Player.SetPause(false);
        }
    }
    void Fail(string reason)
    {
        requested = false; pauseRequested = false; activityNativeState = "Error"; lastBufferPercent = null;
        Volatile.Write(ref snapshot, snapshot with { State = "STOPPED", Status = "ERROR_OCCURRED" }); log(reason);
        PresentationStateChanged?.Invoke();
    }
    void FinishAttempt()
    {
        requested = false; pauseRequested = false; activityNativeState = "Stopped"; lastBufferPercent = null;
        hadVideoOutput |= Player.VoutCount > 0;
        furthestPositionMs = Math.Max(furthestPositionMs, Math.Max(0, Player.Time));
        var code = Volatile.Read(ref accessErrorCode);
        if (PlaybackDiagnostics.EndIsFailure(everPlayed, hadVideoOutput, furthestPositionMs, code))
        {
            Fail(code == 0 ? "媒体提前结束，未确认成功播放，请检测当前媒体。"
                : $"媒体请求 HTTP {code}，且尚未产生视频输出或进度；标记为播放失败。请点击“检测当前媒体”。");
        }
        else
        {
            Volatile.Write(ref snapshot, snapshot with { State = "STOPPED" });
            log("引擎事件 · 媒体播放结束（未确认视频内容完整性）");
            PresentationStateChanged?.Invoke();
        }
    }
    public async Task ProbeAsync(CancellationToken cancellationToken = default)
    {
        var input = source; var probeGeneration = generation;
        if (local || input.Length == 0) { log("检测仅用于网络影片，请先从手机投放影片。"); return; }
        log("媒体来源 · " + PlaybackDiagnostics.DescribeSource(input));
        log("开始独立 HTTP 检测，必要时对比标准 DLNA 请求；每次最多读取 1KB，不记录地址，不携带账号凭证。");
        try
        {
            using var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false, ConnectTimeout = TimeSpan.FromSeconds(5) };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
            var result = await MediaProbe.RunAsync(client, input, false, cancellationToken);
            log($"入口检测 A（普通请求）· HTTP {result.StatusCode} · {result.ContentType} · {result.BodyKind}");
            if (result.StatusCode is 400 or 401 or 403 or 406)
            {
                var comparison = await MediaProbe.RunAsync(client, input, true, cancellationToken);
                log($"入口检测 B（LANPUT 标识 + 标准 DLNA 请求头）· HTTP {comparison.StatusCode} · {comparison.ContentType} · {comparison.BodyKind}");
                log(comparison.IsSuccess
                    ? "两次入口请求结果不同，提示可能存在请求兼容性差异，仍需验证；未自动修改播放器请求。"
                    : "两种入口请求均未成功。尚不能仅凭状态码判定 DRM 或设备认证要求；需结合片源与手机提示。");
            }
            else if (result.IsSuccess)
                log("入口可达；引擎的 403 仍可能来自后续资源或不同请求条件。当前检测没有验证后续资源。");
            else if (result.StatusCode is >= 300 and < 400)
                log("入口返回重定向，检测器不继续跳转；不能据此判断引擎的最终请求结果。");
            if (result.IsSuccess && result.BodyKind.StartsWith("HLS", StringComparison.Ordinal))
            {
                log("继续抽样检测 HLS 资源链 · 客户端标识 " + HlsProbe.Agent + "（与新版 VLC 标识一致；其他请求条件不保证完全相同）");
                using var chainTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                chainTimeout.CancelAfter(TimeSpan.FromSeconds(40));
                await HlsProbe.TraceAsync(client, input, log, chainTimeout.Token);
            }
            if (probeGeneration != generation) log("检测期间媒体或播放会话已切换，上述结果属于检测开始时的旧媒体。");
        }
        catch (OperationCanceledException) { log("媒体连通性检测超时或已取消。"); }
        catch (HttpRequestException) { log("媒体连通性检测网络失败，未记录含地址的原始异常。"); }
        catch (IOException) { log("媒体连通性检测读取失败。"); }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException) { log("HLS 列表解析超时，已停止本次检测。"); }
    }
    public void Dispose()
    {
        disposed = true; generation++; requested = false; pauseRequested = false;
        Player.Stop(); Player.Dispose(); media?.Dispose(); vlc.Dispose();
    }
}
