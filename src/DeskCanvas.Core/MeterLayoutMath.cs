namespace DeskCanvas.Core;

/// <summary>
/// Vertical leftover for the system-monitor meter stack. Extra padding is
/// spread across visible rows instead of leaving a gap under a top-aligned
/// StackPanel. Star rows are avoided because collapsed meters would still
/// consume height.
/// </summary>
public static class MeterLayoutMath
{
    public const double MaxPadding = 14;

    public static double NaturalHeight(double scale, int meterRows, bool network)
    {
        var safe = double.IsFinite(scale) && scale > 0 ? scale : 0;
        var rows = Math.Max(0, meterRows);
        var heading = 18 * safe;
        var row = 25 * safe;
        var net = network ? 34 * safe : 0;
        return heading + rows * row + net;
    }

    public static double RowPadding(double availableHeight, double naturalHeight, int visibleRows)
    {
        if (visibleRows <= 0) return 0;
        if (!double.IsFinite(availableHeight) || !double.IsFinite(naturalHeight)) return 0;
        var leftover = availableHeight - naturalHeight;
        if (leftover <= 0) return 0;
        return Math.Min(MaxPadding, leftover / (visibleRows * 2));
    }
}
