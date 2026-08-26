<#
.SYNOPSIS
    Builds assets/app.ico from the SVG logos.

.DESCRIPTION
    Renders the logo at every size Windows asks for and packs the results into a
    single multi resolution .ico file.

    Two sources are used. Recode.svg carries the full mark and is used from 32
    pixels upwards. Recode-small.svg is a reduced version used at 16, 20 and 24,
    where the full mark has more detail than there are pixels to draw it with.
    Explorer draws context menu entries at 16, so the small variant is the one
    most people will actually see.

    No external tools are involved. The SVG is translated into WPF geometry and
    rendered by the same imaging stack the application itself uses, so the only
    requirement is PowerShell 7 on Windows.

    Supported SVG subset: rect, circle, path, g, linearGradient, clipPath, and
    the fill, stroke, stroke-width, stroke-linecap, stroke-linejoin, opacity,
    fill-opacity, stroke-opacity, transform and clip-path attributes. That
    covers both logos. Anything outside it is ignored rather than guessed at.

.PARAMETER Output
    Where to write the .ico. Defaults to assets/app.ico.

.PARAMETER PngDirectory
    Optional. Also writes the individual PNG frames here, which is useful for
    checking how the small sizes actually look.

.EXAMPLE
    pwsh -File tools/make-icon.ps1
    pwsh -File tools/make-icon.ps1 -PngDirectory .native-build/icon-preview
#>
[CmdletBinding()]
param(
    [string] $Output,
    [string] $PngDirectory,

    # Also write the logo images an MSIX package needs, into this folder.
    # Used by tools/build-package.ps1 so the packaged shell extension shows the
    # same mark as the executable, rendered from the same SVG.
    [string] $PackageAssets
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $Output) { $Output = Join-Path $RepoRoot 'assets\app.ico' }

# Sizes embedded in the .ico. 16 and 20 matter most: they are what Explorer and
# the context menu draw.
$FullSource  = Join-Path $RepoRoot 'assets\Recode.svg'
$SmallSource = Join-Path $RepoRoot 'assets\Recode-small.svg'
$SmallSizes  = @(16, 20, 24)
$LargeSizes  = @(32, 48, 64, 256)

# ---------------------------------------------------------------------------
# SVG to WPF
# ---------------------------------------------------------------------------

function ConvertTo-Color([string] $Text) {
    return [System.Windows.Media.ColorConverter]::ConvertFromString($Text)
}

function Get-Attr($Node, [string] $Name, [string] $Default = '') {
    $value = $Node.GetAttribute($Name)
    if ([string]::IsNullOrWhiteSpace($value)) { return $Default }
    return $value
}

# Presentation attributes that cascade to child elements. Both logos set the
# stroke of an arrow once on the group and leave the two paths inside bare, so
# without inheritance the arrows resolve to no fill and no stroke and vanish.
$InheritableAttributes = @(
    'fill', 'stroke', 'stroke-width', 'stroke-linecap',
    'stroke-linejoin', 'fill-opacity', 'stroke-opacity'
)

function Merge-Style([hashtable] $Inherited, $Node) {
    $style = @{}
    foreach ($key in $Inherited.Keys) { $style[$key] = $Inherited[$key] }

    foreach ($name in $script:InheritableAttributes) {
        $value = $Node.GetAttribute($name)
        if (-not [string]::IsNullOrWhiteSpace($value)) { $style[$name] = $value }
    }

    return $style
}

function Get-StyleValue([hashtable] $Style, [string] $Name, [string] $Default = '') {
    if ($Style.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace($Style[$Name])) {
        return $Style[$Name]
    }
    return $Default
}

function New-GradientBrush($Node) {
    $brush = New-Object System.Windows.Media.LinearGradientBrush
    $brush.StartPoint = New-Object System.Windows.Point(
        [double](Get-Attr $Node 'x1' '0'), [double](Get-Attr $Node 'y1' '0'))
    $brush.EndPoint = New-Object System.Windows.Point(
        [double](Get-Attr $Node 'x2' '1'), [double](Get-Attr $Node 'y2' '0'))

    # SVG defaults gradient coordinates to the shape's bounding box, which is
    # what RelativeToBoundingBox means here. Both logos rely on that default.
    $brush.MappingMode = [System.Windows.Media.BrushMappingMode]::RelativeToBoundingBox

    foreach ($stop in $Node.ChildNodes) {
        if ($stop.LocalName -ne 'stop') { continue }
        $offset = [double](Get-Attr $stop 'offset' '0')
        $color = ConvertTo-Color (Get-Attr $stop 'stop-color' '#000000')
        $brush.GradientStops.Add((New-Object System.Windows.Media.GradientStop($color, $offset)))
    }

    $brush.Freeze()
    return $brush
}

function Get-Brush {
    param($Value, [hashtable] $Gradients, [double] $Opacity = 1.0)

    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -eq 'none') { return $null }

    $brush = $null
    if ($Value -match '^url\(#(.+)\)$') {
        $id = $Matches[1]
        if ($Gradients.ContainsKey($id)) {
            $brush = $Gradients[$id].Clone()
        }
    } else {
        $brush = New-Object System.Windows.Media.SolidColorBrush((ConvertTo-Color $Value))
    }

    if ($null -eq $brush) { return $null }

    if ($Opacity -lt 1.0) { $brush.Opacity = $Opacity }
    $brush.Freeze()
    return $brush
}

function Get-Geometry($Node) {
    switch ($Node.LocalName) {
        'rect' {
            $x = [double](Get-Attr $Node 'x' '0')
            $y = [double](Get-Attr $Node 'y' '0')
            $w = [double](Get-Attr $Node 'width' '0')
            $h = [double](Get-Attr $Node 'height' '0')
            $rx = [double](Get-Attr $Node 'rx' '0')
            $ry = [double](Get-Attr $Node 'ry' $(Get-Attr $Node 'rx' '0'))
            $rect = New-Object System.Windows.Rect($x, $y, $w, $h)
            return New-Object System.Windows.Media.RectangleGeometry($rect, $rx, $ry)
        }
        'circle' {
            $cx = [double](Get-Attr $Node 'cx' '0')
            $cy = [double](Get-Attr $Node 'cy' '0')
            $r  = [double](Get-Attr $Node 'r' '0')
            $centre = New-Object System.Windows.Point($cx, $cy)
            return New-Object System.Windows.Media.EllipseGeometry($centre, $r, $r)
        }
        'ellipse' {
            $cx = [double](Get-Attr $Node 'cx' '0')
            $cy = [double](Get-Attr $Node 'cy' '0')
            $rx = [double](Get-Attr $Node 'rx' '0')
            $ry = [double](Get-Attr $Node 'ry' '0')
            $centre = New-Object System.Windows.Point($cx, $cy)
            return New-Object System.Windows.Media.EllipseGeometry($centre, $rx, $ry)
        }
        'path' {
            $d = Get-Attr $Node 'd' ''
            if ([string]::IsNullOrWhiteSpace($d)) { return $null }
            # The WPF path mini language accepts SVG path data as written.
            return [System.Windows.Media.Geometry]::Parse($d)
        }
        default { return $null }
    }
}

function New-Pen {
    param([hashtable] $Style, [hashtable] $Gradients)

    $strokeValue = Get-StyleValue $Style 'stroke' ''
    if ([string]::IsNullOrWhiteSpace($strokeValue) -or $strokeValue -eq 'none') { return $null }

    $opacity = [double](Get-StyleValue $Style 'stroke-opacity' '1')
    $brush = Get-Brush -Value $strokeValue -Gradients $Gradients -Opacity $opacity
    if ($null -eq $brush) { return $null }

    $width = [double](Get-StyleValue $Style 'stroke-width' '1')
    $pen = New-Object System.Windows.Media.Pen($brush, $width)

    $cap = switch (Get-StyleValue $Style 'stroke-linecap' 'butt') {
        'round'  { [System.Windows.Media.PenLineCap]::Round }
        'square' { [System.Windows.Media.PenLineCap]::Square }
        default  { [System.Windows.Media.PenLineCap]::Flat }
    }
    $pen.StartLineCap = $cap
    $pen.EndLineCap = $cap

    $pen.LineJoin = switch (Get-StyleValue $Style 'stroke-linejoin' 'miter') {
        'round' { [System.Windows.Media.PenLineJoin]::Round }
        'bevel' { [System.Windows.Media.PenLineJoin]::Bevel }
        default { [System.Windows.Media.PenLineJoin]::Miter }
    }

    $pen.Freeze()
    return $pen
}

<#
    Decides whether an element carrying a filter should be drawn at all.

    Both logos use two kinds of filter. Drop shadows merge the original graphic
    back in through feMergeNode in="SourceGraphic", so the element still has to
    be drawn; only the shadow itself is dropped, which is what an icon wants.

    Glows do not merge the source back in. Their entire output is a blur, so
    drawing the element without the blur would produce a hard edged duplicate
    sitting behind the real artwork. Those elements are skipped.
#>
function Test-FilterKeepsSource {
    param([string] $FilterValue, [hashtable] $Filters)

    if ([string]::IsNullOrWhiteSpace($FilterValue)) { return $true }
    if ($FilterValue -notmatch '^url\(#(.+)\)$') { return $true }

    $id = $Matches[1]
    if (-not $Filters.ContainsKey($id)) { return $true }

    $filter = $Filters[$id]
    foreach ($child in $filter.GetElementsByTagName('*')) {
        if ($child.LocalName -eq 'feMergeNode' -and (Get-Attr $child 'in' '') -eq 'SourceGraphic') {
            return $true
        }
    }

    return $false
}

function Get-Transform([string] $Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }

    $group = New-Object System.Windows.Media.TransformGroup

    foreach ($match in [regex]::Matches($Value, '(\w+)\s*\(([^)]*)\)')) {
        $name = $match.Groups[1].Value
        $numbers = @([regex]::Matches($match.Groups[2].Value, '-?[\d.]+') | ForEach-Object { [double]$_.Value })

        switch ($name) {
            'translate' {
                $tx = if ($numbers.Count -gt 0) { $numbers[0] } else { 0 }
                $ty = if ($numbers.Count -gt 1) { $numbers[1] } else { 0 }
                $group.Children.Add((New-Object System.Windows.Media.TranslateTransform($tx, $ty)))
            }
            'scale' {
                $sx = if ($numbers.Count -gt 0) { $numbers[0] } else { 1 }
                $sy = if ($numbers.Count -gt 1) { $numbers[1] } else { $sx }
                $group.Children.Add((New-Object System.Windows.Media.ScaleTransform($sx, $sy)))
            }
            'rotate' {
                $angle = if ($numbers.Count -gt 0) { $numbers[0] } else { 0 }
                $group.Children.Add((New-Object System.Windows.Media.RotateTransform($angle)))
            }
        }
    }

    if ($group.Children.Count -eq 0) { return $null }
    return $group
}

function ConvertTo-Drawing {
    param($Node, [hashtable] $Gradients, [hashtable] $Clips, [hashtable] $Filters, [hashtable] $Inherited = @{})

    $group = New-Object System.Windows.Media.DrawingGroup

    foreach ($child in $Node.ChildNodes) {
        if ($child.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
        if ($child.LocalName -in @('defs', 'title', 'desc', 'metadata')) { continue }

        if (-not (Test-FilterKeepsSource -FilterValue (Get-Attr $child 'filter' '') -Filters $Filters)) {
            continue
        }

        $style = Merge-Style -Inherited $Inherited -Node $child
        $drawing = $null

        if ($child.LocalName -eq 'g') {
            $drawing = ConvertTo-Drawing -Node $child -Gradients $Gradients -Clips $Clips `
                -Filters $Filters -Inherited $style
            if ($drawing.Children.Count -eq 0) { continue }
        } else {
            $geometry = Get-Geometry $child
            if ($null -eq $geometry) { continue }

            $fillOpacity = [double](Get-StyleValue $style 'fill-opacity' '1')

            # Defaults to none rather than to the SVG default of black, so that
            # a stroke only shape is not filled by accident. Anything that ends
            # up with neither is reported below instead of disappearing quietly.
            $brush = Get-Brush -Value (Get-StyleValue $style 'fill' 'none') -Gradients $Gradients -Opacity $fillOpacity
            $pen = New-Pen -Style $style -Gradients $Gradients

            if ($null -eq $brush -and $null -eq $pen) {
                Write-Warning "Skipped a <$($child.LocalName)> with no fill and no stroke."
                continue
            }

            $drawing = New-Object System.Windows.Media.GeometryDrawing($brush, $pen, $geometry)
        }

        $opacity = [double](Get-Attr $child 'opacity' '1')
        $clipValue = Get-Attr $child 'clip-path' ''
        $transform = Get-Transform (Get-Attr $child 'transform' '')

        if ($opacity -lt 1.0 -or $clipValue -or $transform) {
            $wrapper = New-Object System.Windows.Media.DrawingGroup
            $wrapper.Children.Add($drawing)

            if ($opacity -lt 1.0) { $wrapper.Opacity = $opacity }
            if ($transform) { $wrapper.Transform = $transform }

            if ($clipValue -match '^url\(#(.+)\)$' -and $Clips.ContainsKey($Matches[1])) {
                $wrapper.ClipGeometry = $Clips[$Matches[1]]
            }

            $drawing = $wrapper
        }

        $group.Children.Add($drawing)
    }

    return $group
}

function Import-Svg([string] $Path) {
    if (-not (Test-Path $Path)) { throw "SVG not found: $Path" }

    $xml = New-Object System.Xml.XmlDocument
    $xml.Load($Path)
    $svg = $xml.DocumentElement

    $gradients = @{}
    $clips = @{}
    $filters = @{}

    foreach ($node in $svg.GetElementsByTagName('*')) {
        $id = Get-Attr $node 'id' ''
        if (-not $id) { continue }

        switch ($node.LocalName) {
            'linearGradient' { $gradients[$id] = New-GradientBrush $node }
            'filter'         { $filters[$id] = $node }
            'clipPath'       {
                foreach ($shape in $node.ChildNodes) {
                    if ($shape.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
                    $geometry = Get-Geometry $shape
                    if ($geometry) { $clips[$id] = $geometry; break }
                }
            }
        }
    }

    return ConvertTo-Drawing -Node $svg -Gradients $gradients -Clips $clips -Filters $filters
}

# ---------------------------------------------------------------------------
# Rendering
# ---------------------------------------------------------------------------

function Render-Frame {
    param($Drawing, [int] $Size)

    $bounds = $Drawing.Bounds
    if ($bounds.Width -le 0 -or $bounds.Height -le 0) {
        throw "The drawing has no visible content."
    }

    # Scaled to the content, not to the SVG canvas. The full logo sits inside a
    # large transparent margin, and keeping that margin would waste more than
    # half of a 16 pixel icon.
    $scale = [Math]::Min($Size / $bounds.Width, $Size / $bounds.Height)
    $offsetX = ($Size - $bounds.Width * $scale) / 2.0
    $offsetY = ($Size - $bounds.Height * $scale) / 2.0

    $visual = New-Object System.Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()
    $context.PushTransform((New-Object System.Windows.Media.TranslateTransform($offsetX, $offsetY)))
    $context.PushTransform((New-Object System.Windows.Media.ScaleTransform($scale, $scale)))
    $context.PushTransform((New-Object System.Windows.Media.TranslateTransform(-$bounds.X, -$bounds.Y)))
    $context.DrawDrawing($Drawing)
    $context.Pop()
    $context.Pop()
    $context.Pop()
    $context.Close()

    $target = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(
        $Size, $Size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $target.Render($visual)

    # RenderTargetBitmap produces premultiplied alpha. Icons store straight
    # alpha, so the conversion below is required and not cosmetic.
    $converted = New-Object System.Windows.Media.Imaging.FormatConvertedBitmap(
        $target, [System.Windows.Media.PixelFormats]::Bgra32, $null, 0)
    $converted.Freeze()

    return $converted
}

# The leading comma in the returns below is load bearing. Without it PowerShell
# enumerates the byte array into the pipeline and the caller receives an
# Object[] of boxed bytes, which BinaryWriter will not write.

function Get-PixelBytes($Bitmap, [int] $Size) {
    $stride = $Size * 4
    $buffer = New-Object 'byte[]' ($stride * $Size)
    $Bitmap.CopyPixels($buffer, $stride, 0)
    return , $buffer
}

function ConvertTo-Png($Bitmap) {
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($Bitmap))
    $stream = New-Object System.IO.MemoryStream
    $encoder.Save($stream)
    return , $stream.ToArray()
}

<#
    Builds a 32 bit DIB for one icon entry.

    An icon directory entry holds a bitmap with a doubled height: the colour
    image first, then a one bit AND mask. The mask is redundant for 32 bit
    icons because the alpha channel already carries transparency, but the
    format still requires the bytes to be there.
#>
function ConvertTo-Dib($Bitmap, [int] $Size) {
    $pixels = Get-PixelBytes $Bitmap $Size
    $stride = $Size * 4
    $maskStride = [int]([Math]::Floor(($Size + 31) / 32) * 4)

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)

    # BITMAPINFOHEADER
    $writer.Write([int]40)
    $writer.Write([int]$Size)
    $writer.Write([int]($Size * 2))
    $writer.Write([int16]1)
    $writer.Write([int16]32)
    $writer.Write([int]0)                              # BI_RGB
    $writer.Write([int]($stride * $Size + $maskStride * $Size))
    $writer.Write([int]0)
    $writer.Write([int]0)
    $writer.Write([int]0)
    $writer.Write([int]0)

    # Colour rows, bottom up
    for ($y = $Size - 1; $y -ge 0; $y--) {
        $writer.Write($pixels, $y * $stride, $stride)
    }

    # AND mask, all zero
    $blank = New-Object 'byte[]' $maskStride
    for ($y = 0; $y -lt $Size; $y++) {
        $writer.Write($blank, 0, $maskStride)
    }

    $writer.Flush()
    return , $stream.ToArray()
}

function Write-Icon {
    param([hashtable[]] $Frames, [string] $Path)

    $directory = Split-Path $Path
    if ($directory -and -not (Test-Path $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $stream = [System.IO.File]::Create($Path)
    try {
        $writer = New-Object System.IO.BinaryWriter($stream)

        # ICONDIR
        $writer.Write([int16]0)
        $writer.Write([int16]1)
        $writer.Write([int16]$Frames.Count)

        # Each ICONDIRENTRY is 16 bytes and they follow the 6 byte header.
        $offset = 6 + 16 * $Frames.Count

        foreach ($frame in $Frames) {
            # 256 is stored as 0, which is why the format tops out there.
            $dimension = if ($frame.Size -ge 256) { 0 } else { $frame.Size }

            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)                      # palette entries
            $writer.Write([byte]0)                      # reserved
            $writer.Write([int16]1)                     # colour planes
            $writer.Write([int16]32)                    # bits per pixel
            $writer.Write([int]$frame.Data.Length)
            $writer.Write([int]$offset)

            $offset += $frame.Data.Length
        }

        foreach ($frame in $Frames) {
            $writer.Write($frame.Data)
        }

        $writer.Flush()
    }
    finally {
        $stream.Dispose()
    }
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

Write-Host "Reading $([IO.Path]::GetFileName($FullSource)) and $([IO.Path]::GetFileName($SmallSource))"

$fullDrawing = Import-Svg $FullSource
$smallDrawing = Import-Svg $SmallSource

if ($PngDirectory) {
    New-Item -ItemType Directory -Force -Path $PngDirectory | Out-Null
}

$frames = @()

foreach ($size in ($SmallSizes + $LargeSizes | Sort-Object)) {
    $drawing = if ($size -in $SmallSizes) { $smallDrawing } else { $fullDrawing }
    $variant = if ($size -in $SmallSizes) { 'small' } else { 'full' }

    $bitmap = Render-Frame -Drawing $drawing -Size $size

    # PNG compression is only worth it for the 256 pixel frame. Smaller entries
    # stay as uncompressed bitmaps, which is what every Windows shell version
    # reads without hesitation.
    $data = if ($size -ge 256) { ConvertTo-Png $bitmap } else { ConvertTo-Dib $bitmap $size }

    $frames += @{ Size = $size; Data = $data }
    Write-Host ("  {0,3} px  {1,-6} {2,7:N0} bytes" -f $size, $variant, $data.Length)

    if ($PngDirectory) {
        $pngPath = Join-Path $PngDirectory ("recode-{0}.png" -f $size)
        [System.IO.File]::WriteAllBytes($pngPath, (ConvertTo-Png $bitmap))
    }
}

Write-Icon -Frames $frames -Path $Output

Write-Host ""
Write-Host "Wrote $Output ($((Get-Item $Output).Length) bytes, $($frames.Count) sizes)"
if ($PngDirectory) {
    Write-Host "PNG previews in $PngDirectory"
}

if ($PackageAssets) {
    New-Item -ItemType Directory -Force -Path $PackageAssets | Out-Null

    # The names are fixed by the MSIX schema. The scale suffixed variants let
    # Windows pick a sharp image rather than resampling one, which is visible
    # in the Start menu and the app list.
    $packageLogos = @(
        @{ Name = 'Square44x44Logo.png';              Size = 44 },
        @{ Name = 'Square44x44Logo.targetsize-16.png'; Size = 16 },
        @{ Name = 'Square44x44Logo.targetsize-24.png'; Size = 24 },
        @{ Name = 'Square44x44Logo.targetsize-32.png'; Size = 32 },
        @{ Name = 'Square44x44Logo.targetsize-48.png'; Size = 48 },
        @{ Name = 'Square44x44Logo.scale-200.png';     Size = 88 },
        @{ Name = 'Square150x150Logo.png';             Size = 150 },
        @{ Name = 'Square150x150Logo.scale-200.png';   Size = 300 },
        @{ Name = 'StoreLogo.png';                     Size = 50 }
    )

    Write-Host ""
    Write-Host "Package assets:"

    foreach ($logo in $packageLogos) {
        $drawing = if ($logo.Size -le 24) { $smallDrawing } else { $fullDrawing }
        $bitmap = Render-Frame -Drawing $drawing -Size $logo.Size
        $path = Join-Path $PackageAssets $logo.Name
        [System.IO.File]::WriteAllBytes($path, (ConvertTo-Png $bitmap))
        Write-Host ("  {0,-36} {1,3} px" -f $logo.Name, $logo.Size)
    }
}
