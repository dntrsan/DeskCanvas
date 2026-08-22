namespace DeskCanvas.App.Services;

internal sealed class SmoothedNetworkGraph
{
    private readonly double[] history = new double[30];
    private readonly double[] start = new double[30];
    private readonly double[] target = new double[30];
    private int count, next, fallingSamples;
    private double displayScale = 1024;
    private TimeSpan elapsed;
    internal bool IsAnimating { get; private set; }
    internal int Count => count;
    internal double Scale => displayScale;

    internal void Add(double sample)
    {
        history[next] = Math.Max(0, double.IsFinite(sample) ? sample : 0); next = (next + 1) % history.Length; count = Math.Min(history.Length, count + 1);
        var peak = Math.Max(1024, Values().DefaultIfEmpty(0).Max());
        if (peak >= displayScale) { displayScale = peak; fallingSamples = 0; }
        else if (++fallingSamples > 3) displayScale = Math.Max(peak, displayScale * .88);
        SetTarget(Values().Select(x => Math.Clamp(x / displayScale, 0, 1)).ToArray());
    }

    internal IReadOnlyList<double> Advance(TimeSpan delta)
    {
        if (!IsAnimating) return target[..count];
        elapsed += delta; var t = Math.Clamp(elapsed.TotalMilliseconds / 700d, 0, 1); t = 1 - Math.Pow(1 - t, 3); // EaseOut cubic
        var values = Enumerable.Range(0, count).Select(i => Math.Clamp(start[i] + (target[i] - start[i]) * t, 0, 1)).ToArray();
        if (t >= 1) IsAnimating = false;
        return values;
    }

    internal void Stop() => IsAnimating = false;
    private void SetTarget(IReadOnlyList<double> values)
    {
        var current = Advance(TimeSpan.Zero);
        Array.Clear(start); Array.Clear(target);
        for (var i = 0; i < count; i++) { start[i] = i < current.Count ? current[i] : 0; target[i] = i < values.Count ? values[i] : 0; }
        elapsed = TimeSpan.Zero; IsAnimating = count > 1 && start.Take(count).Where((x, i) => Math.Abs(x - target[i]) > .0001).Any();
    }
    private double[] Values() => Enumerable.Range(0, count).Select(i => history[(next - count + i + history.Length) % history.Length]).ToArray();
}
