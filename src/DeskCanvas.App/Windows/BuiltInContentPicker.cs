using System.Windows;
using System.Windows.Controls;
using DeskCanvas.App.Services;
using DeskCanvas.Core;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;

namespace DeskCanvas.App.Windows;

internal sealed class BuiltInContentPicker : Window
{
    private readonly INowPlayingService nowPlaying;
    private readonly ISystemMetricsService metrics;
    private readonly ICodexUsageService? codexUsage;
    private readonly CanvasItem previewItem = new() { ContentKind = CanvasContentKinds.Clock, Width = 320, Height = 180 };
    private readonly ComboBox kind = new() { Margin = new Thickness(0, 4, 0, 10) };
    private readonly ComboBox clockStyle = new() { Margin = new Thickness(0, 4, 0, 8) };
    private readonly ComboBox monitorStyle = new() { Margin = new Thickness(0, 4, 0, 8) };
    private readonly ComboBox surfaceStyle = new() { Margin = new Thickness(0, 4, 0, 8) };
    private readonly CheckBox use24 = new() { Content = "24時間表記", IsChecked = true, Margin = new Thickness(0, 3, 0, 3) };
    private readonly CheckBox showSeconds = new() { Content = "秒を表示", IsChecked = true, Margin = new Thickness(0, 3, 0, 3) };
    private readonly CheckBox showYear = new() { Content = "年を表示", IsChecked = false, Margin = new Thickness(0, 3, 0, 3) };
    private readonly CheckBox showMonthDay = new() { Content = "月日を表示", IsChecked = true, Margin = new Thickness(0, 3, 0, 3) };
    private readonly CheckBox art = new() { Content = "アルバムアート", IsChecked = true, Margin = new Thickness(0, 3, 0, 3) };
    private readonly CheckBox timeline = new() { Content = "タイムライン", IsChecked = true, Margin = new Thickness(0, 3, 0, 3) };
    private readonly CheckBox sourceApp = new() { Content = "再生元アプリ", IsChecked = true, Margin = new Thickness(0, 3, 0, 3) };
    private readonly CheckBox cpu = new() { Content = "CPU", IsChecked = true, Margin = new Thickness(0, 3, 0, 3) };
    private readonly CheckBox memory = new() { Content = "RAM", IsChecked = true, Margin = new Thickness(0, 3, 0, 3) };
    private readonly CheckBox gpu = new() { Content = "GPU", IsChecked = true, Margin = new Thickness(0, 3, 0, 3) };
    private readonly CheckBox vram = new() { Content = "VRAM", IsChecked = true, Margin = new Thickness(0, 3, 0, 3) };
    private readonly CheckBox network = new() { Content = "ネットワーク", IsChecked = true, Margin = new Thickness(0, 3, 0, 3) };
    private readonly StackPanel options = new(); private readonly ContentControl previewHost = new() { HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
    private IDesktopItemContent? preview;

    internal BuiltInContentPicker(Window? owner, INowPlayingService nowPlaying, ISystemMetricsService metrics, ICodexUsageService? codexUsage = null)
    {
        Owner = owner; this.nowPlaying = nowPlaying; this.metrics = metrics; this.codexUsage = codexUsage;
        Title = "ウィジェットを追加"; Width = 860; Height = 520; MinWidth = 780; MinHeight = 450; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Design.Frozen(Design.WindowBackground); Foreground = Design.LabelPrimaryBrush; FontFamily = Design.TextFont;
        kind.ItemsSource = new[]
        {
            new KindChoice(CanvasContentKinds.Clock, "時計"),
            new KindChoice(CanvasContentKinds.NowPlaying, "再生中"),
            new KindChoice(CanvasContentKinds.SystemMonitor, "システムモニター"),
            new KindChoice(CanvasContentKinds.CodexUsage, "Codexリミット")
        }; kind.SelectedIndex = 0;
        clockStyle.ItemsSource = Enum.GetValues<ClockStyle>(); clockStyle.SelectedItem = ClockStyle.Digital;
        monitorStyle.ItemsSource = new[]
        {
            new Choice<SystemMonitorStyle>(SystemMonitorStyle.Detail, "詳細"),
            new Choice<SystemMonitorStyle>(SystemMonitorStyle.Meters, "メーター")
        };
        monitorStyle.SelectedIndex = 0;
        surfaceStyle.ItemsSource = new[]
        {
            new Choice<WidgetSurfaceStyle>(WidgetSurfaceStyle.Standard, "標準"),
            new Choice<WidgetSurfaceStyle>(WidgetSurfaceStyle.MinimalGlass, "ガラス"),
            new Choice<WidgetSurfaceStyle>(WidgetSurfaceStyle.LiquidGlass, "リキッドガラス")
        };
        surfaceStyle.SelectedIndex = 0;
        kind.SelectionChanged += (_, _) => RebuildPreview();
        clockStyle.SelectionChanged += (_, _) => ApplyOptions();
        monitorStyle.SelectionChanged += (_, _) => ApplyOptions();
        surfaceStyle.SelectionChanged += (_, _) => ApplyOptions();
        foreach (var check in new[] { use24, showSeconds, showYear, showMonthDay, art, timeline, sourceApp, cpu, memory, gpu, vram, network }) { check.Checked += (_, _) => ApplyOptions(); check.Unchecked += (_, _) => ApplyOptions(); }
        var kinds = new StackPanel { Margin = new Thickness(24, 22, 12, 22) }; kinds.Children.Add(SectionLabel("種類")); kinds.Children.Add(kind);
        options.Margin = new Thickness(12, 22, 24, 22);
        // The preview sits in a recessed well so the widget's own card edge stays readable.
        var previewBorder = new Border { Margin = new Thickness(12, 22, 12, 22), Padding = new Thickness(20), Background = Design.Frozen(Design.Argb(0x66, 0x00, 0x00, 0x00)), BorderBrush = Design.SeparatorBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Child = previewHost };
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(186) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 350 }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(216) }); Grid.SetColumn(kinds, 0); Grid.SetColumn(previewBorder, 1); Grid.SetColumn(options, 2); grid.Children.Add(kinds); grid.Children.Add(previewBorder); grid.Children.Add(options);
        var add = new Button { Content = "デスクトップへ追加", Margin = new Thickness(8, 0, 0, 0), MinWidth = 150 }; add.Click += (_, _) => { ApplyOptions(); DialogResult = true; };
        if (TryFindResource("AccentButton") is Style accentStyle) add.Style = accentStyle;
        var cancel = new Button { Content = "キャンセル", MinWidth = 100 }; cancel.Click += (_, _) => DialogResult = false;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(24, 0, 24, 22) }; buttons.Children.Add(cancel); buttons.Children.Add(add);
        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.Children.Add(grid); Grid.SetRow(buttons, 1); root.Children.Add(buttons); Content = root;
        Closed += (_, _) => preview?.Dispose(); RebuildPreview();
    }

    internal string SelectedKind => (kind.SelectedItem as KindChoice)?.Kind ?? CanvasContentKinds.Clock;
    internal ClockOptions ClockOptions => previewItem.Clock.Clone();
    internal NowPlayingOptions NowPlayingOptions => previewItem.NowPlaying.Clone();
    internal SystemMonitorOptions SystemMonitorOptions => previewItem.SystemMonitor.Clone();

    private void RebuildPreview()
    {
        ApplyOptions();
        preview?.Dispose();
        preview = SelectedKind switch
        {
            CanvasContentKinds.NowPlaying => new NowPlayingItemContent(previewItem, nowPlaying),
            CanvasContentKinds.SystemMonitor => new SystemMonitorItemContent(previewItem, metrics),
            CanvasContentKinds.CodexUsage => new CodexUsageItemContent(codexUsage ?? new CodexUsageService(previewMode: true)),
            _ => new ClockItemContent(previewItem)
        };
        var previewSize = SelectedKind switch
        {
            CanvasContentKinds.NowPlaying => new System.Windows.Size(360, 220),
            CanvasContentKinds.SystemMonitor => new System.Windows.Size(360, 200),
            CanvasContentKinds.CodexUsage => new System.Windows.Size(300, 180),
            _ => new System.Windows.Size(300, 150)
        };
        var presenter = new Grid { Width = previewSize.Width, Height = previewSize.Height };
        presenter.Children.Add(preview.View);
        previewHost.Content = presenter;
        preview.SetActive(true);
        options.Children.Clear(); options.Children.Add(SectionLabel($"{(kind.SelectedItem as KindChoice)?.Name} の設定"));
        if (SelectedKind == CanvasContentKinds.Clock) { options.Children.Add(SectionLabel("スタイル", topMargin: 18)); options.Children.Add(clockStyle); options.Children.Add(SectionLabel("サーフェス", topMargin: 14)); options.Children.Add(surfaceStyle); options.Children.Add(use24); options.Children.Add(showSeconds); options.Children.Add(showYear); options.Children.Add(showMonthDay); }
        else if (SelectedKind == CanvasContentKinds.NowPlaying) { options.Children.Add(SectionLabel("サーフェス", topMargin: 18)); options.Children.Add(surfaceStyle); options.Children.Add(art); options.Children.Add(timeline); options.Children.Add(sourceApp); }
        else if (SelectedKind == CanvasContentKinds.SystemMonitor) { options.Children.Add(SectionLabel("スタイル", topMargin: 18)); options.Children.Add(monitorStyle); options.Children.Add(SectionLabel("サーフェス", topMargin: 14)); options.Children.Add(surfaceStyle); options.Children.Add(cpu); options.Children.Add(memory); options.Children.Add(gpu); options.Children.Add(vram); options.Children.Add(network); }
    }
    private void ApplyOptions()
    {
        previewItem.ContentKind = SelectedKind; previewItem.Clock.Style = (ClockStyle)(clockStyle.SelectedItem ?? ClockStyle.Digital); previewItem.Clock.Use24Hour = use24.IsChecked == true; previewItem.Clock.ShowSeconds = showSeconds.IsChecked == true; previewItem.Clock.ShowYear = showYear.IsChecked == true; previewItem.Clock.ShowMonthDay = showMonthDay.IsChecked == true;
        var surface = (surfaceStyle.SelectedItem as Choice<WidgetSurfaceStyle>)?.Value ?? WidgetSurfaceStyle.Standard;
        previewItem.Clock.SurfaceStyle = surface;
        previewItem.NowPlaying.SurfaceStyle = surface;
        previewItem.NowPlaying.ShowAlbumArt = art.IsChecked == true; previewItem.NowPlaying.ShowTimeline = timeline.IsChecked == true; previewItem.NowPlaying.ShowSourceApp = sourceApp.IsChecked == true;
        previewItem.SystemMonitor.SurfaceStyle = surface;
        previewItem.SystemMonitor.Style = (monitorStyle.SelectedItem as Choice<SystemMonitorStyle>)?.Value ?? SystemMonitorStyle.Detail;
        previewItem.SystemMonitor.ShowCpu = cpu.IsChecked == true; previewItem.SystemMonitor.ShowMemory = memory.IsChecked == true; previewItem.SystemMonitor.ShowGpu = gpu.IsChecked == true; previewItem.SystemMonitor.ShowVram = vram.IsChecked == true; previewItem.SystemMonitor.ShowNetwork = network.IsChecked == true;
        if (preview is ClockItemContent clock) clock.Refresh();
        else if (preview is NowPlayingItemContent playing) playing.Refresh();
        else if (preview is SystemMonitorItemContent monitor) monitor.Refresh();
    }
    private static TextBlock SectionLabel(string text, double topMargin = 0)
    {
        var block = Design.Text(text, 11, Design.LabelTertiaryBrush, FontWeights.SemiBold);
        block.Margin = new Thickness(0, topMargin, 0, 6);
        return block;
    }

    private sealed record KindChoice(string Kind, string Name) { public override string ToString() => Name; }
    private sealed record Choice<T>(T Value, string Name) { public override string ToString() => Name; }
}