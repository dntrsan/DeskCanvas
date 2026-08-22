using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

internal static class V30CardShadowGuaranteeFinal
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(typeof(Border), FrameworkElement.LoadedEvent, new RoutedEventHandler(StripBuiltInCardShadow));
        EventManager.RegisterClassHandler(typeof(MediaWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(MediaLoaded));
    }

    private static void StripBuiltInCardShadow(object sender, RoutedEventArgs _)
    {
        // WidgetTheme cards contain a WPF Panel; image/GIF content is an Image.
        if (sender is Border { Child: System.Windows.Controls.Panel, Effect: DropShadowEffect } card) card.Effect = null;
    }

    private static void MediaLoaded(object sender, RoutedEventArgs _)
    {
        if (sender is MediaWindow window) window.Dispatcher.BeginInvoke(window.RemoveV30BuiltInCardShadowFinal, DispatcherPriority.Loaded);
    }
}

public partial class MediaWindow
{
    internal void RemoveV30BuiltInCardShadowFinal()
    {
        if (item.ContentKind is not (CanvasContentKinds.Clock or CanvasContentKinds.NowPlaying or CanvasContentKinds.SystemMonitor)) return;
        if (content.View is Border card) card.Effect = null;
    }
}
