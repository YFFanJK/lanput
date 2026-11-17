[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$folder = Join-Path ([IO.Path]::GetTempPath()) ('LANPUT-xml-test-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $folder | Out-Null
    # Use character codes so this test script itself remains ASCII in PowerShell 5.1.
    $description = [string][char]0x5C40 + [char]0x57DF + [char]0x7F51 + [char]0x6295 + [char]0x5C4F + [char]0x5668
    $xmlText = '<Project><PropertyGroup><Description>' + $description + '</Description><Version>0.4.2</Version></PropertyGroup></Project>'
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $projectPath = Join-Path $folder 'project.csproj'
    [IO.File]::WriteAllText($projectPath, $xmlText, $utf8)
    $xml = New-Object System.Xml.XmlDocument
    $xml.Load($projectPath)
    if ([string]$xml.Project.PropertyGroup.Description -cne $description) { throw 'UTF-8 description changed.' }
    if ([string]$xml.Project.PropertyGroup.Version -cne '0.4.2') { throw 'Version changed.' }
    # Exercise the actual build preflight against the real project without compiling/publishing.
    $root = Split-Path $PSScriptRoot -Parent
    $xml.Load((Join-Path $root 'src/LANPUT.Windows/LANPUT.Windows.csproj'))
    if ([string]$xml.Project.PropertyGroup.Version -cne '0.4.2') { throw 'Actual project version changed.' }
    Write-Host 'PASS XML byte loading preserves UTF-8 Chinese metadata without BOM and reads the real project version.'
}
finally { if (Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder -Recurse -Force } }
