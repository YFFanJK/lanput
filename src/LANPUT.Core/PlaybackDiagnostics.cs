namespace LANPUT.Core;

// Never return raw native messages: they may contain signed URLs or headers.
public static class PlaybackDiagnostics
{
    public static int? AccessErrorCode(string raw)
    {
        var label = Classify(raw);
        if (label?.StartsWith("HTTP 401", StringComparison.Ordinal) == true) return 401;
        if (label?.StartsWith("HTTP 403", StringComparison.Ordinal) == true) return 403;
        if (label?.StartsWith("HTTP 404", StringComparison.Ordinal) == true) return 404;
        return null;
    }
    // A Playing event is not evidence that any video/data was successfully rendered.
    // An unrelated failed optional request must not invalidate an otherwise completed video.
    public static bool EndIsFailure(bool enteredPlaying, bool hadVideoOutput, long furthestPositionMs, int accessErrorCode)
        => !enteredPlaying || ((accessErrorCode is 401 or 403 or 404) && !hadVideoOutput && furthestPositionMs <= 0);

    public static string SourceScope(Uri uri)
    {
        if (System.Net.IPAddress.TryParse(uri.Host, out var ip))
        {
            if (System.Net.IPAddress.IsLoopback(ip)) return "电脑本机服务";
            if (NetworkChoice.IsPrivate(ip)) return $"局域网服务 {ip}:{uri.Port}";
            return "公网 IP 来源";
        }
        return "域名来源（未判断是否为局域网服务）";
    }
    public static string? Classify(string raw)
    {
        var s = raw.ToLowerInvariant();
        if (s.Contains("401 unauthorized") || s.Contains("http 401") || s.Contains("error 401")) return "HTTP 401：媒体请求未获授权";
        if (s.Contains("403 forbidden") || s.Contains("http 403") || s.Contains("error 403")) return "HTTP 403：媒体服务器拒绝访问";
        if (s.Contains("404 not found") || s.Contains("http 404") || s.Contains("error 404")) return "HTTP 404：媒体资源不存在";
        if (s.Contains("tls") && (s.Contains("fail") || s.Contains("error")) || s.Contains("certificate verification failed")) return "TLS：安全连接或证书校验失败";
        if (s.Contains("connection timed out") || s.Contains("connection timeout")) return "网络：连接超时";
        if (s.Contains("connection refused")) return "网络：连接被拒绝";
        if (s.Contains("cannot resolve") || s.Contains("host not found")) return "网络：媒体域名解析失败";
        if (s.Contains("no suitable decoder") || s.Contains("codec not supported") || s.Contains("no decoder modules matched")) return "解码：未找到可用解码器";
        if ((s.Contains("direct3d") || s.Contains("d3d11")) && (s.Contains("failed") || s.Contains("error"))) return "显示：Direct3D 输出失败，可尝试兼容渲染";
        if (s.Contains("cannot open") || s.Contains("unable to open") || s.Contains("failed to open")) return "媒体：无法打开输入资源（原因尚未确定）";
        if (s.Contains("no suitable access") || s.Contains("no access modules matched")) return "媒体：没有适用的访问模块";
        if (s.Contains("no demux modules matched") || s.Contains("cannot peek")) return "媒体：无法识别或读取媒体封装";
        return null;
    }
    public static string DescribeSource(string raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) return "地址类型未知";
        if (uri.Scheme is not ("http" or "https")) return "本地测试媒体";
        var kind = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant() switch
        { ".m3u8" => "HLS", ".mp4" or ".m4v" => "MP4", ".ts" => "MPEG-TS", ".mkv" => "MKV", _ => "未知封装" };
        return $"{uri.Scheme.ToUpperInvariant()} · {SourceScope(uri)} · {kind} · {(uri.Query.Length > 0 ? "含查询参数（已隐藏）" : "无查询参数")}";
    }
    public static string ContentType(string? raw) => raw?.ToLowerInvariant() switch
    {
        "video/mp4" => "video/mp4", "video/mp2t" => "video/mp2t", "video/mpeg" => "video/mpeg",
        "application/vnd.apple.mpegurl" or "application/x-mpegurl" => "HLS 播放列表",
        "text/html" => "HTML 网页", "application/json" => "JSON 响应", "application/octet-stream" => "二进制数据", _ => "其他或未声明"
    };
}
