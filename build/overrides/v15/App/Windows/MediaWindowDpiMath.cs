using System.Windows;

namespace DeskCanvas.App.Windows;

internal static class MediaWindowDpiMath
{
    internal static double PhysicalSide(double dip, double scale) => Math.Ceiling(dip * scale);
    internal static Vector ResizeCenterShift(double widthDeltaDip, double heightDeltaDip, double radians, double dpiX, double dpiY)
    {
        var localX = widthDeltaDip / 2 * Math.Cos(radians) - heightDeltaDip / 2 * Math.Sin(radians);
        var localY = widthDeltaDip / 2 * Math.Sin(radians) + heightDeltaDip / 2 * Math.Cos(radians);
        return new Vector(localX * dpiX, localY * dpiY);
    }
}
