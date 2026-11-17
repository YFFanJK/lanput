using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LANPUT.Core;

public sealed record LyricsRecord(long Id, string TrackName, string ArtistName, string AlbumName,
    double Duration, bool Instrumental, string? PlainLyrics, string? SyncedLyrics)
{
    public override string ToString() => $"{TrackName} · {ArtistName} · {TimeSpan.FromSeconds(Math.Clamp(Duration, 0, 86400)):mm\\:ss}";
}

public sealed class LyricsClient(HttpClient http)
{
    readonly SemaphoreSlim gate = new(1);
    readonly Dictionary<string, LyricsRecord[]> cache = new();
    DateTimeOffset nextRequest;
    // Prefer a precise recording, but never leave usable search results waiting
    // for a confirmation click. Manual selection remains available for corrections.
    public static LyricsRecord? PreferredMatch(IEnumerable<LyricsRecord> records, MusicIdentity song, double seconds)
    {
        static string Key(string? value) => Regex.Replace(value?.ToLowerInvariant() ?? "", @"[^\p{L}\p{N}]", "");
        var usable = records.Where(r => r is not null && (r.Instrumental
            || !string.IsNullOrWhiteSpace(r.SyncedLyrics) || !string.IsNullOrWhiteSpace(r.PlainLyrics))).ToArray();
        var exact = ExactMatch(usable, song, seconds);
        if (exact is not null) return exact;
        double Score(LyricsRecord r)
        {
            double score = Key(r.TrackName) == Key(song.Title) ? 100 : 0;
            if (song.Artist.Length > 0 && Key(r.ArtistName) == Key(song.Artist)) score += 60;
            if (song.Album.Length > 0 && Key(r.AlbumName) == Key(song.Album)) score += 15;
            if (seconds > 0 && double.IsFinite(r.Duration)) score += Math.Max(0, 35 - Math.Abs(r.Duration - seconds));
            if (!string.IsNullOrWhiteSpace(r.SyncedLyrics)) score += 20;
            return score;
        }
        return usable.OrderByDescending(Score).FirstOrDefault();
    }
    public static LyricsRecord? ExactMatch(IEnumerable<LyricsRecord> records, MusicIdentity song, double seconds)
    {
        static string Key(string s) => Regex.Replace(s.ToLowerInvariant(), @"[\s\p{P}]", "");
        if (song.Artist.Length == 0 || seconds <= 0) return null;
        var matches = records.Where(r => Key(r.TrackName ?? "") == Key(song.Title)
            && Key(r.ArtistName ?? "") == Key(song.Artist) && Math.Abs(r.Duration - seconds) <= 2
            && (song.Album.Length == 0 || Key(r.AlbumName ?? "") == Key(song.Album))).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
    public async Task<LyricsRecord[]> SearchAsync(MusicIdentity song, CancellationToken token)
    {
        string key = song.Title + "\n" + song.Artist;
        await gate.WaitAsync(token);
        try
        {
            if (cache.TryGetValue(key, out var known)) return known;
            // Never retry automatically on a rate limit; subsequent requests respect Retry-After.
            if (nextRequest > DateTimeOffset.UtcNow) throw new HttpRequestException("歌词服务繁忙，请稍后重试");
            nextRequest = DateTimeOffset.UtcNow.AddMilliseconds(500);
            string uri = "https://lrclib.net/api/search?track_name=" + Uri.EscapeDataString(song.Title)
                + (song.Artist.Length == 0 ? "" : "&artist_name=" + Uri.EscapeDataString(song.Artist));
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("LANPUT/0.9.0");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
            {
                nextRequest = response.Headers.RetryAfter?.Date ?? DateTimeOffset.UtcNow.Add(
                    response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1));
                nextRequest = nextRequest.AddMilliseconds(Random.Shared.Next(200, 1000));
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 2_000_000) throw new InvalidDataException("歌词响应过大");
            await response.Content.LoadIntoBufferAsync(2_000_000, token);
            var records = JsonSerializer.Deserialize<LyricsRecord[]>(await response.Content.ReadAsStringAsync(token),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            if (cache.Count >= 32) cache.Clear();
            cache[key] = records.Take(20).ToArray();
            return cache[key];
        }
        finally { gate.Release(); }
    }
}
