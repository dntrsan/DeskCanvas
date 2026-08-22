namespace DeskCanvas.Core;

public static class V12UintAverageExtensions
{
    public static double Average(this uint[] values)
    {
        ArgumentOutOfRangeException.ThrowIfZero(values.Length);
        double total = 0;
        foreach (var value in values) total += value;
        return total / values.Length;
    }
}
