<#
.SYNOPSIS
  Generate the Nucleus mod branding: assets/branding/nucleus-256.png (256x256) and assets/branding/nucleus.ico
  (multi-size, derived from the same render). Deterministic placeholder using the mod palette
  (teal #33C4C4, magenta #FF66C2) on a dark-teal rounded square with an 'N' glyph. Windows-only
  (System.Drawing.Common). Re-run after editing the palette/glyph; commit the produced assets.

  To swap in real artwork, drop a 256x256 PNG at assets/branding/nucleus-256.png and re-run this script
  with -IcoOnly to regenerate the .ico from it (see assets/branding/README.md).

  -IcoOnly : skip the PNG render; build nucleus.ico from the existing nucleus-256.png.
#>
[CmdletBinding()]
param([switch]$IcoOnly)
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$repo = Split-Path $PSScriptRoot -Parent
$outDir = Join-Path $repo 'assets\branding'
New-Item -ItemType Directory -Force $outDir | Out-Null
$pngPath = Join-Path $outDir 'nucleus-256.png'
$icoPath = Join-Path $outDir 'nucleus.ico'

# Mod palette (kept in sync with the in-game UI theme tokens).
$bg     = [System.Drawing.Color]::FromArgb(255, 8, 32, 31)    # dark teal field
$teal   = [System.Drawing.Color]::FromArgb(255, 51, 196, 196) # #33C4C4
$magenta= [System.Drawing.Color]::FromArgb(255, 255, 102, 194) # #FF66C2

# Render the icon at an arbitrary square size. Proportional metrics keep it legible from 16px to 256px.
function New-NucleusBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
        $g.Clear([System.Drawing.Color]::Transparent)

        $pad = [Math]::Max(1, [int]($size * 0.06))
        $radius = [Math]::Max(2, [int]($size * 0.22))
        $rect = New-Object System.Drawing.Rectangle($pad, $pad, ($size - 2 * $pad), ($size - 2 * $pad))

        # Rounded-square path.
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $d = $radius * 2
        $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
        $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
        $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
        $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
        $path.CloseFigure()

        # Fill the field, then a teal border ring.
        $bgBrush = New-Object System.Drawing.SolidBrush($bg)
        $g.FillPath($bgBrush, $path)
        $bgBrush.Dispose()

        $penW = [Math]::Max(1.0, [single]($size * 0.035))
        $pen = New-Object System.Drawing.Pen($teal, $penW)
        $pen.Alignment = [System.Drawing.Drawing2D.PenAlignment]::Inset
        $g.DrawPath($pen, $path)
        $pen.Dispose()

        # The 'N' glyph (magenta), centered. Use a bold sans-serif present on every Windows install.
        $fontSize = [single]($size * 0.60)
        $font = $null
        foreach ($family in @('Arial', 'Segoe UI', 'Tahoma')) {
            try { $font = New-Object System.Drawing.Font($family, $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel); break } catch { }
        }
        if ($null -eq $font) { $font = New-Object System.Drawing.Font([System.Drawing.FontFamily]::GenericSansSerif, $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel) }

        $fmt = New-Object System.Drawing.StringFormat
        $fmt.Alignment = [System.Drawing.StringAlignment]::Center
        $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
        $glyphBrush = New-Object System.Drawing.SolidBrush($magenta)
        # Nudge the optical center up a hair (font metrics sit low for caps).
        $textRect = New-Object System.Drawing.RectangleF(0, [single](-$size * 0.04), [single]$size, [single]$size)
        $g.DrawString('N', $font, $glyphBrush, $textRect, $fmt)
        $glyphBrush.Dispose()
        $fmt.Dispose()
        $font.Dispose()
        $path.Dispose()
    }
    finally { $g.Dispose() }
    return $bmp
}

# Encode a Bitmap to a PNG byte[].
function Get-PngBytes([System.Drawing.Bitmap]$bmp) {
    $ms = New-Object System.IO.MemoryStream
    try {
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        return $ms.ToArray()
    }
    finally { $ms.Dispose() }
}

if (-not $IcoOnly) {
    $master = New-NucleusBitmap 256
    try { $master.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png) } finally { $master.Dispose() }
    Write-Host "[branding] wrote $pngPath (256x256)" -ForegroundColor Green
}
elseif (-not (Test-Path $pngPath)) {
    throw "-IcoOnly requested but $pngPath does not exist. Run without -IcoOnly first, or drop a 256x256 PNG there."
}

# Build the .ico from the 256x256 PNG: a single PNG-compressed entry (Vista+ ICO supports embedded PNG; Windows
# downscales it for the shortcut). Reading the PNG file straight to bytes avoids PowerShell's array-unrolling of a
# byte[] return, which previously produced a truncated icon.
$png = [System.IO.File]::ReadAllBytes($pngPath)
$fs = New-Object System.IO.FileStream($icoPath, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)
try {
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]1)   # ICONDIR: reserved, type=icon, count=1
    $bw.Write([byte]0); $bw.Write([byte]0)                             # width/height 0 => 256
    $bw.Write([byte]0); $bw.Write([byte]0)                             # palette count, reserved
    $bw.Write([uint16]1); $bw.Write([uint16]32)                        # color planes, bits per pixel
    $bw.Write([uint32]$png.Length)                                     # size of the PNG resource
    $bw.Write([uint32]22)                                              # offset = 6 (ICONDIR) + 16 (one ICONDIRENTRY)
    $bw.Write($png)
}
finally { $bw.Dispose(); $fs.Dispose() }

Write-Host "[branding] wrote $icoPath (256px PNG icon, $([math]::Round($png.Length/1kb,1)) KB)" -ForegroundColor Green
