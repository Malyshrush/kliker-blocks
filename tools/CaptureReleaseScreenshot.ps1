param([string]$Output = 'screenshots\editor-ru.png', [string]$PreviewArgument = '--ui-preview',
    [switch]$Resize)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WindowCaptureNative {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out RECT rect);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr handle, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr handle, int x, int y, int width, int height, bool repaint);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
'@
$null = [WindowCaptureNative]::SetProcessDPIAware()
$project = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $project 'bin\Release\net10.0-windows10.0.19041.0\Kliker.Blocks.exe'
$target = Join-Path $project $Output
New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
$process = Start-Process -FilePath $exe -ArgumentList $PreviewArgument -PassThru
try {
    $handle = [IntPtr]::Zero
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 200
        $process.Refresh()
        $handle = $process.MainWindowHandle
        if ($handle -ne [IntPtr]::Zero) { break }
    }
    if ($handle -eq [IntPtr]::Zero) { throw 'Preview window did not appear.' }
    if ($Resize) { $null = [WindowCaptureNative]::MoveWindow($handle, 40, 40, 1500, 900, $true) }
    Start-Sleep -Milliseconds 700
    $rect = [WindowCaptureNative+RECT]::new()
    if (-not [WindowCaptureNative]::GetWindowRect($handle, [ref]$rect)) { throw 'Cannot read preview bounds.' }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    $bitmap = [Drawing.Bitmap]::new($width, $height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $dc = $graphics.GetHdc()
        try { $ok = [WindowCaptureNative]::PrintWindow($handle, $dc, 2) }
        finally { $graphics.ReleaseHdc($dc) }
        if (-not $ok) { throw 'PrintWindow failed.' }
        $bitmap.Save($target, [Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
    Write-Output "Captured $target ($width x $height)"
} finally {
    if (-not $process.HasExited) { $null = $process.CloseMainWindow(); $null = $process.WaitForExit(5000) }
}
