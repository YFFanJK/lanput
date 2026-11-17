using System.Globalization;

namespace LANPUT.Core;

public sealed record PlaybackInfo(
    bool HasMedia = false, string State = "尚无影片", string Source = "暂无", string Rendering = "暂无",
    string? VideoCodec = null, uint Width = 0, uint Height = 0, uint FrameRateNum = 0, uint FrameRateDen = 0,
    uint VideoBitrate = 0, string? AudioCodec = null, uint SampleRate = 0, uint Channels = 0, uint AudioBitrate = 0,
    double? BufferPercent = null, int? DisplayedFrames = null, int? LostFrames = null, int? DecodedFrames = null)
{
    public string ToDisplayText() => string.Join(Environment.NewLine,
        $"播放状态     {State}",
        $"媒体来源     {Source}",
        $"渲染设置     {Rendering}", "",
        $"分辨率       {(Width > 0 && Height > 0 ? $"{Width} × {Height}" : "暂无")}",
        $"视频编码     {Known(VideoCodec)}",
        $"标称帧率     {FrameRate(FrameRateNum, FrameRateDen)}",
        $"视频轨道码率 {Bitrate(VideoBitrate)}", "",
        $"音频编码     {Known(AudioCodec)}",
        $"采样率       {(SampleRate > 0 ? $"{SampleRate:N0} Hz" : "暂无")}",
        $"声道数       {(Channels > 0 ? Channels.ToString(CultureInfo.InvariantCulture) : "暂无")}",
        $"音频轨道码率 {Bitrate(AudioBitrate)}", "",
        $"缓冲进度     {(PlaybackActivity.ValidPercent(BufferPercent ?? double.NaN) is { } buffer ? buffer.ToString("0.#", CultureInfo.InvariantCulture) + " %" : "暂无")}",
        $"已解码视频帧 {Counter(DecodedFrames)}",
        $"已显示视频帧 {Counter(DisplayedFrames)}",
        $"丢失视频帧   {Counter(LostFrames)}");

    static string Known(string? value) => string.IsNullOrWhiteSpace(value) ? "暂无" : value;
    static string Counter(int? value) => value is >= 0 ? value.Value.ToString("N0", CultureInfo.InvariantCulture) : "暂无";
    public static string FrameRate(uint numerator, uint denominator) => numerator > 0 && denominator > 0
        ? ((double)numerator / denominator).ToString("0.###", CultureInfo.InvariantCulture) + " fps" : "暂无";
    public static string Bitrate(uint bitsPerSecond) => bitsPerSecond == 0 ? "暂无"
        : bitsPerSecond >= 1000000 ? (bitsPerSecond / 1000000d).ToString("0.##", CultureInfo.InvariantCulture) + " Mbps"
        : (bitsPerSecond / 1000d).ToString("0.##", CultureInfo.InvariantCulture) + " kbps";
}
