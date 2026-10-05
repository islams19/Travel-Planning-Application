param([string]$BuildRoot = 'Builds/Reviews')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $BuildRoot).Path
$exe = Join-Path $root 'TravelPlannerReviews.exe'
$id = [Guid]::NewGuid().ToString('N')
$file = "auth-smoke-$id.db"
$logs = Join-Path (Get-Location).Path "Logs/Reviews/$id"
New-Item -ItemType Directory -Path $logs -Force | Out-Null
foreach ($phase in @('register-reviews','reopen-reviews')) {
    $log = Join-Path $logs "$phase.log"
    $arguments = @('-batchmode','-nographics','-reviewSmoke','-authFile',$file,'-logFile',('"' + $log + '"'))
    if ($phase -eq 'reopen-reviews') { $arguments += '-reviewReopen' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(120000)) { $process.Kill(); throw "Review $phase timed out. See $log" }
    $process.Refresh()
    $output = Get-Content -LiteralPath $log -Raw
    if ($process.ExitCode -ne 0 -or $output -notmatch 'REVIEWS_UI_SMOKE_PASS') { throw "Review $phase failed. See $log" }
    Write-Output "PASS $phase - review UI, stars, fake browser, empty/invalid-link, cancellation and recovery checks. Log: $log"
    Select-String -LiteralPath $log -Pattern 'REVIEWS_LOAD_TIMING' | ForEach-Object { $_.Line }
}
