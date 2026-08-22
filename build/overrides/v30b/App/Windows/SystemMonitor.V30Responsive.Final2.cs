using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using DeskCanvas.App.Services;
using DeskCanvas.Core;
using Microsoft.Win32;
using WpfColor = System.Windows.Media.Color;
using WpfPath = System.Windows.Shapes.Path;

namespace DeskCanvas.App.Windows;

internal static class SystemMonitorLayoutMath
{
    internal static bool DividerVisible(bool hasMetric, bool hasNetwork) => hasMetric && hasNetwork;
    internal static (double Padding, double Header, double Row, bool Dense) Values(double width, double height, int rows, bool divider)
    {
        width = Math.Max(96, width); height = Math.Max(96, height);
        var pad = Math.Clamp(Math.Min(width, height) * .052, 5, 12);
        var header = Math.Clamp(height * .105, 12, 24);
        var dividerSpace = divider ? Math.Clamp(height * .075, 5, 14) : 0;
        return (pad, header, Math.Max(14, (height - header - dividerSpace) / Math.Max(1, rows)), width < 280 || height < 260);
    }
}

internal sealed class SystemMonitorItemContent : IDesktopItemContent
{
    private readonly CanvasItem item;
    private readonly ISystemMetricsService service;
    private readonly Border root;
    private readonly StackPanel rows = new();
    private readonly StackPanel header = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock headerText = new() { Text = "SYSTEM STATUS", FontWeight = FontWeights.SemiBold };
    private readonly Border divider = new() { Height = 1 };
    private readonly MetricRow cpu = new("CPU", "M5,4 L15,4 L15,14 L5,14 Z M8,1 L8,4 M12,1 L12,4 M8,14 L8,17 M12,14 L12,17 M1,8 L5,8 M1,12 L5,12 M15,8 L19,8 M15,12 L19,12");
    private readonly MetricRow ram = new("RAM", "M2,5 L18,5 L18,15 L2,15 Z M5,8 L15,8 M6,2 L6,5 M10,2 L10,5 M14,2 L14,5 M6,15 L6,18 M10,15 L10,18 M14,15 L14,18");
    private readonly MetricRow gpu = new("GPU", "M2,5 L18,5 L18,15 L2,15 Z M6,9 A2,2 0 1 0 6.1,9 M14,9 A2,2 0 1 0 14.1,9 M1,2 L4,5 M19,2 L16,5 M1,18 L4,15 M19,18 L16,15");
    private readonly NetworkRow down = new("DOWN", false), up = new("UP", true);
    private readonly SizeChangedEventHandler layoutChanged;
    private IDisposable? lease;
    private bool active;

    internal SystemMonitorItemContent(CanvasItem item, ISystemMetricsService service)
    {
        this.item = item; this.service = service;
        layoutChanged = (_, _) => Layout();
        header.Children.Add(headerText);
        rows.Children.Add(header); rows.Children.Add(cpu.View); rows.Children.Add(ram.View); rows.Children.Add(gpu.View); rows.Children.Add(divider); rows.Children.Add(down.View); rows.Children.Add(up.View);
        rows.UseLayoutRounding = true; rows.SnapsToDevicePixels = true;
        root = WidgetTheme.Card(item, rows);
        root.CornerRadius = new CornerRadius(20);
        root.Padding = new Thickness(18);
        root.BorderThickness = new Thickness(1);
        root.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 1, Opacity = .14 };
        root.UseLayoutRounding = true; root.SnapsToDevicePixels = true;
        root.SizeChanged += layoutChanged;
        Update(service.Snapshot);
    }

    public UIElement View => root;
    public void SetActive(bool value)
    {
        if (active == value) return; active = value;
        if (value) { service.SnapshotChanged += Changed; SystemEvents.UserPreferenceChanged += PreferenceChanged; lease = service.Acquire(); Update(service.Snapshot); }
        else { service.SnapshotChanged -= Changed; SystemEvents.UserPreferenceChanged -= PreferenceChanged; lease?.Dispose(); lease = null; cpu.Stop(); ram.Stop(); gpu.Stop(); }
    }
    internal void Refresh() => Update(service.Snapshot);
    public void Dispose() { SetActive(false); root.SizeChanged -= layoutChanged; cpu.Dispose(); ram.Dispose(); gpu.Dispose(); down.Dispose(); up.Dispose(); }
    private void Changed(object? _, SystemMetricsSnapshot snapshot) => root.Dispatcher.BeginInvoke(() => Update(snapshot));
    private void PreferenceChanged(object? _, UserPreferenceChangedEventArgs __) => root.Dispatcher.BeginInvoke(() => Update(service.Snapshot));

    private void Update(SystemMetricsSnapshot snapshot)
    {
        root.Background = new SolidColorBrush(WpfColor.FromArgb(196, 26, 28, 33));
        root.BorderBrush = new SolidColorBrush(WpfColor.FromArgb(54, 255, 255, 255));
        var accent = WpfColor.FromRgb(148, 188, 255);
        headerText.Foreground = new SolidColorBrush(WpfColor.FromRgb(143, 149, 161));
        cpu.Update(snapshot.Cpu, ProcessorClock.Read(), accent);
        ram.Update(snapshot.Memory, Aux(snapshot.Memory), accent);
        gpu.Update(snapshot.Gpu, Aux(snapshot.Gpu), accent);
        down.Update(snapshot.Download); up.Update(snapshot.Upload);
        cpu.View.Visibility = item.SystemMonitor.ShowCpu ? Visibility.Visible : Visibility.Collapsed;
        ram.View.Visibility = item.SystemMonitor.ShowMemory ? Visibility.Visible : Visibility.Collapsed;
        gpu.View.Visibility = item.SystemMonitor.ShowGpu ? Visibility.Visible : Visibility.Collapsed;
        down.View.Visibility = up.View.Visibility = item.SystemMonitor.ShowNetwork ? Visibility.Visible : Visibility.Collapsed;
        divider.Visibility = SystemMonitorLayoutMath.DividerVisible(item.SystemMonitor.ShowCpu || item.SystemMonitor.ShowMemory || item.SystemMonitor.ShowGpu, item.SystemMonitor.ShowNetwork) ? Visibility.Visible : Visibility.Collapsed;
        divider.Background = new SolidColorBrush(WpfColor.FromArgb(32, 255, 255, 255));
        Layout();
    }

    // Do not Measure with infinite height or assign item.Height here: manual sizes stay manual.
    private void Layout()
    {
        var fallbackWidth = Math.Max(96, item.Width - root.Padding.Left - root.Padding.Right);
        var fallbackHeight = Math.Max(96, item.Height - root.Padding.Top - root.Padding.Bottom);
        var width = Math.Max(96, root.ActualWidth > 1 ? root.ActualWidth - root.Padding.Left - root.Padding.Right : fallbackWidth);
        var height = Math.Max(96, root.ActualHeight > 1 ? root.ActualHeight - root.Padding.Top - root.Padding.Bottom : fallbackHeight);
        var metrics = new[] { cpu, ram, gpu }.Where(row => row.View.Visibility == Visibility.Visible).ToArray();
        var network = down.View.Visibility == Visibility.Visible;
        var values = SystemMonitorLayoutMath.Values(width, height, metrics.Length + (network ? 2 : 0), divider.Visibility == Visibility.Visible);
        header.Height = values.Header; header.Margin = new Thickness(0);
        headerText.FontSize = Math.Clamp(values.Row * .24, 8, 10); headerText.Margin = new Thickness(0, Math.Max(0, values.Row * .04), 0, 0);
        foreach (var row in metrics) row.Layout(width, values.Row, values.Dense);
        divider.Margin = new Thickness(0, Math.Max(1, values.Row * .12), 0, Math.Max(1, values.Row * .12));
        if (network) { down.Layout(width, values.Row, values.Dense); up.Layout(width, values.Row, values.Dense); }
    }

    private static string Aux(MetricValue value) { var split = value.Text.IndexOf("  ", StringComparison.Ordinal); return value.Availability == MetricAvailability.Available ? (split >= 0 ? value.Text[(split + 2)..] : value.Text) : value.Availability == MetricAvailability.Loading ? "…" : "--"; }

    private sealed class MetricRow
    {
        private readonly TextBlock label = new() { VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock value = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock aux = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly Border track = new() { Height = 2, CornerRadius = new CornerRadius(1), Background = new SolidColorBrush(WpfColor.FromArgb(28, 255, 255, 255)) };
        private readonly Border fill = new() { Height = 2, CornerRadius = new CornerRadius(1), HorizontalAlignment = HorizontalAlignment.Left };
        private readonly Grid bar = new() { Height = 2, VerticalAlignment = VerticalAlignment.Bottom };
        private readonly ColumnDefinition labelColumn = new(), valueColumn = new();
        private double percentage;

        internal Grid View { get; }

        internal MetricRow(string name, string _)
        {
            label.Text = name;
            View = new Grid();
            View.ColumnDefinitions.Add(labelColumn);
            View.ColumnDefinitions.Add(valueColumn);
            View.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            View.Children.Add(label);
            Grid.SetColumn(value, 1);
            View.Children.Add(value);
            Grid.SetColumn(aux, 2);
            View.Children.Add(aux);
            bar.Children.Add(track);
            bar.Children.Add(fill);
            Grid.SetColumnSpan(bar, 3);
            View.Children.Add(bar);
            bar.SizeChanged += (_, _) => Paint();
        }

        internal void Layout(double width, double height, bool dense)
        {
            View.Height = height;
            labelColumn.Width = new GridLength(dense ? 44 : 52);
            valueColumn.Width = new GridLength(dense ? 44 : 52);
            label.FontSize = Math.Clamp(height * .24, 8, 12);
            value.FontSize = Math.Clamp(height * .26, 9, 13);
            aux.FontSize = Math.Clamp(height * .22, 8, 11);
            var gap = Math.Clamp(height * .18, 5, 10);
            label.Margin = value.Margin = aux.Margin = new Thickness(0, 0, 0, gap);
            bar.Margin = new Thickness(0, 0, 0, Math.Clamp(height * .12, 3, 7));
            Paint();
        }

        internal void Update(MetricValue metric, string extra, WpfColor accent)
        {
            label.Foreground = aux.Foreground = new SolidColorBrush(WpfColor.FromRgb(196, 201, 211));
            value.Foreground = new SolidColorBrush(WpfColor.FromRgb(247, 248, 250));
            value.Text = metric.Availability == MetricAvailability.Available ? $"{metric.Value:0}%" : metric.Availability == MetricAvailability.Loading ? "…" : "--";
            aux.Text = extra;
            aux.ToolTip = extra;
            percentage = metric.Availability == MetricAvailability.Available ? SystemMetricRingMath.Clamp(metric.Value ?? 0) : 0;
            fill.Background = new SolidColorBrush(accent);
            Paint();
        }

        private void Paint() => fill.Width = Math.Max(0, bar.ActualWidth * percentage / 100d);
        internal void Stop() { }
        internal void Dispose() { }
    }

    private sealed class NetworkRow
    {
        private readonly TextBlock label = new() { VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock text = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, TextTrimming = TextTrimming.CharacterEllipsis };
        internal Grid View { get; }

        internal NetworkRow(string name, bool _)
        {
            label.Text = name;
            View = new Grid();
            View.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            View.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            View.Children.Add(label);
            Grid.SetColumn(text, 1);
            View.Children.Add(text);
        }

        internal void Layout(double width, double height, bool dense)
        {
            View.Height = height;
            label.FontSize = Math.Clamp(height * .23, 8, 11);
            text.FontSize = Math.Clamp(height * .25, 8, 12);
        }

        internal void Update(MetricValue metric)
        {
            label.Foreground = new SolidColorBrush(WpfColor.FromRgb(143, 149, 161));
            text.Foreground = new SolidColorBrush(WpfColor.FromRgb(247, 248, 250));
            text.Text = metric.Text;
        }

        internal void Dispose() { }
    }
}

internal static class SystemMetricRingMath { internal static double Clamp(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 100) : 0; internal static double CubicEaseOut(double from, double to, double progress) { var t = Math.Clamp(progress, 0, 1); return from + (to - from) * (1 - Math.Pow(1 - t, 3)); } }
internal static class ProcessorClock { [StructLayout(LayoutKind.Sequential)] private struct Info { public uint N, Max, Current, Limit, MaxIdle, CurrentIdle; } [DllImport("powrprof.dll")] private static extern uint CallNtPowerInformation(int level, IntPtr input, int inputLength, [Out] Info[] output, int outputLength); internal static string Read() { try { var data = new Info[Math.Max(1, Environment.ProcessorCount)]; return CallNtPowerInformation(11, IntPtr.Zero, 0, data, Marshal.SizeOf<Info>() * data.Length) == 0 ? SystemMetricMath.FormatProcessorClock(data.Select(x => x.Current)) : "-- GHz"; } catch { return "-- GHz"; } } }
