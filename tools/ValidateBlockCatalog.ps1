param([Parameter(Mandatory = $true)][string]$ExecutablePath)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$siteRoot = Join-Path (Split-Path $projectRoot -Parent) 'site\docs'
$temporaryRoot = Join-Path $projectRoot ('obj\catalog-validation-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporaryRoot | Out-Null

function Export-Catalog([string]$Language, [string]$OutputPath) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = (Resolve-Path -LiteralPath $ExecutablePath).Path
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    foreach ($argument in @('--export-site-catalog', $Language, $OutputPath)) {
        $start.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::Start($start)
    $process.WaitForExit()
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $OutputPath)) {
        throw "Application failed to export the $Language block catalog (exit $($process.ExitCode))."
    }
}

try {
    $ruExport = Join-Path $temporaryRoot 'blocks-ru.json'
    $enExport = Join-Path $temporaryRoot 'blocks-en.json'
    Export-Catalog 'ru' $ruExport
    Export-Catalog 'en' $enExport
    $ru = @(Get-Content -LiteralPath $ruExport -Raw -Encoding utf8 | ConvertFrom-Json)
    $en = @(Get-Content -LiteralPath $enExport -Raw -Encoding utf8 | ConvertFrom-Json)
    $siteRu = @(Get-Content -LiteralPath (Join-Path $siteRoot 'data\blocks-ru.json') -Raw -Encoding utf8 | ConvertFrom-Json)
    $siteEn = @(Get-Content -LiteralPath (Join-Path $siteRoot 'data\blocks-en.json') -Raw -Encoding utf8 | ConvertFrom-Json)
    foreach ($set in @(@($ru, 'runtime RU'), @($en, 'runtime EN'), @($siteRu, 'site RU'), @($siteEn, 'site EN'))) {
        if ($set[0].Count -eq 0) { throw "The $($set[1]) block catalog is empty." }
        $duplicates = @($set[0] | Group-Object kind | Where-Object Count -gt 1)
        if ($duplicates.Count) { throw "Duplicate block kinds in $($set[1]) catalog: $($duplicates.Name -join ', ')" }
    }
    $runtimeKindsRu = @($ru | ForEach-Object kind)
    $runtimeKindsEn = @($en | ForEach-Object kind)
    $siteKindsRu = @($siteRu | ForEach-Object kind)
    $siteKindsEn = @($siteEn | ForEach-Object kind)
    foreach ($comparison in @(
        @($runtimeKindsRu, $runtimeKindsEn, 'runtime RU/EN'),
        @($runtimeKindsRu, $siteKindsRu, 'runtime/site RU'),
        @($runtimeKindsRu, $siteKindsEn, 'runtime/site EN'))) {
        if (($comparison[0] -join "`n") -cne ($comparison[1] -join "`n")) {
            throw "Block catalog mismatch: $($comparison[2]). Refresh the website catalogs from this executable."
        }
    }
    $index = Get-Content -LiteralPath (Join-Path $siteRoot 'index.html') -Raw -Encoding utf8
    $renderer = Get-Content -LiteralPath (Join-Path $siteRoot 'app.js') -Raw -Encoding utf8
    if ($renderer.Contains('found.slice(0,88)') -or $renderer.Contains('found.slice(0, 88)')) {
        throw 'Website renderer truncates the catalog to 88 cards.'
    }
    $stat = [regex]::Match($index, '<div class="hero-bottom">.*?<span>(\d+)\s*<small data-i18n="statBlocks">',
        [Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $stat.Success -or [int]$stat.Groups[1].Value -ne $ru.Count) {
        throw "Website block counter is stale. App exports $($ru.Count) catalog entries."
    }
    $inventory = Get-Content -LiteralPath (Join-Path $projectRoot 'FEATURES.md') -Raw -Encoding utf8
    if (-not $inventory.Contains("Всего блоков: **$($ru.Count)**.")) {
        throw "FEATURES.md block count is stale. App exports $($ru.Count) catalog entries."
    }
    if (@($ru | Where-Object { [string]::IsNullOrWhiteSpace($_.title) -or [string]::IsNullOrWhiteSpace($_.help) }).Count) {
        throw 'A block is missing its localized title or help text.'
    }
    Write-Output "Block catalog synchronized: $($ru.Count) RU/EN entries, runtime/site/FEATURES.md match."
}
finally {
    Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
}
