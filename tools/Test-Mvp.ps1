param(
    [string]$Executable = 'Builds/TravelPlannerDevelopment/TravelPlannerDevelopment.exe',
    [ValidateRange(90, 180)][int]$TimeoutSeconds = 150
)
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $Executable).Path
$buildRoot = Split-Path -Parent $exe
# Read the build's identity rather than assuming it matches today's project settings.
$metadataPath = Join-Path $buildRoot 'release-validation.json'
if (-not (Test-Path -LiteralPath $metadataPath)) { throw "Build metadata missing: $metadataPath. Build the current development artifact first." }
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
if ($metadata.developmentBuild -ne $true) { throw 'Test-Mvp requires a development build; use the separate release harness for release QA.' }
foreach ($name in @($metadata.companyName, $metadata.productName)) {
    if ([string]::IsNullOrWhiteSpace($name) -or $name -in @('.', '..') -or $name.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) { throw 'Invalid company/product name in build metadata.' }
}
$profileRoot = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
$persistent = Join-Path (Join-Path (Join-Path $profileRoot 'AppData/LocalLow') $metadata.companyName) $metadata.productName
$normalDatabase = Join-Path $persistent 'travel.db'
$runId = [Guid]::NewGuid().ToString('N')
$logs = Join-Path (Get-Location).Path "Logs/Mvp/$runId"
New-Item -ItemType Directory -Path $logs -Force | Out-Null

function Get-NormalDatabaseState {
    $state = [ordered]@{}
    foreach ($suffix in @('', '-journal', '-wal', '-shm')) {
        $path = $normalDatabase + $suffix
        $state[$suffix] = if (Test-Path -LiteralPath $path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { $null }
    }
    return ($state | ConvertTo-Json -Compress)
}

$features = @(
    @{ Name = 'authentication'; Flag = '-authSmoke'; First = '-authRegister'; Reopen = $null; Marker = 'AUTH_UI_SMOKE_PASS' },
    @{ Name = 'flights'; Flag = '-flightSmoke'; First = $null; Reopen = '-flightReopen'; Marker = 'FLIGHT_UI_SMOKE_PASS' },
    @{ Name = 'destinations'; Flag = '-destinationSmoke'; First = $null; Reopen = '-destinationReopen'; Marker = 'DESTINATION_UI_SMOKE_PASS' },
    @{ Name = 'reviews'; Flag = '-reviewSmoke'; First = $null; Reopen = '-reviewReopen'; Marker = 'REVIEWS_UI_SMOKE_PASS' },
    @{ Name = 'trips'; Flag = '-tripSmoke'; First = $null; Reopen = '-tripReopen'; Marker = 'SAVED_TRIPS_UI_SMOKE_PASS' },
    @{ Name = 'tracking'; Flag = '-trackingSmoke'; First = $null; Reopen = '-trackingReopen'; Marker = 'PRICE_TRACKING_UI_SMOKE_PASS' }
)
$before = Get-NormalDatabaseState
$before | Set-Content -LiteralPath (Join-Path $logs 'personal-database-before.json')
$passed = 0
try {
    foreach ($feature in $features) {
        # One unique database per feature; its second process reuses that same fixture.
        $fixture = 'auth-smoke-' + [Guid]::NewGuid().ToString('N') + '.db'
        foreach ($phase in @('first', 'reopen')) {
            $log = Join-Path $logs ($feature.Name + '-' + $phase + '.log')
            $arguments = @('-batchmode', '-nographics', $feature.Flag, '-authFile', $fixture, '-logFile', ('"' + $log + '"'))
            $extra = if ($phase -eq 'first') { $feature.First } else { $feature.Reopen }
            if ($extra) { $arguments += $extra }
            $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
            if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
                $process.Kill(); $process.WaitForExit()
                throw "$($feature.Name) $phase timed out. See $log"
            }
            $process.Refresh()
            if (-not (Test-Path -LiteralPath $log)) { throw "Player log missing: $log" }
            $output = Get-Content -LiteralPath $log -Raw
            if ($process.ExitCode -ne 0 -or $output -notmatch [regex]::Escape($feature.Marker)) { throw "$($feature.Name) $phase failed (exit $($process.ExitCode)). See $log" }
            if ((Get-NormalDatabaseState) -ne $before) { throw "Personal database or sidecar changed during $($feature.Name) $phase. See $logs" }
            $passed++
            Write-Output "PASS $($feature.Name) $phase - $log"
            Select-String -LiteralPath $log -Pattern '_TIMING' | ForEach-Object { $_.Line }
        }
    }
}
finally {
    $after = Get-NormalDatabaseState
    $after | Set-Content -LiteralPath (Join-Path $logs 'personal-database-after.json')
    if ($after -ne $before) { throw "Personal database guard failed. Compare before/after evidence in $logs; no files were reset or deleted." }
}
Write-Output "MVP_REGRESSION_PASS $passed/12 player scenarios; personal database unchanged. Logs: $logs"
