param([Parameter(Mandatory = $true)][string]$Version)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$site = Join-Path (Split-Path $root -Parent) 'site\docs\index.html'
$project = Get-Content -LiteralPath (Join-Path $root 'KLIKER Blocks.csproj') -Raw
$installer = Get-Content -LiteralPath (Join-Path $root 'Installer\KlikerBlocks.iss') -Raw
$readme = Get-Content -LiteralPath (Join-Path $root 'README.md') -Raw
$ruHelp = Get-Content -LiteralPath (Join-Path $root 'AppHelp.cs') -Raw
$enHelp = Get-Content -LiteralPath (Join-Path $root 'EnglishHelp.cs') -Raw
if (-not $project.Contains("<Version>$Version</Version>")) { throw 'Project version mismatch.' }
if (-not $installer.Contains("#define AppVersion `"$Version`"")) { throw 'Installer version mismatch.' }
if (-not $readme.Contains("releases/tag/v$Version")) { throw 'README release link is stale.' }
if (-not $ruHelp.Contains('AppIdentity.VersionLabel') -or
    -not $enHelp.Contains('AppIdentity.VersionLabel')) { throw 'Built-in help is not tied to assembly version.' }
if (-not (Test-Path -LiteralPath $site)) { throw 'Website source is missing.' }
$html = Get-Content -LiteralPath $site -Raw
foreach ($arch in @('x64', 'x86')) {
    foreach ($kind in @('Setup', 'Portable')) {
        if (-not $html.Contains("KlikerBlocks-$kind-$Version-$arch")) {
            throw "Website download link is stale: $kind $arch"
        }
    }
}
if (-not $html.Contains("$Version")) { throw 'Website version is stale.' }
$release = Join-Path $root ("publish-v$($Version.Replace('.', ''))\RELEASE_NOTES_$Version.md")
if (-not (Test-Path -LiteralPath $release)) { throw 'Bilingual release notes are missing.' }
Write-Output "Version $Version matches project, installer, RU/EN help, README, site and release notes."
