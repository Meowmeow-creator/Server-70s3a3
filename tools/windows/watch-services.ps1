# Local service watchdog: keeps the channel (7002) and entrance (17001) alive.
# Each pass restarts whatever is missing using the start-*.bat helpers, which
# also rotate the evidence logs the services open with CreateNew.
# Started detached (WMI) so it outlives the shell that launched it.
$ErrorActionPreference = 'Continue'
$run = 'D:\70S3A3\local-run'
$log = Join-Path $run 'logs\watchdog.log'
New-Item -ItemType Directory -Force -Path (Join-Path $run 'logs') | Out-Null
function Note([string]$m) { "$(Get-Date -Format s) $m" | Add-Content -Path $log -Encoding UTF8 }
Note "watchdog started"
while ($true) {
    try {
        if (-not (Get-Process -Name 'DNF70-Channel-FG' -ErrorAction SilentlyContinue)) {
            Note "channel missing -> restart"
            Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', (Join-Path $run 'start-channel.bat') -WindowStyle Hidden
            Start-Sleep -Seconds 6
        }
        if (-not (Get-Process -Name 'DNF70-Entrance-FG' -ErrorAction SilentlyContinue)) {
            Note "entrance missing -> restart"
            Start-Process -FilePath 'cmd.exe' -ArgumentList '/c', (Join-Path $run 'start-entrance.bat') -WindowStyle Hidden
            Start-Sleep -Seconds 6
        }
    } catch {
        Note ("error: " + $_.Exception.Message)
    }
    Start-Sleep -Seconds 10
}
