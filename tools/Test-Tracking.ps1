param([string]$BuildRoot = 'Builds/PriceTracking')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $BuildRoot).Path
$exe = Join-Path $root 'TravelPlannerTracking.exe'
$id = [Guid]::NewGuid().ToString('N')
$file = "auth-smoke-$id.db"
$logs = Join-Path (Get-Location).Path "Logs/PriceTracking/$id"
New-Item -ItemType Directory -Path $logs -Force | Out-Null
foreach ($phase in @('register-track','reopen-track')) {
    $log = Join-Path $logs "$phase.log"
    $arguments = @('-batchmode','-nographics','-trackingSmoke','-authFile',$file,'-logFile',('"' + $log + '"'))
    if ($phase -eq 'reopen-track') { $arguments += '-trackingReopen' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(180000)) { $process.Kill(); throw "Price tracking $phase timed out. See $log" }
    $process.Refresh()
    $output = Get-Content -LiteralPath $log -Raw
    if ($process.ExitCode -ne 0 -or $output -notmatch 'PRICE_TRACKING_UI_SMOKE_PASS') { throw "Price tracking $phase failed. See $log" }
    Write-Output "PASS $phase - two-account tracking, shared price events, badges, read state, demo controls and cancellation. Log: $log"
    Select-String -LiteralPath $log -Pattern 'PRICE_TRACKING_LOAD_TIMING' | ForEach-Object { $_.Line }
}
