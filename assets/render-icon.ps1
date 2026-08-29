# 渲染 TELL Launcher 应用图标（GDI+，无外部依赖）。
# 设计源见 icon.svg；产物：
#   assets/icon-<尺寸>.png   各尺寸 PNG（供打包与预览）
#   TELLLauncher/app.png     界面左上角 logo（128px）
# 打包 ICO 见 pack-ico.py。
# 用法：powershell -NoProfile -File render-icon.ps1

Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$outDir = $PSScriptRoot

function Argb([int]$a, [int]$r, [int]$g, [int]$b) {
    return [System.Drawing.Color]::FromArgb($a, $r, $g, $b)
}

function New-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

$bmp = New-Object System.Drawing.Bitmap(512, 512)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

# ---- 底板：蓝灰垂直渐变 ----
$tilePath = New-RoundedRectPath 16 16 480 480 104
$tileRect = New-Object System.Drawing.Rectangle(16, 16, 480, 480)
$tileBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $tileRect, (Argb 255 0x2E 0x4A 0x63), (Argb 255 0x17 0x22 0x2E), 90)
$g.FillPath($tileBrush, $tilePath)

# ---- 顶部高光：上缘白色渐隐，45% 处衰减到零 ----
$sheenBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $tileRect, (Argb 33 255 255 255), (Argb 0 255 255 255), 90)
$blend = New-Object System.Drawing.Drawing2D.Blend
$blend.Positions = [float[]]@(0.0, 0.45, 1.0)
$blend.Factors = [float[]]@(1.0, 0.0, 0.0)
$sheenBrush.Blend = $blend
$g.FillPath($sheenBrush, $tilePath)

# ---- 内描边：白色 10% ----
$strokePath = New-RoundedRectPath 17.5 17.5 477 477 102.5
$strokePen = New-Object System.Drawing.Pen((Argb 26 255 255 255), 3)
$g.DrawPath($strokePen, $strokePath)

# ---- 动势尾迹 + 纸飞机：整体缩小并上仰，留出呼吸空间 ----
$g.TranslateTransform(256, 256)
$g.RotateTransform(-14)
$g.ScaleTransform(0.78, 0.78)
$g.TranslateTransform(-256, -256)

$trailPen = New-Object System.Drawing.Pen((Argb 71 0x9B 0xDC 0xFF), 14)
$trailPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$trailPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($trailPen, 36, 186, 78, 186)
$trailPen.Color = Argb 115 0x9B 0xDC 0xFF
$g.DrawLine($trailPen, 24, 256, 86, 256)
$trailPen.Color = Argb 71 0x9B 0xDC 0xFF
$g.DrawLine($trailPen, 36, 326, 78, 326)

# ---- 纸飞机两个翼面 ----
function Fill-Wing {
    param([float[][]]$Points, [System.Drawing.Color]$Top, [System.Drawing.Color]$Bottom)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pts = [System.Drawing.PointF[]]::new($Points.Count)
    for ($i = 0; $i -lt $Points.Count; $i++) {
        $pts[$i] = New-Object System.Drawing.PointF($Points[$i][0], $Points[$i][1])
    }
    $path.AddPolygon($pts)
    $minX = ($pts | ForEach-Object { $_.X } | Measure-Object -Minimum).Minimum
    $minY = ($pts | ForEach-Object { $_.Y } | Measure-Object -Minimum).Minimum
    $maxX = ($pts | ForEach-Object { $_.X } | Measure-Object -Maximum).Maximum
    $maxY = ($pts | ForEach-Object { $_.Y } | Measure-Object -Maximum).Maximum
    $rect = New-Object System.Drawing.RectangleF(
        $minX, $minY, ($maxX - $minX), ($maxY - $minY))
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect, $Top, $Bottom, 90)
    $g.FillPath($brush, $path)
}

Fill-Wing -Points @(@(421, 256), @(106, 121), @(106, 226), @(331, 256)) `
    -Top (Argb 255 0x9B 0xDC 0xFF) -Bottom (Argb 255 0x56 0xAD 0xF0)
Fill-Wing -Points @(@(421, 256), @(331, 256), @(106, 286), @(106, 391)) `
    -Top (Argb 255 0x47 0x91 0xD2) -Bottom (Argb 255 0x2B 0x6B 0xA6)

# ---- 折线高光 ----
$foldPen = New-Object System.Drawing.Pen((Argb 70 255 255 255), 2)
$g.DrawLine($foldPen, 421, 256, 331, 256)

$g.ResetTransform()
$g.Dispose()

# ---- 输出：app.png + 多尺寸 BMP 格式 app.ico ----
# csc 的 Win32 资源解析器只认 BMP 格式的 ICO 条目，PNG 内嵌条目会让编译报
# "Unable to read beyond the end of the stream"，因此这里手工编码 BMP 条目。
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$icons = @()
foreach ($s in $sizes) {
    $scaled = New-Object System.Drawing.Bitmap($s, $s)
    $sg = [System.Drawing.Graphics]::FromImage($scaled)
    $sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $sg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $sg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $sg.DrawImage($bmp, (New-Object System.Drawing.Rectangle(0, 0, $s, $s)))
    $sg.Dispose()

    if ($s -eq 128) {
        $scaled.Save((Join-Path $PSScriptRoot "..\TELLLauncher\app.png"),
            [System.Drawing.Imaging.ImageFormat]::Png)
    }
    if ($s -eq 256) {
        $scaled.Save((Join-Path $outDir "icon-256.png"),
            [System.Drawing.Imaging.ImageFormat]::Png)
    }

    # 取出 BGRA 像素（Format32bppArgb 的内存序即 B,G,R,A）
    $rect = New-Object System.Drawing.Rectangle(0, 0, $s, $s)
    $bits = $scaled.LockBits($rect,
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $pixels = New-Object byte[] ($s * $s * 4)
    [System.Runtime.InteropServices.Marshal]::Copy(
        $bits.Scan0, $pixels, 0, $pixels.Length)
    $scaled.UnlockBits($bits)

    # BMP 自底向上存储，翻转行序
    $rowLength = $s * 4
    $flipped = New-Object byte[] ($s * $s * 4)
    for ($y = 0; $y -lt $s; $y++) {
        [Array]::Copy($pixels, ($s - 1 - $y) * $rowLength,
            $flipped, $y * $rowLength, $rowLength)
    }

    # AND 掩码：32bpp 时全 0（透明度由 alpha 通道承担）
    $maskRowLength = [Math]::Ceiling($s / 32.0) * 4
    $mask = New-Object byte[] ($maskRowLength * $s)

    $icons += ,@($s, $flipped, $mask)
    $scaled.Dispose()
}

$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($stream)
$writer.Write([uint16]0)          # reserved
$writer.Write([uint16]1)          # type: icon
$writer.Write([uint16]$icons.Count)

$offset = 6 + 16 * $icons.Count
foreach ($entry in $icons) {
    $s = $entry[0]
    $writer.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $writer.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $writer.Write([byte]0)        # color count
    $writer.Write([byte]0)        # reserved
    $writer.Write([uint16]1)      # planes
    $writer.Write([uint16]32)     # bit count
    $writer.Write([uint32]($entry[1].Length + $entry[2].Length + 40))
    $writer.Write([uint32]$offset)
    $offset += $entry[1].Length + $entry[2].Length + 40
}

foreach ($entry in $icons) {
    $s = $entry[0]
    # BITMAPINFOHEADER：高度为两倍（XOR + AND 两块数据）
    $writer.Write([uint32]40)
    $writer.Write([int32]$s)
    $writer.Write([int32]($s * 2))
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]0)      # compression: BI_RGB
    $writer.Write([uint32]($entry[1].Length + $entry[2].Length))
    $writer.Write([int32]0)
    $writer.Write([int32]0)
    $writer.Write([uint32]0)
    $writer.Write([uint32]0)
    $writer.Write($entry[1])
    $writer.Write($entry[2])
}

$writer.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $PSScriptRoot "..\TELLLauncher\app.ico"), $stream.ToArray())
$writer.Dispose()
$bmp.Dispose()
Write-Output "rendered: icon-256.png + TELLLauncher/app.png + TELLLauncher/app.ico"
