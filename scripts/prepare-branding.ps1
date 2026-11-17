[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$assets = Join-Path $root 'src/LANPUT.Windows/Assets'
$fullSource = [Drawing.Image]::FromFile((Join-Path $assets 'logo-transparent-source.png'))
$markSource = [Drawing.Image]::FromFile((Join-Path $assets 'mark-transparent-source.png'))
try {
    # Keep both user-selected generated PNGs byte-for-byte for the WPF UI.
    # Only the Windows ICO and installer BMP formats need rendering.
    Copy-Item (Join-Path $assets 'logo-transparent-source.png') (Join-Path $assets 'logo.png') -Force
    Copy-Item (Join-Path $assets 'mark-transparent-source.png') (Join-Path $assets 'app-icon.png') -Force
    $mark = New-Object Drawing.Rectangle(0, 0, $markSource.Width, $markSource.Height)
    function Render-Brand($image, [int]$width, [int]$height, [string]$path, [Drawing.Color]$background) {
        $bitmap = New-Object Drawing.Bitmap($width, $height)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear($background)
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $scale = [Math]::Min(($width * .94) / $image.Width, ($height * .94) / $image.Height)
            $w = [single]($image.Width * $scale); $h = [single]($image.Height * $scale)
            $target = New-Object Drawing.RectangleF(([single](($width-$w)/2)), ([single](($height-$h)/2)), $w, $h)
            $graphics.DrawImage($image, $target)
            $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Bmp)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
    Render-Brand $fullSource 164 314 (Join-Path $assets 'installer-banner.bmp') ([Drawing.Color]::FromArgb(14,17,20))
    Render-Brand $markSource 55 55 (Join-Path $assets 'installer-small.bmp') ([Drawing.Color]::White)
    $sizes = @(16,24,32,48,64,128,256)
    $frames = @()
    foreach ($size in $sizes) {
        $bitmap = New-Object Drawing.Bitmap($size, $size)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $stream = New-Object IO.MemoryStream
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $w = [single]($size * .94); $h = [single]($w * $mark.Height / $mark.Width)
            $target = New-Object Drawing.RectangleF(([single](($size-$w)/2)), ([single](($size-$h)/2)), $w, $h)
            $graphics.DrawImage($markSource, $target, $mark, [Drawing.GraphicsUnit]::Pixel)
            $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
            $frames += ,$stream.ToArray()
        } finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
    $file = [IO.File]::Create((Join-Path $assets 'LANPUT.ico'))
    $writer = New-Object IO.BinaryWriter($file)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($i=0; $i -lt $sizes.Count; $i++) {
            $edge = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
            $writer.Write([byte]$edge); $writer.Write([byte]$edge)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
            $offset += $frames[$i].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    } finally { $writer.Dispose() }
    Write-Host 'Brand assets prepared from the two user-selected transparent PNGs.'
} finally { $fullSource.Dispose(); $markSource.Dispose() }
