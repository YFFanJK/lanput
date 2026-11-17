using System.Net;
using System.Text.Json;

namespace LANPUT.Core;

public sealed record UpdateRelease(string Version, string DownloadUrl, string? Notes = null);

public static class SoftwareUpdates
{
    public static Uri RequireHttps(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            throw new FormatException("更新地址必须是 HTTPS 地址，且不能包含账号或片段。");
        return uri;
    }

    public static Version ParseVersion(string value)
    {
        if (!System.Version.TryParse(value, out var version) || version.Major < 0)
            throw new FormatException("版本号格式无效。");
        return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
    }

    public static UpdateRelease Parse(string json)
    {
        var release = JsonSerializer.Deserialize<UpdateRelease>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new FormatException("更新信息为空。");
        ParseVersion(release.Version);
        RequireHttps(release.DownloadUrl);
        if (release.Notes?.Length > 4000) throw new FormatException("更新说明过长。");
        return release;
    }

    public static bool IsNewer(UpdateRelease release, string current) => ParseVersion(release.Version) > ParseVersion(current);

    public static async Task<UpdateRelease> CheckAsync(string feedUrl, CancellationToken cancellationToken, HttpMessageHandler? transport = null)
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.All };
        using var client = new HttpClient(transport ?? handler) { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LANPUT-UpdateChecker/1.0");
        var uri = RequireHttps(feedUrl);
        for (int redirect = 0; redirect <= 5; redirect++)
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                if (response.Headers.Location is not { } location) throw new HttpRequestException("缺少重定向地址。");
                uri = RequireHttps(new Uri(uri, location).AbsoluteUri);
                continue;
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 65536) throw new FormatException("更新信息过大。");
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var bytes = new byte[4096];
            int count;
            while ((count = await stream.ReadAsync(bytes, cancellationToken)) > 0)
            {
                if (buffer.Length + count > 65536) throw new FormatException("更新信息过大。");
                buffer.Write(bytes, 0, count);
            }
            return Parse(System.Text.Encoding.UTF8.GetString(buffer.ToArray()).TrimStart('\uFEFF'));
        }
        throw new HttpRequestException("更新地址重定向次数过多。");
    }
}
