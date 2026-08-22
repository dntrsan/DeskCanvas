using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace DeskCanvas.App.Services;

internal enum CodexUsageSource
{
    None,
    Live,
    LocalHistory,
    Preview
}

internal sealed record CodexLimitWindow(int UsedPercent, long? WindowDurationMinutes, DateTimeOffset? ResetsAt)
{
    internal int RemainingPercent => Math.Clamp(100 - UsedPercent, 0, 100);
}

internal sealed record CodexUsageSnapshot(
    CodexLimitWindow? Primary,
    CodexLimitWindow? Secondary,
    string PlanType,
    string? CreditBalance,
    bool UnlimitedCredits,
    DateTimeOffset? UpdatedAt,
    CodexUsageSource Source,
    string? Error)
{
    internal static CodexUsageSnapshot Loading { get; } = new(null, null, "", null, false, null, CodexUsageSource.None, null);
    internal bool HasUsage => Primary is not null;
}

internal interface ICodexUsageService : IDisposable
{
    CodexUsageSnapshot Snapshot { get; }
    event EventHandler<CodexUsageSnapshot>? SnapshotChanged;
    IDisposable Acquire();
}

internal sealed class CodexUsageService : ICodexUsageService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(12);
    private readonly object gate = new();
    private readonly bool previewMode;
    private System.Threading.Timer? timer;
    private CancellationTokenSource? lifetime;
    private int leases;
    private int refreshing;
    private bool disposed;

    internal CodexUsageService(bool previewMode = false)
    {
        this.previewMode = previewMode;
        Snapshot = previewMode
            ? new CodexUsageSnapshot(
                new CodexLimitWindow(27, 10_080, DateTimeOffset.Now.AddDays(4).AddHours(7)),
                null,
                "plus",
                null,
                false,
                DateTimeOffset.Now,
                CodexUsageSource.Preview,
                null)
            : CodexUsageSnapshot.Loading;
    }

    public CodexUsageSnapshot Snapshot { get; private set; }
    public event EventHandler<CodexUsageSnapshot>? SnapshotChanged;

    public IDisposable Acquire()
    {
        lock (gate)
        {
            if (disposed) return EmptyLease.Instance;
            leases++;
            if (leases == 1 && !previewMode)
            {
                var activeLifetime = new CancellationTokenSource();
                var activeToken = activeLifetime.Token;
                lifetime = activeLifetime;
                timer = new System.Threading.Timer(_ => _ = RefreshAsync(activeToken), null, TimeSpan.Zero, RefreshInterval);
            }
        }
        return new Lease(this);
    }

    private void Release()
    {
        lock (gate)
        {
            if (leases == 0) return;
            leases--;
            if (leases == 0)
            {
                timer?.Dispose();
                timer = null;
                lifetime?.Cancel();
                lifetime?.Dispose();
                lifetime = null;
                Interlocked.Exchange(ref refreshing, 0);
            }
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref refreshing, 1) != 0) return;
        try
        {
            CodexUsageSnapshot next;
            try
            {
                next = await ReadFromAppServerAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException error)
            {
                next = SnapshotFailure(error.Message);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                next = await Task.Run(() => ReadFromHistory(error.Message), cancellationToken).ConfigureAwait(false);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                var current = Snapshot;
                if (current.HasUsage &&
                    (!next.HasUsage || (next.UpdatedAt is not null && current.UpdatedAt > next.UpdatedAt)))
                {
                    Publish(current with { Error = next.Error ?? "ライブ更新に失敗しました。" });
                }
                else
                {
                    Publish(next);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Interlocked.Exchange(ref refreshing, 0);
        }
    }

    private static async Task<CodexUsageSnapshot> ReadFromAppServerAsync(CancellationToken cancellationToken)
    {
        var executable = ResolveCodexExecutable();
        if (executable is null) throw new FileNotFoundException("Codexが見つかりません。");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        var psi = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("app-server");
        psi.ArgumentList.Add("--stdio");
        using var process = new Process { StartInfo = psi };
        if (!process.Start()) throw new InvalidOperationException("Codex App Serverを開始できませんでした。");
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);

        try
        {
            await process.StandardInput.WriteLineAsync(
                "{\"id\":1,\"method\":\"initialize\",\"params\":{\"clientInfo\":{\"name\":\"deskcanvas\",\"version\":\"1.0\"},\"capabilities\":null}}")
                .ConfigureAwait(false);
            await process.StandardInput.FlushAsync(timeout.Token).ConfigureAwait(false);
            using var initialize = await ReadResponseAsync(process, 1, timeout.Token).ConfigureAwait(false);
            ThrowForProtocolError(initialize.RootElement);

            await process.StandardInput.WriteLineAsync("{\"method\":\"initialized\",\"params\":{}}")
                .ConfigureAwait(false);
            await process.StandardInput.WriteLineAsync("{\"id\":2,\"method\":\"account/rateLimits/read\",\"params\":null}")
                .ConfigureAwait(false);
            await process.StandardInput.FlushAsync(timeout.Token).ConfigureAwait(false);
            using var response = await ReadResponseAsync(process, 2, timeout.Token).ConfigureAwait(false);
            ThrowForProtocolError(response.RootElement);
            var result = response.RootElement.GetProperty("result");
            var rateLimits = SelectRateLimits(result);
            return ParseRateLimits(rateLimits, DateTimeOffset.Now, CodexUsageSource.Live, null, camelCase: true);
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
            }
            try { await errorTask.ConfigureAwait(false); }
            catch (Exception) { }
        }
    }

    private static async Task<JsonDocument> ReadResponseAsync(Process process, int expectedId, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Codex App Serverが終了しました。" : error.Trim());
            }
            JsonDocument document;
            try { document = JsonDocument.Parse(line); }
            catch (JsonException) { continue; }
            if (document.RootElement.TryGetProperty("id", out var id) && id.TryGetInt32(out var value) && value == expectedId)
                return document;
            document.Dispose();
        }
    }

    private static void ThrowForProtocolError(JsonElement response)
    {
        if (!response.TryGetProperty("error", out var error)) return;
        var message = error.TryGetProperty("message", out var value) ? value.GetString() : null;
        throw new InvalidOperationException(message ?? "Codexからエラーが返されました。");
    }

    private static string? ResolveCodexExecutable()
    {
        var explicitPath = Environment.GetEnvironmentVariable("DESKCANVAS_CODEX_EXE");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath)) return explicitPath;
        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "OpenAI", "Codex", "bin", "codex.exe");
        if (File.Exists(local)) return local;
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(folder => Path.Combine(folder.Trim(), "codex.exe"))
            .FirstOrDefault(File.Exists);
    }

    internal static CodexUsageSnapshot ReadFromHistory(string liveError)
    {
        var homes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var configuredHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (!string.IsNullOrWhiteSpace(configuredHome)) homes.Add(configuredHome);
        var profileEnvironment = Environment.GetEnvironmentVariable("USERPROFILE");
        if (!string.IsNullOrWhiteSpace(profileEnvironment)) homes.Add(Path.Combine(profileEnvironment, ".codex"));
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDataDirectory = string.IsNullOrWhiteSpace(localAppData) ? null : Directory.GetParent(localAppData);
        var profileFromLocalAppData = appDataDirectory?.Parent?.FullName;
        if (!string.IsNullOrWhiteSpace(profileFromLocalAppData)) homes.Add(Path.Combine(profileFromLocalAppData, ".codex"));
        homes.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex"));
        var files = new List<FileInfo>();
        foreach (var codexHome in homes)
        {
            foreach (var name in new[] { "sessions", "archived_sessions" })
            {
                var root = Path.Combine(codexHome, name);
                if (!Directory.Exists(root)) continue;
                try { files.AddRange(new DirectoryInfo(root).EnumerateFiles("*.jsonl", SearchOption.AllDirectories)); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            }
        }

        foreach (var file in files.OrderByDescending(file => file.LastWriteTimeUtc).Take(40))
        {
            var candidate = ReadLatestRateLimit(file);
            if (candidate is not null) return candidate with { Error = liveError };
        }

        return SnapshotFailure(liveError);
    }

    internal static CodexUsageSnapshot? ReadLatestRateLimit(FileInfo file)
    {
        CodexUsageSnapshot? latest = null;
        try
        {
            const FileShare sharing = FileShare.ReadWrite | FileShare.Delete;
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, sharing);
            const long maximumTailBytes = 4L * 1024 * 1024;
            var offset = Math.Max(0, stream.Length - maximumTailBytes);
            if (offset > 0) stream.Seek(offset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            if (offset > 0) reader.ReadLine();
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (!line.Contains("\"rate_limits\"", StringComparison.Ordinal)) continue;
                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    if (!root.TryGetProperty("payload", out var payload) ||
                        !payload.TryGetProperty("rate_limits", out var rateLimits) ||
                        rateLimits.ValueKind != JsonValueKind.Object) continue;
                    var timestamp = new DateTimeOffset(file.LastWriteTimeUtc);
                    if (root.TryGetProperty("timestamp", out var timestampValue) &&
                        DateTimeOffset.TryParse(timestampValue.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
                        timestamp = parsed;
                    var limitId = GetString(rateLimits, "limit_id");
                    var candidate = ParseRateLimits(rateLimits, timestamp, CodexUsageSource.LocalHistory, null, camelCase: false);
                    if (candidate.HasUsage && (string.IsNullOrWhiteSpace(limitId) || string.Equals(limitId, "codex", StringComparison.OrdinalIgnoreCase)))
                        latest = candidate;
                }
                catch (JsonException) { }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }

        return latest;
    }

    internal static CodexUsageSnapshot ParseRateLimits(
        JsonElement element,
        DateTimeOffset updatedAt,
        CodexUsageSource source,
        string? error,
        bool camelCase)
    {
        var primary = ParseWindow(element, "primary", camelCase);
        var secondary = ParseWindow(element, "secondary", camelCase);
        var plan = GetString(element, camelCase ? "planType" : "plan_type") ?? "";
        string? balance = null;
        var unlimited = false;
        if (element.TryGetProperty("credits", out var credits) && credits.ValueKind == JsonValueKind.Object)
        {
            balance = GetString(credits, camelCase ? "balance" : "balance") ?? GetNumberString(credits, "balance");
            unlimited = GetBoolean(credits, camelCase ? "unlimited" : "unlimited");
        }
        return new CodexUsageSnapshot(primary, secondary, plan, balance, unlimited, updatedAt, source, error);
    }

    internal static JsonElement SelectRateLimits(JsonElement result)
    {
        var selected = result.GetProperty("rateLimits");
        if (!result.TryGetProperty("rateLimitsByLimitId", out var buckets) || buckets.ValueKind != JsonValueKind.Object)
            return selected;
        if (buckets.TryGetProperty("codex", out var codexBucket)) return codexBucket;
        var first = buckets.EnumerateObject().FirstOrDefault();
        return first.Value.ValueKind == JsonValueKind.Object ? first.Value : selected;
    }

    private static CodexLimitWindow? ParseWindow(JsonElement root, string name, bool camelCase)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        var usedName = camelCase ? "usedPercent" : "used_percent";
        var durationName = camelCase ? "windowDurationMins" : "window_minutes";
        var resetName = camelCase ? "resetsAt" : "resets_at";
        if (!value.TryGetProperty(usedName, out var usedValue) || usedValue.ValueKind != JsonValueKind.Number) return null;
        int used;
        if (!usedValue.TryGetInt32(out used))
        {
            if (!usedValue.TryGetDouble(out var usedDouble) || !double.IsFinite(usedDouble)) return null;
            used = (int)Math.Round(usedDouble, MidpointRounding.AwayFromZero);
        }
        long? duration = value.TryGetProperty(durationName, out var durationValue) && durationValue.TryGetInt64(out var minutes) ? minutes : null;
        DateTimeOffset? reset = value.TryGetProperty(resetName, out var resetValue) && resetValue.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
        return new CodexLimitWindow(Math.Clamp(used, 0, 100), duration, reset);
    }

    private static string? GetString(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? GetNumberString(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            return number.ToString(CultureInfo.InvariantCulture);
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static bool GetBoolean(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static CodexUsageSnapshot SnapshotFailure(string error) =>
        new(null, null, "", null, false, DateTimeOffset.Now, CodexUsageSource.None, error);

    private void Publish(CodexUsageSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(this, snapshot);
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            leases = 0;
            timer?.Dispose();
            timer = null;
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            Interlocked.Exchange(ref refreshing, 0);
        }
    }

    private sealed class Lease(CodexUsageService owner) : IDisposable
    {
        private CodexUsageService? owner = owner;
        public void Dispose() => Interlocked.Exchange(ref owner, null)?.Release();
    }

    private sealed class EmptyLease : IDisposable
    {
        internal static EmptyLease Instance { get; } = new();
        public void Dispose() { }
    }
}
