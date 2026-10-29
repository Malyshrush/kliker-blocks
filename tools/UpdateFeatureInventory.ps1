param([switch]$Check)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$catalogPath = Join-Path $projectRoot '..\site\docs\data\blocks-ru.json'
$documentPath = Join-Path $projectRoot 'FEATURES.md'
if (-not (Test-Path -LiteralPath $catalogPath)) { throw "Block catalog is missing: $catalogPath" }
if (-not (Test-Path -LiteralPath $documentPath)) { throw "Feature document is missing: $documentPath" }

$blocks = @(Get-Content -LiteralPath $catalogPath -Raw -Encoding utf8 | ConvertFrom-Json)
if ($blocks.Count -eq 0) { throw 'Block catalog is empty.' }
$kinds = @($blocks | ForEach-Object { $_.kind })
if (@($kinds | Sort-Object -Unique).Count -ne $blocks.Count) { throw 'Duplicate block kinds in catalog.' }

function Escape-Cell([string]$value) {
    $value.Replace('|', '\|').Replace("`r`n", '<br>').Replace("`n", '<br>').Trim()
}

$lines = [Collections.Generic.List[string]]::new()
$lines.Add("Всего блоков: **$($blocks.Count)**. Описание отражает опубликованную справку блока; для входов, выходов, настроек и примеров откройте саму справку.")
$lines.Add('')
foreach ($group in $blocks | Group-Object category | Sort-Object Name) {
    $lines.Add("### $($group.Name)")
    $lines.Add('')
    $lines.Add('| Блок (вид) | Что делает / зачем нужен |')
    $lines.Add('| --- | --- |')
    foreach ($block in $group.Group | Sort-Object title) {
        $title = Escape-Cell $block.title
        $kind = Escape-Cell $block.kind
        $description = Escape-Cell $block.help
        $lines.Add(('| **{0}** (`{1}`) | {2} |' -f $title, $kind, $description))
    }
    $lines.Add('')
}

$document = Get-Content -LiteralPath $documentPath -Raw -Encoding utf8
$start = '<!-- BLOCK_INVENTORY_START -->'
$end = '<!-- BLOCK_INVENTORY_END -->'
$startAt = $document.IndexOf($start, [StringComparison]::Ordinal)
$endAt = $document.IndexOf($end, [StringComparison]::Ordinal)
if ($startAt -lt 0 -or $endAt -lt $startAt) { throw 'Feature inventory markers are missing or reversed.' }
$generated = $start + "`n" + ($lines -join "`n") + $end
$updated = $document.Substring(0, $startAt) + $generated + $document.Substring($endAt + $end.Length)
if ($Check) {
    if ($updated -ne $document) { throw 'FEATURES.md block inventory is stale. Run tools/UpdateFeatureInventory.ps1.' }
    Write-Output "Feature inventory is current: $($blocks.Count) blocks."
} else {
    [IO.File]::WriteAllText($documentPath, $updated, [Text.UTF8Encoding]::new($false))
    Write-Output "Updated FEATURES.md: $($blocks.Count) blocks."
}
