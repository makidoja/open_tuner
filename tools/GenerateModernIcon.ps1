param(
    [string]$OutputPath = "OpenTunerModern.ico"
)

Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class NativeIconMethods
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool DestroyIcon(IntPtr handle);
}
"@

$size = 64
$bitmap = New-Object System.Drawing.Bitmap $size, $size
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$graphics.Clear([System.Drawing.Color]::Transparent)

$outer = New-Object System.Drawing.Rectangle 2, 2, 60, 60
$gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $outer,
    [System.Drawing.Color]::FromArgb(17, 72, 126),
    [System.Drawing.Color]::FromArgb(5, 22, 38),
    55.0
)
$graphics.FillEllipse($gradient, $outer)

$rim = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(70, 170, 255), 2.5)
$graphics.DrawEllipse($rim, (New-Object System.Drawing.Rectangle 3, 3, 58, 58))

$dish = New-Object System.Drawing.Drawing2D.GraphicsPath
$dish.AddBezier(
    (New-Object System.Drawing.PointF 15, 21),
    (New-Object System.Drawing.PointF 20, 39),
    (New-Object System.Drawing.PointF 34, 47),
    (New-Object System.Drawing.PointF 45, 47)
)
$dish.AddBezier(
    (New-Object System.Drawing.PointF 45, 47),
    (New-Object System.Drawing.PointF 31, 52),
    (New-Object System.Drawing.PointF 18, 43),
    (New-Object System.Drawing.PointF 15, 21)
)
$whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$graphics.FillPath($whiteBrush, $dish)

$whitePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 3.0)
$whitePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$whitePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawLine($whitePen, 28, 39, 43, 26)
$graphics.DrawLine($whitePen, 28, 43, 22, 55)
$graphics.DrawLine($whitePen, 17, 55, 35, 55)

$feedBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(103, 215, 255))
$graphics.FillEllipse($feedBrush, 40, 23, 6, 6)

$wavePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(36, 214, 239), 3.0)
$wavePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$wavePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawArc($wavePen, 34, 10, 18, 18, 285, 72)
$graphics.DrawArc($wavePen, 31, 6, 27, 27, 286, 72)
$graphics.DrawArc($wavePen, 28, 2, 36, 36, 287, 72)

$parent = Split-Path -Parent $OutputPath
if ($parent -and -not (Test-Path $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

$hIcon = $bitmap.GetHicon()
try {
    $icon = [System.Drawing.Icon]::FromHandle($hIcon)
    $stream = [System.IO.File]::Open($OutputPath, [System.IO.FileMode]::Create)
    try {
        $icon.Save($stream)
    }
    finally {
        $stream.Dispose()
        $icon.Dispose()
    }
}
finally {
    [NativeIconMethods]::DestroyIcon($hIcon) | Out-Null
    $wavePen.Dispose()
    $feedBrush.Dispose()
    $whitePen.Dispose()
    $whiteBrush.Dispose()
    $dish.Dispose()
    $rim.Dispose()
    $gradient.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Host "Generated modern OpenTuner icon: $OutputPath"
