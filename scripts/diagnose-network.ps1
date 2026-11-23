[CmdletBinding()]
param([string]$OutputPath = '')
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
if (-not $OutputPath) { $OutputPath = Join-Path $PSScriptRoot ('LANPUT-network-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.txt') }
$report = New-Object 'System.Collections.Generic.List[string]'
function Add-Line([string]$value) { $report.Add($value) }
function Hide-UserPath([string]$value) {
    if ($env:USERPROFILE) { return $value.Replace($env:USERPROFILE, '[USERPROFILE]') }
    return $value
}
function Section([string]$title, [scriptblock]$read) {
    Add-Line ''; Add-Line ('=== ' + $title + ' ===')
    try { & $read } catch { Add-Line ('读取失败: ' + (Hide-UserPath $_.Exception.Message)) }
}
Add-Line 'LANPUT 连接诊断（只读；不会创建规则、申请管理员授权或更改网络）'
Add-Line ('时间: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
Add-Line '本机 HTTP 检查成功不代表手机能通过防火墙访问；仍需外部设备验证。'
$processes = @(Get-Process -Name LANPUT -ErrorAction SilentlyContinue)
$targets = New-Object 'System.Collections.Generic.List[string]'
Section '运行中的 LANPUT' {
    if (-not $processes.Count) { Add-Line '未发现 LANPUT 进程：请先打开软件并开始接收。' }
    foreach ($process in $processes) {
        $path = $process.Path
        if ($path) { $targets.Add($path) }
        Add-Line ('PID={0}; EXE={1}; Version={2}' -f $process.Id, (Hide-UserPath $path), $process.FileVersion)
    }
    $installed = Join-Path $env:LOCALAPPDATA 'Programs/LANPUT/LANPUT.exe'
    if (Test-Path -LiteralPath $installed) {
        if (-not $targets.Contains($installed)) { $targets.Add($installed) }
        Add-Line ('默认安装文件: ' + (Hide-UserPath $installed) + '; Version=' + (Get-Item -LiteralPath $installed).VersionInfo.FileVersion)
    }
}
Section '软件记忆的接收设置（不读取媒体地址）' {
    $settingsFile = Join-Path $env:LOCALAPPDATA 'LANPUT/settings.json'
    if (Test-Path -LiteralPath $settingsFile) {
        $settings = Get-Content -LiteralPath $settingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
        Add-Line ('Address={0}; NetworkName={1}' -f $settings.Address, $settings.NetworkName)
    } else { Add-Line '尚无接收设置：首次启动有多个网卡时，需要手动选网卡并开始接收。' }
    $desktopFile = Join-Path $env:LOCALAPPDATA 'LANPUT/desktop.json'
    if (Test-Path -LiteralPath $desktopFile) {
        $desktop = Get-Content -LiteralPath $desktopFile -Raw -Encoding UTF8 | ConvertFrom-Json
        Add-Line ('AutoReceive=' + $desktop.AutoReceive)
    }
}
Section 'Windows 网络类别与 IPv4（不读取 Wi-Fi 名称、MAC 或公网 IP）' {
    $profiles = @(Get-NetConnectionProfile -ErrorAction Stop)
    $adapters = @(Get-NetAdapter -ErrorAction Stop)
    foreach ($ip in @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction Stop | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' })) {
        $profile = $profiles | Where-Object InterfaceIndex -eq $ip.InterfaceIndex | Select-Object -First 1
        $adapter = $adapters | Where-Object InterfaceIndex -eq $ip.InterfaceIndex | Select-Object -First 1
        Add-Line ('Interface={0}; IPv4={1}/{2}; Category={3}; AdapterStatus={4}; HardwareInterface={5}' -f $ip.InterfaceAlias, $ip.IPAddress, $ip.PrefixLength, $profile.NetworkCategory, $adapter.Status, $adapter.HardwareInterface)
    }
}
$listeners = @()
Section 'TCP 41000 与 UDP 1900 实际监听者' {
    $script:listeners = @(Get-NetTCPConnection -State Listen -LocalPort 41000 -ErrorAction SilentlyContinue)
    if (-not $listeners.Count) { Add-Line 'TCP 41000 未监听：接收服务未启动，或启动失败。' }
    foreach ($listener in $listeners) {
        $owner = Get-Process -Id $listener.OwningProcess -ErrorAction Stop
        Add-Line ('TCP {0}:41000; PID={1}; Program={2}' -f $listener.LocalAddress, $listener.OwningProcess, (Hide-UserPath $owner.Path))
    }
    $endpoints = @(Get-NetUDPEndpoint -LocalPort 1900 -ErrorAction SilentlyContinue)
    foreach ($endpoint in $endpoints) {
        $owner = Get-Process -Id $endpoint.OwningProcess -ErrorAction Stop
        Add-Line ('UDP {0}:1900; PID={1}; Process={2}' -f $endpoint.LocalAddress, $endpoint.OwningProcess, $owner.ProcessName)
    }
    if (-not ($endpoints | Where-Object { $processes.Id -contains $_.OwningProcess })) { Add-Line '未发现 LANPUT 自己的 UDP 1900 监听。Windows SSDP 服务监听不等于 LANPUT 已启动。' }
}
Section '接收地址本机 HTTP 探测（绕过系统代理）' {
    foreach ($listener in @($listeners | Where-Object { $_.LocalAddress -ne '0.0.0.0' -and $_.LocalAddress -notlike '*:*' } | Select-Object -First 4)) {
        $uri = 'http://' + $listener.LocalAddress + ':41000/description.xml'
        $response = $null
        try {
            $request = [Net.HttpWebRequest]::Create($uri); $request.Proxy = $null; $request.Timeout = 3000; $request.ReadWriteTimeout = 3000; $request.AllowAutoRedirect = $false
            $response = $request.GetResponse()
            $reader = New-Object IO.StreamReader($response.GetResponseStream())
            try { $body = $reader.ReadToEnd() } finally { $reader.Dispose() }
            Add-Line ('HTTP {0}; MediaRenderer={1}; Address={2}' -f [int]$response.StatusCode, $body.Contains('MediaRenderer'), $listener.LocalAddress)
        } catch { Add-Line ('HTTP failed; Address={0}; Error={1}' -f $listener.LocalAddress, $_.Exception.GetType().Name) }
        finally { if ($response) { $response.Close() } }
    }
    if (-not $listeners.Count) { Add-Line '未探测：没有监听地址。' }
}
Section '有效防火墙配置' {
    foreach ($profile in @(Get-NetFirewallProfile -PolicyStore ActiveStore -ErrorAction Stop)) {
        Add-Line ('Profile={0}; Enabled={1}; AllowInboundRules={2}; AllowLocalFirewallRules={3}; DefaultInboundAction={4}; NotifyOnListen={5}' -f $profile.Name, $profile.Enabled, $profile.AllowInboundRules, $profile.AllowLocalFirewallRules, $profile.DefaultInboundAction, $profile.NotifyOnListen)
    }
}
Section '当前程序实际生效的入站规则（含允许与阻止）' {
    $filters = @(Get-NetFirewallApplicationFilter -PolicyStore ActiveStore -ErrorAction Stop)
    $matches = @($filters | Where-Object { $targets -icontains [Environment]::ExpandEnvironmentVariables($_.Program) })
    $rules = @($matches | Get-NetFirewallRule -ErrorAction Stop | Where-Object Direction -eq Inbound)
    if (-not $rules.Count) { Add-Line '当前程序路径没有入站规则。其他安装路径的规则不会替代此路径。' }
    foreach ($rule in $rules) {
        $ports = $rule | Get-NetFirewallPortFilter -ErrorAction Stop
        $addresses = $rule | Get-NetFirewallAddressFilter -ErrorAction Stop
        $app = $rule | Get-NetFirewallApplicationFilter -ErrorAction Stop
        Add-Line ('Name={0}; Enabled={1}; Action={2}; Profiles={3}; Protocol={4}; LocalPort={5}; LocalAddress={6}; RemoteAddress={7}; Program={8}; Source={9}' -f $rule.Name, $rule.Enabled, $rule.Action, $rule.Profile, $ports.Protocol, ($ports.LocalPort -join ','), ($addresses.LocalAddress -join ','), ($addresses.RemoteAddress -join ','), (Hide-UserPath $app.Program), $rule.PolicyStoreSourceType)
    }
}
Add-Line ''; Add-Line '若需进一步分析：在 LANPUT 接收设置运行连接检查，并导出软件诊断；保留本报告一起提供。'
Add-Line '报告含局域网 IP 和网卡名称；对外公开前请检查。'
[IO.File]::WriteAllLines([IO.Path]::GetFullPath($OutputPath), $report, (New-Object Text.UTF8Encoding($true)))
Write-Output ('诊断已保存: ' + $OutputPath)
