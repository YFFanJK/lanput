using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace LANPUT.Core;

public sealed record MusicIdentity(string Title = "", string Artist = "", string Album = "")
{
    public static string Clean(string? value) => value?.Trim() is { } s && s.Length <= 500
        && s.ToLowerInvariant() is not ("null" or "unknown" or "anonymous" or "未知" or "未知艺术家") ? s : "";
    public static MusicIdentity FromDidl(string metadata)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(metadata), new XmlReaderSettings
                { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 262144 });
            var xml = XDocument.Load(reader);
            string Get(string name) => Clean(xml.Descendants().FirstOrDefault(x => x.Name.LocalName == name)?.Value);
            return new(Get("title"), Get("artist") is { Length: > 0 } artist ? artist : Get("creator"), Get("album"));
        }
        catch (XmlException) { return new(); }
    }
}

public sealed record LyricLine(long TimeMs, string Text);
public sealed class LrcLyrics
{
    public IReadOnlyList<LyricLine> Lines { get; }
    public int OffsetMs { get; }
    LrcLyrics(List<LyricLine> lines, int offset) { Lines = lines; OffsetMs = offset; }
    public static LrcLyrics Parse(string text)
    {
        if (text.Length > 1_000_000) throw new InvalidDataException("歌词文件过大");
        var lines = new List<LyricLine>(); int offset = 0;
        foreach (string raw in text.Split('\n'))
        {
            var tag = Regex.Match(raw, @"\[offset:([+-]?\d+)\]", RegexOptions.IgnoreCase);
            if (tag.Success && int.TryParse(tag.Groups[1].Value, out int n)) offset = Math.Clamp(n, -600000, 600000);
            var stamps = Regex.Matches(raw, @"\[(\d{1,4}):([0-5]\d)(?:[.:](\d{1,3}))?\]");
            if (stamps.Count == 0) continue;
            string content = Regex.Replace(raw, @"\[[^\]]*\]", "").Trim();
            foreach (Match stamp in stamps)
            {
                long time = long.Parse(stamp.Groups[1].Value) * 60000 + long.Parse(stamp.Groups[2].Value) * 1000;
                if (stamp.Groups[3].Success) time += int.Parse(stamp.Groups[3].Value.PadRight(3, '0'));
                lines.Add(new(time, content));
            }
        }
        return new(lines.OrderBy(x => x.TimeMs).Distinct().ToList(), offset);
    }
    // Positive offset advances lyrics. Recompute from the player clock after every seek/pause.
    public int IndexAt(long positionMs, int adjustmentMs = 0)
    {
        decimal time = (decimal)positionMs + OffsetMs + adjustmentMs;
        int lo = 0, hi = Lines.Count - 1, result = -1;
        while (lo <= hi) { int mid = lo + (hi - lo) / 2; if (Lines[mid].TimeMs <= time) { result = mid; lo = mid + 1; } else hi = mid - 1; }
        return result;
    }
}
