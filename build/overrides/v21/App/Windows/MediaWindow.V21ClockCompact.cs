using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

internal static class ClockCompactLayoutMath
{
    internal const double MinimumSize = 32;
    internal const double ComfortableWidth = 132;
    internal const double ComfortableHeight = 96;

    internal static double Scale(double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return 1;
        return Math.Clamp(Math.Min(width / ComfortableWidth, height / ComfortableHeight), MinimumSize / ComfortableWidth, 1);
    }

    internal static (double Width, double Height) Clamp(double width, double height)
        => (Math.Max(MinimumSize, width), Math.Max(MinimumSize, height));
}

public partial class MediaWindow
{
    static MediaWindow()
    {
        EventManager.RegisterClassHandler(typeof(MediaWindow), SizeChangedEvent,
            new SizeChangedEventHandler(static (sender, _) => ((MediaWindow)sender).ApplyClockCompactScale()));
        EventManager.RegisterClassHandler(typeof(MediaWindow), PreviewMouseMoveEvent,
            new MouseEventHandler(static (sender, args) => ((MediaWindow)sender).QueueClockCompactResize(args)), true);
    }

    private void ApplyClockCompactScale()
    {
        if (item.ContentKind != CanvasContentKinds.Clock) return;
        var scale = ClockCompactLayoutMath.Scale(ActualWidth, ActualHeight);
        ItemContent.RenderTransformOrigin = new Point(.5, .5);
        ItemContent.LayoutTransform = scale >= .999 ? Transform.Identity : new ScaleTransform(scale, scale);
    }

    private void QueueClockCompactResize(MouseEventArgs args)
    {
        if (item.ContentKind != CanvasContentKinds.Clock || dragOperation != DragOperation.Resize || args.LeftButton != MouseButtonState.Pressed) return;
        var current = PointToScreen(args.GetPosition(this));
        var dx = current.X - startScreen.X;
        var dy = current.Y - startScreen.Y;
        var dpi = VisualTreeHelper.GetDpi(this);
        var localX = dx / dpi.DpiScaleX * Math.Cos(startRotation * Math.PI / 180) + dy / dpi.DpiScaleY * Math.Sin(startRotation * Math.PI / 180);
        var localY = -dx / dpi.DpiScaleX * Math.Sin(startRotation * Math.PI / 180) + dy / dpi.DpiScaleY * Math.Cos(startRotation * Math.PI / 180);
        var minimum = ClockCompactLayoutMath.MinimumSize;
        var scale = Math.Max(minimum / Math.Min(startWidth, startHeight), Math.Max((startWidth + localX) / startWidth, (startHeight + localY) / startHeight));
        var (width, height) = ClockCompactLayoutMath.Clamp(startWidth * scale, startHeight * scale);
        Dispatcher.BeginInvoke(() =>
        {
            if (dragOperation != DragOperation.Resize) return;
            item.Width = width;
            item.Height = height;
            var shift = MediaWindowDpiMath.ResizeCenterShift(width - startWidth, height - startHeight, startRotation * Math.PI / 180, dpi.DpiScaleX, dpi.DpiScaleY);
            item.CenterX = startCenterX + shift.X;
            item.CenterY = startCenterY + shift.Y;
            changed(item, false);
        }, DispatcherPriority.Input);
    }
}
