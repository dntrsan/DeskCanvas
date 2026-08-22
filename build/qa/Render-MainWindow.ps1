<#
    Renders MainWindow.xaml to PNG without a .NET SDK, so the visual design can be
    reviewed. Populates the list and detail pane with sample data.
        powershell -sta -File build\qa\Render-MainWindow.ps1 -SourceRoot <path> -OutDir <path>
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SourceRoot,
    [Parameter(Mandatory)][string]$OutDir
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml

# A static render needs no change notification, only the bound properties.
Add-Type -TypeDefinition @'
public class SampleItem
{
    public string DisplayName { get; set; }
    public string KindLabel { get; set; }
    public bool IsLocked { get; set; }
}
'@

function Get-Parsable {
    param([string]$Path)
    $document = New-Object System.Xml.XmlDocument
    $document.PreserveWhitespace = $true
    $document.Load($Path)
    $xamlNs = 'http://schemas.microsoft.com/winfx/2006/xaml'
    foreach ($node in $document.SelectNodes('//*')) {
        foreach ($attribute in @($node.Attributes)) {
            if ($attribute.NamespaceURI -eq $xamlNs -and $attribute.LocalName -eq 'Class') {
                $null = $node.Attributes.Remove($attribute); continue
            }
            if ($attribute.Prefix) { continue }
            if ($attribute.Value -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { continue }
            if ($attribute.LocalName -notmatch '^(Click|Drop|DragOver|SelectionChanged|ValueChanged|MouseEnter|MouseLeave|MouseLeftButtonDown|Loaded|Closing|Checked|Unchecked)$') { continue }
            $null = $node.Attributes.Remove($attribute)
        }
    }
    return $document.OuterXml
}

# Publish App.xaml resources into application scope for StaticResource lookups.
$appText = Get-Content -Path (Join-Path $SourceRoot 'App.xaml') -Raw
$body = [regex]::Match($appText, '<Application\.Resources>(?<body>.*)</Application\.Resources>', 'Singleline').Groups['body'].Value
$dictionaryXaml = @"
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
$body
</ResourceDictionary>
"@
if (-not [System.Windows.Application]::Current) { $null = New-Object System.Windows.Application }
[System.Windows.Application]::Current.Resources = [Windows.Markup.XamlReader]::Parse($dictionaryXaml)

$window = [Windows.Markup.XamlReader]::Parse((Get-Parsable -Path (Join-Path $SourceRoot 'Windows\MainWindow.xaml')))

$items = New-Object System.Collections.ObjectModel.ObservableCollection[object]
foreach ($spec in @(
        @{ n = 'wallpaper-cat.gif'; k = 'GIF'; l = $false },
        @{ n = '時計'; k = '時計'; l = $false },
        @{ n = 'システムモニター'; k = 'システムモニター'; l = $true },
        @{ n = 'album-cover-artwork-long-name.png'; k = '画像'; l = $false })) {
    $item = New-Object SampleItem
    $item.DisplayName = $spec.n; $item.KindLabel = $spec.k; $item.IsLocked = $spec.l
    $items.Add($item)
}

$list = $window.FindName('ItemsList')
$list.ItemsSource = $items
$list.SelectedIndex = 3

# Reproduce the state the code-behind sets once an item is selected.
$window.FindName('EmptyMessage').Visibility = 'Collapsed'
$window.FindName('EditorPanel').Visibility = 'Visible'
$window.FindName('SelectedName').Text = 'album-cover-artwork-long-name.png'
$window.FindName('OpacitySlider').Value = 88
$window.FindName('OpacityValue').Text = '88%'
$window.FindName('RotationSlider').Value = -12
$window.FindName('RotationValue').Text = '-12°'
$window.FindName('DecorationCombo').SelectedIndex = 1
$window.FindName('ItemLockCheck').IsChecked = $true
$window.FindName('StartupCheck').IsChecked = $true
$window.FindName('StatusText').Text = '編集中 — 個別ロックしていない素材へカーソルを載せると操作できます'

$width = 900
$height = 620

# A Window cannot be arranged offscreen, so its content is rendered on a matching surface.
$content = $window.Content
$window.Content = $null
$surface = New-Object System.Windows.Controls.Border
$surface.Background = $window.Background
$surface.Width = $width
$surface.Height = $height
$surface.Child = $content
$surface.Measure((New-Object System.Windows.Size($width, $height)))
$surface.Arrange((New-Object System.Windows.Rect(0, 0, $width, $height)))
$surface.UpdateLayout()

# Layout of virtualised list rows settles a frame later.
[System.Windows.Threading.Dispatcher]::CurrentDispatcher.Invoke(
    [System.Windows.Threading.DispatcherPriority]::SystemIdle, [action] {})
$surface.UpdateLayout()

$scale = 2
$bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(
    ($width * $scale), ($height * $scale), (96 * $scale), (96 * $scale),
    [System.Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($surface)

if (-not (Test-Path $OutDir)) { $null = New-Item -ItemType Directory -Path $OutDir }
$target = Join-Path $OutDir 'mainwindow.png'
$encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$stream = [System.IO.File]::Create($target)
try { $encoder.Save($stream) } finally { $stream.Dispose() }

Write-Host "Wrote $target" -ForegroundColor Green
