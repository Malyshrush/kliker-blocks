param([string]$Version = '1.2.3', [switch]$LocalCandidate, [string]$CandidateSuffix = '')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$release = Join-Path $root ('publish-v' + ($Version -replace '\.', '') + $CandidateSuffix)
if ($LocalCandidate) {
    $project = Get-Content -LiteralPath (Join-Path $root 'KLIKER Blocks.csproj') -Raw
    $installer = Get-Content -LiteralPath (Join-Path $root 'Installer\KlikerBlocks.iss') -Raw
    $ruHelp = Get-Content -LiteralPath (Join-Path $root 'AppHelp.cs') -Raw
    $enHelp = Get-Content -LiteralPath (Join-Path $root 'EnglishHelp.cs') -Raw
    if (-not $project.Contains("<Version>$Version</Version>") -or
        -not $installer.Contains("#define AppVersion `"$Version`"")) { throw 'Candidate project or installer version mismatch.' }
    if (-not $ruHelp.Contains('AppIdentity.VersionLabel') -or
        -not $enHelp.Contains('AppIdentity.VersionLabel')) { throw 'Candidate built-in help is not tied to assembly version.' }
    $notes = Join-Path $release "RELEASE_NOTES_$Version.md"
    if (-not (Test-Path -LiteralPath $notes)) { throw 'Candidate bilingual release notes are missing.' }
    $notesText = Get-Content -LiteralPath $notes -Raw
    if (-not ($notesText -match '\p{IsCyrillic}') -or -not $notesText.Contains('Local candidate')) {
        throw 'Candidate release notes must identify the local candidate in Russian and English.'
    }
} else {
    & (Join-Path $PSScriptRoot 'ValidateVersion.ps1') -Version $Version -CandidateSuffix $CandidateSuffix
}
if (-not $LocalCandidate) {
    & (Join-Path $PSScriptRoot 'UpdateFeatureInventory.ps1') -Check
} else {
    Write-Output 'Skipping released-feature inventory check for local candidate; run it before publication.'
}
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
    $setupPath = Join-Path $release "KlikerBlocks-Setup-$Version-$arch.exe"
    if (-not (Test-Path -LiteralPath $setupPath)) { throw "Setup installer missing: $arch" }
    $payloadPath = if ($CandidateSuffix) {
        Join-Path $root "installer-payload-v$($Version -replace '\.', '')$CandidateSuffix-candidate-$arch"
    } else {
        Join-Path $root "installer-payload-v$($Version -replace '\.', '')-notes-$arch"
    }
    if (-not (Test-Path -LiteralPath $payloadPath -PathType Container)) {
        $payloadPath = Join-Path $root "installer-payload-v$($Version -replace '\.', '')-release-$arch"
    }
    if (-not (Test-Path -LiteralPath $payloadPath -PathType Container)) {
        $payloadPath = Join-Path $root "installer-payload-v$($Version -replace '\.', '')-candidate-$arch"
    }
    if (-not (Test-Path -LiteralPath $payloadPath -PathType Container)) {
        $payloadPath = Join-Path $root "installer-payload-v$($Version -replace '\.', '')-final-$arch"
    }
    $payload = (Resolve-Path -LiteralPath $payloadPath).Path
    $files = Get-ChildItem -LiteralPath $payload -File -Recurse
    if (-not ($files.Name -contains 'Kliker.Blocks.exe')) { throw "Executable missing: $arch" }
    foreach ($file in $files) { Test-Entry $file.FullName.Substring($payload.Length + 1) }
    $zipPath = Join-Path $release "KlikerBlocks-Portable-$Version-$arch.zip"
    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entryFiles = @($zip.Entries | Where-Object { -not $_.FullName.EndsWith('/') })
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName.EndsWith('/')) { continue }
            Test-Entry $entry.FullName
        }
        if ($entryFiles.Count -ne $files.Count) {
            throw "ZIP and payload differ for ${arch}: $($entryFiles.Count) files versus $($files.Count)"
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
