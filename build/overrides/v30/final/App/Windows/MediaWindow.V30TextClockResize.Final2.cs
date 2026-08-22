using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

internal static class V30ClockResizeMathFinal
{
    internal const double Minimum = 32;
    internal static double UniformScale(double width, double height) =>
        !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0
            ? 1 : Math.Clamp(Math.Min(width / 132d, height / 96d), Minimum / 132d, 1);
}

public partial class MediaWindow
{
    static MediaWindow()
    {
        EventManager.RegisterClassHandler(typeof(MediaWindow), SizeChangedEvent, new SizeChangedEventHandler(static (sender, _) => ((MediaWindow)sender).ApplyV30ClockSizingFinal()));
        EventManager.RegisterClassHandler(typeof(MediaWindow), PreviewMouseMoveEvent, new MouseEventHandler(static (sender, args) => ((MediaWindow)sender).ApplyV30ClockResizeFinal(args)), true);
    }

    private void ApplyV30ClockSizingFinal()
    {
        if (item.ContentKind != CanvasContentKinds.Clock || item.Clock.Backgroundless || content.View is not System.Windows.Controls.Border card) return;
        var scale = V30ClockResizeMathFinal.UniformScale(item.Width, item.Height);
        ItemContent.RenderTransformOrigin = new Point(.5, .5);
        ItemContent.LayoutTransform = scale >= .999 ? Transform.Identity : new ScaleTransform(scale, scale);
        var visualScale = Math.Clamp(Math.Min(item.Width, item.Height) / 96d, 1d / 3d, 1d);
        card.Padding = new Thickness(Math.Max(2, 14 * visualScale));
        card.CornerRadius = new CornerRadius(Math.Max(7, 25 * visualScale));
        card.Effect = null;
    }

    private void ApplyV30ClockResizeFinal(MouseEventArgs args)
    {
        if (item.ContentKind != CanvasContentKinds.Clock || !item.Clock.Backgroundless || dragOperation != DragOperation.Resize || args.LeftButton != MouseButtonState.Pressed) return;
        var current = PointToScreen(args.GetPosition(this));
        var dpi = VisualTreeHelper.GetDpi(this);
        var dx = (current.X - startScreen.X) / dpi.DpiScaleX;
        var dy = (current.Y - startScreen.Y) / dpi.DpiScaleY;
        var radians = startRotation * Math.PI / 180d;
        var localX = dx * Math.Cos(radians) + dy * Math.Sin(radians);
        var localY = -dx * Math.Sin(radians) + dy * Math.Cos(radians);
        var width = Math.Max(V30ClockResizeMathFinal.Minimum, startWidth + localX);
        var height = Math.Max(V30ClockResizeMathFinal.Minimum, startHeight + localY);
        item.Width = width;
        item.Height = height;
        var shift = MediaWindowDpiMath.ResizeCenterShift(width - startWidth, height - startHeight, radians, dpi.DpiScaleX, dpi.DpiScaleY);
        item.CenterX = startCenterX + shift.X;
        item.CenterY = startCenterY + shift.Y;
        changed(item, false);
        args.Handled = true;
    }
}
