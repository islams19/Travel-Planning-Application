param(
    [string]$BuildDirectory = (Join-Path $PSScriptRoot '..\Builds\SqliteProof')
)
$ErrorActionPreference = 'Stop'
$buildRoot = (Resolve-Path -LiteralPath $BuildDirectory).Path
$exe = Join-Path $buildRoot 'TravelPlannerSqliteProof.exe'
if (!(Test-Path -LiteralPath $exe)) { throw "Build not found: $exe" }
$logRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\Logs\SqliteProof'))
[IO.Directory]::CreateDirectory($logRoot) | Out-Null
$runId = [Guid]::NewGuid().ToString('N')
$fileName = "smoke-$runId.db"

function Invoke-Proof([string]$Name, [string]$DatabaseFile, [int]$ExpectedVisits, [bool]$ExpectSuccess) {
    $log = Join-Path $logRoot "$Name-$runId.log"
    $arguments = @('-batchmode', '-nographics', '-sqliteProofSmoke', '-sqliteProofFile',
        $DatabaseFile, '-sqliteProofExpectedVisits', $ExpectedVisits,
        '-logFile', ('"' + $log + '"'))
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(60000)) {
        Stop-Process -Id $process.Id
        throw "Player timed out: $log"
    }
    $process.Refresh()
    $contents = Get-Content -LiteralPath $log -Raw
    $marker = if ($ExpectSuccess) { 'SQLITE_PROOF_PASS' } else { 'SQLITE_PROOF_FAIL' }
    $code = if ($ExpectSuccess) { 0 } else { 1 }
    if ($process.ExitCode -ne $code -or !$contents.Contains($marker)) {
        throw "Unexpected $Name result (exit $($process.ExitCode)); see $log"
    }
    Write-Host "PASS $Name : $log"
    return $contents
}

$first = Invoke-Proof 'first-launch' $fileName 1 $true
$second = Invoke-Proof 'second-launch' $fileName 2 $true
$createdPattern = 'created=([^ ]+)'
if ([regex]::Match($first, $createdPattern).Groups[1].Value -ne [regex]::Match($second, $createdPattern).Groups[1].Value) {
    throw 'The row creation time changed after restarting the player.'
}
if ($first -notmatch 'existing=False' -or $second -notmatch 'existing=True') {
    throw 'The player did not demonstrate first creation followed by existing-row loading.'
}

# The log supplies the actual persistentDataPath; only new UUID-named test files are written.
$databasePath = [regex]::Match($first, 'path=([^\r\n]+)').Groups[1].Value
if (!$databasePath -or [IO.Path]::GetFileName($databasePath) -ne $fileName) { throw 'Unexpected database path.' }
$corruptName = "corrupt-$runId.db"
$corruptPath = Join-Path ([IO.Path]::GetDirectoryName($databasePath)) $corruptName
$bytes = New-Object byte[] 1024
for ($index = 0; $index -lt $bytes.Length; $index++) { $bytes[$index] = 171 }
[IO.File]::WriteAllBytes($corruptPath, $bytes)
$before = (Get-FileHash -LiteralPath $corruptPath -Algorithm SHA256).Hash
$null = Invoke-Proof 'corrupt-database' $corruptName 1 $false
if ((Get-FileHash -LiteralPath $corruptPath -Algorithm SHA256).Hash -ne $before) { throw 'Corrupt database was overwritten.' }

# Temporarily move only this build's native DLL; always restore it, including on failure.
$native = @(Get-ChildItem -LiteralPath $buildRoot -Filter e_sqlite3.dll -Recurse)
if ($native.Count -ne 1) { throw 'Expected exactly one native SQLite DLL in the player build.' }
$nativePath = $native[0].FullName
if (!$nativePath.StartsWith($buildRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Native DLL is outside the build directory.'
}
$disabledPath = $nativePath + '.test-disabled'
if (Test-Path -LiteralPath $disabledPath) { throw 'A previous disabled DLL exists; restore it before continuing.' }
Move-Item -LiteralPath $nativePath -Destination $disabledPath
try { $null = Invoke-Proof 'missing-native' "missing-$runId.db" 1 $false }
finally { Move-Item -LiteralPath $disabledPath -Destination $nativePath }

Write-Host "All four Windows player checks passed. Test database retained at: $databasePath"
