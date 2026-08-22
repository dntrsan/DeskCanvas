namespace DeskCanvas.App.Windows;

// Kept as a small pure compatibility surface for the existing v21 resize tests.
// The active resize behavior is implemented by MediaWindow.V30TextClockResize.
internal static class ClockCompactLayoutMath
{
    internal const double MinimumSize = 32;
    internal const double ComfortableWidth = 132;
    internal const double ComfortableHeight = 96;
    internal static double Scale(double width, double height) =>
        !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0
            ? 1 : Math.Clamp(Math.Min(width / ComfortableWidth, height / ComfortableHeight), MinimumSize / ComfortableWidth, 1);
    internal static (double Width, double Height) Clamp(double width, double height) => (Math.Max(MinimumSize, width), Math.Max(MinimumSize, height));
}
