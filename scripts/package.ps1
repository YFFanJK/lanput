[CmdletBinding()]
param([string]$FeedUrl = '', [string]$DownloadUrl = '', [string]$Compiler = '', [string]$Notes = 'LANPUT installer and online update checking.')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $PSBoundParameters.ContainsKey('FeedUrl')) {
    $defaults = Get-Content (Join-Path $root 'installer/update-defaults.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $FeedUrl = [string]$defaults.FeedUrl
}
$projectXml = New-Object System.Xml.XmlDocument
$projectXml.Load((Join-Path $root 'src/LANPUT.Windows/LANPUT.Windows.csproj'))
$version = [string]$projectXml.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Packaging requires a stable major.minor.patch version.' }
foreach ($url in @($FeedUrl, $DownloadUrl)) {
    if ($url -and ([uri]$url).Scheme -ne 'https') { throw 'Release URLs must use HTTPS.' }
}
if (-not $Compiler) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $Compiler = $command.Source }
    else {
        $Compiler = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 7\ISCC.exe") |
            Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
}
if (-not $Compiler -or -not (Test-Path -LiteralPath $Compiler)) { throw 'Install Inno Setup (ISCC.exe) or pass -Compiler.' }
& (Join-Path $PSScriptRoot 'build.ps1') -SkipArchive
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$destination = Join-Path $root 'dist/LANPUT-win-x64'
@{ FeedUrl = $FeedUrl; CheckOnStartup = $true } | ConvertTo-Json | Set-Content (Join-Path $destination 'update-defaults.json') -Encoding UTF8
& $Compiler "/DAppVersion=$version" (Join-Path $root 'installer/LANPUT.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$installer = Join-Path $root "dist/LANPUT-$version-Setup-x64.exe"
$portable = Join-Path $root "dist/LANPUT-$version-win-x64.zip"
Compress-Archive -Path (Join-Path $destination '*') -DestinationPath $portable -Force
$hashes = @($installer, $portable) | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $(Split-Path $_ -Leaf)"
}
$hashes | Set-Content (Join-Path $root 'dist/SHA256SUMS.txt') -Encoding ASCII
if ($DownloadUrl) {
    @{ version = $version; downloadUrl = $DownloadUrl; notes = $Notes } |
        ConvertTo-Json | Set-Content (Join-Path $root 'dist/latest.json') -Encoding UTF8
} else {
    Write-Host 'No download URL supplied; latest.example.json is a template, not a live feed.'
    @{ version = $version; downloadUrl = 'https://YOUR-DOMAIN.example/releases/'; notes = 'Replace this URL before publishing.' } |
        ConvertTo-Json | Set-Content (Join-Path $root 'dist/latest.example.json') -Encoding UTF8
}
Write-Host "Installer: $installer"
Write-Host "Portable: $portable"
