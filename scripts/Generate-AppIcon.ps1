$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDirectory = Join-Path $PSScriptRoot '../src/EdgeLord/Assets'
[IO.Directory]::CreateDirectory($assetDirectory) | Out-Null

# A crown, worn at a rakish angle, on a rounded tile. The lord of Edge doesn't take it seriously either.
function New-Icon([string]$name, [string]$tile, [string]$crown) {
    $canvas = [Drawing.Bitmap]::new(1024, 1024)
    $graphics = [Drawing.Graphics]::FromImage($canvas)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([Drawing.Color]::Transparent)
    $graphics.ScaleTransform(4, 4)

    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc(8, 8, 96, 96, 180, 90)
    $path.AddArc(152, 8, 96, 96, 270, 90)
    $path.AddArc(152, 152, 96, 96, 0, 90)
    $path.AddArc(8, 152, 96, 96, 90, 90)
    $path.CloseFigure()
    $brush = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($tile))
    $graphics.FillPath($brush, $path)
    $brush.Dispose(); $path.Dispose()

    $graphics.TranslateTransform(128, 136)
    $graphics.RotateTransform(-14)
    $graphics.TranslateTransform(-128, -136)
    $gold = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($crown))
    # Three points, then a band underneath with a sliver of tile between them.
    $points = [Drawing.PointF[]]@(
        [Drawing.PointF]::new(58, 166), [Drawing.PointF]::new(46, 90), [Drawing.PointF]::new(96, 128),
        [Drawing.PointF]::new(128, 66), [Drawing.PointF]::new(160, 128), [Drawing.PointF]::new(210, 90),
        [Drawing.PointF]::new(198, 166))
    $graphics.FillPolygon($gold, $points)
    $band = [Drawing.Drawing2D.GraphicsPath]::new()
    $band.AddArc(58, 176, 20, 20, 90, 180)
    $band.AddArc(178, 176, 20, 20, 270, 180)
    $band.CloseFigure()
    $graphics.FillPath($gold, $band)
    foreach ($tip in @(@(46, 90), @(128, 66), @(210, 90))) {
        $graphics.FillEllipse($gold, $tip[0] - 15, $tip[1] - 15, 30, 30)
    }
    $band.Dispose(); $gold.Dispose()
    $graphics.Dispose()

    $sizes = @(16, 20, 24, 32, 40, 48, 64, 256)
    $frames = @()
    foreach ($size in $sizes) {
        $bitmap = [Drawing.Bitmap]::new($size, $size)
        $g = [Drawing.Graphics]::FromImage($bitmap)
        $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.DrawImage($canvas, [Drawing.Rectangle]::new(0, 0, $size, $size))
        $stream = [IO.MemoryStream]::new()
        $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        $frames += ,$stream.ToArray()
        # The README shows the big one.
        if ($size -eq 256 -and $name -eq 'EdgeLord') { $bitmap.Save((Join-Path $assetDirectory "$name.png"), [Drawing.Imaging.ImageFormat]::Png) }
        $stream.Dispose(); $g.Dispose(); $bitmap.Dispose()
    }
    $canvas.Dispose()

    $file = [IO.File]::Create((Join-Path $assetDirectory "$name.ico"))
    $writer = [IO.BinaryWriter]::new($file)
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    $writer.Dispose()
}

New-Icon 'EdgeLord' '#5B3FA8' '#F5C542'
# Grey when paused, so the tray shows at a glance whether it's working.
New-Icon 'EdgeLord-off' '#8A8A8A' '#D9D9D9'
Write-Output 'Generated EdgeLord.ico and EdgeLord-off.ico.'
