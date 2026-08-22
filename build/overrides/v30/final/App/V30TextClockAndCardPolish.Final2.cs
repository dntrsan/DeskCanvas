using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

internal static class V30TextClockAndCardPolishFinal
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.Border), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnBorderLoaded));
        EventManager.RegisterClassHandler(typeof(MediaWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnMediaLoaded));
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnMainLoaded));
        EventManager.RegisterClassHandler(typeof(BuiltInContentPicker), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnPickerLoaded));
    }

    private static void OnBorderLoaded(object sender, RoutedEventArgs _)
    {
        if (sender is System.Windows.Controls.Border { CornerRadius.TopLeft: 25, Padding.Left: 14 } card && card.Effect is DropShadowEffect)
            card.Effect = null;
    }

    private static void OnMediaLoaded(object sender, RoutedEventArgs _)
    {
        if (sender is MediaWindow window)
        {
            window.ApplyV30TextClockFinal();
            window.SizeChanged += (_, _) => window.ApplyV30TextClockFinal();
        }
    }

    private static void OnMainLoaded(object sender, RoutedEventArgs _)
    {
        if (sender is MainWindow window) window.AddV30ClockEditorFinal();
    }

    private static void OnPickerLoaded(object sender, RoutedEventArgs _)
    {
        if (sender is BuiltInContentPicker picker) V30PickerClockOptions.Enable(picker);
    }
}

internal static class V30ClockTextStyleFinal
{
    internal static System.Windows.Media.FontFamily ResolveFamily(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return System.Windows.SystemFonts.MessageFontFamily;
        try
        {
            var requested = new System.Windows.Media.FontFamily(name);
            return System.Windows.Media.Fonts.SystemFontFamilies.Any(font => string.Equals(font.Source, requested.Source, StringComparison.OrdinalIgnoreCase))
                ? requested : System.Windows.SystemFonts.MessageFontFamily;
        }
        catch { return System.Windows.SystemFonts.MessageFontFamily; }
    }

    internal static Brush ResolveColor(string? text, Brush fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        try
        {
            var parsed = System.Windows.Media.ColorConverter.ConvertFromString(text);
            return parsed is Color color ? new SolidColorBrush(color) : fallback;
        }
        catch { return fallback; }
    }

    internal static FontWeight ResolveWeight(ClockFontWeight value) => value switch
    {
        ClockFontWeight.Light => FontWeights.Light,
        ClockFontWeight.SemiBold => FontWeights.SemiBold,
        ClockFontWeight.Bold => FontWeights.Bold,
        ClockFontWeight.Black => FontWeights.Black,
        _ => FontWeights.Regular
    };
}

public partial class MediaWindow
{
    private System.Windows.Controls.TextBlock? v30TextClockFinal;
    private DispatcherTimer? v30TextClockTimerFinal;

    internal void ApplyV30TextClockFinal()
    {
        if (item.ContentKind != CanvasContentKinds.Clock || !item.Clock.Backgroundless || content.View is not System.Windows.Controls.Border card) return;
        if (v30TextClockFinal is null)
        {
            v30TextClockFinal = new System.Windows.Controls.TextBlock
            {
                FontSize = 96,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap
            };
            card.Child = new System.Windows.Controls.Viewbox
            {
                Stretch = Stretch.Fill,
                StretchDirection = StretchDirection.Both,
                Child = v30TextClockFinal
            };
            v30TextClockTimerFinal = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            v30TextClockTimerFinal.Tick += (_, _) => UpdateV30TextClockFinal();
            v30TextClockTimerFinal.Start();
            Closed += (_, _) => v30TextClockTimerFinal?.Stop();
        }
        card.Effect = null;
        card.Background = Brushes.Transparent;
        card.BorderBrush = Brushes.Transparent;
        card.BorderThickness = new Thickness(0);
        card.Padding = new Thickness(0);
        card.CornerRadius = new CornerRadius(0);
        UpdateV30TextClockFinal();
    }

    private void UpdateV30TextClockFinal()
    {
        if (v30TextClockFinal is null) return;
        var options = item.Clock;
        var now = DateTime.Now;
        v30TextClockFinal.Text = now.ToString(options.Use24Hour
            ? (options.ShowSeconds ? "HH:mm:ss" : "HH:mm")
            : (options.ShowSeconds ? "h:mm:ss tt" : "h:mm tt"));
        var palette = WidgetTheme.Resolve(item);
        v30TextClockFinal.FontFamily = V30ClockTextStyleFinal.ResolveFamily(options.FontFamily);
        v30TextClockFinal.FontWeight = V30ClockTextStyleFinal.ResolveWeight(options.FontWeight);
        v30TextClockFinal.Foreground = V30ClockTextStyleFinal.ResolveColor(options.TextColor, new SolidColorBrush(palette.Foreground));
    }
}

public partial class MainWindow
{
    private System.Windows.Controls.CheckBox? v30BackgroundlessFinal;
    private System.Windows.Controls.TextBox? v30FontFinal;
    private System.Windows.Controls.TextBox? v30ColorFinal;
    private System.Windows.Controls.ComboBox? v30WeightFinal;
    private System.Windows.Controls.TextBlock? v30ClockHeadingFinal;
    private bool v30UpdatingClockEditorFinal;

    internal void AddV30ClockEditorFinal()
    {
        if (v30BackgroundlessFinal is not null) return;
        v30ClockHeadingFinal = new System.Windows.Controls.TextBlock { Text = "文字だけ時計", Margin = new Thickness(0, 12, 0, 3), FontWeight = FontWeights.SemiBold };
        v30BackgroundlessFinal = new System.Windows.Controls.CheckBox { Content = "背景なし・文字だけ", Margin = new Thickness(0, 2, 0, 3) };
        v30FontFinal = new System.Windows.Controls.TextBox { ToolTip = "フォント名", Margin = new Thickness(0, 2, 0, 2) };
        v30ColorFinal = new System.Windows.Controls.TextBox { ToolTip = "文字色 (#RRGGBB または色名)", Margin = new Thickness(0, 2, 0, 2) };
        v30WeightFinal = new System.Windows.Controls.ComboBox { ItemsSource = Enum.GetValues<ClockFontWeight>(), Margin = new Thickness(0, 2, 0, 4) };
        v30BackgroundlessFinal.Checked += (_, _) => SaveV30ClockEditorFinal();
        v30BackgroundlessFinal.Unchecked += (_, _) => SaveV30ClockEditorFinal();
        v30FontFinal.LostFocus += (_, _) => SaveV30ClockEditorFinal();
        v30ColorFinal.LostFocus += (_, _) => SaveV30ClockEditorFinal();
        v30WeightFinal.SelectionChanged += (_, _) => SaveV30ClockEditorFinal();
        EditorPanel.Children.Add(v30ClockHeadingFinal);
        EditorPanel.Children.Add(v30BackgroundlessFinal);
        EditorPanel.Children.Add(new System.Windows.Controls.TextBlock { Text = "フォント" });
        EditorPanel.Children.Add(v30FontFinal);
        EditorPanel.Children.Add(new System.Windows.Controls.TextBlock { Text = "文字色" });
        EditorPanel.Children.Add(v30ColorFinal);
        EditorPanel.Children.Add(new System.Windows.Controls.TextBlock { Text = "太さ" });
        EditorPanel.Children.Add(v30WeightFinal);
        ItemsList.SelectionChanged += (_, _) => RefreshV30ClockEditorFinal();
        RefreshV30ClockEditorFinal();
    }

    private void RefreshV30ClockEditorFinal()
    {
        if (v30BackgroundlessFinal is null) return;
        var isClock = SelectedItem?.ContentKind == CanvasContentKinds.Clock;
        foreach (var element in new FrameworkElement[] { v30ClockHeadingFinal!, v30BackgroundlessFinal, v30FontFinal!, v30ColorFinal!, v30WeightFinal! }) element.Visibility = isClock ? Visibility.Visible : Visibility.Collapsed;
        if (!isClock) return;
        v30UpdatingClockEditorFinal = true;
        var options = SelectedItem!.Clock;
        v30BackgroundlessFinal.IsChecked = options.Backgroundless;
        v30FontFinal!.Text = options.FontFamily;
        v30ColorFinal!.Text = options.TextColor;
        v30WeightFinal!.SelectedItem = options.FontWeight;
        v30UpdatingClockEditorFinal = false;
    }

    private void SaveV30ClockEditorFinal()
    {
        if (v30UpdatingClockEditorFinal || SelectedItem is not { ContentKind: CanvasContentKinds.Clock } item || v30BackgroundlessFinal is null) return;
        item.Clock.Backgroundless = v30BackgroundlessFinal.IsChecked == true;
        item.Clock.FontFamily = v30FontFinal?.Text ?? "";
        item.Clock.TextColor = v30ColorFinal?.Text ?? "";
        item.Clock.FontWeight = v30WeightFinal?.SelectedItem is ClockFontWeight weight ? weight : ClockFontWeight.Regular;
        controller.SetTheme(item, item.Theme);
    }
}

internal static class V30PickerClockOptions
{
    private const string Tag = "v30-clock-options-final";
    private static readonly ConditionalWeakTable<BuiltInContentPicker, object> Hooks = new();

    internal static void Enable(BuiltInContentPicker picker)
    {
        if (!Hooks.TryGetValue(picker, out _))
        {
            Hooks.Add(picker, new object());
            if (Field<System.Windows.Controls.ComboBox>(picker, "kind") is { } kind)
                kind.SelectionChanged += (_, _) => picker.Dispatcher.BeginInvoke(() => Ensure(picker), DispatcherPriority.Loaded);
        }
        Ensure(picker);
    }

    private static void Ensure(BuiltInContentPicker picker)
    {
        var preview = Field<CanvasItem>(picker, "previewItem");
        var options = Field<System.Windows.Controls.StackPanel>(picker, "options");
        if (preview?.ContentKind != CanvasContentKinds.Clock || options is null || options.Children.OfType<FrameworkElement>().Any(element => Equals(element.Tag, Tag))) return;
        var backgroundless = new System.Windows.Controls.CheckBox { Content = "背景なし・文字だけ", IsChecked = preview.Clock.Backgroundless, Tag = Tag };
        var font = new System.Windows.Controls.TextBox { ToolTip = "フォント名", Text = preview.Clock.FontFamily, Tag = Tag };
        var color = new System.Windows.Controls.TextBox { ToolTip = "文字色 (#RRGGBB または色名)", Text = preview.Clock.TextColor, Tag = Tag };
        var weight = new System.Windows.Controls.ComboBox { ItemsSource = Enum.GetValues<ClockFontWeight>(), SelectedItem = preview.Clock.FontWeight, Tag = Tag };
        void Apply()
        {
            preview.Clock.Backgroundless = backgroundless.IsChecked == true;
            preview.Clock.FontFamily = font.Text ?? "";
            preview.Clock.TextColor = color.Text ?? "";
            preview.Clock.FontWeight = weight.SelectedItem is ClockFontWeight selected ? selected : ClockFontWeight.Regular;
            picker.GetType().GetMethod("RebuildPreview", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(picker, null);
            picker.Dispatcher.BeginInvoke(() => Ensure(picker), DispatcherPriority.Loaded);
        }
        backgroundless.Checked += (_, _) => Apply();
        backgroundless.Unchecked += (_, _) => Apply();
        font.LostFocus += (_, _) => Apply();
        color.LostFocus += (_, _) => Apply();
        weight.SelectionChanged += (_, _) => Apply();
        options.Children.Add(new System.Windows.Controls.TextBlock { Text = "文字だけ時計", Margin = new Thickness(0, 12, 0, 2), FontWeight = FontWeights.SemiBold, Tag = Tag });
        options.Children.Add(backgroundless);
        options.Children.Add(new System.Windows.Controls.TextBlock { Text = "フォント", Tag = Tag });
        options.Children.Add(font);
        options.Children.Add(new System.Windows.Controls.TextBlock { Text = "文字色", Tag = Tag });
        options.Children.Add(color);
        options.Children.Add(new System.Windows.Controls.TextBlock { Text = "太さ", Tag = Tag });
        options.Children.Add(weight);
    }

    private static T? Field<T>(object source, string name) where T : class => source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(source) as T;
}
