using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DeskCanvas.App.Windows;

internal static class V30SystemAuxFit
{
    private sealed class HookState
    {
        internal bool Pending;
        internal EventHandler? Handler;
    }

    private static readonly ConditionalWeakTable<TextBlock, HookState> Hooks = new();
    private static readonly Regex MemoryValue = new(
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

        Apply(root);
        root.SizeChanged += (_, _) => Apply(root);
    }

    private static void Apply(Border root)
    {
        foreach (var row in Descendants(root).OfType<Grid>())
        {
            var label = row.Children.OfType<TextBlock>()
                .FirstOrDefault(text => Grid.GetColumn(text) == 1 && text.Text is "CPU" or "RAM" or "GPU");
            var auxiliary = row.Children.OfType<TextBlock>()
                .FirstOrDefault(text => Grid.GetColumn(text) == 3);
            if (label is null || auxiliary is null)
                continue;

            Fit(root, row, label, auxiliary);
            if (label.Text != "RAM" || Hooks.TryGetValue(auxiliary, out _))
                continue;

            var state = new HookState();
            EventHandler handler = (_, _) => ScheduleMemoryFit(root, auxiliary, state);
            state.Handler = handler;
            Hooks.Add(auxiliary, state);
            TextDescriptor().AddValueChanged(auxiliary, handler);
            root.Unloaded += (_, _) =>
            {
                if (state.Handler is { } subscribed)
                    TextDescriptor().RemoveValueChanged(auxiliary, subscribed);
                state.Handler = null;
            };
        }
    }

    private static DependencyPropertyDescriptor TextDescriptor() =>
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));

    private static void ScheduleMemoryFit(Border root, TextBlock auxiliary, HookState state)
    {
        if (state.Pending || root.ActualWidth >= 280 || !MemoryValue.IsMatch(auxiliary.Text))
            return;

        state.Pending = true;
        auxiliary.Dispatcher.BeginInvoke(() =>
        {
            state.Pending = false;
            CompactMemory(auxiliary);
        }, DispatcherPriority.Send);
    }

    private static void Fit(Border root, Grid row, TextBlock label, TextBlock auxiliary)
    {
        var width = Math.Max(96, root.ActualWidth);
        if (width < 280 && row.ColumnDefinitions.Count >= 4)
        {
            row.ColumnDefinitions[1].Width = new GridLength(Math.Clamp(width * .17, 36, 48));
            if (label.Text == "RAM")
                CompactMemory(auxiliary);
        }

        var rowBased = Math.Clamp(Math.Max(1, row.ActualHeight) * .235, 6.5, 12);
        var widthBased = Math.Clamp(width * .032, 6.5, 10);
        auxiliary.FontSize = Math.Min(rowBased, widthBased);
        auxiliary.TextTrimming = TextTrimming.CharacterEllipsis;
    }

    private static void CompactMemory(TextBlock auxiliary)
    {
        var memory = MemoryValue.Match(auxiliary.Text);
        if (memory.Success)
            auxiliary.Text = $"{memory.Groups["used"].Value}/{memory.Groups["total"].Value} GB";
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
