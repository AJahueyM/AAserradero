# Generates all brand assets from the source logo:
#   - SPA icons  -> src/Web/public  (favicon.ico, favicon-16/32, apple-touch, icon-192/512, maskable)
#   - App logo   -> src/Web/src/assets/logo.png  (transparent, for in-app UI)
#   - Entra Company Branding assets -> infra/entra/branding (banner/square/background)
# Re-run after replacing logo-source.png to regenerate everything.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$brandDir  = $PSScriptRoot                               # ...\infra\entra\branding
$repo      = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $brandDir))  # repo root
$publicDir = Join-Path $repo 'src\Web\public'
$assetsDir = Join-Path $repo 'src\Web\src\assets'
$srcLogo   = Join-Path $brandDir 'logo-source.png'

$cream = [System.Drawing.ColorTranslator]::FromHtml('#f7f6f1')   # app background.default
$green = [System.Drawing.ColorTranslator]::FromHtml('#273c18')   # primary.light (dark green)
$green2= [System.Drawing.ColorTranslator]::FromHtml('#395723')   # primary.main

function New-HqGraphics([System.Drawing.Bitmap]$bmp) {
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
  $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
  return $g
}

# Draws the source logo centred on a square canvas. widthFraction = logo width / canvas size.
function New-SquareIcon([int]$size, [object]$bg, [double]$widthFraction) {
  $logo = [System.Drawing.Image]::FromFile($srcLogo)
  try {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = New-HqGraphics $bmp
    if ($null -eq $bg) { $g.Clear([System.Drawing.Color]::Transparent) }
    else { $g.Clear([System.Drawing.Color]$bg) }
    $targetW = [double]$size * $widthFraction
    $scale   = $targetW / $logo.Width
    $w = [int][Math]::Round($logo.Width * $scale)
    $h = [int][Math]::Round($logo.Height * $scale)
    $x = [int][Math]::Round(($size - $w) / 2.0)
    $y = [int][Math]::Round(($size - $h) / 2.0)
    $g.DrawImage($logo, $x, $y, $w, $h)
    $g.Dispose()
    return $bmp
  } finally { $logo.Dispose() }
}

# Scales the logo to an exact height on a transparent canvas (width follows aspect). For banner/app logo.
function New-LogoAtHeight([int]$height) {
  $logo = [System.Drawing.Image]::FromFile($srcLogo)
  try {
    $aspect = [double]$logo.Width / [double]$logo.Height
    $w = [int][Math]::Round($height * $aspect)
    $bmp = New-Object System.Drawing.Bitmap($w, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = New-HqGraphics $bmp
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($logo, 0, 0, $w, $height)
    $g.Dispose()
    return $bmp
  } finally { $logo.Dispose() }
}

function New-LogoAtWidth([int]$width) {
  $logo = [System.Drawing.Image]::FromFile($srcLogo)
  try {
    $aspect = [double]$logo.Height / [double]$logo.Width
    $h = [int][Math]::Round($width * $aspect)
    $bmp = New-Object System.Drawing.Bitmap($width, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = New-HqGraphics $bmp
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($logo, 0, 0, $width, $h)
    $g.Dispose()
    return $bmp
  } finally { $logo.Dispose() }
}

function Save-Png([System.Drawing.Bitmap]$bmp, [string]$path) {
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  Write-Output ("  {0}  ({1}x{2}, {3:N0} bytes)" -f (Split-Path $path -Leaf), $bmp.Width, $bmp.Height, (Get-Item $path).Length)
}

function Get-PngBytes([System.Drawing.Bitmap]$bmp) {
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $bytes = $ms.ToArray(); $ms.Dispose(); return ,$bytes
}

# Builds a Vista-style PNG-compressed .ico from the given square sizes.
function Save-Ico([int[]]$sizes, [string]$path) {
  $entries = @()
  foreach ($s in $sizes) {
    $bmp = New-SquareIcon $s $cream 0.86
    $entries += ,@{ size = $s; png = (Get-PngBytes $bmp) }
    $bmp.Dispose()
  }
  $fs = [System.IO.File]::Open($path, [System.IO.FileMode]::Create)
  $bw = New-Object System.IO.BinaryWriter($fs)
  $bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$entries.Count)   # ICONDIR
  $offset = 6 + (16 * $entries.Count)
  foreach ($e in $entries) {
    $dim = if ($e.size -ge 256) { 0 } else { $e.size }
    $bw.Write([Byte]$dim); $bw.Write([Byte]$dim); $bw.Write([Byte]0); $bw.Write([Byte]0)  # w,h,colors,reserved
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)                                            # planes, bpp
    $bw.Write([UInt32]$e.png.Length); $bw.Write([UInt32]$offset)                           # size, offset
    $offset += $e.png.Length
  }
  foreach ($e in $entries) { $bw.Write($e.png) }
  $bw.Flush(); $bw.Dispose(); $fs.Dispose()
  Write-Output ("  {0}  ({1} sizes, {2:N0} bytes)" -f (Split-Path $path -Leaf), $entries.Count, (Get-Item $path).Length)
}

Write-Output '== SPA icons (src/Web/public) =='
$b = New-SquareIcon 16  $cream 0.90; Save-Png $b (Join-Path $publicDir 'favicon-16.png');       $b.Dispose()
$b = New-SquareIcon 32  $cream 0.88; Save-Png $b (Join-Path $publicDir 'favicon-32.png');       $b.Dispose()
$b = New-SquareIcon 180 $cream 0.82; Save-Png $b (Join-Path $publicDir 'apple-touch-icon.png'); $b.Dispose()
$b = New-SquareIcon 192 $cream 0.82; Save-Png $b (Join-Path $publicDir 'icon-192.png');         $b.Dispose()
$b = New-SquareIcon 512 $cream 0.82; Save-Png $b (Join-Path $publicDir 'icon-512.png');         $b.Dispose()
$b = New-SquareIcon 512 $cream 0.60; Save-Png $b (Join-Path $publicDir 'icon-maskable-512.png');$b.Dispose()
Save-Ico @(16,32,48) (Join-Path $publicDir 'favicon.ico')

Write-Output '== App logo (src/Web/src/assets) =='
$b = New-LogoAtWidth 640; Save-Png $b (Join-Path $assetsDir 'logo.png'); $b.Dispose()

Write-Output '== Entra Company Branding assets (infra/entra/branding) =='
# Square logo 240x240 transparent (padded)
$b = New-SquareIcon 240 $null 0.86; Save-Png $b (Join-Path $brandDir 'entra-square-logo.png'); $b.Dispose()
# Banner logo: max 280x60, must be < 10 KB. Try decreasing heights until under budget.
$bannerPath = Join-Path $brandDir 'entra-banner-logo.png'
foreach ($h in 60,54,48,42,36) {
  $b = New-LogoAtHeight $h
  Save-Png $b $bannerPath
  $len = (Get-Item $bannerPath).Length
  $b.Dispose()
  if ($len -le 10000) { Write-Output ("  -> banner accepted at height $h ($len bytes)"); break }
  else { Write-Output ("  -> height $h too big ($len bytes), shrinking...") }
}
# Background 1920x1080 vertical gradient dark green -> main green (< 300 KB)
$bg = New-Object System.Drawing.Bitmap(1920, 1080, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g = New-HqGraphics $bg
$rect = New-Object System.Drawing.Rectangle(0, 0, 1920, 1080)
$lg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $green, $green2, 90.0)
$g.FillRectangle($lg, $rect)
$g.Dispose(); $lg.Dispose()
$bgPath = Join-Path $brandDir 'entra-background.png'
$bg.Save($bgPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bg.Dispose()
Write-Output ("  entra-background.png  (1920x1080, {0:N0} bytes)" -f (Get-Item $bgPath).Length)

Write-Output 'DONE.'
