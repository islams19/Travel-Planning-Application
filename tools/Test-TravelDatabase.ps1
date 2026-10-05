param([string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Builds\TravelDatabase'))
$ErrorActionPreference = 'Stop'
$buildRoot = (Resolve-Path -LiteralPath $BuildDirectory).Path
$exe = Join-Path $buildRoot 'TravelPlannerDatabase.exe'
if (!(Test-Path -LiteralPath $exe)) { throw "Build not found: $exe" }
$logRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Logs\TravelDatabase'))
[IO.Directory]::CreateDirectory($logRoot) | Out-Null
$runId = [Guid]::NewGuid().ToString('N')
$fileName = "travel-smoke-$runId.db"

function Invoke-Database([string]$Name, [string]$FileName, [bool]$ExpectedSuccess, [int]$Copied, [bool]$WriteMarker) {
    $log = Join-Path $logRoot "$Name-$runId.log"
    $arguments = @('-batchmode', '-nographics', '-travelSeedSmoke', '-travelSeedFile', $FileName,
        '-travelSeedExpectedCopied', $Copied, '-travelSeedExpectedUsers', '1', '-travelSeedExpectedTrips', '1',
        '-logFile', ('"' + $log + '"'))
    if ($WriteMarker) { $arguments += '-travelSeedWriteMarker' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(60000)) { Stop-Process -Id $process.Id; throw "Player timed out: $log" }
    $process.Refresh()
    $text = Get-Content -LiteralPath $log -Raw
    $marker = if ($ExpectedSuccess) { 'TRAVEL_DATABASE_PASS' } else { 'TRAVEL_DATABASE_FAIL' }
    $exitCode = if ($ExpectedSuccess) { 0 } else { 1 }
    if ($process.ExitCode -ne $exitCode -or !$text.Contains($marker)) { throw "Unexpected $Name result; see $log" }
    Write-Host "PASS $Name : $log"
    return $text
}

$first = Invoke-Database 'first-copy' $fileName $true 1 $true
$path = [regex]::Match($first, 'path=([^\r\n]+)').Groups[1].Value
if (!$path -or [IO.Path]::GetFileName($path) -ne $fileName) { throw 'Unexpected writable database path.' }
$originalHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
$null = Invoke-Database 'preserve-user-trip' $fileName $true 0 $false
if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $originalHash) { throw 'Second launch rewrote the database.' }

$corruptName = "travel-corrupt-$runId.db"
$corruptPath = Join-Path ([IO.Path]::GetDirectoryName($path)) $corruptName
$bytes = New-Object byte[] 1024
for ($index = 0; $index -lt $bytes.Length; $index++) { $bytes[$index] = 171 }
[IO.File]::WriteAllBytes($corruptPath, $bytes)
$corruptHash = (Get-FileHash -LiteralPath $corruptPath -Algorithm SHA256).Hash
$null = Invoke-Database 'corrupt-existing' $corruptName $false 0 $false
if ((Get-FileHash -LiteralPath $corruptPath -Algorithm SHA256).Hash -ne $corruptHash) { throw 'Corrupt file was replaced.' }

# Test this build's seed packaging only. Preserve the source seed and every existing writable DB.
$seedPath = Join-Path $buildRoot 'TravelPlannerDatabase_Data\StreamingAssets\Database\travel_seed.db'
$disabledPath = $seedPath + '.test-disabled'
if (!(Test-Path -LiteralPath $seedPath) -or (Test-Path -LiteralPath $disabledPath)) { throw 'Unexpected seed file state.' }
if (!$seedPath.StartsWith($buildRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Seed is outside build.' }
Move-Item -LiteralPath $seedPath -Destination $disabledPath
try {
    $null = Invoke-Database 'missing-seed' "travel-missing-$runId.db" $false 1 $false
    $null = Invoke-Database 'existing-without-seed' $fileName $true 0 $false
}
finally { Move-Item -LiteralPath $disabledPath -Destination $seedPath }
Write-Host "All five player checks passed. Test user/trip database retained: $path"
