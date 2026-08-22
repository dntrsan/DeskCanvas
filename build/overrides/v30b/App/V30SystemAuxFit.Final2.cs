using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskCanvas.App.Windows;

internal static class V30SystemAuxFit
{
    [ModuleInitializer]
    internal static void Register() => EventManager.RegisterClassHandler(
        typeof(Border), FrameworkElement.LoadedEvent, new RoutedEventHandler(Loaded));

    private static void Loaded(object sender, RoutedEventArgs _)
    {
        if (sender is not Border root ||
            !Descendants(root).OfType<TextBlock>().Any(text => text.Text == "SYSTEM STATUS"))
            return;

        Apply(root);
        root.SizeChanged += (_, _) => Apply(root);
    }

    private static void Apply(Border root)
    {
        var width = Math.Max(96, root.ActualWidth);
        foreach (var row in Descendants(root).OfType<Grid>())
        {
            var label = row.Children.OfType<TextBlock>()
                .FirstOrDefault(text => Grid.GetColumn(text) == 1 && text.Text is "CPU" or "RAM" or "GPU");
            if (label is null)
                continue;

            var auxiliary = row.Children.OfType<TextBlock>()
                .FirstOrDefault(text => Grid.GetColumn(text) == 3);
            if (auxiliary is null)
                continue;

            if (width < 280 && row.ColumnDefinitions.Count >= 4)
            {
                // Preserve the ring, but return desktop-width label spacing to
                // the measured auxiliary value on narrow/tall cards.
                row.ColumnDefinitions[1].Width = new GridLength(Math.Clamp(width * .17, 36, 48));
            }

            var rowBased = Math.Clamp(Math.Max(1, row.ActualHeight) * .235, 6.5, 12);
            var widthBased = Math.Clamp(width * .032, 6.5, 10);
            auxiliary.FontSize = Math.Min(rowBased, widthBased);
            auxiliary.TextTrimming = TextTrimming.CharacterEllipsis;
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
