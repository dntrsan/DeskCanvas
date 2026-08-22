namespace DeskCanvas.Core;

/// <summary>Pure VRAM percent/text formatting. Sampling stays in the app service.</summary>
public static class GpuMemoryMath
{
    public static MetricValue FromBytes(double usedBytes, double totalBytes)
    {
        if (!double.IsFinite(usedBytes) || usedBytes < 0)
        {
            return MetricValue.Unavailable("--");
        }

        if (!double.IsFinite(totalBytes) || totalBytes <= 0)
        {
            return MetricValue.Unavailable("--");
        }

        var used = Math.Min(usedBytes, totalBytes);
        var percent = Math.Clamp(used * 100d / totalBytes, 0, 100);
        return MetricValue.From(percent, $"{percent:0}%  {FormatBytes(used)} / {FormatBytes(totalBytes)}");
    }

    public static string FormatBytes(double bytes)
    {
        var value = Math.Max(0, bytes);
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var index = 0;
        while (value >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return $"{value:0.#} {units[index]}";
    }
}
