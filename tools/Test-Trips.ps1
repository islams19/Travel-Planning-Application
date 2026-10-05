param([string]$BuildRoot = 'Builds/SavedTrips')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $BuildRoot).Path
$exe = Join-Path $root 'TravelPlannerTrips.exe'
$id = [Guid]::NewGuid().ToString('N')
$file = "auth-smoke-$id.db"
$logs = Join-Path (Get-Location).Path "Logs/SavedTrips/$id"
New-Item -ItemType Directory -Path $logs -Force | Out-Null
foreach ($phase in @('register-save','reopen-save')) {
    $log = Join-Path $logs "$phase.log"
    $arguments = @('-batchmode','-nographics','-tripSmoke','-authFile',$file,'-logFile',('"' + $log + '"'))
    if ($phase -eq 'reopen-save') { $arguments += '-tripReopen' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(150000)) { $process.Kill(); throw "Saved trips $phase timed out. See $log" }
    $process.Refresh()
    $output = Get-Content -LiteralPath $log -Raw
    if ($process.ExitCode -ne 0 -or $output -notmatch 'SAVED_TRIPS_UI_SMOKE_PASS') { throw "Saved trips $phase failed. See $log" }
    Write-Output "PASS $phase - actual trip controls, all item kinds, persistence, isolation and cancelled writes. Log: $log"
    Select-String -LiteralPath $log -Pattern 'SAVED_TRIPS_LOAD_TIMING' | ForEach-Object { $_.Line }
}
