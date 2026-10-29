param(
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [Parameter(Mandatory = $true)][string]$Executable,
    [Parameter(Mandatory = $true)][string]$Output,
    [switch]$Maximize,
    [switch]$ScreenCapture
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ReleaseWindowCapture {
    [StructLayout(LayoutKind.Sequential)] public struct Rect {
        public int Left, Top, Right, Bottom;
    }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
}
'@
$expected = (Resolve-Path -LiteralPath $Executable).Path
$target = Get-Process -Id $ProcessId -ErrorAction Stop
if ($target.Path -ne $expected) { throw "Process path does not match the requested test build." }
$window = $target.MainWindowHandle
if ($window -eq [IntPtr]::Zero) { throw 'The test build has no visible main window.' }
if ($Maximize) {
    [void][ReleaseWindowCapture]::ShowWindow($window, 3)
    Start-Sleep -Milliseconds 350
}
if ($ScreenCapture) {
    [void][ReleaseWindowCapture]::SetForegroundWindow($window)
    Start-Sleep -Milliseconds 700
}
$rect = New-Object ReleaseWindowCapture+Rect
if (-not [ReleaseWindowCapture]::GetWindowRect($window, [ref]$rect)) { throw 'Cannot read window bounds.' }
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
if ($width -lt 300 -or $height -lt 200 -or $width -gt 5000 -or $height -gt 4000) {
    throw "Unexpected window size: $width x $height"
}
$bitmap = [Drawing.Bitmap]::new($width, $height)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
try {
    if ($ScreenCapture) {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [Drawing.Size]::new($width, $height))
    }
    else {
        $dc = $graphics.GetHdc()
        try { $ok = [ReleaseWindowCapture]::PrintWindow($window, $dc, 0) }
        finally { $graphics.ReleaseHdc($dc) }
        if (-not $ok) { throw 'PrintWindow failed.' }
    }
    $bitmap.Save($Output, [Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}
Write-Output "Captured $width x ${height}: $Output"
