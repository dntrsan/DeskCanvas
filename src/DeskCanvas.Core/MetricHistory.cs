namespace DeskCanvas.Core;

/// <summary>Fixed-length chronological ring. Push ignores non-finite samples.</summary>
public sealed class MetricHistory
{
    private readonly double[] buffer;
    private int count;
    private int next;

    public MetricHistory(int capacity = 60)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        buffer = new double[capacity];
    }

    public int Capacity => buffer.Length;
    public int Count => count;

    public void Push(double value)
    {
        if (!double.IsFinite(value)) return;
        buffer[next] = value;
        next = (next + 1) % buffer.Length;
        if (count < buffer.Length) count++;
    }

    public void Push(MetricValue metric)
    {
        if (metric.Availability == MetricAvailability.Available && metric.Value is { } value)
        {
            Push(value);
        }
    }

    public void Clear()
    {
        count = 0;
        next = 0;
        Array.Clear(buffer);
    }

    public double[] Snapshot()
    {
        var result = new double[count];
        var start = count < buffer.Length ? 0 : next;
        for (var index = 0; index < count; index++)
        {
            result[index] = buffer[(start + index) % buffer.Length];
        }

        return result;
    }
}
