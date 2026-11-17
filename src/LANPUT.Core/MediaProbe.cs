using System.Net.Http.Headers;
using System.Text;

namespace LANPUT.Core;

public sealed record MediaProbeResult(int StatusCode, string ContentType, string BodyKind)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;
}

public static class MediaProbe
{
    // Both profiles identify as LANPUT. No device impersonation or account credentials.
    public static async Task<MediaProbeResult> RunAsync(HttpClient client, string address, bool dlnaHeaders, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("HTTP(S) source required");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Range = new RangeHeaderValue(0, 1023);
        if (dlnaHeaders)
        {
            request.Headers.UserAgent.ParseAdd("LanCast/0.2.3 UPnP/1.0 DLNADOC/1.50");
            request.Headers.TryAddWithoutValidation("getcontentFeatures.dlna.org", "1");
            request.Headers.TryAddWithoutValidation("transferMode.dlna.org", "Streaming");
        }
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var type = PlaybackDiagnostics.ContentType(response.Content.Headers.ContentType?.MediaType);
        if (!response.IsSuccessStatusCode) return new((int)response.StatusCode, type, "未读取响应正文");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        var bytes = new byte[1024]; var count = await stream.ReadAsync(bytes, timeout.Token);
        var prefix = Encoding.UTF8.GetString(bytes, 0, count).TrimStart('\uFEFF', ' ', '\r', '\n');
        string kind;
        if (prefix.StartsWith("#EXTM3U", StringComparison.Ordinal)) kind = "HLS 播放列表（未检测后续分片或密钥）";
        else if (prefix.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) || prefix.StartsWith("<html", StringComparison.OrdinalIgnoreCase)) kind = "HTML 网页，尚未证实为视频数据";
        else kind = $"已读取 {count} 字节，媒体类型未据此确认";
        return new((int)response.StatusCode, type, kind);
    }
}
