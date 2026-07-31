using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DeskCanvas.App.Windows;

internal static class MainWindowVersionLabel
{
    [ModuleInitializer]
    internal static void Register()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(ApplyOnLoaded));
    }

    private static void ApplyOnLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is not MainWindow window)
            return;

        window.Title = ApplicationVersion.WindowTitle;
        var heading = FindHeading(window);
        if (heading is null || heading.Inlines.OfType<Run>().Any(run => run.Text == ApplicationVersion.Display))
            return;

        heading.Inlines.Clear();
        heading.Inlines.Add(new Run("DeskCanvas"));
        heading.Inlines.Add(new Run($"  {ApplicationVersion.Display}")
        {
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(201, 194, 255))
        });
        AutomationProperties.SetName(heading, ApplicationVersion.WindowTitle);
    }

    private static TextBlock? FindHeading(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is TextBlock { Text: "DeskCanvas", FontSize: >= 24 } heading)
                return heading;
            var nested = FindHeading(child);
            if (nested is not null)
                return nested;
        }
        return null;
    }
}
