<#
.SYNOPSIS
    Draw the eye icon and write assets\augensicher.ico.

.DESCRIPTION
    The same eye the PowerShell version drew at run time, now drawn once at
    every size Windows asks for (16 to 256 px) and stored as PNG frames in
    one .ico. Run again only if the drawing changes; the .ico is committed.
#>
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$Root = Split-Path -Parent $PSScriptRoot
$Out = Join-Path $Root 'assets\augensicher.ico'
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null

function Draw-Eye([int]$size) {
    $k = $size / 32.0
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(40, 60, 110)), ([Math]::Max(1.0, 2 * $k))
    $iris = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(50, 110, 200))
    $pupil = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::Black)
    $g.FillEllipse($white, 1 * $k, 8 * $k, 30 * $k, 16 * $k)
    $g.DrawEllipse($pen, 1 * $k, 8 * $k, 30 * $k, 16 * $k)
    $g.FillEllipse($iris, 11 * $k, 8 * $k, 10 * $k, 16 * $k)
    $g.FillEllipse($pupil, 14 * $k, 12 * $k, 4 * $k, 8 * $k)
    $white.Dispose(); $pen.Dispose(); $iris.Dispose(); $pupil.Dispose(); $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$frames = foreach ($s in $sizes) { , (Draw-Eye $s) }

# ICO: 6-byte header, a 16-byte entry per frame, then the PNG data.
$ms = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $ms
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $data = $frames[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }   # 0 means 256 in an ICO entry
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$data.Length); $w.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($data in $frames) { $w.Write($data) }
$w.Flush()
[System.IO.File]::WriteAllBytes($Out, $ms.ToArray())
Write-Host "Wrote $Out ($($ms.Length) bytes)"
