using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

// ClockItemContent refreshes its palette every second.  The guard observes the
// two palette-owned DPs synchronously, so a backgroundless clock cannot expose
// a themed card for even one rendered frame between timer ticks.
internal static class V30TextClockTransparencyGuard
{
    [ModuleInitializer]
    internal static void Initialize() => EventManager.RegisterClassHandler(typeof(MediaWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(Loaded));

    private static void Loaded(object sender, RoutedEventArgs _)
    {
        if (sender is MediaWindow window) window.BindV30TextClockTransparencyGuard();
    }
}

public partial class MediaWindow
{
    private Border? v30TransparencyCard;
    private bool v30ApplyingTransparency;

    internal void BindV30TextClockTransparencyGuard()
    {
        if (v30TransparencyCard is not null || item.ContentKind != CanvasContentKinds.Clock || !item.Clock.Backgroundless || content.View is not Border card) return;
        v30TransparencyCard = card;
        DependencyPropertyDescriptor.FromProperty(Border.BackgroundProperty, typeof(Border)).AddValueChanged(card, V30TextClockCardPaletteChanged);
        DependencyPropertyDescriptor.FromProperty(Border.BorderBrushProperty, typeof(Border)).AddValueChanged(card, V30TextClockCardPaletteChanged);
        ClearV30TextClockCard();
        Closed += (_, _) => UnbindV30TextClockTransparencyGuard();
    }

    private void V30TextClockCardPaletteChanged(object? sender, EventArgs _) => ClearV30TextClockCard();

    private void ClearV30TextClockCard()
    {
        if (v30ApplyingTransparency || v30TransparencyCard is not { } card) return;
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
        finally { v30ApplyingTransparency = false; }
    }

    private void UnbindV30TextClockTransparencyGuard()
    {
        if (v30TransparencyCard is not { } card) return;
        DependencyPropertyDescriptor.FromProperty(Border.BackgroundProperty, typeof(Border)).RemoveValueChanged(card, V30TextClockCardPaletteChanged);
        DependencyPropertyDescriptor.FromProperty(Border.BorderBrushProperty, typeof(Border)).RemoveValueChanged(card, V30TextClockCardPaletteChanged);
        v30TransparencyCard = null;
    }
}
