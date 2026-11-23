using LibVLCSharp.Shared;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;

namespace LANPUT.Desktop;

internal static class NativeVlc
{
    static int linuxResolverInstalled;
    static readonly ConcurrentDictionary<string, IntPtr> linuxLibraries = new();
    public static void Initialize()
    {
        var custom = Environment.GetEnvironmentVariable("LANPUT_VLC_PATH");
        if (OperatingSystem.IsLinux())
        {
            // Runtime distro packages supply versioned sonames; the unversioned
            // libvlc.so link belongs to libvlc-dev and is not a user requirement.
            if (Interlocked.Exchange(ref linuxResolverInstalled, 1) == 0)
                NativeLibrary.SetDllImportResolver(typeof(LibVLC).Assembly, (name, _, _) =>
                {
                    var soname = name switch { "libvlc" => "libvlc.so.5", "libvlccore" => "libvlccore.so.9", _ => null };
                    if (soname is null) return IntPtr.Zero;
                    return linuxLibraries.GetOrAdd(soname, value => NativeLibrary.Load(
                        !string.IsNullOrWhiteSpace(custom) && File.Exists(Path.Combine(custom, value)) ? Path.Combine(custom, value) : value));
                });
            LibVLCSharp.Shared.Core.Initialize(); return;
        }
        if (!string.IsNullOrWhiteSpace(custom)) { LibVLCSharp.Shared.Core.Initialize(custom); return; }
        var bundled = Path.Combine(AppContext.BaseDirectory, "vlc");
        if (Directory.Exists(bundled)) { LibVLCSharp.Shared.Core.Initialize(bundled); return; }
        if (OperatingSystem.IsMacOS())
        {
            var frameworks = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Frameworks", "vlc"));
            if (Directory.Exists(frameworks))
            {
                Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", Path.Combine(frameworks, "plugins"));
                LibVLCSharp.Shared.Core.Initialize(Path.Combine(frameworks, "lib")); return;
            }
            var installed = "/Applications/VLC.app/Contents/MacOS/lib";
            if (Directory.Exists(installed)) { LibVLCSharp.Shared.Core.Initialize(installed); return; }
        }
        LibVLCSharp.Shared.Core.Initialize();
    }

    public static string Help => OperatingSystem.IsLinux()
        ? "未能加载 LibVLC。Ubuntu / Debian 请安装 libvlc5、vlc-plugin-base、vlc-plugin-video-output。安装后重启 LANPUT。"
        : OperatingSystem.IsMacOS()
        ? "未能加载 LibVLC。请安装与电脑架构一致的 VLC 到 /Applications，或使用包含播放引擎的完整 LANPUT 安装包。"
        : "未能加载 LibVLC。请通过 LANPUT_VLC_PATH 指定同架构的 VLC 库目录。";
}
