using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using DeskCanvas.Core;

namespace DeskCanvas.App.Services;

internal sealed record SystemMetricsSnapshot(MetricValue Cpu, MetricValue Memory, MetricValue Gpu, MetricValue Download, MetricValue Upload)
{
    internal MetricValue Vram { get; init; } = MetricValue.Loading();
    internal MetricValue Clock { get; init; } = MetricValue.Loading();
    internal IReadOnlyList<double> CpuHistory { get; init; } = [];
    internal IReadOnlyList<double> GpuHistory { get; init; } = [];
    internal IReadOnlyList<double> VramHistory { get; init; } = [];
    internal IReadOnlyList<double> MemoryHistory { get; init; } = [];
    internal IReadOnlyList<double> DownloadHistory { get; init; } = [];

    internal static SystemMetricsSnapshot Loading { get; } = new(
        MetricValue.Loading(), MetricValue.Loading(), MetricValue.Loading(), MetricValue.Loading(), MetricValue.Loading());
}

internal interface ISystemMetricsService : IDisposable
{
    SystemMetricsSnapshot Snapshot { get; }
    event EventHandler<SystemMetricsSnapshot>? SnapshotChanged;
    IDisposable Acquire();
}

/// <summary>One 1 Hz background sampler. It starts only while a widget or picker is visible.</summary>
internal sealed class SystemMetricsService : ISystemMetricsService
{
    private readonly object sync = new();
    private System.Threading.Timer? timer;
    private readonly GpuMetricsReader gpuReader = new();
    private readonly GpuMemoryReader vramReader = new();
    private readonly MetricHistory cpuHistory = new();
    private readonly MetricHistory gpuHistory = new();
    private readonly MetricHistory vramHistory = new();
    private readonly MetricHistory memoryHistory = new();
    private readonly MetricHistory downloadHistory = new();
    private int consumers;
    private int sampling;
    private bool disposed;
    private (ulong Idle, ulong Kernel, ulong User, DateTimeOffset At)? cpuPrevious;
    private Dictionary<string, (long Received, long Sent, DateTimeOffset At)> networkPrevious = [];

    public SystemMetricsSnapshot Snapshot { get; private set; } = SystemMetricsSnapshot.Loading;
    public event EventHandler<SystemMetricsSnapshot>? SnapshotChanged;

    public IDisposable Acquire()
    {
        if (disposed) return EmptyLease.Instance;
        if (Interlocked.Increment(ref consumers) == 1)
        {
            lock (sync)
            {
                cpuPrevious = null;
                networkPrevious = [];
                cpuHistory.Clear();
                gpuHistory.Clear();
                vramHistory.Clear();
                memoryHistory.Clear();
                downloadHistory.Clear();
                Snapshot = SystemMetricsSnapshot.Loading;
                timer ??= new System.Threading.Timer(static state => ((SystemMetricsService)state!).Sample(), this, Timeout.Infinite, Timeout.Infinite);
                timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
            }
        }
        return new Lease(this);
    }

    private void Release()
    {
        if (Interlocked.Decrement(ref consumers) != 0) return;
        lock (sync)
        {
            timer?.Change(Timeout.Infinite, Timeout.Infinite);
            cpuPrevious = null;
            networkPrevious = [];
            cpuHistory.Clear();
            gpuHistory.Clear();
            vramHistory.Clear();
            memoryHistory.Clear();
            downloadHistory.Clear();
        }
    }

    private void Sample()
    {
        if (disposed || Volatile.Read(ref consumers) == 0 || Interlocked.Exchange(ref sampling, 1) != 0) return;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var cpu = ReadCpu(now);
            var memory = ReadMemory();
            var (down, up) = ReadNetwork(now);
            var gpu = gpuReader.Read();
            var vram = vramReader.Read();
            var clock = ProcessorClock.Read();
            SystemMetricsSnapshot snapshot;
            lock (sync)
            {
                cpuHistory.Push(cpu);
                gpuHistory.Push(gpu);
                vramHistory.Push(vram);
                memoryHistory.Push(memory);
                downloadHistory.Push(down);
                snapshot = new SystemMetricsSnapshot(cpu, memory, gpu, down, up)
                {
                    Vram = vram,
                    Clock = clock,
                    CpuHistory = cpuHistory.Snapshot(),
                    GpuHistory = gpuHistory.Snapshot(),
                    VramHistory = vramHistory.Snapshot(),
                    MemoryHistory = memoryHistory.Snapshot(),
                    DownloadHistory = downloadHistory.Snapshot()
                };
            }
            if (!disposed && Volatile.Read(ref consumers) > 0) Publish(snapshot);
        }
        catch (Exception)
        {
            if (!disposed && Volatile.Read(ref consumers) > 0) Publish(SystemMetricsSnapshot.Loading);
        }
        finally { Volatile.Write(ref sampling, 0); }
    }

    private MetricValue ReadCpu(DateTimeOffset now)
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            lock (sync) cpuPrevious = null;
            return MetricValue.Unavailable("--");
        }
        var current = (ToUInt64(idle), ToUInt64(kernel), ToUInt64(user), now);
        MetricValue result;
        lock (sync)
        {
            result = SystemMetricMath.Cpu(current.Item1, current.Item2, current.Item3, cpuPrevious, now);
            cpuPrevious = current;
        }
        return result;
    }

    private static MetricValue ReadMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0) return MetricValue.Unavailable("--");
        var used = status.TotalPhys - status.AvailPhys;
        var percent = Math.Clamp(used * 100d / status.TotalPhys, 0, 100);
        return MetricValue.From(percent, $"{percent:0}%  {FormatBytes(used)} / {FormatBytes(status.TotalPhys)}");
    }

    private (MetricValue Down, MetricValue Up) ReadNetwork(DateTimeOffset now)
    {
        if (!PhysicalNetworkReader.TryReadAdapters(out var adapters))
        {
            lock (sync) networkPrevious = [];
            return (MetricValue.Unavailable("--"), MetricValue.Unavailable("--"));
        }

        long downDelta = 0, upDelta = 0;
        var elapsed = TimeSpan.Zero;
        var comparable = false;
        lock (sync)
        {
            foreach (var adapter in adapters)
            {
                if (networkPrevious.TryGetValue(adapter.Id, out var previous))
                {
                    var slice = now - previous.At;
                    if (slice <= TimeSpan.Zero || slice > TimeSpan.FromSeconds(10)) continue;
                    if (adapter.Received < previous.Received || adapter.Sent < previous.Sent) continue;
                    downDelta = SaturatingAdd(downDelta, adapter.Received - previous.Received);
                    upDelta = SaturatingAdd(upDelta, adapter.Sent - previous.Sent);
                    if (slice > elapsed) elapsed = slice;
                    comparable = true;
                }
            }
            networkPrevious = adapters.ToDictionary(adapter => adapter.Id, adapter => (adapter.Received, adapter.Sent, now));
        }
        if (!comparable) return (MetricValue.Loading(), MetricValue.Loading());
        return (
            SystemMetricMath.Rate(downDelta, 0, elapsed, "秒"),
            SystemMetricMath.Rate(upDelta, 0, elapsed, "秒"));
    }

    private static long SaturatingAdd(long current, long value) =>
        value >= long.MaxValue - current ? long.MaxValue : current + value;

    private void Publish(SystemMetricsSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(this, snapshot);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Interlocked.Exchange(ref consumers, 0);
        if (timer is not null)
        {
            using var finished = new ManualResetEvent(false);
            timer.Dispose(finished);
            finished.WaitOne(TimeSpan.FromSeconds(1));
            timer = null;
        }
        gpuReader.Dispose();
        vramReader.Dispose();
    }

    private static string FormatBytes(ulong bytes)
    {
        var value = (double)bytes; var units = new[] { "B", "KB", "MB", "GB", "TB" }; var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        return $"{value:0.#} {units[index]}";
    }

    private static ulong ToUInt64(FileTime value) => ((ulong)value.HighDateTime << 32) | value.LowDateTime;

    [StructLayout(LayoutKind.Sequential)] private struct FileTime { public uint LowDateTime; public uint HighDateTime; }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatusEx { public uint Length; public uint MemoryLoad; public ulong TotalPhys; public ulong AvailPhys; public ulong TotalPageFile; public ulong AvailPageFile; public ulong TotalVirtual; public ulong AvailVirtual; public ulong AvailExtendedVirtual; }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    private sealed class Lease(SystemMetricsService owner) : IDisposable { private SystemMetricsService? owner = owner; public void Dispose() => Interlocked.Exchange(ref owner, null)?.Release(); }
    private sealed class EmptyLease : IDisposable { internal static readonly EmptyLease Instance = new(); public void Dispose() { } }
}

internal sealed class GpuMetricsReader : IDisposable
{
    private readonly object sync = new();
    private IntPtr query;
    private IntPtr counter;
    private bool primed;
    private bool disposed;

    internal MetricValue Read()
    {
        lock (sync)
        {
            if (disposed) return MetricValue.Unavailable("--");
            try
            {
                if (query == IntPtr.Zero)
                {
                    var initialization = Initialize();
                    if (initialization is not null) return initialization.Value;
                    // Rate-based PDH counters need a later collection. The shared 1 Hz
                    // sampler supplies it without blocking its timer thread.
                    return MetricValue.Loading();
                }

                if (PdhCollectQueryData(query) != 0)
                {
                    Reset();
                    return MetricValue.Loading();
                }
                if (!primed)
                {
                    primed = true;
                    return MetricValue.Loading();
                }

                uint count = 0, bufferSize = 0;
                var status = PdhGetFormattedCounterArray(counter, PdhFmtDouble, ref bufferSize, ref count, IntPtr.Zero);
                if (status == PdhNoData || (status == 0 && count == 0)) return MetricValue.Unavailable("--");
                if (status != PdhMoreData || bufferSize == 0)
                {
                    Reset();
                    return MetricValue.Loading();
                }

                var buffer = Marshal.AllocHGlobal((int)bufferSize);
                try
                {
                    status = PdhGetFormattedCounterArray(counter, PdhFmtDouble, ref bufferSize, ref count, buffer);
                    if (status == PdhNoData || count == 0) return MetricValue.Unavailable("--");
                    if (status != 0)
                    {
                        Reset();
                        return MetricValue.Loading();
                    }
                    var size = Marshal.SizeOf<PdhFmtCounterValueItem>();
                    var samples = new List<GpuCounterSample>((int)count);
                    for (var index = 0; index < count; index++)
                    {
                        var item = Marshal.PtrToStructure<PdhFmtCounterValueItem>(buffer + index * size);
                        samples.Add(new GpuCounterSample(item.Name ?? "", item.Value.DoubleValue, item.Value.CStatus is PdhValidData or PdhNewData));
                    }
                    return SystemMetricMath.BusiestGpuEngine(samples);
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
            catch (DllNotFoundException) { Reset(); return MetricValue.Unavailable("--"); }
            catch (EntryPointNotFoundException) { Reset(); return MetricValue.Unavailable("--"); }
            catch (Exception) { Reset(); return MetricValue.Loading(); }
        }
    }

    private MetricValue? Initialize()
    {
        var status = PdhOpenQuery(null, IntPtr.Zero, out query);
        if (status != 0)
        {
            query = IntPtr.Zero;
            counter = IntPtr.Zero;
            return MetricValue.Loading();
        }
        status = PdhAddEnglishCounter(query, "\\GPU Engine(*)\\Utilization Percentage", IntPtr.Zero, out counter);
        if (status != 0)
        {
            Reset();
            return status is PdhNoObject or PdhNoCounter ? MetricValue.Unavailable("--") : MetricValue.Loading();
        }
        if (PdhCollectQueryData(query) != 0)
        {
            Reset();
            return MetricValue.Loading();
        }
        primed = true;
        return null;
    }

    private void Reset()
    {
        if (query != IntPtr.Zero) PdhCloseQuery(query);
        query = IntPtr.Zero;
        counter = IntPtr.Zero;
        primed = false;
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            Reset();
        }
    }

    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhMoreData = 0x800007D2;
    private const uint PdhNoData = 0x800007D5;
    private const uint PdhNoObject = 0xC0000BB8;
    private const uint PdhNoCounter = 0xC0000BB9;
    private const uint PdhValidData = 0x00000000;
    private const uint PdhNewData = 0x00000001;
    [StructLayout(LayoutKind.Sequential)] private struct PdhFmtCounterValueItem { [MarshalAs(UnmanagedType.LPWStr)] public string? Name; public PdhFmtCounterValue Value; }
    [StructLayout(LayoutKind.Explicit)] private struct PdhFmtCounterValue { [FieldOffset(0)] public uint CStatus; [FieldOffset(8)] public double DoubleValue; }
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhOpenQuery(string? source, IntPtr userData, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhAddEnglishCounter(IntPtr query, string path, IntPtr userData, out IntPtr counter);
    [DllImport("pdh.dll")] private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bufferSize, ref uint itemCount, IntPtr itemBuffer);
    [DllImport("pdh.dll")] private static extern uint PdhCloseQuery(IntPtr query);
}

internal sealed class GpuMemoryReader : IDisposable
{
    private const string DisplayClassPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
    private readonly object sync = new();
    private IntPtr query;
    private IntPtr counter;
    private bool primed;
    private bool disposed;
    private ulong cachedTotal;
    private DateTimeOffset totalReadAt;

    internal MetricValue Read()
    {
        lock (sync)
        {
            if (disposed) return MetricValue.Unavailable("--");
            try
            {
                if (query == IntPtr.Zero)
                {
                    var initialization = Initialize();
                    if (initialization is not null) return initialization.Value;
                    return MetricValue.Loading();
                }

                if (PdhCollectQueryData(query) != 0)
                {
                    Reset();
                    return MetricValue.Loading();
                }
                if (!primed)
                {
                    primed = true;
                    return MetricValue.Loading();
                }

                uint count = 0, bufferSize = 0;
                var status = PdhGetFormattedCounterArray(counter, PdhFmtDouble, ref bufferSize, ref count, IntPtr.Zero);
                if (status == PdhNoData || (status == 0 && count == 0)) return MetricValue.Unavailable("--");
                if (status != PdhMoreData || bufferSize == 0)
                {
                    Reset();
                    return MetricValue.Loading();
                }

                var buffer = Marshal.AllocHGlobal((int)bufferSize);
                try
                {
                    status = PdhGetFormattedCounterArray(counter, PdhFmtDouble, ref bufferSize, ref count, buffer);
                    if (status == PdhNoData || count == 0) return MetricValue.Unavailable("--");
                    if (status != 0)
                    {
                        Reset();
                        return MetricValue.Loading();
                    }
                    var size = Marshal.SizeOf<PdhFmtCounterValueItem>();
                    double used = 0;
                    var found = false;
                    for (var index = 0; index < count; index++)
                    {
                        var item = Marshal.PtrToStructure<PdhFmtCounterValueItem>(buffer + index * size);
                        if (item.Value.CStatus is not (PdhValidData or PdhNewData) || !double.IsFinite(item.Value.DoubleValue)) continue;
                        used += Math.Max(0, item.Value.DoubleValue);
                        found = true;
                    }
                    if (!found) return MetricValue.Unavailable("--");
                    return GpuMemoryMath.FromBytes(used, ReadDedicatedBudget());
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
            catch (DllNotFoundException) { Reset(); return MetricValue.Unavailable("--"); }
            catch (EntryPointNotFoundException) { Reset(); return MetricValue.Unavailable("--"); }
            catch (Exception) { Reset(); return MetricValue.Loading(); }
        }
    }

    private MetricValue? Initialize()
    {
        var status = PdhOpenQuery(null, IntPtr.Zero, out query);
        if (status != 0)
        {
            query = IntPtr.Zero;
            counter = IntPtr.Zero;
            return MetricValue.Loading();
        }
        status = PdhAddEnglishCounter(query, "\\GPU Adapter Memory(*)\\Dedicated Usage", IntPtr.Zero, out counter);
        if (status != 0)
        {
            Reset();
            return status is PdhNoObject or PdhNoCounter ? MetricValue.Unavailable("--") : MetricValue.Loading();
        }
        if (PdhCollectQueryData(query) != 0)
        {
            Reset();
            return MetricValue.Loading();
        }
        primed = true;
        return null;
    }

    private ulong ReadDedicatedBudget()
    {
        var now = DateTimeOffset.UtcNow;
        if (cachedTotal > 0 && now - totalReadAt < TimeSpan.FromSeconds(30)) return cachedTotal;
        ulong max = 0;
        try
        {
            using var displayClass = Registry.LocalMachine.OpenSubKey(DisplayClassPath, writable: false);
            if (displayClass is not null)
            {
                foreach (var childName in displayClass.GetSubKeyNames())
                {
                    if (childName.Length == 0 || !childName.All(char.IsDigit)) continue;
                    try
                    {
                        using var adapter = displayClass.OpenSubKey(childName, writable: false);
                        var raw = adapter?.GetValue("HardwareInformation.qwMemorySize");
                        var bytes = raw switch
                        {
                            long signed => signed > 0 ? (ulong)signed : 0,
                            ulong unsigned => unsigned,
                            int signed => signed > 0 ? (ulong)signed : 0,
                            byte[] blob when blob.Length >= 8 => BitConverter.ToUInt64(blob, 0),
                            _ => 0UL
                        };
                        if (bytes > max) max = bytes;
                    }
                    catch (Exception error) when (error is UnauthorizedAccessException or System.Security.SecurityException)
                    {
                    }
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }

        cachedTotal = max;
        totalReadAt = now;
        return cachedTotal;
    }

    private void Reset()
    {
        if (query != IntPtr.Zero) PdhCloseQuery(query);
        query = IntPtr.Zero;
        counter = IntPtr.Zero;
        primed = false;
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            Reset();
        }
    }

    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhMoreData = 0x800007D2;
    private const uint PdhNoData = 0x800007D5;
    private const uint PdhNoObject = 0xC0000BB8;
    private const uint PdhNoCounter = 0xC0000BB9;
    private const uint PdhValidData = 0x00000000;
    private const uint PdhNewData = 0x00000001;
    [StructLayout(LayoutKind.Sequential)] private struct PdhFmtCounterValueItem { [MarshalAs(UnmanagedType.LPWStr)] public string? Name; public PdhFmtCounterValue Value; }
    [StructLayout(LayoutKind.Explicit)] private struct PdhFmtCounterValue { [FieldOffset(0)] public uint CStatus; [FieldOffset(8)] public double DoubleValue; }
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhOpenQuery(string? source, IntPtr userData, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhAddEnglishCounter(IntPtr query, string path, IntPtr userData, out IntPtr counter);
    [DllImport("pdh.dll")] private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bufferSize, ref uint itemCount, IntPtr itemBuffer);
    [DllImport("pdh.dll")] private static extern uint PdhCloseQuery(IntPtr query);
}

internal static class ProcessorClock
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Info
    {
        public uint N, Max, Current, Limit, MaxIdle, CurrentIdle;
    }

    [DllImport("powrprof.dll")]
    private static extern uint CallNtPowerInformation(int level, IntPtr input, int inputLength, [Out] Info[] output, int outputLength);

    internal static MetricValue Read()
    {
        try
        {
            var data = new Info[Math.Max(1, Environment.ProcessorCount)];
            if (CallNtPowerInformation(11, IntPtr.Zero, 0, data, Marshal.SizeOf<Info>() * data.Length) != 0)
            {
                return MetricValue.Unavailable("-- GHz");
            }

            var text = SystemMetricMath.FormatProcessorClock(data.Select(sample => sample.Current));
            if (text == "-- GHz") return MetricValue.Unavailable(text);
            var values = data.Where(sample => sample.Current > 0).Select(sample => sample.Current / 1000d).ToArray();
            var ghz = values.Length == 0 ? 0 : values.Average();
            return MetricValue.From(ghz, text);
        }
        catch (Exception)
        {
            return MetricValue.Unavailable("-- GHz");
        }
    }
}

internal static class PhysicalNetworkReader
{
    private const uint NcfVirtual = 0x1;
    private const uint NcfPhysical = 0x4;
    private const string NetworkClassPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
    private static readonly object Sync = new();
    private static HashSet<Guid> physicalAdapterIds = [];
    private static DateTimeOffset adapterIdsReadAt;

    internal static bool TryReadAdapters(out List<(string Id, long Received, long Sent)> adapters)
    {
        adapters = [];
        try
        {
            var physicalIds = GetPhysicalAdapterIds();
            if (physicalIds.Count == 0) return false;
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (!Guid.TryParse(adapter.Id, out var id) || !physicalIds.Contains(id)) continue;
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                if ((uint)adapter.NetworkInterfaceType is 24 or 131) continue;
                IPv4InterfaceStatistics? v4 = null;
                IPInterfaceStatistics? ip = null;
                try { ip = adapter.GetIPStatistics(); }
                catch (NetworkInformationException)
                {
                    try { v4 = adapter.GetIPv4Statistics(); }
                    catch (NetworkInformationException) { continue; }
                }
                var received = ip?.BytesReceived ?? v4?.BytesReceived ?? 0;
                var sent = ip?.BytesSent ?? v4?.BytesSent ?? 0;
                adapters.Add((adapter.Id, Math.Max(0, received), Math.Max(0, sent)));
            }
            return adapters.Count > 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NetworkInformationException or System.Security.SecurityException)
        {
            return false;
        }
    }

    internal static bool TryReadTotals(out long received, out long sent)
    {
        received = 0;
        sent = 0;
        if (!TryReadAdapters(out var adapters)) return false;
        foreach (var adapter in adapters)
        {
            received = adapter.Received >= long.MaxValue - received ? long.MaxValue : received + adapter.Received;
            sent = adapter.Sent >= long.MaxValue - sent ? long.MaxValue : sent + adapter.Sent;
        }
        return true;
    }

    private static HashSet<Guid> GetPhysicalAdapterIds()
    {
        lock (Sync)
        {
            var now = DateTimeOffset.UtcNow;
            if (physicalAdapterIds.Count > 0 && now - adapterIdsReadAt < TimeSpan.FromSeconds(30)) return physicalAdapterIds;
            var discovered = new HashSet<Guid>();
            using var networkClass = Registry.LocalMachine.OpenSubKey(NetworkClassPath, writable: false);
            if (networkClass is not null)
            {
                foreach (var childName in networkClass.GetSubKeyNames())
                {
                    try
                    {
                        using var adapterKey = networkClass.OpenSubKey(childName, writable: false);
                        if (adapterKey?.GetValue("NetCfgInstanceId") is not string instanceId ||
                            !Guid.TryParse(instanceId, out var id) ||
                            adapterKey.GetValue("Characteristics") is not object rawCharacteristics)
                        {
                            continue;
                        }
                        var characteristics = Convert.ToUInt32(rawCharacteristics, System.Globalization.CultureInfo.InvariantCulture);
                        if ((characteristics & NcfPhysical) != 0 && (characteristics & NcfVirtual) == 0) discovered.Add(id);
                    }
                    catch (Exception error) when (error is UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        // Some driver-owned class keys deny reads; other adapters remain usable.
                    }
                }
            }
            physicalAdapterIds = discovered;
            adapterIdsReadAt = now;
            return physicalAdapterIds;
        }
    }
}