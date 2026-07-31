using System.Windows;
using System.Windows.Controls;
using DeskCanvas.App.Services;
using DeskCanvas.Core;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace DeskCanvas.App.Windows;

internal sealed class BuiltInContentPicker : Window
{
    private readonly INowPlayingService nowPlaying;
    private readonly ISystemMetricsService metrics;
    private readonly CanvasItem previewItem = new() { ContentKind = CanvasContentKinds.Clock, Width = 320, Height = 180 };
    private readonly ComboBox kind = new() { Margin = new Thickness(0, 4, 0, 10) };
    private readonly ComboBox clockStyle = new() { Margin = new Thickness(0, 4, 0, 8) };
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
    private readonly CheckBox network = new() { Content = "ネットワーク", IsChecked = true, Margin = new Thickness(0, 3, 0, 3) };
    private readonly StackPanel options = new(); private readonly ContentControl previewHost = new() { HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
    private IDesktopItemContent? preview;

    internal BuiltInContentPicker(Window? owner, INowPlayingService nowPlaying, ISystemMetricsService metrics)
    {
        Owner = owner; this.nowPlaying = nowPlaying; this.metrics = metrics;
        Title = "標準コンテンツを追加"; Width = 840; Height = 500; MinWidth = 760; MinHeight = 430; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(17, 18, 26)); Foreground = System.Windows.Media.Brushes.White;
        kind.ItemsSource = new[] { new KindChoice(CanvasContentKinds.Clock, "時計"), new KindChoice(CanvasContentKinds.NowPlaying, "再生中"), new KindChoice(CanvasContentKinds.SystemMonitor, "システムモニター") }; kind.SelectedIndex = 0;
        clockStyle.ItemsSource = Enum.GetValues<ClockStyle>(); clockStyle.SelectedItem = ClockStyle.Digital;
        kind.SelectionChanged += (_, _) => RebuildPreview(); clockStyle.SelectionChanged += (_, _) => ApplyOptions();
        foreach (var check in new[] { use24, showSeconds, showYear, showMonthDay, art, timeline, sourceApp, cpu, memory, gpu, network }) { check.Checked += (_, _) => ApplyOptions(); check.Unchecked += (_, _) => ApplyOptions(); }
        var kinds = new StackPanel { Margin = new Thickness(18) }; kinds.Children.Add(new TextBlock { Text = "種類", FontWeight = FontWeights.SemiBold, FontSize = 16 }); kinds.Children.Add(kind);
        options.Margin = new Thickness(18);
        var previewBorder = new Border { Margin = new Thickness(18), Padding = new Thickness(12), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 20, 29)), BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(68, 255, 255, 255)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Child = previewHost };
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 350 }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(205) }); Grid.SetColumn(kinds, 0); Grid.SetColumn(previewBorder, 1); Grid.SetColumn(options, 2); grid.Children.Add(kinds); grid.Children.Add(previewBorder); grid.Children.Add(options);
        var add = new Button { Content = "デスクトップへ追加", Margin = new Thickness(8), MinWidth = 150 }; add.Click += (_, _) => { ApplyOptions(); DialogResult = true; };
        var cancel = new Button { Content = "キャンセル", Margin = new Thickness(8), MinWidth = 100 }; cancel.Click += (_, _) => DialogResult = false;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16) }; buttons.Children.Add(cancel); buttons.Children.Add(add);
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
            _ => new ClockItemContent(previewItem)
        };
        var previewSize = SelectedKind switch
        {
            CanvasContentKinds.NowPlaying => new System.Windows.Size(360, 220),
            CanvasContentKinds.SystemMonitor => new System.Windows.Size(300, 180),
            _ => new System.Windows.Size(300, 150)
        };
        var presenter = new Grid { Width = previewSize.Width, Height = previewSize.Height };
        presenter.Children.Add(preview.View);
        previewHost.Content = presenter;
        preview.SetActive(true);
        options.Children.Clear(); options.Children.Add(new TextBlock { Text = $"{(kind.SelectedItem as KindChoice)?.Name} の設定", FontWeight = FontWeights.SemiBold, FontSize = 16 });
        if (SelectedKind == CanvasContentKinds.Clock) { options.Children.Add(new TextBlock { Text = "スタイル", Margin = new Thickness(0, 14, 0, 0) }); options.Children.Add(clockStyle); options.Children.Add(use24); options.Children.Add(showSeconds); options.Children.Add(showYear); options.Children.Add(showMonthDay); }
        else if (SelectedKind == CanvasContentKinds.NowPlaying) { options.Children.Add(art); options.Children.Add(timeline); options.Children.Add(sourceApp); }
        else { options.Children.Add(cpu); options.Children.Add(memory); options.Children.Add(gpu); options.Children.Add(network); }
    }
    private void ApplyOptions()
    {
        previewItem.ContentKind = SelectedKind; previewItem.Clock.Style = (ClockStyle)(clockStyle.SelectedItem ?? ClockStyle.Digital); previewItem.Clock.Use24Hour = use24.IsChecked == true; previewItem.Clock.ShowSeconds = showSeconds.IsChecked == true; previewItem.Clock.ShowYear = showYear.IsChecked == true; previewItem.Clock.ShowMonthDay = showMonthDay.IsChecked == true;
        previewItem.NowPlaying.ShowAlbumArt = art.IsChecked == true; previewItem.NowPlaying.ShowTimeline = timeline.IsChecked == true; previewItem.NowPlaying.ShowSourceApp = sourceApp.IsChecked == true;
        previewItem.SystemMonitor.ShowCpu = cpu.IsChecked == true; previewItem.SystemMonitor.ShowMemory = memory.IsChecked == true; previewItem.SystemMonitor.ShowGpu = gpu.IsChecked == true; previewItem.SystemMonitor.ShowNetwork = network.IsChecked == true;
        if (preview is ClockItemContent clock) clock.Refresh();
        else if (preview is NowPlayingItemContent playing) playing.Refresh();
        else if (preview is SystemMonitorItemContent monitor) monitor.Refresh();
    }
    private sealed record KindChoice(string Kind, string Name) { public override string ToString() => Name; }
}