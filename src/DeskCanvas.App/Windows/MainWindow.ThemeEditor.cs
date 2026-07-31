using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

public partial class MainWindow
{
    private System.Windows.Controls.ComboBox? widgetThemeCombo;
    private TextBlock? widgetThemeLabel;
    private bool updatingWidgetTheme;

    static MainWindow()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnAnyMainWindowLoaded));
    }

    private static void OnAnyMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window) window.EnsureThemeEditor();
    }

    private void EnsureThemeEditor()
    {
        if (widgetThemeCombo is not null) return;
        widgetThemeLabel = new TextBlock
        {
            Text = "ウィジェットテーマ",
            Margin = new Thickness(0, 20, 0, 7),
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(168, 169, 183))
        };
        widgetThemeCombo = new System.Windows.Controls.ComboBox
        {
            ItemsSource = Enum.GetValues<WidgetThemeKind>(),
            SelectedItem = WidgetThemeKind.Auto
        };
        widgetThemeCombo.SelectionChanged += WidgetThemeCombo_SelectionChanged;

        var decorationIndex = EditorPanel.Children.IndexOf(DecorationCombo);
        var insertIndex = decorationIndex >= 0 ? decorationIndex + 1 : EditorPanel.Children.Count;
        EditorPanel.Children.Insert(insertIndex, widgetThemeLabel);
        EditorPanel.Children.Insert(insertIndex + 1, widgetThemeCombo);
        ItemsList.SelectionChanged += (_, _) =>
            Dispatcher.BeginInvoke(UpdateThemeEditor, DispatcherPriority.DataBind);
        UpdateThemeEditor();
    }

    private void UpdateThemeEditor()
    {
        if (widgetThemeCombo is null || widgetThemeLabel is null) return;
        var item = SelectedItem;
        var builtIn = item?.ContentKind is
            CanvasContentKinds.Clock or
            CanvasContentKinds.NowPlaying or
            CanvasContentKinds.SystemMonitor;
        widgetThemeLabel.Visibility = builtIn ? Visibility.Visible : Visibility.Collapsed;
        widgetThemeCombo.Visibility = builtIn ? Visibility.Visible : Visibility.Collapsed;
        updatingWidgetTheme = true;
        widgetThemeCombo.SelectedItem = builtIn && item is not null &&
                                        Enum.IsDefined(item.Theme)
            ? item.Theme
            : WidgetThemeKind.Auto;
        updatingWidgetTheme = false;
    }

    private void WidgetThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingWidgetTheme || SelectedItem is not { } item ||
            widgetThemeCombo?.SelectedItem is not WidgetThemeKind theme)
            return;
        controller.SetTheme(item, theme);
        Dispatcher.BeginInvoke(UpdateThemeEditor, DispatcherPriority.DataBind);
    }
}
