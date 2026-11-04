# UNTESTED DRAFT. Not yet run on any machine. Review before running.
#
# Egress-blocked A05 run for the disposable desktop host spike. Requires an elevated PowerShell.
#
# WARNING: the WebView2 block rule cuts network access for EVERY WebView2 application on this
# machine (Teams, Outlook, etc.) for the duration of the run. Rules are removed in `finally`.
#
# What it does:
#   1. Enables Filtering Platform audit (failure) so blocked connections produce Security event 5157.
#   2. Adds outbound block rules for the spike exe and the WebView2 runtime exe.
#   3. Launches the spike; records its window state and its descendant processes' remote endpoints.
#   4. Reads event 5157 for the two exe paths; writes everything to egress-blocked-observations.txt.
#   5. Restores the audit setting and removes the rules.
#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'
$out = Join-Path $PSScriptRoot 'egress-blocked-observations.txt'
$exe = (Resolve-Path (Join-Path $PSScriptRoot '..\DesktopHostSpike\publish\win-x64\DesktopHostSpike.exe')).Path
$webview = Get-ChildItem "${env:ProgramFiles(x86)}\Microsoft\EdgeWebView\Application" -Recurse -Filter msedgewebview2.exe -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
$subcat = 'Filtering Platform Connection'
$priorAudit = (auditpol /get /subcategory:"$subcat" /r | ConvertFrom-Csv).'Inclusion Setting'
$rules = @()
$start = Get-Date
try {
    auditpol /set /subcategory:"$subcat" /failure:enable | Out-Null

    $rules += 'dbmapper-spike-block-host'
    New-NetFirewallRule -DisplayName $rules[-1] -Direction Outbound -Program $exe -Action Block | Out-Null
    if ($webview) {
        $rules += 'dbmapper-spike-block-webview'
        New-NetFirewallRule -DisplayName $rules[-1] -Direction Outbound -Program $webview -Action Block | Out-Null
    }

    $proc = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Seconds 20
    $live = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
    "Window title: $($live.MainWindowTitle)  Responding: $($live.Responding)" | Tee-Object -FilePath $out

    # Descendants of the host PID only (not every WebView2 on the machine).
    $all = Get-CimInstance Win32_Process
    $tree = [System.Collections.Generic.List[int]]::new(); $tree.Add($proc.Id)
    for ($i = 0; $i -lt $tree.Count; $i++) {
        $all | Where-Object ParentProcessId -eq $tree[$i] | ForEach-Object { $tree.Add([int]$_.ProcessId) }
    }
    "Host process tree PIDs: $($tree -join ', ')" | Tee-Object -FilePath $out -Append
    Get-NetTCPConnection -ErrorAction SilentlyContinue | Where-Object { $tree -contains $_.OwningProcess -and $_.RemoteAddress -notin '127.0.0.1', '::1', '0.0.0.0', '::' } |
        Select-Object OwningProcess, State, RemoteAddress, RemotePort | Format-Table -AutoSize | Out-String | Tee-Object -FilePath $out -Append

    # Blocked attempts never reach Established; event 5157 carries the application path.
    Get-WinEvent -FilterHashtable @{ LogName = 'Security'; Id = 5157; StartTime = $start } -ErrorAction SilentlyContinue |
        Where-Object { $_.Message -match [regex]::Escape($exe) -or ($webview -and $_.Message -match [regex]::Escape($webview)) } |
        ForEach-Object { $_.TimeCreated.ToString('o') + ' ' + (($_.Message -split "`r?`n") | Where-Object { $_ -match 'Application Name|Destination Address|Destination Port' }) -join ' | ' } |
        Tee-Object -FilePath $out -Append

    Stop-Process -Id $proc.Id -ErrorAction SilentlyContinue
}
finally {
    foreach ($name in $rules) { Remove-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue }
    if ($priorAudit) { auditpol /set /subcategory:"$subcat" /failure:$(if ($priorAudit -match 'Failure') { 'enable' } else { 'disable' }) | Out-Null }
}
