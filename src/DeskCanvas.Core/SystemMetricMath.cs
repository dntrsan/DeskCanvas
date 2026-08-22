namespace DeskCanvas.Core;

public enum MetricAvailability { Loading, Available, Unavailable }

public readonly record struct MetricValue(MetricAvailability Availability, double? Value, string Text)
{
    public static MetricValue Loading(string text = "取得中") => new(MetricAvailability.Loading, null, text);
    public static MetricValue Unavailable(string text = "--") => new(MetricAvailability.Unavailable, null, text);
    public static MetricValue From(double value, string text) => new(MetricAvailability.Available, value, text);
}

public static class SystemMetricMath
{
    // Windows reports idle, kernel and user times in 100ns units. Kernel already includes idle.
    public static MetricValue Cpu(ulong idle, ulong kernel, ulong user, (ulong Idle, ulong Kernel, ulong User, DateTimeOffset At)? previous, DateTimeOffset now)
    {
        if (previous is null || now - previous.Value.At > TimeSpan.FromSeconds(10)) return MetricValue.Loading();
        var prior = previous.Value;
        if (idle < prior.Idle || kernel < prior.Kernel || user < prior.User) return MetricValue.Loading();
        var total = (kernel - prior.Kernel) + (user - prior.User);
        var idleDelta = idle - prior.Idle;
        if (total == 0 || idleDelta > total) return MetricValue.Loading();
        var value = Math.Clamp((1d - idleDelta / (double)total) * 100d, 0, 100);
        return MetricValue.From(value, $"{value:0}%");
    }

    public static MetricValue Rate(long current, long previous, TimeSpan elapsed, string suffix)
    {
        if (current < previous || elapsed <= TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(10)) return MetricValue.Loading();
        var perSecond = Math.Max(0, (current - previous) / elapsed.TotalSeconds);
        return MetricValue.From(perSecond, FormatRate(perSecond, suffix));
    }

    public static string FormatRate(double bytesPerSecond, string suffix)
    {
        var units = new[] { "B", "KB", "MB", "GB" };
        var unit = 0;
        while (bytesPerSecond >= 1024 && unit < units.Length - 1) { bytesPerSecond /= 1024; unit++; }
        return $"{bytesPerSecond:0.#} {units[unit]}/{suffix}";
    }

    public static bool TrySumPhysicalNetwork(IEnumerable<NetworkCounterSample> samples, out long received, out long sent)
    {
        received = 0;
        sent = 0;
        var found = false;
        foreach (var sample in samples)
        {
            if (!sample.IsHardware || !sample.IsUp || sample.InterfaceType is 24 or 131) continue;
            found = true;
            received = SaturatingAdd(received, sample.Received);
            sent = SaturatingAdd(sent, sample.Sent);
        }
        return found;
    }

    private static long SaturatingAdd(long current, ulong value) =>
        value >= (ulong)(long.MaxValue - current) ? long.MaxValue : current + (long)value;
    public static MetricValue BusiestGpuEngine(IEnumerable<GpuCounterSample> samples)
    {
        var totals = new Dictionary<GpuEngineIdentity, double>();
        foreach (var sample in samples)
        {
            if (!sample.IsValid ||
                !double.IsFinite(sample.Value) ||
                !TryParseGpuEngineIdentity(sample.InstanceName, out var identity))
            {
                continue;
            }

            totals.TryGetValue(identity, out var total);
            totals[identity] = total + Math.Max(0, sample.Value);
        }

        if (totals.Count == 0)
        {
            return MetricValue.Loading();
        }

        // GPU Engine exposes one instance per process contribution. Contributions for
        // the same physical adapter/engine are summed, then the engine is capped at 100%.
        var busiest = totals.Values.Max(value => Math.Clamp(value, 0, 100));
        return MetricValue.From(busiest, $"{busiest:0}%");
    }

    public static bool TryParseGpuEngineIdentity(string? instanceName, out GpuEngineIdentity identity)
    {
        identity = default;
        if (string.IsNullOrWhiteSpace(instanceName))
        {
            return false;
        }

        var tokens = instanceName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index <= tokens.Length - 9; index++)
        {
            if (!tokens[index].Equals("luid", StringComparison.OrdinalIgnoreCase) ||
                !tokens[index + 3].Equals("phys", StringComparison.OrdinalIgnoreCase) ||
                !tokens[index + 5].Equals("eng", StringComparison.OrdinalIgnoreCase) ||
                !tokens[index + 7].Equals("engtype", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var engineType = string.Join('_', tokens[(index + 8)..]);
            var duplicateSuffix = engineType.LastIndexOf('#');
            if (duplicateSuffix >= 0)
            {
                engineType = engineType[..duplicateSuffix];
            }

            if (engineType.Length == 0)
            {
                return false;
            }

            identity = new GpuEngineIdentity(
                tokens[index + 1],
                tokens[index + 2],
                tokens[index + 4],
                tokens[index + 6],
                engineType);
            return true;
        }

        return false;
    }
}

public readonly record struct GpuCounterSample(string InstanceName, double Value, bool IsValid = true);

public readonly record struct GpuEngineIdentity(
    string LuidHigh,
    string LuidLow,
    string PhysicalAdapter,
    string Engine,
    string EngineType);
public readonly record struct NetworkCounterSample(bool IsHardware, bool IsUp, uint InterfaceType, ulong Received, ulong Sent);
