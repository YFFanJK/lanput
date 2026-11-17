[CmdletBinding()]
param([switch]$Run, [switch]$SkipArchive)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
# Load XML bytes directly: Windows PowerShell 5.1 Get-Content defaults to ANSI.
$projectXml = New-Object System.Xml.XmlDocument
$projectXml.Load((Join-Path $root 'src/LANPUT.Windows/LANPUT.Windows.csproj'))
$version = [string]$projectXml.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+([-.][A-Za-z0-9.]+)?$') { throw 'Invalid project version.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host 'Install the .NET 10 SDK, then run this script again:' -ForegroundColor Yellow
    Write-Host 'winget install --id Microsoft.DotNet.SDK.10 --exact'
    exit 1
}
if (-not ((dotnet --list-sdks) -match '^10\.')) { throw '.NET 10 SDK is required.' }
dotnet run --project tests/LANPUT.Tests/LANPUT.Tests.csproj --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Protocol tests failed. Publishing stopped.' }
$destination = Join-Path $root 'dist/LANPUT-win-x64'
dotnet publish src/LANPUT.Windows/LANPUT.Windows.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=false -o $destination
if ($LASTEXITCODE -ne 0) { throw 'Windows build failed.' }
Copy-Item (Join-Path $root 'README.md') $destination
Copy-Item (Join-Path $root 'LICENSE') $destination
New-Item -ItemType Directory -Path (Join-Path $destination 'licenses') -Force | Out-Null
Copy-Item (Join-Path $root 'licenses/*') (Join-Path $destination 'licenses') -Recurse -Force
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $destination
Copy-Item (Join-Path $root 'docs/ACCEPTANCE.md') $destination
Copy-Item (Join-Path $root 'docs/CINEMA-ACCEPTANCE.md') $destination
Copy-Item (Join-Path $root 'docs/DESKTOP-ACCEPTANCE.md') $destination
Copy-Item (Join-Path $root 'docs/PLAYBACK-INFO.md') $destination
Copy-Item (Join-Path $root 'docs/CINEMA-0.7.0.md') $destination
Copy-Item (Join-Path $root 'docs/CINEMA-0.7.1.md') $destination
Copy-Item (Join-Path $root 'docs/WINDOW-BOUNDS.md') $destination
Copy-Item (Join-Path $root 'docs/MUSIC-0.8.0.md') $destination
Copy-Item (Join-Path $root 'docs/RELEASE-0.8.1.md') $destination
Copy-Item (Join-Path $root 'docs/RELEASE-0.9.0.md') $destination
Copy-Item (Join-Path $root 'docs/RELEASE-0.9.1.md') $destination
Copy-Item (Join-Path $root 'docs/STARTUP-0.9.2.md') $destination
Copy-Item (Join-Path $root 'docs/STARTUP-0.9.3.md') $destination
Copy-Item (Join-Path $root 'docs/MUSIC-TRANSITION-0.9.4.md') $destination
Copy-Item (Join-Path $root 'docs/VINYL-0.9.0.md') $destination
Copy-Item (Join-Path $root 'scripts/firewall.ps1') $destination
Copy-Item (Join-Path $root 'scripts/firewall.cmd') $destination
Copy-Item (Join-Path $root 'installer/update-defaults.json') $destination
$portableZip = Join-Path $root ("dist/LANPUT-" + $version + "-win-x64.zip")
if (-not $SkipArchive) {
    Compress-Archive -Path (Join-Path $destination '*') -DestinationPath $portableZip -Force
    Write-Host "Portable package: $portableZip" -ForegroundColor Green
}
Write-Host "Ready: $destination/LANPUT.exe" -ForegroundColor Green
Write-Host 'Keep the entire output folder, including libvlc DLLs and plugins.'
if ($Run) { Start-Process (Join-Path $destination 'LANPUT.exe') }
