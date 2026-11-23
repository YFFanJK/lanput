using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Media;
using LANPUT.Core;

namespace LANPUT.Windows;

public partial class MainWindow
{
    readonly HttpClient lyricsHttp = new() { Timeout = TimeSpan.FromSeconds(15) };
    LyricsClient? lyricsClient;
    CancellationTokenSource? lyricsCancellation;
    bool musicMode, localLyrics;
    long musicUiRevision = -1;
    string musicQuery = "";
    LrcLyrics? lyrics;
    int lyricAdjustment, lastLyricIndex = int.MinValue;

    void UpdateMusicPresentation(PlaybackSnapshot snapshot)
    {
        if (MusicOverlay is null) return;
        bool newMedia = musicUiRevision != playback.MediaRevision;
        bool live = snapshot.Status == "OK" && snapshot.State is "PLAYING" or "PAUSED_PLAYBACK" or "TRANSITIONING";
        // SetAVTransportURI briefly reports STOPPED before Play. Keep the current
        // room while a new source is being identified, rather than replaying a
        // cinema/music transition for every audio track.
        bool identifying = musicMode && snapshot.Status == "OK" && snapshot.Uri.Length > 0
            && !playback.HasVideoOutput && !playback.HasKnownVideoTrack
            && (live || newMedia);
        bool active = (playback.IsAudioOnly && live) || identifying;
        if (musicUiRevision != playback.MediaRevision)
        {
            musicUiRevision = playback.MediaRevision;
            StopRecordRotation();
            CancelLyrics(); lyrics = null; localLyrics = false; musicQuery = "";
            lastLyricIndex = int.MinValue; lyricAdjustment = 0;
            MusicCandidates.ItemsSource = null; LyricsPlain.Text = "";
            MusicFarPrevious.Text = MusicFarNext.Text = MusicPrevious.Text = MusicNext.Text = ""; MusicCurrent.Text = "让音乐填满这一刻。";
            MusicLyricsStatus.Text = "等待歌曲信息";
        }
        if (musicMode != active)
        {
            musicMode = active;
            if (!active) { CancelLyrics(); musicQuery = ""; MusicTools.IsExpanded = false; }
            RequestMusicScene(active);
        }
        UpdateRecordRotation(snapshot);
        if (!active) return;
        MusicTitle.Text = playback.Music.Title.Length > 0 ? playback.Music.Title : "正在播放音乐";
        MediaTitle.Text = MusicTitle.Text;
        MusicArtist.Text = playback.Music.Artist.Length > 0 ? playback.Music.Artist
            : playback.IsLocal ? "文件未提供歌手信息" : "手机未提供歌手信息";
        MusicAlbum.Text = playback.Music.Album;
        MusicAlbum.Visibility = string.IsNullOrWhiteSpace(MusicAlbum.Text) ? Visibility.Collapsed : Visibility.Visible;
        MusicPlaying.Text = "私人音乐厅";
        string key = playback.Music.Title + "\n" + playback.Music.Artist;
        if (!localLyrics && musicQuery != key && playback.Music.Title.Length > 0 && snapshot.DurationMs > 0)
        {
            musicQuery = key;
            lyrics = null; LyricsPlain.Text = ""; lastLyricIndex = int.MinValue;
            MusicFarPrevious.Text = MusicFarNext.Text = MusicPrevious.Text = MusicNext.Text = ""; MusicCurrent.Text = "让音乐填满这一刻。";
            MusicSearchTitle.Text = playback.Music.Title; MusicSearchArtist.Text = playback.Music.Artist;
            _ = SearchLyricsAsync(playback.Music, snapshot.DurationMs / 1000d);
        }
        UpdateLyricClock(snapshot.PositionMs);
        PreviousLyricButton.IsEnabled = NextLyricButton.IsEnabled = playback.CanSeek && lyrics is { Lines.Count: > 0 };
    }

    void CancelLyrics() { lyricsCancellation?.Cancel(); lyricsCancellation?.Dispose(); lyricsCancellation = null; }
    async Task SearchLyricsAsync(MusicIdentity song, double duration)
    {
        CancelLyrics();
        var cancellation = lyricsCancellation = new CancellationTokenSource();
        var token = cancellation.Token; long revision = playback.MediaRevision;
        MusicLyricsStatus.Text = "正在匹配在线歌词 · LRCLIB";
        try
        {
            lyricsClient ??= new LyricsClient(lyricsHttp);
            var candidates = await lyricsClient.SearchAsync(song, token);
            if (token.IsCancellationRequested || revision != playback.MediaRevision || !musicMode || localLyrics || closing) return;
            MusicCandidates.ItemsSource = candidates;
            var match = LyricsClient.PreferredMatch(candidates, song, duration);
            if (match is not null)
            {
                MusicCandidates.SelectedItem = match;
                ApplyLyrics(match);
            }
            else MusicLyricsStatus.Text = "暂无可用歌词 · 可修改歌名、歌手或导入 LRC";
        }
        catch (OperationCanceledException) { if (!token.IsCancellationRequested && !closing) MusicLyricsStatus.Text = "歌词服务超时 · 可重试或导入 LRC"; }
        catch (Exception e) when (e is HttpRequestException or InvalidDataException or System.Text.Json.JsonException)
        {
            if (!token.IsCancellationRequested && revision == playback.MediaRevision && !closing)
                MusicLyricsStatus.Text = "在线歌词暂不可用 · 可稍后重试或导入 LRC";
        }
    }
    void ApplyLyrics(LyricsRecord record)
    {
        if ((record.SyncedLyrics?.Length ?? 0) > 1_000_000 || (record.PlainLyrics?.Length ?? 0) > 1_000_000)
        { MusicLyricsStatus.Text = "歌词内容过大，请选择其他版本或导入 LRC"; return; }
        lyrics = LrcLyrics.Parse(record.SyncedLyrics ?? ""); lastLyricIndex = int.MinValue;
        LyricsPlain.Text = lyrics.Lines.Count == 0 ? record.PlainLyrics ?? "" : "";
        MusicLyricsStatus.Text = record.Instrumental ? "纯音乐 · 尽情聆听" : lyrics.Lines.Count > 0
            ? "同步歌词 · LRCLIB" : LyricsPlain.Text.Length > 0 ? "文本歌词 · 此版本没有时间轴" : "此版本暂无歌词";
        if (lyrics.Lines.Count == 0)
        {
            MusicFarPrevious.Text = MusicFarNext.Text = MusicPrevious.Text = MusicNext.Text = "";
            MusicCurrent.Text = record.Instrumental ? "此刻，只有旋律。" : "让音乐填满这一刻。";
        }
        UpdateLyricClock(playback.Snapshot.PositionMs);
    }
    void UpdateLyricClock(long position)
    {
        // The tools content may not have a visual namescope until first expanded.
        MusicLyricsHint.Text = MusicLyricsStatus.Text;
        // Missing lyrics still need an explanation outside the optional tools.
        MusicLyricsHint.Visibility = (lyrics is null || lyrics.Lines.Count == 0) && LyricsPlain.Text.Length == 0
            ? Visibility.Visible : Visibility.Collapsed;
        MusicOffset.Text = $"歌词提前 {lyricAdjustment / 1000d:+0.0;-0.0;0.0} 秒";
        LyricsPlainScroll.Visibility = LyricsPlain.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        LyricLines.Visibility = LyricsPlain.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
        if (lyrics is null || lyrics.Lines.Count == 0) return;
        int index = lyrics.IndexAt(position, lyricAdjustment);
        if (index == lastLyricIndex) return;
        lastLyricIndex = index;
        string Line(int n) => n >= 0 && n < lyrics.Lines.Count ? lyrics.Lines[n].Text : "";
        MusicPrevious.Text = Line(index - 1);
        MusicFarPrevious.Text = Line(index - 2);
        MusicCurrent.Text = index < 0 ? "前奏，慢慢听。" : Line(index);
        MusicNext.Text = Line(index + 1);
        MusicFarNext.Text = Line(index + 2);
        AnimateValue(LyricLines, OpacityProperty, 1, 280, from: .35);
        AnimateValue(LyricTranslate, TranslateTransform.YProperty, 0, 280, from: 8);
    }
    async void MusicSearch_Click(object sender, RoutedEventArgs e)
    {
        if (!musicMode || string.IsNullOrWhiteSpace(MusicSearchTitle.Text)) return;
        localLyrics = false;
        await SearchLyricsAsync(new(MusicIdentity.Clean(MusicSearchTitle.Text), MusicIdentity.Clean(MusicSearchArtist.Text)), playback.Snapshot.DurationMs / 1000d);
    }
    void MusicChoose_Click(object sender, RoutedEventArgs e)
    {
        if (MusicCandidates.SelectedItem is not LyricsRecord record || !musicMode) return;
        CancelLyrics(); localLyrics = true; ApplyLyrics(record);
    }
    void MusicOffsetEarlier_Click(object sender, RoutedEventArgs e) { lyricAdjustment = Math.Min(60000, lyricAdjustment + 500); lastLyricIndex = int.MinValue; UpdateLyricClock(playback.Snapshot.PositionMs); }
    void MusicOffsetLater_Click(object sender, RoutedEventArgs e) { lyricAdjustment = Math.Max(-60000, lyricAdjustment - 500); lastLyricIndex = int.MinValue; UpdateLyricClock(playback.Snapshot.PositionMs); }
    void MusicImport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "LRC 歌词|*.lrc", Title = "为当前音乐导入歌词" };
        long revision = playback.MediaRevision;
        // VideoView uses a separate native overlay window. Hide its visual surface
        // while the modal picker is open so it cannot cover or intercept the dialog.
        // This does not detach the media player or pause the audio.
        bool? selected;
        Video.Visibility = Visibility.Hidden;
        try { selected = dialog.ShowDialog(this); }
        finally { Video.Visibility = Visibility.Visible; }
        if (selected != true || revision != playback.MediaRevision || !musicMode) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 1_000_000) throw new InvalidDataException();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            byte[] bytes = File.ReadAllBytes(dialog.FileName);
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException) { text = Encoding.GetEncoding(936).GetString(bytes); }
            if (bytes.Length >= 2 && ((bytes[0] == 255 && bytes[1] == 254) || (bytes[0] == 254 && bytes[1] == 255)))
                text = File.ReadAllText(dialog.FileName);
            var parsed = LrcLyrics.Parse(text);
            if (parsed.Lines.Count == 0) { MusicLyricsStatus.Text = "文件中没有有效的 LRC 时间标签"; return; }
            CancelLyrics(); localLyrics = true; lyrics = parsed; LyricsPlain.Text = "";
            lastLyricIndex = int.MinValue; lyricAdjustment = 0;
            MusicLyricsStatus.Text = "本地 LRC · 跟随播放进度同步";
            UpdateLyricClock(playback.Snapshot.PositionMs);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { MusicLyricsStatus.Text = "歌词读取失败，请检查文件大小与编码"; }
    }
}
