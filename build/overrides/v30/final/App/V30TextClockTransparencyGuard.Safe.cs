using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

internal static class V30TextClockTransparencyGuard
{
    [ModuleInitializer]
    internal static void Initialize() => EventManager.RegisterClassHandler(
        typeof(MediaWindow),
        FrameworkElement.LoadedEvent,
        new RoutedEventHandler(Loaded));

    private static void Loaded(object sender, RoutedEventArgs _)
    {
        if (sender is MediaWindow window) window.BindV30TextClockTransparencyGuard();
    }
}

public partial class MediaWindow
{
    private Border? v30TransparencyCard;
    private bool v30ApplyingTransparency;
    private bool v30TransparencyClearPending;

    internal void BindV30TextClockTransparencyGuard()
    {
        if (v30TransparencyCard is not null ||
            item.ContentKind != CanvasContentKinds.Clock ||
            !item.Clock.Backgroundless ||
            content.View is not Border card)
            return;

        v30TransparencyCard = card;
        Descriptor(Border.BackgroundProperty).AddValueChanged(card, V30TextClockCardPaletteChanged);
        Descriptor(Border.BorderBrushProperty).AddValueChanged(card, V30TextClockCardPaletteChanged);
        ClearV30TextClockCard();
        Closed += (_, _) => UnbindV30TextClockTransparencyGuard();
    }

    private static DependencyPropertyDescriptor Descriptor(DependencyProperty property) =>
        DependencyPropertyDescriptor.FromProperty(property, typeof(Border));

    private void V30TextClockCardPaletteChanged(object? sender, EventArgs _)
    {
        if (v30ApplyingTransparency || v30TransparencyClearPending || v30TransparencyCard is null)
            return;

        // A DependencyProperty change callback must not mutate the same
        // Freezable-backed property while WPF is updating it.  Send priority
        // runs after the current SetValue completes and before Render.
        v30TransparencyClearPending = true;
        Dispatcher.BeginInvoke(() =>
        {
            v30TransparencyClearPending = false;
            ClearV30TextClockCard();
        }, DispatcherPriority.Send);
    }

    private void ClearV30TextClockCard()
    {
        if (v30ApplyingTransparency || v30TransparencyCard is not { } card)
            return;

        v30ApplyingTransparency = true;
        try
        {
            card.Background = Brushes.Transparent;
            card.BorderBrush = Brushes.Transparent;
            card.BorderThickness = new Thickness(0);
            card.Padding = new Thickness(0);
            card.CornerRadius = new CornerRadius(0);
            card.Effect = null;
        }
        finally
        {
            v30ApplyingTransparency = false;
        }
    }

    private void UnbindV30TextClockTransparencyGuard()
    {
        if (v30TransparencyCard is not { } card)
            return;

        Descriptor(Border.BackgroundProperty).RemoveValueChanged(card, V30TextClockCardPaletteChanged);
        Descriptor(Border.BorderBrushProperty).RemoveValueChanged(card, V30TextClockCardPaletteChanged);
        v30TransparencyCard = null;
        v30TransparencyClearPending = false;
    }
}
