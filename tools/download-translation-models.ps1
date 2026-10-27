param([int]$ThrottleLimit = 4)

$ErrorActionPreference = 'Stop'
$models = @('ru_en', 'en_ru', 'fr_en', 'en_fr', 'es_en', 'en_es', 'zh_en', 'en_zh', 'de_en', 'en_de')
$destination = Join-Path $PSScriptRoot '..\Assets\TranslationRuntime\models'
New-Item -ItemType Directory -Path $destination -Force | Out-Null

$models | ForEach-Object -Parallel {
    $ErrorActionPreference = 'Continue'
    $PSNativeCommandUseErrorActionPreference = $false
    $name = $_
    $root = $using:destination
    $partial = Join-Path $root ($name + '.argosmodel.partial')
    $complete = Join-Path $root ($name + '.argosmodel')
    if (Test-Path -LiteralPath $complete) {
        "Already downloaded: $name"
        return
    }
    $url = "https://raw.githubusercontent.com/LibreTranslate/LibreTranslate-Models/main/$name.argosmodel"
    for ($attempt = 1; $attempt -le 30; $attempt++) {
        & curl.exe -sS -L -C - --connect-timeout 20 --max-time 120 -o $partial $url 2>$null
        if ($LASTEXITCODE -eq 0) {
            try {
                $zip = [System.IO.Compression.ZipFile]::OpenRead($partial)
                $metadata = $zip.Entries | Where-Object { $_.FullName -eq 'metadata.json' -or $_.FullName -like '*/metadata.json' } | Select-Object -First 1
                if (-not $metadata) { throw "No metadata in $name" }
                $zip.Dispose()
                Move-Item -LiteralPath $partial -Destination $complete
                "Downloaded: $name"
                return
            } catch {
                "Invalid model ${name}: $_"
                return
            }
        }
        Start-Sleep -Seconds 2
    }
    "Download incomplete: $name"
} -ThrottleLimit $ThrottleLimit
