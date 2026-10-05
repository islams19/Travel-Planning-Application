param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.Net.Http

# Only these pinned packages and four runtime files are imported. Unity supplies
# its own System.Memory/Buffers facades; do not import a second framework copy.
$packages = @(
    @{ Id='sqlite-net-pcl'; Version='1.11.285'; Hash='0589DC86602ADC062B9A89D3E712283CA489066EE507F3F1B0398407657C2550'; Entry='lib/netstandard2.0/SQLite-net.dll'; Output='Managed/SQLite-net.dll' },
    @{ Id='sqlitepclraw.core'; Version='3.0.3'; Hash='30BB8DECACDF856C955F78B8A62E79290A7D27C251F37B020AF79E52EB032AAC'; Entry='lib/netstandard2.0/SQLitePCLRaw.core.dll'; Output='Managed/SQLitePCLRaw.core.dll' },
    @{ Id='sqlitepclraw.provider.e_sqlite3'; Version='3.0.3'; Hash='6EB54FBBA60405B2AD7CAADFE4F57CB03659B269591602F92AC7BA14FD35B85D'; Entry='lib/netstandard2.0/SQLitePCLRaw.provider.e_sqlite3.dll'; Output='Managed/SQLitePCLRaw.provider.e_sqlite3.dll' },
    @{ Id='sourcegear.sqlite3'; Version='3.53.3'; Hash='107F90869538E964DEDBAD9B74922FA04092ADB078DE4791F99709CA9ED9F050'; Entry='runtimes/win-x64/native/e_sqlite3.dll'; Output='x86_64/e_sqlite3.dll' }
)
$destination = Join-Path $ProjectRoot 'Assets/Plugins/SQLite'
New-Item -ItemType Directory -Force -Path $destination,(Join-Path $destination 'Managed'),(Join-Path $destination 'x86_64'),(Join-Path $destination 'Licenses') | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)
function Get-BytesHash([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-','') }
    finally { $sha.Dispose() }
}
function Write-PluginMeta([string]$Path, [bool]$Native) {
    # Preserve GUIDs on reimport. First-import GUID is stable for this relative path.
    if (Test-Path -LiteralPath ($Path + '.meta')) { return }
    $guid = (Get-BytesHash $utf8.GetBytes('TravelPlanning.SQLite/' + [IO.Path]::GetFileName($Path))).Substring(0,32).ToLowerInvariant()
    $cpu = if ($Native) { 'x86_64' } else { 'AnyCPU' }
    $meta = @"
fileFormatVersion: 2
guid: $guid
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any:
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 1
      settings:
        CPU: $cpu
        OS: Windows
  - first:
      Standalone: Win64
    second:
      enabled: 1
      settings:
        CPU: $cpu
  userData:
  assetBundleName:
  assetBundleVariant:
"@
    [IO.File]::WriteAllText($Path + '.meta', $meta + "`n", $utf8)
}
$client = New-Object Net.Http.HttpClient
$inventory = @()
try {
    foreach ($package in $packages) {
        $id = $package.Id; $version = $package.Version
        $url = "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.$version.nupkg"
        $bytes = $client.GetByteArrayAsync($url).GetAwaiter().GetResult()
        if ((Get-BytesHash $bytes) -ne $package.Hash) { throw "Package SHA-256 mismatch: $id $version" }
        $stream = New-Object IO.MemoryStream(,$bytes)
        $archive = New-Object IO.Compression.ZipArchive($stream)
        try {
            $entry = $archive.GetEntry($package.Entry)
            if ($null -eq $entry) { throw "Missing package entry: $($package.Entry)" }
            $target = Join-Path $destination $package.Output
            $inputStream = $entry.Open(); $outputStream = [IO.File]::Create($target)
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose(); $inputStream.Dispose() }
            Write-PluginMeta $target ($package.Output -like 'x86_64/*')
            # Preserve original metadata and supplied license text from each package.
            foreach ($notice in $archive.Entries | Where-Object { $_.FullName -match '(^LICENSE\.txt$|\.nuspec$)' }) {
                $reader = New-Object IO.StreamReader($notice.Open())
                try { [IO.File]::WriteAllText((Join-Path $destination "Licenses/$id-$([IO.Path]::GetFileName($notice.FullName))"), $reader.ReadToEnd(), $utf8) }
                finally { $reader.Dispose() }
            }
            $inventory += [ordered]@{ package=$id; version=$version; source=$url; packageSha256=$package.Hash; packageEntry=$package.Entry; file=$package.Output; fileSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $target).Hash }
        } finally { $archive.Dispose(); $stream.Dispose() }
    }
    $licenseUrl = 'https://raw.githubusercontent.com/ericsink/SQLitePCL.raw/f3f967adafad853375597a6387cacb7573c5b343/LICENSE.TXT'
    $license = $client.GetStringAsync($licenseUrl).GetAwaiter().GetResult()
    [IO.File]::WriteAllText((Join-Path $destination 'Licenses/SQLitePCLRaw-APACHE-2.0.txt'), $license, $utf8)
    [IO.File]::WriteAllText((Join-Path $destination 'dependency-inventory.json'), ($inventory | ConvertTo-Json -Depth 4) + "`n", $utf8)
} finally { $client.Dispose() }
Write-Output 'Imported pinned SQLite libraries. Reopen Unity after replacing a loaded native DLL.'
