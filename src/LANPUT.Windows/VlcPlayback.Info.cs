using LANPUT.Core;
using LibVLCSharp.Shared;

namespace LANPUT.Windows;

public sealed partial class VlcPlayback
{
    // Called only on the UI dispatcher, which also owns media replacement/disposal.
    public PlaybackInfo ReadPlaybackInfo()
    {
        dispatcher.VerifyAccess();
        if (disposed) return new();
        var info = new PlaybackInfo(HasMedia: media is not null,
            State: media is null ? "尚无影片" : presentationHeld && !pauseRequested ? "正在准备放映" : Activity.Label, Source: media is null ? "暂无" : local ? "本地文件" : "网络投放",
            Rendering: compatibility ? "兼容模式 · 软件解码 / Direct3D 9" : "默认模式 · 解码与输出由引擎选择");
        if (media is null) return info;
        var videoId = Player.VideoTrack;
        var audioId = Player.AudioTrack;
        foreach (var track in media.Tracks)
        {
            if (track.TrackType == TrackType.Video && track.Id == videoId)
            {
                var video = track.Data.Video;
                info = info with { VideoCodec = media.CodecDescription(track.TrackType, track.Codec),
                    Width = video.Width, Height = video.Height, FrameRateNum = video.FrameRateNum,
                    FrameRateDen = video.FrameRateDen, VideoBitrate = track.Bitrate };
            }
            else if (track.TrackType == TrackType.Audio && track.Id == audioId)
                info = info with { AudioCodec = media.CodecDescription(track.TrackType, track.Codec),
                    SampleRate = track.Data.Audio.Rate, Channels = track.Data.Audio.Channels, AudioBitrate = track.Bitrate };
        }
        // LibVLC returns a zero structure when statistics are unavailable. Do not
        // present those zeros as measured data, or retain totals after stopping.
        if (snapshot.State is "PLAYING" or "PAUSED_PLAYBACK" or "TRANSITIONING")
        {
            var stats = media.Statistics;
            if (videoId >= 0 && (stats.DecodedVideo > 0 || stats.DisplayedPictures > 0 || stats.LostPictures > 0))
                info = info with { DecodedFrames = stats.DecodedVideo, DisplayedFrames = stats.DisplayedPictures, LostFrames = stats.LostPictures };
        }
        return info with { BufferPercent = Activity.Phase == PlaybackPhase.Buffering ? lastBufferPercent : null };
    }
}
