namespace DeskCanvas.App.Services;

internal readonly record struct NetworkGraphPoint(double X, double Y);
internal static class NetworkGraphMath
{
    internal static IReadOnlyList<NetworkGraphPoint> Points(IReadOnlyList<double> normalized, double width = 54, double height = 16, double amplitude = 14)
    {
        if (normalized.Count == 0) return [];
        return Enumerable.Range(0, normalized.Count).Select(index => new NetworkGraphPoint(
            normalized.Count == 1 ? 0 : index * width / (normalized.Count - 1),
            height - Math.Clamp(normalized[index], 0, 1) * amplitude)).ToArray();
    }
}
