using System.Globalization;

namespace LANPUT.Core;

// Presentation state stays separate from the UPnP transport state.
public enum PlaybackPhase { Idle, Opening, Buffering, WaitingForVideo, Playing, Paused, Error }

public sealed record PlaybackActivity(PlaybackPhase Phase, double? BufferPercent = null)
{
    public string Label => Phase switch
    {
        PlaybackPhase.Opening => "正在连接媒体来源",
        PlaybackPhase.Buffering => BufferPercent is { } percent
            ? "正在缓冲 · " + percent.ToString("0", CultureInfo.InvariantCulture) + "%" : "正在缓冲",
        PlaybackPhase.WaitingForVideo => "等待视频画面",
        PlaybackPhase.Playing => "正在播放",
        PlaybackPhase.Paused => "已暂停",
        PlaybackPhase.Error => "播放失败 · 请查看诊断",
        _ => "已停止"
    };

    public static double? ValidPercent(double? value) => value is { } percent && double.IsFinite(percent)
        && percent is >= 0 and <= 100 ? percent : null;

    public static PlaybackActivity Resolve(PlaybackSnapshot snapshot, string nativeState, bool hasVideo, double? bufferPercent)
    {
        if (snapshot.Status != "OK") return new(PlaybackPhase.Error);
        // Stop/pause wins over a late buffering event from the native player.
        if (snapshot.Uri.Length == 0 || snapshot.State is "STOPPED" or "NO_MEDIA_PRESENT") return new(PlaybackPhase.Idle);
        if (snapshot.State == "PAUSED_PLAYBACK") return new(PlaybackPhase.Paused);
        if (nativeState == "Buffering") return new(PlaybackPhase.Buffering, ValidPercent(bufferPercent));
        if (snapshot.State == "TRANSITIONING" || nativeState == "Opening") return new(PlaybackPhase.Opening);
        if (snapshot.State == "PLAYING") return new(hasVideo ? PlaybackPhase.Playing : PlaybackPhase.WaitingForVideo);
        return new(PlaybackPhase.Idle);
    }
}

public static class PlaybackNavigation
{
    public static long? RelativeSeek(long position, long duration, long delta)
    {
        if (duration <= 0) return null;
        // Decimal prevents overflow even for malformed or unusually long media clocks.
        return (long)Math.Clamp((decimal)position + delta, 0m, (decimal)duration);
    }
}
