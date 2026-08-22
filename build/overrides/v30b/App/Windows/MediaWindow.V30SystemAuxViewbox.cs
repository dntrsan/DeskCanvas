using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskCanvas.App.Windows;

internal static class V30SystemAuxViewbox
{
    [ModuleInitializer]
    internal static void Register() => EventManager.RegisterClassHandler(
        typeof(MediaWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(Loaded));

    private static void Loaded(object sender, RoutedEventArgs _)
    {
        if (sender is not MediaWindow window ||
            !Descendants(window).OfType<TextBlock>().Any(text => text.Text == "SYSTEM STATUS"))
            return;

        foreach (var row in Descendants(window).OfType<Grid>().ToArray())
        {
            var label = row.Children.OfType<TextBlock>()
                .FirstOrDefault(text => Grid.GetColumn(text) == 1 && text.Text is "CPU" or "RAM" or "GPU");
            var auxiliary = row.Children.OfType<TextBlock>()
                .FirstOrDefault(text => Grid.GetColumn(text) == 3);
            if (label is null || auxiliary is null)
                continue;

            var margin = auxiliary.Margin;
            row.Children.Remove(auxiliary);
            auxiliary.Margin = new Thickness(0);
            auxiliary.TextTrimming = TextTrimming.None;
            var fit = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = margin,
                Child = auxiliary
            };
            Grid.SetColumn(fit, 3);
            row.Children.Add(fit);
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
