param([string]$BuildRoot = 'Builds/TravelPlannerRelease', [switch]$Screenshots)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $BuildRoot).Path
$exe = Join-Path $root 'TravelPlanner.exe'
$settings = Get-Content -LiteralPath (Join-Path $root 'release-validation.json') -Raw | ConvertFrom-Json
if ($settings.developmentBuild -ne $false) { throw 'This harness requires the nondevelopment release artifact.' }
foreach ($name in @($settings.companyName, $settings.productName)) {
    if ([string]::IsNullOrWhiteSpace($name) -or $name -in @('.', '..') -or $name.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) { throw 'Invalid company/product name in build metadata.' }
}
$persistent = Join-Path ([Environment]::GetFolderPath('UserProfile')) ('AppData/LocalLow/' + $settings.companyName + '/' + $settings.productName)
$normal = Join-Path $persistent 'travel.db'
function Get-NormalState {
    $state = @{}
    foreach ($suffix in @('', '-wal', '-shm', '-journal')) {
        $path = $normal + $suffix
        $state[$suffix] = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { 'absent' }
    }
    return $state
}
$normalBefore = Get-NormalState
$id = [Guid]::NewGuid().ToString('N')
$file = "auth-smoke-$id.db"
$logs = Join-Path (Get-Location).Path "Logs/Release/$id"
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$normalBefore | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logs 'personal-database-before.json')

function Invoke-Release([string]$Name, [string[]]$Flags, [bool]$ExpectedSuccess, [bool]$Graphics = $false) {
    $log = Join-Path $logs ($Name + '.log')
    $arguments = @('-logFile', ('"' + $log + '"')) + $Flags
    if ($Graphics) { $arguments += @('-force-d3d11', '-screen-fullscreen', '0') }
    else { $arguments += @('-batchmode', '-nographics') }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $timeout = if ($ExpectedSuccess) { 240000 } else { 20000 }
    if (-not $process.WaitForExit($timeout)) { $process.Kill(); throw "Release check $Name timed out. See $log" }
    $process.Refresh()
    $output = Get-Content -LiteralPath $log -Raw
    if ($ExpectedSuccess) {
        if ($process.ExitCode -ne 0 -or $output -notmatch 'RELEASE_UI_SMOKE_PASS development=False') { throw "Release check $Name failed. See $log" }
    } else {
        if ($process.ExitCode -eq 0 -or $output -notmatch 'AUTH_QA_ARGUMENTS_REJECTED|RELEASE_UI_SMOKE_FAIL') { throw "Invalid release arguments were not rejected in $Name. See $log" }
    }
    Write-Output "PASS $Name - $log"
}

try {
    Invoke-Release 'reject-missing-file' @('-releaseSmoke') $false
    Invoke-Release 'reject-normal-file' @('-releaseSmoke', '-authFile', 'travel.db') $false
    Invoke-Release 'reject-malformed-guid' @('-releaseSmoke', '-authFile', 'auth-smoke-not-a-guid.db') $false
    Invoke-Release 'reject-traversal' @('-releaseSmoke', '-authFile', '../travel.db') $false
    Invoke-Release 'reject-duplicate-file' @('-releaseSmoke', '-authFile', $file, '-authFile', $file) $false
    Invoke-Release 'reject-combined-smoke' @('-releaseSmoke', '-authSmoke', '-authFile', $file) $false
    Invoke-Release 'reject-development-flag' @('-flightSmoke', '-authFile', $file) $false
    if (Test-Path -LiteralPath (Join-Path $persistent $file)) { throw 'Invalid argument checks created the isolated fixture unexpectedly.' }
    $flags = @('-releaseSmoke', '-authFile', $file)
    if ($Screenshots) {
        $captures = Join-Path $logs 'Screenshots'
        $flags += @('-releaseScreenshots', '-releaseCaptureDir', ('"' + $captures + '"'))
    }
    Invoke-Release 'first-launch' $flags $true $Screenshots.IsPresent
    if (-not (Test-Path -LiteralPath (Join-Path $persistent $file))) { throw 'First launch did not create the isolated fixture.' }
    Invoke-Release 'reopen' @('-releaseSmoke', '-releaseReopen', '-authFile', $file) $true
    if (-not (Test-Path -LiteralPath (Join-Path $persistent $file))) { throw 'Reopen lost the isolated fixture.' }
    if ($Screenshots -and @(Get-ChildItem -LiteralPath $captures -Filter '*.png').Count -ne 36) { throw 'Expected eight normal screens plus a login error at four resolutions (36 PNG files).' }
} finally {
    $normalAfter = Get-NormalState
    $normalAfter | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logs 'personal-database-after.json')
    foreach ($suffix in $normalBefore.Keys) {
        if ($normalBefore[$suffix] -ne $normalAfter[$suffix]) { throw "The normal user database or sidecar changed: $normal$suffix" }
    }
}
Write-Output "PASS normal database and sidecar preservation. Isolated fixture: $file"
if ($Screenshots) { Write-Output "Screenshots require visual inspection: $captures" }
