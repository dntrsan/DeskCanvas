using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

internal static class ClockCompactLayoutMath
{
    internal const double MinimumSize = 32;
    internal const double ComfortableWidth = 132;
    internal const double ComfortableHeight = 96;
    internal static double Scale(double width, double height)
        => !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0 ? 1 : Math.Clamp(Math.Min(width / ComfortableWidth, height / ComfortableHeight), MinimumSize / ComfortableWidth, 1);
    internal static (double Width, double Height) Clamp(double width, double height) => (Math.Max(MinimumSize, width), Math.Max(MinimumSize, height));
}

public partial class MediaWindow
{
    static MediaWindow()
    {
        EventManager.RegisterClassHandler(typeof(MediaWindow), SizeChangedEvent, new SizeChangedEventHandler(static (sender, _) => ((MediaWindow)sender).ApplyClockCompactVisuals()));
        EventManager.RegisterClassHandler(typeof(MediaWindow), PreviewMouseMoveEvent, new MouseEventHandler(static (sender, args) => ((MediaWindow)sender).ApplyClockCompactResize(args)), true);
    }

    private void ApplyClockCompactVisuals()
    {
        if (item.ContentKind != CanvasContentKinds.Clock || content.View is not System.Windows.Controls.Border card) return;
        var scale = ClockCompactLayoutMath.Scale(item.Width, item.Height);
        ItemContent.RenderTransformOrigin = new Point(.5, .5);
        ItemContent.LayoutTransform = scale >= .999 ? Transform.Identity : new ScaleTransform(scale, scale);
        var visualScale = Math.Clamp(Math.Min(item.Width, item.Height) / 96d, MinimumCardScale, 1d);
        card.Padding = new Thickness(Math.Max(2, 14 * visualScale));
        card.CornerRadius = new CornerRadius(Math.Max(7, 25 * visualScale));
        card.Effect = new DropShadowEffect { Color = Colors.Black, ShadowDepth = Math.Max(1, 4 * visualScale), BlurRadius = Math.Max(4, 20 * visualScale), Opacity = .3 * visualScale };
    }

    private const double MinimumCardScale = 1d / 3d;
    private void ApplyClockCompactResize(MouseEventArgs args)
    {
        if (item.ContentKind != CanvasContentKinds.Clock || dragOperation != DragOperation.Resize || args.LeftButton != MouseButtonState.Pressed) return;
        var current = PointToScreen(args.GetPosition(this));
        var dpi = VisualTreeHelper.GetDpi(this);
        var dx = (current.X - startScreen.X) / dpi.DpiScaleX;
        var dy = (current.Y - startScreen.Y) / dpi.DpiScaleY;
        var radians = startRotation * Math.PI / 180;
        var localX = dx * Math.Cos(radians) + dy * Math.Sin(radians);
        var localY = -dx * Math.Sin(radians) + dy * Math.Cos(radians);
        var scale = Math.Max(ClockCompactLayoutMath.MinimumSize / Math.Min(startWidth, startHeight), Math.Max((startWidth + localX) / startWidth, (startHeight + localY) / startHeight));
        var (width, height) = ClockCompactLayoutMath.Clamp(startWidth * scale, startHeight * scale);
        item.Width = width;
        item.Height = height;
        var shift = MediaWindowDpiMath.ResizeCenterShift(width - startWidth, height - startHeight, radians, dpi.DpiScaleX, dpi.DpiScaleY);
        item.CenterX = startCenterX + shift.X;
        item.CenterY = startCenterY + shift.Y;
        changed(item, false);
        // This class handler is the only resize path for a compact Clock, so the 48 DIP general handler cannot overwrite it.
        args.Handled = true;
    }
}
