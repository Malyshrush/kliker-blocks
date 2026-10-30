param([string]$Version = '1.2.3')
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
    $existing = $release.assets | Where-Object { $_.name -eq $name } | Select-Object -First 1
    if ($existing -and $existing.state -eq 'uploaded') {
        Write-Output "Already published: $name"
        continue
    }
    if ($existing) { throw "GitHub has an incomplete asset named $name ($($existing.state)); remove it before retrying." }
    $path = Join-Path $releaseDir $name
    $uri = "$uploadBase`?name=$([Uri]::EscapeDataString($name))"
    $responsePath = Join-Path $project ("obj\github-upload-response-$PID.json")
    $curlArgs = @(
        '--retry', '4', '--retry-all-errors', '--retry-delay', '8',
        '--connect-timeout', '30', '--max-time', '3600', '-sS', '-X', 'POST',
        '-H', "Authorization: Bearer $token", '-H', 'Accept: application/vnd.github+json',
        '-H', 'Content-Type: application/octet-stream', '-H', 'User-Agent: KlikerBlocks-release-publisher',
        '-H', 'Expect:', '--data-binary', "@$path", '-o', $responsePath, '-w', '%{http_code}', $uri
    )
    $httpStatus = & curl.exe @curlArgs
    if ($LASTEXITCODE -ne 0) { throw "GitHub upload failed for $name (curl exit $LASTEXITCODE, HTTP $httpStatus)." }
    $asset = Get-Content -LiteralPath $responsePath -Raw | ConvertFrom-Json
    Remove-Item -LiteralPath $responsePath -Force
    if ($asset.state -ne 'uploaded') { throw "Upload not complete: $name ($($asset.state))" }
    Write-Output "Uploaded $name ($($asset.size) bytes)"
}
Write-Output "Release: $($release.html_url)"
