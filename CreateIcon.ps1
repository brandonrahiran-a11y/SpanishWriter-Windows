$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = Join-Path $PSScriptRoot 'assets\logo.png'
$destination = Join-Path $PSScriptRoot 'assets\SpanishWriter.ico'
$sourceImage = [System.Drawing.Image]::FromFile($source)
$sizes = @(16, 32, 48, 256)
$images = New-Object System.Collections.Generic.List[byte[]]
try {
    foreach ($size in $sizes) {
        $bitmap = New-Object System.Drawing.Bitmap($size, $size)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::FromArgb(1, 11, 23))
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                $height = [int][Math]::Round($size * $sourceImage.Height / $sourceImage.Width)
                $graphics.DrawImage($sourceImage, 0, [int](($size - $height) / 2), $size, $height)
            } finally { $graphics.Dispose() }
            $stream = New-Object System.IO.MemoryStream
            try {
                $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                $images.Add($stream.ToArray())
            } finally { $stream.Dispose() }
        } finally { $bitmap.Dispose() }
    }
} finally { $sourceImage.Dispose() }

$file = [System.IO.File]::Create($destination)
$writer = New-Object System.IO.BinaryWriter($file)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$images.Count)
    $offset = 6 + 16 * $images.Count
    for ($i = 0; $i -lt $images.Count; $i++) {
        $size = $sizes[$i]
        $writer.Write([byte]($size % 256))
        $writer.Write([byte]($size % 256))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length)
        $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($bytes in $images) { $writer.Write($bytes) }
} finally { $writer.Dispose() }
Write-Output $destination
