using System.Xml;

namespace LANPUT.Core;

public static class MediaPresentation
{
    public static string LocalTitle(string path) => Path.GetFileName(path);

    // RGB is a pixel format, not evidence that a video is a still image.
    public static bool IsImage(string source, string metadata, bool local)
    {
        if (local) return Path.GetExtension(source).ToLowerInvariant() is
            ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp" or ".tif" or ".tiff";
        if (string.IsNullOrWhiteSpace(metadata)) return false;
        try
        {
            var xml = XmlSafe.Parse(metadata);
            return xml.Descendants().Any(e => e.Name.LocalName == "class"
                && (e.Value.Trim() == "object.item.imageItem"
                    || e.Value.Trim().StartsWith("object.item.imageItem.", StringComparison.Ordinal)));
        }
        catch (XmlException) { return false; }
    }
}
