#!/usr/bin/env python3
"""Build on the target OS. Produces preview packages, never uploads/releases them."""
import argparse
import hashlib
import os
from pathlib import Path
import platform
import shutil
import subprocess
import tarfile
import tempfile

ROOT = Path(__file__).resolve().parents[1]


def run(*args, **kwargs):
    subprocess.run([str(arg) for arg in args], check=True, **kwargs)


def digest(path):
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rid", required=True, choices=["linux-x64", "linux-arm64", "osx-x64", "osx-arm64"])
    parser.add_argument("--output", type=Path)
    parser.add_argument("--published-dir", type=Path, help="Use an existing self-contained dotnet publish directory for this RID")
    parser.add_argument("--vlc-app", type=Path, default=Path("/Applications/VLC.app"))
    args = parser.parse_args()
    is_mac = args.rid.startswith("osx-")
    if (is_mac and platform.system() != "Darwin") or (not is_mac and platform.system() != "Linux"):
        parser.error("Run on the target OS: macOS for osx-*, Linux for linux-*.")
    machine = {"aarch64": "arm64", "arm64": "arm64", "x86_64": "x64", "AMD64": "x64"}.get(platform.machine())
    if machine != args.rid.split("-", 1)[1]:
        parser.error("Use a native runner of the requested architecture for packaging and VLC verification.")
    output = (args.output or ROOT / "dist" / "desktop-preview" / args.rid).resolve()
    # Keep staging on the native filesystem: Windows-mounted WSL folders can
    # report 0777 even after chmod, which Debian control metadata rejects.
    output.mkdir(parents=True, exist_ok=True)
    staging = tempfile.TemporaryDirectory(prefix="lanput-package-")
    stage = Path(staging.name)
    publish = stage / "publish"
    if args.published_dir:
        import json
        source = args.published_dir.resolve()
        deps = json.loads((source / "LANPUT.Desktop.deps.json").read_text(encoding="utf-8"))
        if not deps["runtimeTarget"]["name"].endswith("/" + args.rid) or not (source / "libhostfxr.dylib" if is_mac else source / "libhostfxr.so").exists():
            parser.error("--published-dir must contain a self-contained publish for this exact RID.")
        shutil.copytree(source, publish)
    else:
        run("dotnet", "publish", ROOT / "src/LANPUT.Desktop/LANPUT.Desktop.csproj", "-c", "Release",
            "-r", args.rid, "--self-contained", "true", "-p:PublishSingleFile=false", "-p:PublishTrimmed=false", "-o", publish)
    for filename in ["LICENSE", "THIRD-PARTY-NOTICES.md"]:
        shutil.copy2(ROOT / filename, publish / filename)
    shutil.copytree(ROOT / "licenses", publish / "licenses")
    shutil.copy2(ROOT / "src/LANPUT.Desktop/README.md", publish / "README.md")
    # Cross-published files on NTFS can carry 0777. Installed program files must
    # not become world-writable after copying to a native filesystem.
    for path in [publish, *publish.rglob("*")]:
        if not path.is_symlink():
            path.chmod(0o755 if path.is_dir() else 0o644)
    (publish / "LANPUT.Desktop").chmod(0o755)
    assets = []
    basename = "LANPUT-0.9.5-preview-" + args.rid
    if is_mac:
        app = stage / "LANPUT.app"
        contents = app / "Contents"
        contents.mkdir(parents=True)
        shutil.move(str(publish), contents / "MacOS")
        shutil.copy2(ROOT / "installer/macos/Info.plist", contents / "Info.plist")
        resources = contents / "Resources"
        resources.mkdir()
        iconset = stage / "LANPUT.iconset"
        iconset.mkdir()
        for size in [16, 32, 128, 256, 512]:
            for scale in [1, 2]:
                suffix = "@2x" if scale == 2 else ""
                run("sips", "-z", size * scale, size * scale, ROOT / "src/LANPUT.Windows/Assets/app-icon.png",
                    "--out", iconset / f"icon_{size}x{size}{suffix}.png", stdout=subprocess.DEVNULL)
        run("iconutil", "-c", "icns", iconset, "-o", resources / "LANPUT.icns")
        native = args.vlc_app / "Contents/MacOS"
        if not (native / "lib/libvlc.dylib").exists() or not (native / "plugins").is_dir():
            parser.error("Install matching VLC.app first; --vlc-app can select its location.")
        run("lipo", "-verify_arch", "arm64" if machine == "arm64" else "x86_64", native / "lib/libvlc.dylib")
        vlc = contents / "Frameworks/vlc"
        vlc.mkdir(parents=True)
        for folder in ["lib", "plugins", "share"]:
            if (native / folder).exists():
                shutil.copytree(native / folder, vlc / folder, symlinks=True)
        (resources / "VLC-SOURCE.txt").write_text(
            "VLC runtime copied from " + str(args.vlc_app) + "\n"
            "Upstream binaries and corresponding source: https://www.videolan.org/vlc/download-macosx.html\n"
            "Source archives: https://download.videolan.org/pub/videolan/vlc/\n"
            "Preserve THIRD-PARTY-NOTICES.md and upstream notices when redistributing.\n", encoding="utf-8")
        # A real Developer ID may be supplied by the build environment. The
        # default ad-hoc signature is for testing and does NOT imply notarization.
        identity = os.environ.get("LANPUT_SIGN_IDENTITY", "-")
        entitlement = ROOT / "installer/macos/entitlements.plist"
        for path in sorted(contents.rglob("*"), key=lambda p: len(p.parts), reverse=True):
            if path.is_symlink() or not path.is_file():
                continue
            file_type = subprocess.check_output(["file", "-b", str(path)], text=True)
            if "Mach-O" in file_type:
                run("codesign", "--force", "--sign", identity, "--options", "runtime", "--entitlements", entitlement, path)
        run("codesign", "--force", "--sign", identity, "--options", "runtime", "--entitlements", entitlement, app)
        run("codesign", "--verify", "--deep", "--strict", app)
        run(contents / "MacOS/LANPUT.Desktop", "--verify-engine")
        disk = stage / "disk"
        disk.mkdir()
        shutil.move(str(app), disk / "LANPUT.app")
        (disk / "Applications").symlink_to("/Applications")
        dmg = output / (basename + ".dmg")
        run("hdiutil", "create", "-volname", "LANPUT Preview", "-srcfolder", disk, "-ov", "-format", "UDZO", dmg)
        profile = os.environ.get("LANPUT_NOTARY_PROFILE")
        if profile:
            if identity == "-":
                parser.error("Notarization requires LANPUT_SIGN_IDENTITY with a Developer ID certificate.")
            run("xcrun", "notarytool", "submit", dmg, "--keychain-profile", profile, "--wait")
            run("xcrun", "stapler", "staple", dmg)
        assets.append(dmg)
    else:
        (publish / "LANPUT.Desktop").chmod(0o755)
        run(publish / "LANPUT.Desktop", "--verify-engine")
        launcher = publish / "run.sh"
        launcher.write_text('#!/bin/sh\ncd -- "$(dirname -- "$0")" || exit 1\nexec ./LANPUT.Desktop "$@"\n', encoding="utf-8")
        launcher.chmod(0o755)
        archive = output / (basename + ".tar.gz")
        with tarfile.open(archive, "w:gz") as bundle:
            bundle.add(publish, arcname="LANPUT")
        assets.append(archive)
        if shutil.which("dpkg-deb"):
            debroot = stage / "debian"
            appdir = debroot / "opt/lanput"
            appdir.parent.mkdir(parents=True)
            shutil.copytree(publish, appdir)
            desktopdir = debroot / "usr/share/applications"
            desktopdir.mkdir(parents=True)
            (desktopdir / "lanput.desktop").write_text(
                "[Desktop Entry]\nType=Application\nName=LANPUT\nComment=LAN media receiver\n"
                "Exec=/opt/lanput/LANPUT.Desktop\nIcon=lanput\nTerminal=false\nCategories=AudioVideo;Player;\n", encoding="utf-8")
            icons = debroot / "usr/share/icons/hicolor/256x256/apps"
            icons.mkdir(parents=True)
            shutil.copy2(ROOT / "src/LANPUT.Windows/Assets/app-icon.png", icons / "lanput.png")
            control = debroot / "DEBIAN"
            control.mkdir()
            control.chmod(0o755)
            (control / "control").write_text(
                "Package: lanput\nVersion: 0.9.5~preview1\nSection: video\nPriority: optional\n"
                f"Architecture: {'amd64' if machine == 'x64' else 'arm64'}\n"
                "Maintainer: LANPUT Community\n"
                "Depends: libvlc5, vlc-plugin-base, vlc-plugin-video-output, libx11-6, libice6, libsm6, libfontconfig1, libxcb1, libxkbcommon0, libgl1\n"
                "Recommends: fonts-noto-cjk\n"
                "Description: LANPUT cross-platform preview LAN media receiver\n", encoding="utf-8")
            (control / "control").chmod(0o644)
            deb = output / (basename + ".deb")
            run("dpkg-deb", "--root-owner-group", "--build", debroot, deb)
            assets.append(deb)
    (output / "SHA256SUMS.txt").write_text("".join(f"{digest(path)}  {path.name}\n" for path in assets), encoding="utf-8")
    print("Preview packages:")
    for path in assets:
        print(path)
    staging.cleanup()


if __name__ == "__main__":
    main()
