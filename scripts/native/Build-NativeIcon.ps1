[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
# Render our own restricted SVG path source without launching a window or using the desktop.
Add-Type -AssemblyName PresentationCore, WindowsBase
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source = Join-Path $repo 'src/native/LensDocsStudio.Native/Resources/Brand/application.svg'
$destination = Join-Path $repo 'src/native/LensDocsStudio.Native/Resources/Brand/application.ico'
[xml]$artwork = Get-Content -LiteralPath $source -Raw
$frames = @()
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $drawing = [System.Windows.Media.DrawingVisual]::new()
    $context = $drawing.RenderOpen()
    $context.PushTransform([System.Windows.Media.ScaleTransform]::new($size / 256.0, $size / 256.0))
    foreach ($path in $artwork.svg.path) {
        $fill = $null
        if ($path.fill -ne 'none') { $fill = [System.Windows.Media.BrushConverter]::new().ConvertFromString([string]$path.fill) }
        $pen = $null
        if ($path.stroke) {
            $brush = [System.Windows.Media.BrushConverter]::new().ConvertFromString([string]$path.stroke)
            $pen = [System.Windows.Media.Pen]::new($brush, [double]$path.'stroke-width')
            $pen.StartLineCap = $pen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
        }
        $context.DrawGeometry($fill, $pen, [System.Windows.Media.Geometry]::Parse([string]$path.d))
    }
    $context.Pop(); $context.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($drawing)
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.MemoryStream]::new()
    $encoder.Save($stream); $bytes = $stream.ToArray(); $stream.Dispose()
    $frames += @{ Size = $size; Bytes = $bytes }
}
$file = [System.IO.File]::Create($destination)
$writer = [System.IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset); $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose(); $file.Dispose() }
Write-Output 'Original native icon generated at 16, 24, 32, 48, 64, 128 and 256 pixels.'
