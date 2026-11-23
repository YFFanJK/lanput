using System.Globalization;

namespace LANPUT.Core;

public sealed record PlaybackInfo(
    bool HasMedia = false, string State = "尚无影片", string Source = "暂无", string Rendering = "暂无",
    string? VideoCodec = null, uint Width = 0, uint Height = 0, uint FrameRateNum = 0, uint FrameRateDen = 0,
    uint VideoBitrate = 0, string? AudioCodec = null, uint SampleRate = 0, uint Channels = 0, uint AudioBitrate = 0,
    double? BufferPercent = null, int? DisplayedFrames = null, int? LostFrames = null, int? DecodedFrames = null,
    bool IsImage = false, int? VideoTracks = null, int? AudioTracks = null,
    bool VideoSelected = false, bool AudioSelected = false,
    string Decoding = "未确认（由引擎自动选择）")
{
    public string MediaType => !HasMedia ? "尚无媒体" : IsImage ? "图片"
        : VideoTracks > 0 ? "视频" : AudioTracks > 0 ? "纯音频" : "识别中";

    public string ToDisplayText()
    {
        var lines = new List<string> {
        $"播放状态     {State}",
        $"媒体类型     {MediaType}",
        $"媒体来源     {Source}",
        $"渲染设置     {Rendering}", "" };
        if (!HasMedia) return string.Join(Environment.NewLine, lines);
        if (IsImage || VideoTracks != 0)
        {
            lines.Add($"分辨率       {(Width > 0 && Height > 0 ? $"{Width} × {Height}" : "引擎未提供")}");
            if (IsImage)
            {
                lines.Add($"图像解码格式 {Known(VideoCodec)}");
                lines.Add("图片说明     静态图片，帧率与视频丢帧统计不适用");
            }
            else
            {
                lines.Add($"视频轨道     {TrackStatus(VideoTracks, VideoSelected)}");
                lines.Add($"视频编码     {Known(VideoCodec)}");
                lines.Add($"标称帧率     {Provided(FrameRate(FrameRateNum, FrameRateDen))}（片源参数）");
                lines.Add("实际输出帧率 未测量");
                lines.Add($"视频轨道码率 {Provided(Bitrate(VideoBitrate))}");
                lines.Add($"视频解码方式 {Decoding}");
            }
        }
        else lines.Add("视频轨道     未识别到视频轨道");
        lines.Add("");
        lines.Add($"音频轨道     {(IsImage ? "不适用（图片）" : TrackStatus(AudioTracks, AudioSelected))}");
        if (!IsImage && AudioTracks != 0) lines.AddRange([
        $"音频编码     {Known(AudioCodec)}",
        $"采样率       {(SampleRate > 0 ? $"{SampleRate:N0} Hz" : "引擎未提供")}",
        $"声道数       {(Channels > 0 ? Channels.ToString(CultureInfo.InvariantCulture) : "引擎未提供")}",
        $"音频轨道码率 {Provided(Bitrate(AudioBitrate))}" ]);
        lines.Add("");
        lines.Add($"缓冲进度     {(PlaybackActivity.ValidPercent(BufferPercent ?? double.NaN) is { } buffer ? buffer.ToString("0.#", CultureInfo.InvariantCulture) + " %" : "当前无缓冲百分比")}");
        if (!IsImage && VideoTracks != 0) lines.AddRange([
            $"已解码视频帧 {Counter(DecodedFrames)}",
            $"已显示视频帧 {Counter(DisplayedFrames)}",
            $"丢失视频帧   {Counter(LostFrames)}" ]);
        return string.Join(Environment.NewLine, lines);
    }

    static string TrackStatus(int? count, bool selected) => count is null ? "等待引擎识别"
        : count == 0 ? "未识别到轨道" : $"共 {count} 条 · {(selected ? "已选择播放轨道" : "尚未选择播放轨道")}";
    static string Provided(string value) => value == "暂无" ? "引擎未提供" : value;
    static string Known(string? value) => string.IsNullOrWhiteSpace(value) ? "引擎未提供" : value;
    static string Counter(int? value) => value is >= 0 ? value.Value.ToString("N0", CultureInfo.InvariantCulture) : "引擎未提供";
    public static string FrameRate(uint numerator, uint denominator) => numerator > 0 && denominator > 0
        ? ((double)numerator / denominator).ToString("0.###", CultureInfo.InvariantCulture) + " fps" : "暂无";
    public static string Bitrate(uint bitsPerSecond) => bitsPerSecond == 0 ? "暂无"
        : bitsPerSecond >= 1000000 ? (bitsPerSecond / 1000000d).ToString("0.##", CultureInfo.InvariantCulture) + " Mbps"
        : (bitsPerSecond / 1000d).ToString("0.##", CultureInfo.InvariantCulture) + " kbps";
}
