using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace DeskCanvas.App.Windows;

internal static class V30EditorScrollHosts
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(MainLoaded));
        EventManager.RegisterClassHandler(typeof(BuiltInContentPicker), FrameworkElement.LoadedEvent, new RoutedEventHandler(PickerLoaded));
    }

    private static void MainLoaded(object sender, RoutedEventArgs _)
    {
        if (sender is MainWindow window) EnsureScroll(window.EditorPanel);
    }

    private static void PickerLoaded(object sender, RoutedEventArgs _)
    {
        if (sender is not BuiltInContentPicker picker) return;
        var options = picker.GetType().GetField("options", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(picker) as StackPanel;
        if (options is not null) EnsureScroll(options);
    }

    private static void EnsureScroll(StackPanel panel)
    {
        if (panel.Parent is ScrollViewer || panel.Parent is not Panel parent) return;
        var index = parent.Children.IndexOf(panel);
        if (index < 0) return;
        var column = Grid.GetColumn(panel);
        var row = Grid.GetRow(panel);
        parent.Children.RemoveAt(index);
        var scroll = new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 5, 0)
        };
        Grid.SetColumn(scroll, column);
        Grid.SetRow(scroll, row);
        parent.Children.Insert(index, scroll);
    }
}
