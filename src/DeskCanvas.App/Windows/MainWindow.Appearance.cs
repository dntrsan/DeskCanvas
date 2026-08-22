using System.Windows;
using System.Windows.Controls;
using DeskCanvas.Core;
using ComboBox = System.Windows.Controls.ComboBox;

namespace DeskCanvas.App.Windows;

public partial class MainWindow
{
    private bool appearanceReady;

    private void EnsureAppearanceEditor()
    {
        if (appearanceReady) return;
        appearanceReady = true;
        SurfaceCombo.ItemsSource = new SurfaceChoice[]
        {
            new(WidgetSurfaceStyle.Standard, "標準"),
            new(WidgetSurfaceStyle.MinimalGlass, "ガラス"),
            new(WidgetSurfaceStyle.LiquidGlass, "リキッドガラス")
        };
        SurfaceCombo.DisplayMemberPath = nameof(SurfaceChoice.Name);
        SurfaceCombo.SelectedValuePath = nameof(SurfaceChoice.Value);
        MonitorStyleCombo.ItemsSource = new MonitorStyleChoice[]
        {
            new(SystemMonitorStyle.Detail, "詳細"),
            new(SystemMonitorStyle.Meters, "メーター")
        };
        MonitorStyleCombo.DisplayMemberPath = nameof(MonitorStyleChoice.Name);
        MonitorStyleCombo.SelectedValuePath = nameof(MonitorStyleChoice.Value);
    }

    private void UpdateAppearanceEditor()
    {
        EnsureAppearanceEditor();
        var item = SelectedItem;
        var builtIn = item?.ContentKind is CanvasContentKinds.Clock or CanvasContentKinds.NowPlaying or CanvasContentKinds.SystemMonitor;
        var monitor = item?.ContentKind == CanvasContentKinds.SystemMonitor;
        AppearanceSectionLabel.Visibility = builtIn ? Visibility.Visible : Visibility.Collapsed;
        AppearanceGroup.Visibility = builtIn ? Visibility.Visible : Visibility.Collapsed;
        MonitorStyleRow.Visibility = monitor ? Visibility.Visible : Visibility.Collapsed;
        SurfaceRow.Style = (Style)FindResource(monitor ? "Row" : "LastRow");
        if (!builtIn || item is null) return;

        var previous = updatingEditor;
        updatingEditor = true;
        SurfaceCombo.SelectedValue = WidgetTheme.SurfaceOf(item);
        MonitorStyleCombo.SelectedValue = item.SystemMonitor.Style;
        updatingEditor = previous;
    }

    private void SurfaceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingEditor || SelectedItem is not { } item || SurfaceCombo.SelectedValue is not WidgetSurfaceStyle style) return;
        controller.SetSurfaceStyle(item, style);
        UpdateAppearanceEditor();
    }

    private void MonitorStyleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingEditor || SelectedItem is not { } item || MonitorStyleCombo.SelectedValue is not SystemMonitorStyle style) return;
        item.SystemMonitor.Style = style;
        controller.RefreshBuiltIn(item);
        UpdateAppearanceEditor();
    }

    private sealed record SurfaceChoice(WidgetSurfaceStyle Value, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record MonitorStyleChoice(SystemMonitorStyle Value, string Name)
    {
        public override string ToString() => Name;
    }
}
