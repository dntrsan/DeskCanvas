namespace DeskCanvas.App.Services;

internal static class ReadOnlySpanExtensions
{
    internal static bool All(this ReadOnlySpan<double> values, Func<double, bool> predicate)
    {
        for (var index = 0; index < values.Length; index++)
        {
            if (!predicate(values[index])) return false;
        }

        return true;
    }
}
