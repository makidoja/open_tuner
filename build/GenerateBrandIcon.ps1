param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\OpenTunerModern.ico')
)

Add-Type -AssemblyName System.Drawing

$bitmap = New-Object System.Drawing.Bitmap 64, 64
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$graphics.Clear([System.Drawing.Color]::Transparent)

$outer = New-Object System.Drawing.Rectangle 2, 2, 60, 60
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $outer,
    [System.Drawing.Color]::FromArgb(17,72,126),
    [System.Drawing.Color]::FromArgb(5,22,38),
    55.0
)
$graphics.FillEllipse($bg, $outer)
$bg.Dispose()

$rim = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(70,170,255)), 2.5
$graphics.DrawEllipse($rim, (New-Object System.Drawing.Rectangle 3,3,58,58))
$rim.Dispose()

$dish = New-Object System.Drawing.Drawing2D.GraphicsPath
$dish.AddBezier(
    (New-Object System.Drawing.PointF 15,21),
    (New-Object System.Drawing.PointF 20,39),
    (New-Object System.Drawing.PointF 34,47),
    (New-Object System.Drawing.PointF 45,47)
)
$dish.AddBezier(
    (New-Object System.Drawing.PointF 45,47),
    (New-Object System.Drawing.PointF 31,52),
    (New-Object System.Drawing.PointF 18,43),
    (New-Object System.Drawing.PointF 15,21)
)
$whiteBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$graphics.FillPath($whiteBrush, $dish)
$whiteBrush.Dispose()
$dish.Dispose()

$whitePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 3
$whitePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$whitePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawLine($whitePen, 28,39,43,26)
$graphics.DrawLine($whitePen, 28,43,22,55)
$graphics.DrawLine($whitePen, 17,55,35,55)
$whitePen.Dispose()

$feed = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(103,215,255))
$graphics.FillEllipse($feed, (New-Object System.Drawing.RectangleF 40,23,6,6))
$feed.Dispose()

$wave = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(36,214,239)), 3
$wave.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$wave.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawArc($wave, 34,10,18,18,285,72)
$graphics.DrawArc($wave, 31,6,27,27,286,72)
$graphics.DrawArc($wave, 28,2,36,36,287,72)
$wave.Dispose()

$graphics.Dispose()

$handle = $bitmap.GetHicon()
try {
    $icon = [System.Drawing.Icon]::FromHandle($handle)
    $directory = Split-Path -Parent $OutputPath
    if ($directory -and -not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    $stream = [System.IO.File]::Create($OutputPath)
    try {
        $icon.Save($stream)
    }
    finally {
        $stream.Dispose()
    }
}
finally {
    Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NativeIconMethods {
    [DllImport("user32.dll", SetLastError=true)]
    public static extern bool DestroyIcon(IntPtr hIcon);
}
"@ -ErrorAction SilentlyContinue
    [NativeIconMethods]::DestroyIcon($handle) | Out-Null
    $bitmap.Dispose()
}

Write-Host "Generated OpenTuner brand icon: $OutputPath"
