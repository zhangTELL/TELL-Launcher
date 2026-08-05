Add-Type -AssemblyName System.Drawing

$sizes = 16, 32, 48, 256
$images = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # 圆角方形底（Accent 蓝）
    $d = [int]($s * 0.44)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($s - $d, 0, $d, $d, 270, 90)
    $path.AddArc($s - $d, $s - $d, $d, $d, 0, 90)
    $path.AddArc(0, $s - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0x66, 0xC0, 0xF4))
    $g.FillPath($brush, $path)

    # 字母 T（深色）
    $font = New-Object System.Drawing.Font("Segoe UI", [float]($s * 0.52),
        [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $textBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0x17, 0x1D, 0x25))
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $layout = New-Object System.Drawing.RectangleF 0, 0, $s, $s
    $g.DrawString("T", $font, $textBrush, $layout, $sf)

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,@{ size = $s; bytes = $ms.ToArray() }

    $ms.Dispose(); $font.Dispose(); $brush.Dispose(); $textBrush.Dispose()
    $path.Dispose(); $g.Dispose(); $bmp.Dispose()
}

# 打包 ICO（PNG 压缩条目，Vista+ 支持）
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
