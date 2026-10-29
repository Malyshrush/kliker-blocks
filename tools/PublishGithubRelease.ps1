param([string]$Version = '1.2.0')
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$releaseDir = Join-Path $project ('publish-v' + ($Version -replace '\.', ''))
& (Join-Path $PSScriptRoot 'ValidateReleasePayload.ps1') -Version $Version
$envFile = Join-Path (Split-Path $project -Parent) '.env'
$line = Get-Content -LiteralPath $envFile | Where-Object { $_ -match '^GITHUB_TOKEN=' } | Select-Object -First 1
if (-not $line) { throw 'GITHUB_TOKEN is missing from project .env' }
$token = $line.Substring('GITHUB_TOKEN='.Length).Trim().Trim('"', "'")
if (-not $token) { throw 'GITHUB_TOKEN is empty' }
$headers = @{ Authorization = "Bearer $token"; Accept = 'application/vnd.github+json';
    'X-GitHub-Api-Version' = '2022-11-28'; 'User-Agent' = 'KlikerBlocks-release-publisher' }
$base = 'https://api.github.com/repos/Malyshrush/kliker-blocks'
$tag = "v$Version"
$names = @(
    "KlikerBlocks-Setup-$Version-x64.exe", "KlikerBlocks-Setup-$Version-x86.exe",
    "KlikerBlocks-Portable-$Version-x64.zip", "KlikerBlocks-Portable-$Version-x86.zip",
    "RELEASE_NOTES_$Version.md", "editor-ru.png", "automation-ru.png", "help-ru.png", "support-ru.png"
)
foreach ($name in $names) {
    $path = Join-Path $releaseDir $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing release asset: $path" }
}
try { $release = Invoke-RestMethod -Uri "$base/releases/tags/$tag" -Headers $headers -Method Get }
catch {
    if ([int]$_.Exception.Response.StatusCode -ne 404) { throw }
    $body = @{ tag_name = $tag; target_commitish = 'main'; name = "Kliker Blocks $Version";
        body = [IO.File]::ReadAllText((Join-Path $releaseDir "RELEASE_NOTES_$Version.md"));
        draft = $false; prerelease = $true } | ConvertTo-Json -Depth 4
    $release = Invoke-RestMethod -Uri "$base/releases" -Headers $headers -Method Post -Body $body `
        -ContentType 'application/json; charset=utf-8'
    Write-Output "Created $($release.html_url)"
}
$uploadBase = $release.upload_url.Split('{')[0]
foreach ($name in $names) {
    if ($release.assets | Where-Object { $_.name -eq $name }) {
        Write-Output "Already published: $name"
        continue
    }
    $path = Join-Path $releaseDir $name
    $uri = "$uploadBase`?name=$([Uri]::EscapeDataString($name))"
    $asset = Invoke-RestMethod -Uri $uri -Headers $headers -Method Post -InFile $path `
        -ContentType 'application/octet-stream' -TimeoutSec 3600
    if ($asset.state -ne 'uploaded') { throw "Upload not complete: $name ($($asset.state))" }
    Write-Output "Uploaded $name ($($asset.size) bytes)"
}
Write-Output "Release: $($release.html_url)"
