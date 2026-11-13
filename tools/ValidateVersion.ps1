param([Parameter(Mandatory = $true)][string]$Version, [switch]$LocalCandidate, [string]$CandidateSuffix = '')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$site = Join-Path (Split-Path $root -Parent) 'site\docs\index.html'
$project = Get-Content -LiteralPath (Join-Path $root 'KLIKER Blocks.csproj') -Raw
$installer = Get-Content -LiteralPath (Join-Path $root 'Installer\KlikerBlocks.iss') -Raw
$readme = Get-Content -LiteralPath (Join-Path $root 'README.md') -Raw
$bugs = Get-Content -LiteralPath (Join-Path $root 'BUG_REGISTRY.md') -Raw
$ruHelp = Get-Content -LiteralPath (Join-Path $root 'AppHelp.cs') -Raw
$enHelp = Get-Content -LiteralPath (Join-Path $root 'EnglishHelp.cs') -Raw
if (-not $project.Contains("<Version>$Version</Version>")) { throw 'Project version mismatch.' }
if (-not $installer.Contains("#define AppVersion `"$Version`"")) { throw 'Installer version mismatch.' }
if (-not $ruHelp.Contains('AppIdentity.VersionLabel') -or
    -not $enHelp.Contains('AppIdentity.VersionLabel')) { throw 'Built-in help is not tied to assembly version.' }
if (-not (Test-Path -LiteralPath $site)) { throw 'Website source is missing.' }
$node = Get-Command node -ErrorAction SilentlyContinue
if (-not $node) { throw 'Node.js is required to validate website localization.' }
& $node.Source (Join-Path $PSScriptRoot 'ValidateWebsiteLocalization.js')
if ($LASTEXITCODE -ne 0) { throw 'Website localization validation failed.' }

$html = Get-Content -LiteralPath $site -Raw
$releaseVersion = $Version
if ($LocalCandidate) {
    $publishedMatch = [regex]::Match($bugs, 'Последний выпущенный срез:\s*\*\*v(?<version>\d+\.\d+\.\d+)\*\*')
    if (-not $publishedMatch.Success) { throw 'Could not determine the last published version from BUG_REGISTRY.md.' }
    $releaseVersion = $publishedMatch.Groups['version'].Value
    if ($readme -notmatch "(?i)локальный кандидат.*$([regex]::Escape($Version))" -or
        $readme -notmatch "(?i)Local candidate.*$([regex]::Escape($Version))") {
        throw 'README must identify the RU/EN local candidate.'
    }
} elseif (-not $readme.Contains("releases/tag/v$Version")) {
    throw 'README release link is stale.'
}
if (-not $readme.Contains("releases/tag/v$releaseVersion")) { throw 'README published release link is stale.' }
foreach ($arch in @('x64', 'x86')) {
    foreach ($kind in @('Setup', 'Portable')) {
        if (-not $html.Contains("KlikerBlocks-$kind-$releaseVersion-$arch")) {
            throw "Website download link does not match last published release: $kind $arch"
        }
    }
}
if (-not $html.Contains("$releaseVersion")) { throw 'Website version does not match last published release.' }
if (-not $LocalCandidate -and $releaseVersion -ne $Version) { throw 'Public site and registry do not match the release version.' }
$release = Join-Path $root ("publish-v$($Version.Replace('.', ''))$CandidateSuffix\RELEASE_NOTES_$Version.md")
if (-not (Test-Path -LiteralPath $release)) { throw 'Bilingual release notes are missing.' }
if ($LocalCandidate) {
    Write-Output "Local candidate $Version matches project, installer, RU/EN help and release notes; README and site remain on published $releaseVersion."
} else {
    Write-Output "Version $Version matches project, installer, RU/EN help, README, site and release notes."
}
