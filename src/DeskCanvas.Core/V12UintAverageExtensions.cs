namespace DeskCanvas.Core;

internal static class V12UintAverageExtensions
{
    internal static double Average(this uint[] values)
    {
        ArgumentOutOfRangeException.ThrowIfZero(values.Length);
        double total = 0;
        foreach (var value in values) total += value;
        return total / values.Length;
    }
}
