Add-Type -AssemblyName System.Drawing

$assetDirectory = Join-Path $PSScriptRoot '..\Assets'
New-Item -ItemType Directory -Path $assetDirectory -Force | Out-Null
$target = Join-Path $assetDirectory 'Kliker.ico'
$sizes = @(16, 32, 48, 256)
$images = @()

foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $factor = $size / 256.0
    $graphics.ScaleTransform([single]$factor, [single]$factor)

    $background = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(30, 45, 70))
    $blue = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(73, 184, 237))
    $orange = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 175, 68))
    $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $line = [System.Drawing.Pen]::new([System.Drawing.Color]::White, 13)
    $line.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $line.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.FillEllipse($background, 8, 8, 240, 240)
    $graphics.FillRectangle($blue, 43, 68, 91, 58)
    $graphics.FillRectangle($orange, 121, 153, 91, 58)
    $graphics.DrawLine($line, 127, 98, 161, 98)
    $graphics.DrawLine($line, 161, 98, 161, 154)
    $graphics.FillEllipse($white, 35, 89, 16, 16)
    $graphics.FillEllipse($white, 207, 173, 16, 16)
    $graphics.FillEllipse($white, 117, 89, 16, 16)
    $graphics.FillEllipse($white, 153, 145, 16, 16)

    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,$stream.ToArray()
    $stream.Dispose()
    $line.Dispose()
    $white.Dispose()
    $orange.Dispose()
    $blue.Dispose()
    $background.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

$output = [System.IO.File]::Create($target)
$writer = [System.IO.BinaryWriter]::new($output)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
for ($index = 0; $index -lt $images.Count; $index++) {
    $size = $sizes[$index]
    $writer.Write([byte]($size % 256))
    $writer.Write([byte]($size % 256))
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$index].Length)
    $writer.Write([uint32]$offset)
    $offset += $images[$index].Length
}
foreach ($bytes in $images) { $writer.Write([byte[]]$bytes) }
$writer.Dispose()
Write-Output $target
