[CmdletBinding()]
param([switch]$Remove)
$ErrorActionPreference = 'Stop'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run firewall.cmd as Administrator. The receiver itself does not require Administrator.'
}
$group = 'LANPUT receiver'
Get-NetFirewallRule -Group $group -ErrorAction SilentlyContinue | Remove-NetFirewallRule
if ($Remove) { Write-Host 'LANPUT firewall rules removed.'; exit 0 }
$exe = Join-Path $PSScriptRoot 'LANPUT.exe'
if (-not (Test-Path $exe)) { throw 'Run this script from the published folder next to LANPUT.exe.' }
New-NetFirewallRule -DisplayName 'LANPUT HTTP receiver' -Group $group -Direction Inbound -Action Allow -Profile Private -Program $exe -Protocol TCP -LocalPort 41000 -RemoteAddress LocalSubnet | Out-Null
New-NetFirewallRule -DisplayName 'LANPUT SSDP discovery' -Group $group -Direction Inbound -Action Allow -Profile Private -Program $exe -Protocol UDP -LocalPort 1900 -RemoteAddress LocalSubnet | Out-Null
Write-Host 'Allowed TCP 41000 and UDP 1900 for LANPUT on Private networks and LocalSubnet only.'
