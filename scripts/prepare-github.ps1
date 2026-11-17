[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = New-Object System.Xml.XmlDocument
$project.Load((Join-Path $root 'src/LANPUT.Windows/LANPUT.Windows.csproj'))
$version = [string]$project.Project.PropertyGroup.Version
$base = Join-Path $root 'dist/github'
$destination = Join-Path $base 'lanput-ready'
if (Test-Path -LiteralPath $destination) {
    $destination = Join-Path $base ('lanput-ready-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
}
$rootFull = [IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
$destinationFull = [IO.Path]::GetFullPath($destination)
$baseFull = [IO.Path]::GetFullPath($base).TrimEnd('\') + '\'
if (-not $destinationFull.StartsWith($baseFull, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Export must remain inside dist/github.'
}
New-Item -ItemType Directory -Path $destinationFull -Force | Out-Null
$files = New-Object 'System.Collections.Generic.List[System.IO.FileInfo]'
$rootFiles = @('README.md', 'LICENSE', 'CHANGELOG.md', 'CONTRIBUTING.md', 'SECURITY.md',
    'THIRD-PARTY-NOTICES.md', 'VALIDATION.md', 'Directory.Build.props', '.gitignore',
    '.gitattributes', '.editorconfig', 'build-and-run.cmd', 'run.cmd', 'package.cmd')
foreach ($name in $rootFiles) { $files.Add((Get-Item -LiteralPath (Join-Path $root $name))) }
foreach ($folder in @('src', 'tests', 'scripts', 'docs', 'installer', '.github', 'licenses', 'demos')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root $folder) -Recurse -File -Force) {
        $relative = $file.FullName.Substring($rootFull.Length)
        $normalized = $relative.Replace('\', '/')
        if ($normalized -match '(^|/)(bin|obj|node_modules|__pycache__|\.venv|\.git|browser-profile|tests-output)(/|$)') { continue }
        if ($normalized.StartsWith('docs/verification/')) { continue }
        if ($file.Extension -notin @('.cs', '.xaml', '.csproj', '.props', '.targets', '.json', '.md', '.txt', '.ps1', '.cmd', '.py', '.yml', '.yaml', '.png', '.jpg', '.ico', '.bmp', '.iss', '.manifest', '.html', '.css', '.js')) { continue }
        if ($file.Extension -in @('.zip', '.exe', '.dll', '.pdb', '.log', '.nupkg', '.pfx', '.pem', '.key', '.pyc')) { continue }
        if ($file.Name -in @('settings.json', 'desktop.json', 'updates.json', '.env')) { continue }
        if ($file.Name.StartsWith('.env.')) { continue }
        $files.Add($file)
    }
}
$manifest = @()
foreach ($file in $files) {
    $relative = $file.FullName.Substring($rootFull.Length)
    $target = Join-Path $destinationFull $relative
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target
    $manifest += [pscustomobject]@{ path = $relative.Replace('\', '/'); bytes = $file.Length }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = Join-Path $base ('LANPUT-' + $version + '-open-source.zip')
if (Test-Path -LiteralPath $archive) {
    $archive = Join-Path $base ('LANPUT-' + $version + '-open-source-' + [Guid]::NewGuid().ToString('N').Substring(0, 8) + '.zip')
}
[IO.Compression.ZipFile]::CreateFromDirectory($destinationFull, $archive, [IO.Compression.CompressionLevel]::Optimal, $true)
$manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath ($archive + '.files.json') -Encoding UTF8
Write-Host ('Source folder: ' + $destinationFull)
Write-Host ('Source archive: ' + $archive)
Write-Host ('Files: ' + $manifest.Count)
Write-Host 'No remote repository, commit, push or Release was created.'
