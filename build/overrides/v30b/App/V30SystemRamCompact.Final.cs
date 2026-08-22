using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskCanvas.App.Windows;

internal static class V30SystemRamCompact
{
    private sealed class State { internal bool Pending; }
    private static readonly ConditionalWeakTable<TextBlock, State> Hooks = new();
    private static readonly Regex FullMemory = new(
        @"^\s*(?<used>[0-9.]+)\s*GB\s*/\s*(?<total>[0-9.]+)\s*GB\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [ModuleInitializer]
    internal static void Register() => EventManager.RegisterClassHandler(
        typeof(Border), FrameworkElement.LoadedEvent, new RoutedEventHandler(Loaded));

    private static void Loaded(object sender, RoutedEventArgs _)
    {
        if (sender is not Border root ||
            !Descendants(root).OfType<TextBlock>().Any(text => text.Text == "SYSTEM STATUS"))
            return;

        root.Dispatcher.BeginInvoke(() => Hook(root), DispatcherPriority.Loaded);
    }

    private static void Hook(Border root)
    {
        var row = Descendants(root).OfType<Grid>().FirstOrDefault(grid =>
            grid.Children.OfType<TextBlock>().Any(text => Grid.GetColumn(text) == 1 && text.Text == "RAM"));
        var auxiliary = row?.Children.OfType<TextBlock>().FirstOrDefault(text => Grid.GetColumn(text) == 3);
        if (auxiliary is null || Hooks.TryGetValue(auxiliary, out _))
            return;

        var state = new State();
        Hooks.Add(auxiliary, state);
        EventHandler? handler = null;
        handler = (_, _) => Schedule(auxiliary, state);
        TextDescriptor().AddValueChanged(auxiliary, handler);
        root.Unloaded += (_, _) => TextDescriptor().RemoveValueChanged(auxiliary, handler);
        Schedule(auxiliary, state);
    }

    private static void Schedule(TextBlock auxiliary, State state)
    {
        var match = FullMemory.Match(auxiliary.Text);
        if (!match.Success || state.Pending)
            return;

        state.Pending = true;
        auxiliary.Dispatcher.BeginInvoke(() =>
        {
            state.Pending = false;
            var current = FullMemory.Match(auxiliary.Text);
            if (current.Success && (auxiliary.ActualWidth < 90 || auxiliary.IsTextTrimmed()))
                auxiliary.Text = $"{current.Groups["used"].Value}/{current.Groups["total"].Value} GB";
        }, DispatcherPriority.Send);
    }

    private static bool IsTextTrimmed(this TextBlock text)
    {
        text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return text.DesiredSize.Width > text.ActualWidth + .5;
    }

    private static DependencyPropertyDescriptor TextDescriptor() =>
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));

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
