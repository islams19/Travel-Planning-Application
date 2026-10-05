param([string]$BuildRoot = 'Builds/DestinationHub')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $BuildRoot).Path
$exe = Join-Path $root 'TravelPlannerDestinations.exe'
$id = [Guid]::NewGuid().ToString('N')
$file = "auth-smoke-$id.db"
$logs = Join-Path (Get-Location).Path "Logs/DestinationHub/$id"
New-Item -ItemType Directory -Path $logs -Force | Out-Null
foreach ($phase in @('register-explore','reopen-explore')) {
    $log = Join-Path $logs "$phase.log"
    $arguments = @('-batchmode','-nographics','-destinationSmoke','-authFile',$file,'-logFile',('"' + $log + '"'))
    if ($phase -eq 'reopen-explore') { $arguments += '-destinationReopen' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(120000)) { $process.Kill(); throw "Destination $phase timed out. See $log" }
    $process.Refresh()
    $output = Get-Content -LiteralPath $log -Raw
    if ($process.ExitCode -ne 0 -or $output -notmatch 'DESTINATION_UI_SMOKE_PASS') { throw "Destination $phase failed. See $log" }
    Write-Output "PASS $phase - real UI destination, navigation, cancellation and recovery checks. Log: $log"
    Select-String -LiteralPath $log -Pattern 'DESTINATION_LOAD_TIMING' | ForEach-Object { $_.Line }
}
