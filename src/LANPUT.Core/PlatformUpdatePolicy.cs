namespace LANPUT.Core;

public static class PlatformUpdatePolicy
{
    // Cross-platform preview feeds use the same JSON shape as Windows, but must
    // include the exact RID in the asset filename. Never offer a Windows EXE.
    public static void Validate(UpdateRelease release, string runtimeId)
    {
        var uri = SoftwareUpdates.RequireHttps(release.DownloadUrl);
        var name = Uri.UnescapeDataString(uri.Segments.Last()).ToLowerInvariant();
        string[] extensions = runtimeId.StartsWith("osx-", StringComparison.Ordinal) ? [".dmg", ".zip"]
            : runtimeId.StartsWith("linux-", StringComparison.Ordinal) ? [".deb", ".tar.gz", ".appimage"] : [];
        if (extensions.Length == 0 || !name.Contains("-" + runtimeId.ToLowerInvariant() + ".", StringComparison.Ordinal)
            || !extensions.Any(e => name.EndsWith(e, StringComparison.Ordinal)))
            throw new FormatException("更新安装包与当前平台或架构不匹配。");
    }
}
