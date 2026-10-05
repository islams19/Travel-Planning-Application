param([string]$BuildRoot = 'Builds/Authentication')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $BuildRoot).Path
$exe = Join-Path $root 'TravelPlannerAuthentication.exe'
$run = [Guid]::NewGuid().ToString('N')
$file = "auth-smoke-$run.db"
$logs = Join-Path (Get-Location).Path "Logs/Authentication/$run"
New-Item -ItemType Directory -Path $logs -Force | Out-Null
foreach ($phase in @('register','reopen')) {
    $log = Join-Path $logs "$phase.log"
    $arguments = @('-batchmode','-nographics','-authSmoke','-authFile',$file,'-logFile',('"' + $log + '"'))
    if ($phase -eq 'register') { $arguments += '-authRegister' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(90000)) { $process.Kill(); throw "Authentication $phase timed out." }
    $process.Refresh()
    $output = Get-Content -LiteralPath $log -Raw
    if ($process.ExitCode -ne 0 -or $output -notmatch 'AUTH_UI_SMOKE_PASS') { throw "Authentication $phase failed. See $log" }
    Write-Output "PASS $phase - real UI registration/login/logout checks. Log: $log"
}
