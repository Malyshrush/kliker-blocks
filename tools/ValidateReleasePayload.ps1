param([string]$Version = '1.2.1')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$release = Join-Path $root ('publish-v' + ($Version -replace '\.', ''))
& (Join-Path $PSScriptRoot 'ValidateVersion.ps1') -Version $Version
& (Join-Path $PSScriptRoot 'UpdateFeatureInventory.ps1') -Check
function Test-Entry([string]$relative) {
    $name = $relative.Replace('\', '/')
    if ($name -match '(^|/)(computer-capabilities\.json|\.env|appsettings?\.json|LanguageModels|Showcase)(/|$)' -or
        $name -match '\.(log|tmp|bak|ps1|cs)$') { throw "Generated or private file in release: $name" }
    $parts = $name.Split('/')
    if ($parts.Length -eq 1) {
        if ($name -match '\.(dll|exe|pdb)$' -or
            $name -in @('Kliker.Blocks.deps.json', 'Kliker.Blocks.runtimeconfig.json',
                "RELEASE_NOTES_$Version.md")) { return }
        throw "Unexpected release file: $name"
    }
    switch ($parts[0]) {
        'Assets' { if ($name -match '^Assets/Support/[^/]+\.(png|jpg)$') { return } }
        'Examples' { if ($name -match '^Examples/[^/]+\.klb$') { return } }
        'Languages' { if ($name -match '^Languages/en[^/]*\.json$') { return } }
        default { if ($parts[0] -match '^[a-z]{2}(-[A-Za-z]+)?$' -and $name -match '\.dll$') { return } }
    }
    throw "Unexpected release path: $name"
}
foreach ($arch in @('x64', 'x86')) {
    $payload = (Resolve-Path -LiteralPath (Join-Path $root "installer-payload-v$($Version -replace '\.', '')-$arch")).Path
    $files = Get-ChildItem -LiteralPath $payload -File -Recurse
    if (-not ($files.Name -contains 'Kliker.Blocks.exe')) { throw "Executable missing: $arch" }
    foreach ($file in $files) { Test-Entry $file.FullName.Substring($payload.Length + 1) }
    $zipPath = Join-Path $release "KlikerBlocks-Portable-$Version-$arch.zip"
    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName.EndsWith('/')) { continue }
            Test-Entry $entry.FullName
        }
        if ($zip.Entries.Count -ne $files.Count) {
            throw "ZIP and payload differ for ${arch}: $($zip.Entries.Count) versus $($files.Count)"
        }
    } finally { $zip.Dispose() }
    $exe = Join-Path $payload 'Kliker.Blocks.exe'
    $stream = [IO.File]::OpenRead($exe)
    try {
        $reader = [IO.BinaryReader]::new($stream)
        $stream.Position = 0x3c
        $pe = $reader.ReadInt32()
        $stream.Position = $pe + 4
        $machine = $reader.ReadUInt16()
    } finally { $stream.Dispose() }
    if ($machine -ne $(if ($arch -eq 'x64') { 0x8664 } else { 0x14c })) {
        throw "Wrong PE architecture: $arch, 0x$('{0:X4}' -f $machine)"
    }
    Write-Output "${arch}: $($files.Count) files, ZIP verified, PE machine 0x$('{0:X4}' -f $machine)"
}
