namespace DeskCanvas.Core;

/// <summary>
/// Maps a value series onto a WPF-style polyline (y grows downward).
/// <paramref name="floorScale"/> keeps percent charts on a 0–100 axis even when
/// every sample is small; pass 0 to scale to the series peak.
/// </summary>
public static class SparklineGeometry
{
    public static IReadOnlyList<(double X, double Y)> Build(
        IReadOnlyList<double> values,
        double width,
        double height,
        double floorScale = 0,
        bool smooth = true)
    {
        if (values is null || values.Count == 0 || width <= 0 || height <= 0)
        {
            return [];
        }

        var series = smooth && values.Count >= 3 ? Smooth(values) : values;
        var peak = 0d;
        foreach (var value in series)
        {
            if (double.IsFinite(value) && value > peak) peak = value;
        }

        var scale = Math.Max(floorScale, peak);
        if (scale <= 0) scale = 1;

        double MapY(double value)
        {
            var ratio = double.IsFinite(value) ? Math.Clamp(value / scale, 0, 1) : 0;
            return height - height * ratio;
        }

        if (series.Count == 1)
        {
            var y = MapY(series[0]);
            return [(0, y), (width, y)];
        }

        var points = new (double X, double Y)[series.Count];
        var last = series.Count - 1;
        for (var index = 0; index < series.Count; index++)
        {
            points[index] = (width * index / last, MapY(series[index]));
        }

        return points;
    }

    public static IReadOnlyList<(double X, double Y)> Area(
        IReadOnlyList<(double X, double Y)> line,
        double height)
    {
        if (line.Count == 0 || height < 0) return [];
        var area = new (double X, double Y)[line.Count + 2];
        for (var index = 0; index < line.Count; index++) area[index] = line[index];
        area[line.Count] = (line[^1].X, height);
        area[line.Count + 1] = (line[0].X, height);
        return area;
    }

    internal static double[] Smooth(IReadOnlyList<double> values)
    {
        var result = new double[values.Count];
        result[0] = values[0];
        result[^1] = values[^1];
        for (var index = 1; index < values.Count - 1; index++)
        {
            result[index] = (values[index - 1] + values[index] + values[index + 1]) / 3d;
        }

        return result;
    }
}
