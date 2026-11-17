using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace LANPUT.Core;

public sealed record HlsReference(string Kind, Uri Address);
public sealed record HlsInspection(bool IsPlaylist, bool ByteRanges, bool Variables, string Protection, IReadOnlyList<HlsReference> References);

public static class HlsProbe
{
    public const string Agent = "LanCast/0.2.3";
    const int Limit = 262144;
    static readonly Regex Attributes = new("(?:^|,)(?<name>[A-Z0-9-]+)=(?:\"(?<quoted>[^\"]*)\"|(?<plain>[^,]*))", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    public static IReadOnlyDictionary<string, string> ParseAttributes(string line)
    {
        var colon = line.IndexOf(':');
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Attributes.Matches(colon < 0 ? line : line[(colon + 1)..]))
            result[m.Groups["name"].Value] = m.Groups["quoted"].Success ? m.Groups["quoted"].Value : m.Groups["plain"].Value;
        return result;
    }
    static bool IsHttp(Uri uri) => (uri.Scheme is "http" or "https") && uri.UserInfo.Length == 0;
    public static HlsInspection Inspect(string text, Uri origin)
    {
        text = text.TrimStart('\uFEFF', ' ', '\r', '\n');
        var references = new List<HlsReference>();
        if (!text.StartsWith("#EXTM3U", StringComparison.Ordinal)) return new(false, false, false, "未识别", references);
        bool variant = false, byteRanges = false, variables = false;
        var protection = "未见密钥标记（不能据此确认内容未受保护）";
        void Add(string kind, string? value)
        {
            if (value is null || value.Contains("{$", StringComparison.Ordinal)) return;
            if (Uri.TryCreate(origin, value, out var address) && IsHttp(address)) references.Add(new(kind, address));
        }
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.Ordinal)) { variant = true; continue; }
            if (line.StartsWith("#EXT-X-DEFINE:", StringComparison.Ordinal)) { variables = true; continue; }
            if (line.StartsWith("#EXT-X-BYTERANGE:", StringComparison.Ordinal)) { byteRanges = true; continue; }
            if (line.StartsWith("#EXT-X-KEY:", StringComparison.Ordinal) || line.StartsWith("#EXT-X-SESSION-KEY:", StringComparison.Ordinal))
            {
                var a = ParseAttributes(line); var method = a.GetValueOrDefault("METHOD");
                var known = method switch { "NONE" => "NONE", "AES-128" => "AES-128", "SAMPLE-AES" => "SAMPLE-AES", "SAMPLE-AES-CTR" => "SAMPLE-AES-CTR", _ => "其他加密标记" };
                protection = "密钥方法 " + known + (a.TryGetValue("KEYFORMAT", out var format) && format != "identity" ? " · 非 identity 密钥格式" : "");
                if (method != "NONE") Add("密钥", a.GetValueOrDefault("URI"));
                continue;
            }
            if (line.StartsWith("#EXT-X-MAP:", StringComparison.Ordinal))
            {
                var a = ParseAttributes(line); if (a.ContainsKey("BYTERANGE")) byteRanges = true;
                Add("初始化片段", a.GetValueOrDefault("URI")); continue;
            }
            if (line.StartsWith("#EXT-X-MEDIA:", StringComparison.Ordinal))
            {
                var a = ParseAttributes(line);
                if (a.GetValueOrDefault("TYPE") == "AUDIO") Add("音频列表", a.GetValueOrDefault("URI"));
                continue;
            }
            if (line.StartsWith('#')) continue;
            Add(variant ? "子列表" : "视频分片", line); variant = false;
        }
        return new(true, byteRanges, variables, protection, references);
    }
    sealed record FetchResult(int Status, Uri FinalAddress, string Text, bool Truncated);
    static async Task<FetchResult> FetchAsync(HttpClient client, Uri address, bool playlist, CancellationToken token)
    {
        var current = address;
        for (int redirects = 0; redirects <= 3; redirects++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd(Agent);
            // All requests are bounded. Range may itself affect some servers; results are diagnostic.
            request.Headers.Range = new RangeHeaderValue(0, playlist ? Limit : 1023);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            var status = (int)response.StatusCode;
            if (status is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location;
                if (location is null || !Uri.TryCreate(current, location.ToString(), out var next) || !IsHttp(next) || redirects == 3)
                    return new(status, current, "", false);
                current = next; continue;
            }
            if (!response.IsSuccessStatusCode || !playlist) return new(status, current, "", false);
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var buffer = new MemoryStream();
            var bytes = new byte[8192];
            while (buffer.Length <= Limit)
            {
                int count = await stream.ReadAsync(bytes.AsMemory(0, (int)Math.Min(bytes.Length, Limit + 1 - buffer.Length)), token);
                if (count == 0) break;
                buffer.Write(bytes, 0, count);
            }
            var total = response.Content.Headers.ContentRange?.Length;
            var truncated = buffer.Length > Limit || (total.HasValue && total.Value > buffer.Length);
            return new(status, current, Encoding.UTF8.GetString(buffer.ToArray()), truncated);
        }
        throw new InvalidOperationException("Unreachable redirect state");
    }
    public static async Task TraceAsync(HttpClient client, string source, Action<string> log, CancellationToken token)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var origin) || !IsHttp(origin)) throw new ArgumentException("HTTP(S) source required");
        var pending = new Queue<(string Kind, Uri Address, int Depth)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Enqueue(("入口列表", origin, 0));
        int requests = 0;
        while (pending.Count > 0 && requests < 8)
        {
            token.ThrowIfCancellationRequested();
            var item = pending.Dequeue();
            if (!visited.Add(item.Address.AbsoluteUri)) continue;
            requests++;
            bool playlist = item.Kind.EndsWith("列表", StringComparison.Ordinal);
            var result = await FetchAsync(client, item.Address, playlist, token);
            log($"HLS 链路 · {item.Kind} · HTTP {result.Status} · " + PlaybackDiagnostics.SourceScope(result.FinalAddress));
            if (result.Status is < 200 or >= 300 || !playlist) continue;
            if (result.Truncated) { log("HLS 列表超过上限或响应未完整覆盖列表，停止解析该列表以免误判。"); continue; }
            var inspection = Inspect(result.Text, result.FinalAddress);
            if (!inspection.IsPlaylist) { log("该响应未识别为 HLS 列表；未记录正文内容。"); continue; }
            log("HLS 列表信息 · " + inspection.Protection);
            if (inspection.Variables) log("列表含变量定义；变量替换未实现，相关 URI 不检测。");
            if (inspection.ByteRanges) log("列表使用字节范围；分片检测不验证准确的片内范围，不能据此判断完整播放能力。");
            // Sample one of each role, rather than enumerating/download all media.
            foreach (var reference in inspection.References.GroupBy(r => r.Kind).Select(g => g.First()))
            {
                bool child = reference.Kind.EndsWith("列表", StringComparison.Ordinal);
                if (child && item.Depth >= 2) { log("HLS 子列表达到深度上限，停止继续展开。"); continue; }
                pending.Enqueue((reference.Kind, reference.Address, item.Depth + (child ? 1 : 0)));
            }
        }
        if (pending.Count > 0) log("HLS 检测达到请求上限，尚有未检测引用。");
        log("HLS 抽样检测结束；成功状态不证明解码、完整播放或授权能力。没有记录 URL、列表正文或密钥内容。");
    }
}
