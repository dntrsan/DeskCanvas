using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

internal static class V30CardShadowGuarantee
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(typeof(Border), FrameworkElement.LoadedEvent, new RoutedEventHandler(StripBuiltInCardShadow));
        EventManager.RegisterClassHandler(typeof(MediaWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(MediaLoaded));
    }

    private static void StripBuiltInCardShadow(object sender, RoutedEventArgs _)
    {
        // WidgetTheme.Card always has a Grid/Panel child.  Media image/GIF content
        // is an Image and its decoration effect is applied outside this Border.
        if (sender is Border { Child: Panel, Effect: DropShadowEffect } card) card.Effect = null;
    }

    private static void MediaLoaded(object sender, RoutedEventArgs _)
    {
        if (sender is MediaWindow window)
            window.Dispatcher.BeginInvoke(window.RemoveV30BuiltInCardShadow, DispatcherPriority.Loaded);
    }
}

public partial class MediaWindow
{
    internal void RemoveV30BuiltInCardShadow()
    {
        if (item.ContentKind is not (CanvasContentKinds.Clock or CanvasContentKinds.NowPlaying or CanvasContentKinds.SystemMonitor)) return;
        if (content.View is Border card) card.Effect = null;
    }
}
