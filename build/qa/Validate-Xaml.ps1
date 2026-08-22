<#
    Parses the app's XAML with the real WPF parser so template and property errors are
    caught without a .NET SDK present. Run from anywhere:
        powershell -sta -File build\qa\Validate-Xaml.ps1
#>
[CmdletBinding()]
param(
    [string]$SourceRoot
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Xaml

if (-not $SourceRoot) {
    # $PSScriptRoot is empty under some invocation styles, so derive it from the
    # command path instead of trusting the automatic variable.
    $here = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
    $SourceRoot = Join-Path $here '..\..\src\DeskCanvas.App'
}
$SourceRoot = (Resolve-Path $SourceRoot).Path
$failed = $false

# XamlReader has no code-behind, so class hooks and handler attributes are removed.
# Done over the XML DOM rather than with regexes, which cannot be trusted against
# multi-line attributes or nested property elements.
function Convert-ToParsable {
    param([string]$Path)

    $document = New-Object System.Xml.XmlDocument
    $document.PreserveWhitespace = $true
    $document.Load($Path)

    $xamlNs = 'http://schemas.microsoft.com/winfx/2006/xaml'
    $stripped = New-Object System.Collections.Generic.List[string]

    foreach ($node in $document.SelectNodes('//*')) {
        foreach ($attribute in @($node.Attributes)) {
            if ($attribute.NamespaceURI -eq $xamlNs -and $attribute.LocalName -eq 'Class') {
                $null = $node.Attributes.Remove($attribute)
                continue
            }
            # An event handler is an unqualified attribute whose value is a bare
            # identifier: no markup extension, no type converter syntax.
            if ($attribute.Prefix) { continue }
            if ($attribute.Value -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') { continue }
            if ($attribute.LocalName -notmatch '^(Click|Drop|DragOver|SelectionChanged|ValueChanged|MouseEnter|MouseLeave|MouseLeftButtonDown|Loaded|Closing|Checked|Unchecked)$') { continue }
            $stripped.Add(('{0}.{1}' -f $node.LocalName, $attribute.LocalName))
            $null = $node.Attributes.Remove($attribute)
        }
    }

    if ($stripped.Count -gt 0) {
        Write-Host ("        removed handlers: {0}" -f ($stripped -join ', ')) -ForegroundColor DarkGray
    }
    return $document.OuterXml
}

function Test-Xaml {
    param([string]$Label, [string]$Xaml)
    try {
        $result = [Windows.Markup.XamlReader]::Parse($Xaml)
        Write-Host ("  PASS  {0} -> {1}" -f $Label, $result.GetType().Name) -ForegroundColor Green
        return $result
    }
    catch {
        $script:failed = $true
        $inner = $_.Exception
        while ($inner.InnerException) { $inner = $inner.InnerException }
        Write-Host ("  FAIL  {0}" -f $Label) -ForegroundColor Red
        Write-Host ("        {0}" -f $inner.Message) -ForegroundColor Red
        if ($_.Exception -is [System.Windows.Markup.XamlParseException]) {
            Write-Host ("        line {0} pos {1}" -f $_.Exception.LineNumber, $_.Exception.LinePosition) -ForegroundColor Red
        }
        return $null
    }
}

Write-Host "Validating XAML under $SourceRoot" -ForegroundColor Cyan

# --- App.xaml: lift Application.Resources into a standalone ResourceDictionary ---
$appPath = Join-Path $SourceRoot 'App.xaml'
$appText = Get-Content -Path $appPath -Raw
$match = [regex]::Match($appText, '<Application\.Resources>(?<body>.*)</Application\.Resources>', 'Singleline')
if (-not $match.Success) { throw "Could not find Application.Resources in $appPath" }

$dictionaryXaml = @"
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
$($match.Groups['body'].Value)
</ResourceDictionary>
"@

$dictionary = Test-Xaml -Label 'App.xaml (resources)' -Xaml $dictionaryXaml

# Windows resolve StaticResource against the application scope, so publish it first.
if ($dictionary) {
    if (-not [System.Windows.Application]::Current) { $null = New-Object System.Windows.Application }
    [System.Windows.Application]::Current.Resources = $dictionary
}

foreach ($window in @('Windows\MainWindow.xaml', 'Windows\MediaWindow.xaml')) {
    $path = Join-Path $SourceRoot $window
    if (-not (Test-Path $path)) { Write-Host "  SKIP  $window (not found)" -ForegroundColor Yellow; continue }
    $null = Test-Xaml -Label $window -Xaml (Convert-ToParsable -Path $path)
}

# --- Every StaticResource key must exist in the dictionary ---
if ($dictionary) {
    Write-Host "Checking StaticResource keys" -ForegroundColor Cyan
    $files = @($appPath) + (Get-ChildItem -Path (Join-Path $SourceRoot 'Windows') -Filter *.xaml | ForEach-Object { $_.FullName })
    foreach ($file in $files) {
        $body = Get-Content -Path $file -Raw
        $keys = [regex]::Matches($body, 'StaticResource\s+([A-Za-z0-9_]+)') |
            ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
        $localKeys = [regex]::Matches($body, 'x:Key\s*=\s*"([^"]+)"') |
            ForEach-Object { $_.Groups[1].Value }
        foreach ($key in $keys) {
            if ($localKeys -contains $key) { continue }
            if ($dictionary.Contains($key)) { continue }
            $script:failed = $true
            Write-Host ("  FAIL  {0}: missing resource '{1}'" -f (Split-Path $file -Leaf), $key) -ForegroundColor Red
        }
    }
    if (-not $failed) { Write-Host "  PASS  all StaticResource keys resolve" -ForegroundColor Green }
}

if ($failed) { Write-Host "XAML validation FAILED" -ForegroundColor Red; exit 1 }
Write-Host "XAML validation passed" -ForegroundColor Green
