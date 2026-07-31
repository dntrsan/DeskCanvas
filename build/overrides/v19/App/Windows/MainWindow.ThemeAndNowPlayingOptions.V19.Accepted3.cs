using System.Windows;
using System.Windows.Controls;
using WpfCheckBox=System.Windows.Controls.CheckBox;
using WpfComboBox=System.Windows.Controls.ComboBox;
using System.Windows.Threading;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

public partial class MainWindow
{
    private WpfComboBox? widgetThemeCombo;
    private TextBlock? widgetThemeLabel;
    private TextBlock? nowPlayingOptionsLabel;
    private WpfCheckBox? spectrumVisibleCheck;
    private WpfComboBox? progressStyleCombo;
    private bool updatingWidgetTheme;
    private bool updatingNowPlayingOptions;

    static MainWindow()
    {
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnAnyMainWindowLoaded));
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
            Text = "ウィジェットテーマ", Margin = new Thickness(0, 20, 0, 7),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(168, 169, 183))
        };
        widgetThemeCombo = new WpfComboBox { ItemsSource = Enum.GetValues<WidgetThemeKind>(), SelectedItem = WidgetThemeKind.Auto };
        widgetThemeCombo.SelectionChanged += WidgetThemeCombo_SelectionChanged;
        nowPlayingOptionsLabel = new TextBlock
        {
            Text = "再生中の設定", Margin = new Thickness(0, 18, 0, 7),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(168, 169, 183))
        };
        spectrumVisibleCheck = new WpfCheckBox { Content = "オーディオスペクトラムを表示", Margin = new Thickness(0, 2, 0, 6) };
        spectrumVisibleCheck.Checked += NowPlayingOptionChanged;
        spectrumVisibleCheck.Unchecked += NowPlayingOptionChanged;
        progressStyleCombo = new WpfComboBox { ItemsSource = ProgressChoices, Margin = new Thickness(0, 2, 0, 0) };
        progressStyleCombo.SelectionChanged += NowPlayingOptionChanged;

        var decorationIndex = EditorPanel.Children.IndexOf(DecorationCombo);
        var insertIndex = decorationIndex >= 0 ? decorationIndex + 1 : EditorPanel.Children.Count;
        EditorPanel.Children.Insert(insertIndex, widgetThemeLabel);
        EditorPanel.Children.Insert(insertIndex + 1, widgetThemeCombo);
        EditorPanel.Children.Insert(insertIndex + 2, nowPlayingOptionsLabel);
        EditorPanel.Children.Insert(insertIndex + 3, spectrumVisibleCheck);
        EditorPanel.Children.Insert(insertIndex + 4, new TextBlock { Text = "再生バー", Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(168, 169, 183)), Margin = new Thickness(0, 3, 0, 4) });
        EditorPanel.Children.Insert(insertIndex + 5, progressStyleCombo);
        ItemsList.SelectionChanged += (_, _) => Dispatcher.BeginInvoke(UpdateThemeEditor, DispatcherPriority.DataBind);
        UpdateThemeEditor();
    }

    private void UpdateThemeEditor()
    {
        if (widgetThemeCombo is null || widgetThemeLabel is null || nowPlayingOptionsLabel is null || spectrumVisibleCheck is null || progressStyleCombo is null) return;
        var item = SelectedItem;
        var builtIn = item?.ContentKind is CanvasContentKinds.Clock or CanvasContentKinds.NowPlaying or CanvasContentKinds.SystemMonitor;
        var isNowPlaying = item?.ContentKind == CanvasContentKinds.NowPlaying;
        widgetThemeLabel.Visibility = builtIn ? Visibility.Visible : Visibility.Collapsed;
        widgetThemeCombo.Visibility = builtIn ? Visibility.Visible : Visibility.Collapsed;
        nowPlayingOptionsLabel.Visibility = isNowPlaying ? Visibility.Visible : Visibility.Collapsed;
        spectrumVisibleCheck.Visibility = isNowPlaying ? Visibility.Visible : Visibility.Collapsed;
        progressStyleCombo.Visibility = isNowPlaying ? Visibility.Visible : Visibility.Collapsed;
        var progressLabel = EditorPanel.Children.OfType<TextBlock>().FirstOrDefault(control => control.Text == "再生バー");
        if (progressLabel is not null) progressLabel.Visibility = isNowPlaying ? Visibility.Visible : Visibility.Collapsed;
        updatingWidgetTheme = true;
        widgetThemeCombo.SelectedItem = builtIn && item is not null && Enum.IsDefined(item.Theme) ? item.Theme : WidgetThemeKind.Auto;
        updatingWidgetTheme = false;
        updatingNowPlayingOptions = true;
        spectrumVisibleCheck.IsChecked = isNowPlaying && item is not null ? item.NowPlaying.ShowSpectrum : true;
        progressStyleCombo.SelectedItem = Choice(isNowPlaying && item is not null && Enum.IsDefined(item.NowPlaying.ProgressStyle) ? item.NowPlaying.ProgressStyle : NowPlayingProgressStyle.Simple);
        progressStyleCombo.IsEnabled = isNowPlaying && item?.NowPlaying.ShowTimeline == true;
        updatingNowPlayingOptions = false;
    }

    private void WidgetThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (updatingWidgetTheme || SelectedItem is not { } item || widgetThemeCombo?.SelectedItem is not WidgetThemeKind theme) return;
        controller.SetTheme(item, theme);
        Dispatcher.BeginInvoke(UpdateThemeEditor, DispatcherPriority.DataBind);
    }

    private void NowPlayingOptionChanged(object sender, RoutedEventArgs e)
    {
        if (updatingNowPlayingOptions || SelectedItem is not { ContentKind: CanvasContentKinds.NowPlaying } item || spectrumVisibleCheck is null || progressStyleCombo is null) return;
        item.NowPlaying.ShowSpectrum = spectrumVisibleCheck.IsChecked == true;
        item.NowPlaying.ProgressStyle = progressStyleCombo.SelectedItem is ProgressChoice style ? style.Value : NowPlayingProgressStyle.Simple;
        controller.SetTheme(item, item.Theme);
        Dispatcher.BeginInvoke(UpdateThemeEditor, DispatcherPriority.DataBind);
    }
}
