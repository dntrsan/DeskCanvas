using System.Windows;
using System.Windows.Controls;
using DeskCanvas.App.Services;
using DeskCanvas.Core;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace DeskCanvas.App.Windows;

internal sealed class BuiltInContentPicker : Window
{
    private readonly INowPlayingService nowPlaying;
    private readonly ISystemMetricsService metrics;
    private readonly ICodexUsageService codexUsage;
    private readonly CanvasItem previewItem = new() { ContentKind = CanvasContentKinds.Clock, Width = 320, Height = 180, Theme = WidgetThemeKind.Auto };
    private readonly WpfComboBox kind = FieldCombo();
    private readonly WpfComboBox theme = FieldCombo();
    private readonly WpfComboBox surfaceStyle = FieldCombo();
    private readonly WpfComboBox clockStyle = FieldCombo();
    private readonly WpfComboBox progressStyle = FieldCombo();
    private readonly WpfCheckBox use24 = Check("24時間表記", true);
    private readonly WpfCheckBox showSeconds = Check("秒を表示", true);
    private readonly WpfCheckBox showYear = Check("年を表示", false);
    private readonly WpfCheckBox showMonthDay = Check("月日を表示", true);
    private readonly WpfCheckBox art = Check("アルバムアート", true);
    private readonly WpfCheckBox timeline = Check("タイムライン", true);
    private readonly WpfCheckBox spectrum = Check("オーディオスペクトラムを表示", true);
    private readonly WpfCheckBox sourceApp = Check("再生元アプリ", true);
    private readonly WpfCheckBox cpu = Check("CPU", true);
    private readonly WpfCheckBox memory = Check("RAM", true);
    private readonly WpfCheckBox gpu = Check("GPU", true);
    private readonly WpfCheckBox network = Check("ネットワーク", true);
    private readonly StackPanel options = new();
    private readonly ContentControl previewHost = new() { HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
    private IDesktopItemContent? preview;
    private bool rebuilding;

    internal BuiltInContentPicker(Window? owner, INowPlayingService nowPlaying, ISystemMetricsService metrics, ICodexUsageService codexUsage)
    {
        Owner = owner; this.nowPlaying = nowPlaying; this.metrics = metrics; this.codexUsage = codexUsage;
        Title = "標準コンテンツを追加"; Width = 900; Height = 560; MinWidth = 820; MinHeight = 500; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(17, 18, 26)); Foreground = System.Windows.Media.Brushes.White;
        kind.ItemsSource = new[] { new KindChoice(CanvasContentKinds.Clock, "時計"), new KindChoice(CanvasContentKinds.NowPlaying, "再生中"), new KindChoice(CanvasContentKinds.SystemMonitor, "システムモニター"), new KindChoice(CanvasContentKinds.CodexUsage, "Codexリミット") }; kind.SelectedIndex = 0;
        theme.ItemsSource = Enum.GetValues<WidgetThemeKind>(); theme.SelectedItem = WidgetThemeKind.Auto;
        surfaceStyle.ItemsSource = SurfaceStyleChoices; surfaceStyle.SelectedItem = SurfaceChoice(WidgetSurfaceStyle.Standard);
        clockStyle.ItemsSource = Enum.GetValues<ClockStyle>(); clockStyle.SelectedItem = ClockStyle.Digital;
        progressStyle.ItemsSource = ProgressChoices; progressStyle.SelectedItem = Choice(NowPlayingProgressStyle.Simple);
        kind.SelectionChanged += (_, _) => RebuildPreview(); theme.SelectionChanged += (_, _) => ApplyOptions(); surfaceStyle.SelectionChanged += (_, _) => ApplyOptions(); clockStyle.SelectionChanged += (_, _) => ApplyOptions(); progressStyle.SelectionChanged += (_, _) => ApplyOptions();
        foreach (var check in new[] { use24, showSeconds, showYear, showMonthDay, art, timeline, spectrum, sourceApp, cpu, memory, gpu, network }) { check.Checked += (_, _) => ApplyOptions(); check.Unchecked += (_, _) => ApplyOptions(); }
        var selector = new StackPanel { Margin = new Thickness(18) }; selector.Children.Add(Section("種類")); selector.Children.Add(kind); selector.Children.Add(Section("テーマ", new Thickness(0, 18, 0, 0))); selector.Children.Add(theme);
        options.Margin = new Thickness(18);
        var previewBorder = new Border { Margin = new Thickness(12), Padding = new Thickness(10), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 22, 31)), BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(68, 255, 255, 255)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = previewHost };
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 390 }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(215) }); grid.Children.Add(selector); Grid.SetColumn(previewBorder, 1); grid.Children.Add(previewBorder); Grid.SetColumn(options, 2); grid.Children.Add(options);
        var add = new WpfButton { Content = "デスクトップへ追加", Margin = new Thickness(8), MinWidth = 150 }; add.Click += (_, _) => { ApplyOptions(); DialogResult = true; };
        var cancel = new WpfButton { Content = "キャンセル", Margin = new Thickness(8), MinWidth = 100 }; cancel.Click += (_, _) => DialogResult = false;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16) }; buttons.Children.Add(cancel); buttons.Children.Add(add);
        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.Children.Add(grid); Grid.SetRow(buttons, 1); root.Children.Add(buttons); Content = root;
        Closed += (_, _) => { preview?.SetActive(false); preview?.Dispose(); preview = null; }; RebuildPreview();
    }

    internal string SelectedKind => (kind.SelectedItem as KindChoice)?.Kind ?? CanvasContentKinds.Clock;
    internal WidgetThemeKind Theme => theme.SelectedItem is WidgetThemeKind value && Enum.IsDefined(value) ? value : WidgetThemeKind.Auto;
    internal ClockOptions ClockOptions => previewItem.Clock.Clone();
    internal NowPlayingOptions NowPlayingOptions => previewItem.NowPlaying.Clone();
    internal SystemMonitorOptions SystemMonitorOptions => previewItem.SystemMonitor.Clone();
    private void RebuildPreview()
    {
        if (rebuilding) return; rebuilding = true;
        try { ApplyOptions(); preview?.SetActive(false); preview?.Dispose(); preview = SelectedKind switch { CanvasContentKinds.NowPlaying => new NowPlayingItemContent(previewItem, nowPlaying), CanvasContentKinds.SystemMonitor => new SystemMonitorItemContent(previewItem, metrics), CanvasContentKinds.CodexUsage => new CodexUsageItemContent(codexUsage), _ => new ClockItemContent(previewItem) }; var size = SelectedKind switch { CanvasContentKinds.NowPlaying => new Size(360, 220), CanvasContentKinds.SystemMonitor => new Size(320, 210), CanvasContentKinds.CodexUsage => new Size(360, 160), _ => new Size(300, 150) }; var presenter = new Grid { Width = size.Width, Height = size.Height }; presenter.Children.Add(preview.View); previewHost.Content = presenter; preview.SetActive(true); BuildOptions(); }
        finally { rebuilding = false; }
    }
    private void BuildOptions()
    {
        options.Children.Clear(); options.Children.Add(Section($"{(kind.SelectedItem as KindChoice)?.Name} の設定"));
        if (SelectedKind == CanvasContentKinds.Clock) { options.Children.Add(Section("カードスタイル", new Thickness(0, 14, 0, 0))); options.Children.Add(surfaceStyle); options.Children.Add(Section("時計の表示", new Thickness(0, 4, 0, 0))); options.Children.Add(clockStyle); options.Children.Add(use24); options.Children.Add(showSeconds); options.Children.Add(showYear); options.Children.Add(showMonthDay); }
        else if (SelectedKind == CanvasContentKinds.NowPlaying) { options.Children.Add(Section("カードスタイル", new Thickness(0, 14, 0, 0))); options.Children.Add(surfaceStyle); options.Children.Add(art); options.Children.Add(timeline); options.Children.Add(spectrum); options.Children.Add(sourceApp); options.Children.Add(Section("再生バー", new Thickness(0, 12, 0, 0))); progressStyle.IsEnabled = timeline.IsChecked == true; options.Children.Add(progressStyle); }
        else if (SelectedKind == CanvasContentKinds.SystemMonitor) { options.Children.Add(cpu); options.Children.Add(memory); options.Children.Add(gpu); options.Children.Add(network); }
        else { options.Children.Add(new TextBlock { Text = "Codexと同じログイン状態から、残量とリセット時刻を読み取り専用で表示します。", TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.LightGray, Margin = new Thickness(0, 12, 0, 0) }); }
    }
    private void ApplyOptions()
    {
        previewItem.ContentKind = SelectedKind; previewItem.Theme = Theme; var selectedSurface = surfaceStyle.SelectedItem is SurfaceStyleChoice selected ? selected.Value : WidgetSurfaceStyle.Standard; previewItem.Clock.SurfaceStyle = selectedSurface; previewItem.NowPlaying.SurfaceStyle = selectedSurface; previewItem.Clock.Style = clockStyle.SelectedItem is ClockStyle style ? style : ClockStyle.Digital; previewItem.Clock.Use24Hour = use24.IsChecked == true; previewItem.Clock.ShowSeconds = showSeconds.IsChecked == true; previewItem.Clock.ShowYear = showYear.IsChecked == true; previewItem.Clock.ShowMonthDay = showMonthDay.IsChecked == true;
        previewItem.NowPlaying.ShowAlbumArt = art.IsChecked == true; previewItem.NowPlaying.ShowTimeline = timeline.IsChecked == true; previewItem.NowPlaying.ShowSpectrum = spectrum.IsChecked == true; previewItem.NowPlaying.ShowSourceApp = sourceApp.IsChecked == true; previewItem.NowPlaying.ProgressStyle = progressStyle.SelectedItem is ProgressChoice progress ? progress.Value : NowPlayingProgressStyle.Simple; progressStyle.IsEnabled = previewItem.NowPlaying.ShowTimeline;
        previewItem.SystemMonitor.ShowCpu = cpu.IsChecked == true; previewItem.SystemMonitor.ShowMemory = memory.IsChecked == true; previewItem.SystemMonitor.ShowGpu = gpu.IsChecked == true; previewItem.SystemMonitor.ShowNetwork = network.IsChecked == true;
        switch (preview) { case ClockItemContent clock: clock.Refresh(); break; case NowPlayingItemContent playing: playing.Refresh(); break; case SystemMonitorItemContent monitor: monitor.Refresh(); break; }
    }
    private static WpfComboBox FieldCombo() => new() { Margin = new Thickness(0, 4, 0, 10) };
    private static WpfCheckBox Check(string content, bool value) => new() { Content = content, IsChecked = value, Margin = new Thickness(0, 3, 0, 3) };
    private static TextBlock Section(string text, Thickness margin = default) => new() { Text = text, FontWeight = FontWeights.SemiBold, FontSize = 16, Margin = margin };
    private static readonly ProgressChoice[] ProgressChoices = [new(NowPlayingProgressStyle.Simple, "シンプル"), new(NowPlayingProgressStyle.Wave, "ウェーブ")];
    private static readonly SurfaceStyleChoice[] SurfaceStyleChoices = [new(WidgetSurfaceStyle.Standard, "標準"), new(WidgetSurfaceStyle.MinimalGlass, "ミニマルガラス")];
    private static ProgressChoice Choice(NowPlayingProgressStyle value) => ProgressChoices.First(choice => choice.Value == NowPlayingOptions.NormalizeProgressStyle(value));
    private static SurfaceStyleChoice SurfaceChoice(WidgetSurfaceStyle value) => SurfaceStyleChoices.First(choice => choice.Value == (Enum.IsDefined(value) ? value : WidgetSurfaceStyle.Standard));
    private sealed record ProgressChoice(NowPlayingProgressStyle Value, string Name) { public override string ToString() => Name; }
    private sealed record SurfaceStyleChoice(WidgetSurfaceStyle Value, string Name) { public override string ToString() => Name; }
    private sealed record KindChoice(string Kind, string Name) { public override string ToString() => Name; }
}
