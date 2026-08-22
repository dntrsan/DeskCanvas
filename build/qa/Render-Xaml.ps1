<#
    Renders a standalone XAML fragment to PNG. Used for design reference sheets.
        powershell -sta -File build\qa\Render-Xaml.ps1 -XamlPath <file> -OutPath <file.png>
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$XamlPath,
    [Parameter(Mandatory)][string]$OutPath,
    [int]$Scale = 2
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml

$element = [Windows.Markup.XamlReader]::Parse((Get-Content -Path $XamlPath -Raw))
$width = $element.Width
$height = $element.Height
if ([double]::IsNaN($width) -or [double]::IsNaN($height)) {
    throw "Root element must declare explicit Width and Height."
}

$element.Measure((New-Object System.Windows.Size($width, $height)))
$element.Arrange((New-Object System.Windows.Rect(0, 0, $width, $height)))
$element.UpdateLayout()
[System.Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke(
    [System.Windows.Threading.DispatcherPriority]::SystemIdle, [action] {})
$element.UpdateLayout()

$bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(
    [int]($width * $Scale), [int]($height * $Scale), (96 * $Scale), (96 * $Scale),
    [System.Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($element)

$directory = Split-Path -Parent $OutPath
if ($directory -and -not (Test-Path $directory)) { $null = New-Item -ItemType Directory -Path $directory }

$encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$stream = [System.IO.File]::Create($OutPath)
try { $encoder.Save($stream) } finally { $stream.Dispose() }

Write-Host "Wrote $OutPath" -ForegroundColor Green
