param([string]$BuildRoot = 'Builds/FlightSearch')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $BuildRoot).Path
$exe = Join-Path $root 'TravelPlannerFlights.exe'
$id = [Guid]::NewGuid().ToString('N')
$file = "auth-smoke-$id.db"
$logs = Join-Path (Get-Location).Path "Logs/FlightSearch/$id"
New-Item -ItemType Directory -Path $logs -Force | Out-Null
foreach ($phase in @('register-search','reopen-search')) {
    $log = Join-Path $logs "$phase.log"
    $arguments = @('-batchmode','-nographics','-flightSmoke','-authFile',$file,'-logFile',('"' + $log + '"'))
    if ($phase -eq 'reopen-search') { $arguments += '-flightReopen' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(120000)) { $process.Kill(); throw "Flight search $phase timed out. See $log" }
    $process.Refresh()
    $output = Get-Content -LiteralPath $log -Raw
    if ($process.ExitCode -ne 0 -or $output -notmatch 'FLIGHT_UI_SMOKE_PASS') { throw "Flight search $phase failed. See $log" }
    Write-Output "PASS $phase - real UI search/filter/sort/cancellation checks. Log: $log"
    Select-String -LiteralPath $log -Pattern 'FLIGHT_SEARCH_TIMING' | ForEach-Object { $_.Line }
}
