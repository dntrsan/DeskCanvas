namespace DeskCanvas.App.Services;

internal static class ReadOnlySpanLinqV21
{
    internal static bool Any(this ReadOnlySpan<double> values, Func<double, bool> predicate)
    {
        for (var index = 0; index < values.Length; index++) if (predicate(values[index])) return true;
        return false;
    }
}
