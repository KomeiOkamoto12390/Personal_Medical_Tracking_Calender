# Generates src\MedicalCalendar\Assets\app.ico (16-256 px, PNG-compressed entries).
# Usage:  powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1 [-PreviewDir <folder>]
param([string]$PreviewDir = "")

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"

$red     = [System.Drawing.Color]::FromArgb(255, 255, 59, 48)    # #FF3B30, same as "today" in the app
$redDark = [System.Drawing.Color]::FromArgb(255, 214, 40, 32)
$paper   = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)
$edge    = [System.Drawing.Color]::FromArgb(255, 224, 224, 229)
$ring    = [System.Drawing.Color]::FromArgb(255, 72, 72, 74)

function RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Cross($g, [System.Drawing.Color]$color, [float]$cx, [float]$cy, [float]$size) {
    $arm = $size / 3
    $b = New-Object System.Drawing.SolidBrush $color
    $g.FillPath($b, (RoundedRect ($cx - $arm / 2) ($cy - $size / 2) $arm $size ($arm * 0.22)))
    $g.FillPath($b, (RoundedRect ($cx - $size / 2) ($cy - $arm / 2) $size $arm ($arm * 0.22)))
}

function Draw([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    if ($s -le 24) {
        # Small sizes: red rounded square with a white cross (calendar details would be mush).
        $m = [Math]::Max(1, $s * 0.04)
        $g.FillPath((New-Object System.Drawing.SolidBrush $red), (RoundedRect $m $m ($s - 2 * $m) ($s - 2 * $m) ($s * 0.22)))
        Cross $g $paper ($s / 2) ($s / 2) ($s * 0.62)
    }
    else {
        # Calendar page: red header band with binder rings, white body with a red medical cross.
        $m = $s * 0.06; $w = $s - 2 * $m; $r = $s * 0.16
        $top = $m + $s * 0.04
        $h = $w - $s * 0.04
        $page = RoundedRect $m $top $w $h $r
        $g.FillPath((New-Object System.Drawing.SolidBrush $paper), $page)

        $band = $h * 0.30
        $g.SetClip($page)
        $g.FillRectangle((New-Object System.Drawing.SolidBrush $red), $m, $top, $w, $band)
        $g.FillRectangle((New-Object System.Drawing.SolidBrush $redDark), $m, $top + $band - [Math]::Max(1, $s * 0.012), $w, [Math]::Max(1, $s * 0.012))
        $g.ResetClip()
        $g.DrawPath((New-Object System.Drawing.Pen $edge, ([Math]::Max(1, $s * 0.012))), $page)

        # Binder rings
        $rw = $s * 0.07; $rh = $s * 0.16
        foreach ($fx in 0.33, 0.67) {
            $rx = $m + $w * $fx - $rw / 2
            $g.FillPath((New-Object System.Drawing.SolidBrush $ring), (RoundedRect $rx ($top - $rh * 0.45) $rw $rh ($rw / 2)))
        }

        Cross $g $red ($s / 2) ($top + $band + ($h - $band) / 2) ($s * 0.36)
    }
    $g.Dispose()
    return $bmp
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) {
    $bmp = Draw $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($PreviewDir) { $bmp.Save((Join-Path $PreviewDir "icon-$s.png"), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
    , $ms.ToArray()
}

# ICO container: header, one 16-byte directory entry per image, then the PNG data.
$out = Join-Path $PSScriptRoot "..\src\MedicalCalendar\Assets\app.ico"
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))   # width  (0 = 256)
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))   # height
    $bw.Write([byte]0); $bw.Write([byte]0)                      # palette, reserved
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)                 # planes, bits per pixel
    $bw.Write([UInt32]$len); $bw.Write([UInt32]$offset)
    $offset += $len
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Dispose()
Write-Output "Wrote $((Resolve-Path $out).Path) ($((Get-Item $out).Length) bytes)"
