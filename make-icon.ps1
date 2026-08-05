Add-Type -AssemblyName System.Drawing

$sizes = 16, 32, 48, 256
$images = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $u = [float]$s / 100.0   # 单位刻度，100x100 逻辑坐标系

    # ---------- 圆角方形底（Accent 蓝） ----------
    $d = [int]($s * 0.44)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($s - $d, 0, $d, $d, 270, 90)
    $path.AddArc($s - $d, $s - $d, $d, $d, 0, 90)
    $path.AddArc(0, $s - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0x66, 0xC0, 0xF4))
    $g.FillPath($bgBrush, $path)

    # ---------- 火箭（深色） ----------
    $rocketColor = [System.Drawing.Color]::FromArgb(0x17, 0x1D, 0x25)
    $bodyBrush = New-Object System.Drawing.SolidBrush $rocketColor

    $g.TranslateTransform([float]($s / 2), [float]($s / 2))
    $g.RotateTransform(-45)
    $g.TranslateTransform([float](-$s / 2), [float](-$s / 2))

    # 机身：圆角矩形，居中偏上
    $bodyW = 16 * $u
    $bodyH = 42 * $u
    $bodyX = ($s - $bodyW) / 2
    $bodyY = $s * 0.22
    $bodyRadius = 8 * $u
    $bodyPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bodyPath.AddArc($bodyX, $bodyY, $bodyRadius * 2, $bodyRadius * 2, 180, 90)
    $bodyPath.AddArc($bodyX + $bodyW - $bodyRadius * 2, $bodyY, $bodyRadius * 2, $bodyRadius * 2, 270, 90)
    $bodyPath.AddArc($bodyX + $bodyW - $bodyRadius * 2, $bodyY + $bodyH - $bodyRadius * 2, $bodyRadius * 2, $bodyRadius * 2, 0, 90)
    $bodyPath.AddArc($bodyX, $bodyY + $bodyH - $bodyRadius * 2, $bodyRadius * 2, $bodyRadius * 2, 90, 90)
    $bodyPath.CloseFigure()
    $g.FillPath($bodyBrush, $bodyPath)

    # 机头：三角形
    $noseBase = $bodyY + 4 * $u
    $noseTipY = $bodyY - 14 * $u
    $noseHalfW = $bodyW / 2
    $nosePath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $nosePath.AddLine(
        [float]($s / 2), [float]$noseTipY,
        [float](($s + $bodyW) / 2), [float]$noseBase)
    $nosePath.AddLine(
        [float](($s + $bodyW) / 2), [float]$noseBase,
        [float](($s - $bodyW) / 2), [float]$noseBase)
    $nosePath.CloseFigure()
    $g.FillPath($bodyBrush, $nosePath)

    # 左侧尾翼
    $finW = 10 * $u
    $finH = 14 * $u
    $finY = $bodyY + $bodyH - $finH - 2 * $u
    $finPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $finPath.AddLine(
        [float](($s - $bodyW) / 2), [float]$finY,
        [float](($s - $bodyW) / 2 - $finW), [float]($finY + $finH))
    $finPath.AddLine(
        [float](($s - $bodyW) / 2 - $finW), [float]($finY + $finH),
        [float](($s - $bodyW) / 2), [float]($finY + $finH))
    $finPath.CloseFigure()
    $g.FillPath($bodyBrush, $finPath)

    # 右侧尾翼
    $finPath2 = New-Object System.Drawing.Drawing2D.GraphicsPath
    $finPath2.AddLine(
        [float](($s + $bodyW) / 2), [float]$finY,
        [float](($s + $bodyW) / 2 + $finW), [float]($finY + $finH))
    $finPath2.AddLine(
        [float](($s + $bodyW) / 2 + $finW), [float]($finY + $finH),
        [float](($s + $bodyW) / 2), [float]($finY + $finH))
    $finPath2.CloseFigure()
    $g.FillPath($bodyBrush, $finPath2)

    # 尾焰：小三角形
    $flameW = 8 * $u
    $flameH = 14 * $u
    $flameY = $bodyY + $bodyH + 2 * $u
    $flamePath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $flamePath.AddLine(
        [float]($s / 2 - $flameW / 2), [float]$flameY,
        [float]($s / 2), [float]($flameY + $flameH))
    $flamePath.AddLine(
        [float]($s / 2), [float]($flameY + $flameH),
        [float]($s / 2 + $flameW / 2), [float]$flameY)
    $flamePath.CloseFigure()
    $g.FillPath($bodyBrush, $flamePath)

    # 舷窗：小圆（用白色透出底色）
    $winR = 4 * $u
    $winY = $bodyY + $bodyH * 0.28
    $winBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0x66, 0xC0, 0xF4))
    $g.FillEllipse($winBrush, [float]($s / 2 - $winR), [float]$winY, [float]($winR * 2), [float]($winR * 2))

    $g.ResetTransform()

    # ---------- 打包 ICO ----------
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,@{ size = $s; bytes = $ms.ToArray() }

    # 256 尺寸单独存为 PNG（供 WPF 直接使用）
    if ($s -eq 256) {
        $bmp.Save("TELLLauncher\app.png", [System.Drawing.Imaging.ImageFormat]::Png)
    }

    $ms.Dispose(); $bodyBrush.Dispose(); $bgBrush.Dispose(); $winBrush.Dispose()
    $bodyPath.Dispose(); $nosePath.Dispose(); $finPath.Dispose(); $finPath2.Dispose()
    $flamePath.Dispose(); $path.Dispose(); $g.Dispose(); $bmp.Dispose()
}

$outPath = $args[0]
$fs = [System.IO.File]::Create($outPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $dim = if ($img.size -ge 256) { 0 } else { $img.size }
    $bw.Write([byte]$dim)
    $bw.Write([byte]$dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$img.bytes.Length)
    $bw.Write([uint32]$offset)
    $offset += $img.bytes.Length
}
foreach ($img in $images) {
    $bw.Write($img.bytes)
}
$bw.Close()
$fs.Close()

Write-Output "ICO written: $outPath"
