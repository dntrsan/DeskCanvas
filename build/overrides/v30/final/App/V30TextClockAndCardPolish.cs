using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

// This is deliberately applied after the ordinary widget tree has been built.
// Image/GIF content never uses the 25 DIP card signature, so their rendering is
// left alone while all three built-in cards lose the desktop-style shadow.
internal static class V30TextClockAndCardPolish
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(typeof(Border), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnBorderLoaded));
        EventManager.RegisterClassHandler(typeof(MediaWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnMediaLoaded));
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnMainLoaded));
        EventManager.RegisterClassHandler(typeof(BuiltInContentPicker), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnPickerLoaded));
    }

    private static void OnBorderLoaded(object sender, RoutedEventArgs _)
    {
        if (sender is Border { CornerRadius.TopLeft: 25, Padding.Left: 14 } card && card.Effect is DropShadowEffect)
            card.Effect = null;
    }

    private static void OnMediaLoaded(object sender, RoutedEventArgs _)
    {
        if (sender is MediaWindow window)
        {
            window.ApplyV30TextClock();
            window.SizeChanged += (_, _) => window.ApplyV30TextClock();
        }
    }

    private static void OnMainLoaded(object sender, RoutedEventArgs _)
    {
        if (sender is MainWindow window)
            window.AddV30ClockEditor();
    }

    private static void OnPickerLoaded(object sender, RoutedEventArgs _)
    {
        if (sender is BuiltInContentPicker picker)
            picker.EnableV30ClockOptions();
    }
}

internal static class V30ClockTextStyle
{
    internal static FontFamily ResolveFamily(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return SystemFonts.MessageFontFamily;
        try
        {
            var requested = new FontFamily(name);
            return Fonts.SystemFontFamilies.Any(font => string.Equals(font.Source, requested.Source, StringComparison.OrdinalIgnoreCase))
                ? requested
                : SystemFonts.MessageFontFamily;
        }
        catch { return SystemFonts.MessageFontFamily; }
    }

    internal static Brush ResolveColor(string? value, Brush fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        try
        {
            var converted = ColorConverter.ConvertFromString(value);
            return converted is Color color ? new SolidColorBrush(color) : fallback;
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
    private TextBlock? v30TextClock;
    private DispatcherTimer? v30TextClockTimer;

    internal void ApplyV30TextClock()
    {
        if (item.ContentKind != CanvasContentKinds.Clock || !item.Clock.Backgroundless || content.View is not Border card)
            return;

        if (v30TextClock is null)
        {
            v30TextClock = new TextBlock
            {
                FontSize = 96,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap
            };
            card.Child = new Viewbox
            {
                Stretch = Stretch.Fill,
                StretchDirection = StretchDirection.Both,
                Child = v30TextClock
            };
            v30TextClockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            v30TextClockTimer.Tick += (_, _) => UpdateV30TextClock();
            v30TextClockTimer.Start();
            Closed += (_, _) => v30TextClockTimer?.Stop();
        }

        card.Effect = null;
        card.Background = Brushes.Transparent;
        card.BorderBrush = Brushes.Transparent;
        card.BorderThickness = new Thickness(0);
        card.Padding = new Thickness(0);
        card.CornerRadius = new CornerRadius(0);
        UpdateV30TextClock();
    }

    private void UpdateV30TextClock()
    {
        if (v30TextClock is null) return;
        var options = item.Clock;
        var now = DateTime.Now;
        v30TextClock.Text = now.ToString(options.Use24Hour
            ? (options.ShowSeconds ? "HH:mm:ss" : "HH:mm")
            : (options.ShowSeconds ? "h:mm:ss tt" : "h:mm tt"));
        var palette = WidgetTheme.Resolve(item);
        v30TextClock.FontFamily = V30ClockTextStyle.ResolveFamily(options.FontFamily);
        v30TextClock.FontWeight = V30ClockTextStyle.ResolveWeight(options.FontWeight);
        v30TextClock.Foreground = V30ClockTextStyle.ResolveColor(options.TextColor, new SolidColorBrush(palette.Foreground));
    }
}

public partial class MainWindow
{
    private CheckBox? v30Backgroundless;
    private TextBox? v30Font;
    private TextBox? v30Color;
    private ComboBox? v30Weight;
    private TextBlock? v30ClockHeading;
    private bool v30UpdatingClockEditor;

    internal void AddV30ClockEditor()
    {
        if (v30Backgroundless is not null) return;
        v30ClockHeading = new TextBlock { Text = "文字だけ時計", Margin = new Thickness(0, 12, 0, 3), FontWeight = FontWeights.SemiBold };
        v30Backgroundless = new CheckBox { Content = "背景なし・文字だけ", Margin = new Thickness(0, 2, 0, 3) };
        v30Font = new TextBox { ToolTip = "フォント名", Margin = new Thickness(0, 2, 0, 2) };
        v30Color = new TextBox { ToolTip = "文字色 (#RRGGBB または色名)", Margin = new Thickness(0, 2, 0, 2) };
        v30Weight = new ComboBox { ItemsSource = Enum.GetValues<ClockFontWeight>(), Margin = new Thickness(0, 2, 0, 4) };
        v30Backgroundless.Checked += (_, _) => SaveV30ClockEditor();
        v30Backgroundless.Unchecked += (_, _) => SaveV30ClockEditor();
        v30Font.LostFocus += (_, _) => SaveV30ClockEditor();
        v30Color.LostFocus += (_, _) => SaveV30ClockEditor();
        v30Weight.SelectionChanged += (_, _) => SaveV30ClockEditor();
        EditorPanel.Children.Add(v30ClockHeading);
        EditorPanel.Children.Add(v30Backgroundless);
        EditorPanel.Children.Add(new TextBlock { Text = "フォント" });
        EditorPanel.Children.Add(v30Font);
        EditorPanel.Children.Add(new TextBlock { Text = "文字色" });
        EditorPanel.Children.Add(v30Color);
        EditorPanel.Children.Add(new TextBlock { Text = "太さ" });
        EditorPanel.Children.Add(v30Weight);
        ItemsList.SelectionChanged += (_, _) => RefreshV30ClockEditor();
        RefreshV30ClockEditor();
    }

    private void RefreshV30ClockEditor()
    {
        if (v30Backgroundless is null) return;
        var isClock = SelectedItem?.ContentKind == CanvasContentKinds.Clock;
        foreach (var element in new FrameworkElement[] { v30ClockHeading!, v30Backgroundless, v30Font!, v30Color!, v30Weight! })
            element.Visibility = isClock ? Visibility.Visible : Visibility.Collapsed;
        if (!isClock) return;
        v30UpdatingClockEditor = true;
        var options = SelectedItem!.Clock;
        v30Backgroundless.IsChecked = options.Backgroundless;
        v30Font!.Text = options.FontFamily;
        v30Color!.Text = options.TextColor;
        v30Weight!.SelectedItem = options.FontWeight;
        v30UpdatingClockEditor = false;
    }

    private void SaveV30ClockEditor()
    {
        if (v30UpdatingClockEditor || SelectedItem is not { ContentKind: CanvasContentKinds.Clock } item || v30Backgroundless is null) return;
        item.Clock.Backgroundless = v30Backgroundless.IsChecked == true;
        item.Clock.FontFamily = v30Font?.Text ?? "";
        item.Clock.TextColor = v30Color?.Text ?? "";
        item.Clock.FontWeight = v30Weight?.SelectedItem is ClockFontWeight weight ? weight : ClockFontWeight.Regular;
        controller.SetTheme(item, item.Theme); // replace the runtime widget and persist in one operation
    }
}

internal partial class BuiltInContentPicker
{
    private bool v30PickerHooks;

    internal void EnableV30ClockOptions()
    {
        if (!v30PickerHooks)
        {
            v30PickerHooks = true;
            kind.SelectionChanged += (_, _) => Dispatcher.BeginInvoke(EnsureV30PickerClockOptions, DispatcherPriority.Loaded);
        }
        EnsureV30PickerClockOptions();
    }

    private void EnsureV30PickerClockOptions()
    {
        if (SelectedKind != CanvasContentKinds.Clock || options.Children.OfType<FrameworkElement>().Any(x => Equals(x.Tag, "v30-clock-options"))) return;
        var heading = new TextBlock { Text = "文字だけ時計", Margin = new Thickness(0, 12, 0, 2), FontWeight = FontWeights.SemiBold, Tag = "v30-clock-options" };
        var backgroundless = new CheckBox { Content = "背景なし・文字だけ", IsChecked = previewItem.Clock.Backgroundless, Tag = "v30-clock-options" };
        var font = new TextBox { ToolTip = "フォント名", Text = previewItem.Clock.FontFamily, Tag = "v30-clock-options" };
        var color = new TextBox { ToolTip = "文字色 (#RRGGBB または色名)", Text = previewItem.Clock.TextColor, Tag = "v30-clock-options" };
        var weight = new ComboBox { ItemsSource = Enum.GetValues<ClockFontWeight>(), SelectedItem = previewItem.Clock.FontWeight, Tag = "v30-clock-options" };
        void ApplyAndRebuild()
        {
            previewItem.Clock.Backgroundless = backgroundless.IsChecked == true;
            previewItem.Clock.FontFamily = font.Text ?? "";
            previewItem.Clock.TextColor = color.Text ?? "";
            previewItem.Clock.FontWeight = weight.SelectedItem is ClockFontWeight selected ? selected : ClockFontWeight.Regular;
            GetType().GetMethod("RebuildPreview", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(this, null);
            Dispatcher.BeginInvoke(EnsureV30PickerClockOptions, DispatcherPriority.Loaded);
        }
        backgroundless.Checked += (_, _) => ApplyAndRebuild();
        backgroundless.Unchecked += (_, _) => ApplyAndRebuild();
        font.LostFocus += (_, _) => ApplyAndRebuild();
        color.LostFocus += (_, _) => ApplyAndRebuild();
        weight.SelectionChanged += (_, _) => ApplyAndRebuild();
        options.Children.Add(heading);
        options.Children.Add(backgroundless);
        options.Children.Add(new TextBlock { Text = "フォント", Tag = "v30-clock-options" });
        options.Children.Add(font);
        options.Children.Add(new TextBlock { Text = "文字色", Tag = "v30-clock-options" });
        options.Children.Add(color);
        options.Children.Add(new TextBlock { Text = "太さ", Tag = "v30-clock-options" });
        options.Children.Add(weight);
    }
}
