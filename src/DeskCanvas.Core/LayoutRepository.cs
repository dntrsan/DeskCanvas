using System.Security;
using System.Text.Json;

namespace DeskCanvas.Core;

public sealed class LayoutRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string root;
    private readonly string layoutPath;
    private readonly string backupDirectory;
    private DateOnly? lastBackupDay;

    public LayoutRepository(string root)
    {
        this.root = root;
        layoutPath = Path.Combine(root, "layout.json");
        backupDirectory = Path.Combine(root, "Backups");
    }

    public CanvasLayout Load()
    {
        TryCreateDirectories();
        var current = TryRead(layoutPath);
        if (current is not null)
        {
            var migrated = current.Version < 2;
            if (migrated)
            {
                var known = LatestV2Backup();
                if (known is not null) MergeLegacyBuiltIns(current, known);
                else InferLegacyBuiltIns(current);
            }
            return Normalize(current, migrated);
        }
        try
        {
            foreach (var backup in Directory.EnumerateFiles(backupDirectory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
            {
                var recovered = TryRead(backup);
                if (recovered is not null) return Normalize(recovered, recovered.Version < 2);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException)
        {
            // A locked backup folder must not prevent a fresh layout from starting.
        }
        return new CanvasLayout();
    }

    public bool TrySave(CanvasLayout layout, out string? error)
    {
        error = null;
        var temporaryPath = layoutPath + ".tmp";
        try
        {
            TryCreateDirectories();
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(SnapshotForWrite(layout), JsonOptions));
            if (File.Exists(layoutPath)) TryBackupCurrent();
            File.Move(temporaryPath, layoutPath, true);
            PruneBackups();
            return true;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            error = failure.Message;
            return false;
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch { }
        }
    }

    public void Save(CanvasLayout layout)
    {
        if (!TrySave(layout, out var error))
            throw new IOException(error ?? "layout.json を保存できませんでした。");
    }

    private static CanvasLayout SnapshotForWrite(CanvasLayout layout)
    {
        var snapshot = new CanvasLayout
        {
            Version = 2,
            Settings = layout.Settings ?? new CanvasSettings(),
            Items = LayerOrderMath.Normalize((layout.Items ?? []).Where(item => item is not null && CanvasContentKinds.IsSupported(item.ContentKind)).ToList())
        };
        foreach (var item in snapshot.Items) ValidateItem(item);
        return snapshot;
    }

    private static CanvasLayout Normalize(CanvasLayout layout, bool migrated)
    {
        layout.Version = 2;
        layout.Settings ??= new CanvasSettings();
        layout.Items ??= [];
        layout.Items = layout.Items.Where(item => item is not null && CanvasContentKinds.IsSupported(item.ContentKind)).ToList();
        var itemIds = new HashSet<Guid>();
        foreach (var item in layout.Items)
        {
            if (item.Id == Guid.Empty || !itemIds.Add(item.Id))
            {
                item.Id = Guid.NewGuid();
                itemIds.Add(item.Id);
            }
            ValidateItem(item);
            if (migrated && item.ContentKind == CanvasContentKinds.SystemMonitor && item.Height < 320)
            {
                item.Width = Math.Max(320, item.Width);
                item.Height = Math.Max(390, item.Height);
            }
        }
        if (!migrated)
        {
            layout.Items = LayerOrderMath.Normalize(layout.Items);
        }
        return layout;
    }

    private CanvasLayout? LatestV2Backup()
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(backupDirectory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
            {
                var item = TryRead(path);
                if (item?.Version >= 2) return item;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException)
        {
        }
        return null;
    }

    private static void MergeLegacyBuiltIns(CanvasLayout legacy, CanvasLayout known)
    {
        var previous = known.Items
            .Where(item => item.Id != Guid.Empty)
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First());
        foreach (var item in legacy.Items)
        {
            if (!previous.TryGetValue(item.Id, out var source)) continue;
            item.ContentKind = source.ContentKind;
            item.Theme = source.Theme;
            item.DecorationMode = source.DecorationMode;
            item.Clock = source.Clock?.Clone() ?? new ClockOptions();
            item.NowPlaying = source.NowPlaying?.Clone() ?? new NowPlayingOptions();
            item.SystemMonitor = source.SystemMonitor?.Clone() ?? new SystemMonitorOptions();
        }
        InferLegacyBuiltIns(legacy);
    }

    private static void InferLegacyBuiltIns(CanvasLayout layout)
    {
        foreach (var item in layout.Items)
        {
            if (string.IsNullOrWhiteSpace(item.StoredFileName))
            {
                item.ContentKind = item.DisplayName switch
                {
                    "再生中" => CanvasContentKinds.NowPlaying,
                    "時計" => CanvasContentKinds.Clock,
                    "システムモニター" or "システムステータス" => CanvasContentKinds.SystemMonitor,
                    _ => item.ContentKind
                };
            }
            if (item.ContentKind == CanvasContentKinds.Image && Path.GetExtension(item.StoredFileName).Equals(".gif", StringComparison.OrdinalIgnoreCase))
            {
                item.ContentKind = CanvasContentKinds.Gif;
            }
        }
    }

    private static void ValidateItem(CanvasItem item)
    {
        item.DisplayName ??= "";
        item.StoredFileName ??= "";
        item.MonitorDevice ??= "";
        item.DecorationMode = DecorationModes.IsSupported(item.DecorationMode) ? item.DecorationMode : DecorationModes.None;
        item.Theme = Enum.IsDefined(item.Theme) ? item.Theme : WidgetThemeKind.Auto;
        item.Clock ??= new ClockOptions();
        item.NowPlaying ??= new NowPlayingOptions();
        item.SystemMonitor ??= new SystemMonitorOptions();
        if (!Enum.IsDefined(item.Clock.Style))
        {
            item.Clock.Style = ClockStyle.Digital;
        }
        if (!Enum.IsDefined(item.Clock.SurfaceStyle))
        {
            item.Clock.SurfaceStyle = WidgetSurfaceStyle.Standard;
        }
        if (!Enum.IsDefined(item.NowPlaying.SurfaceStyle))
        {
            item.NowPlaying.SurfaceStyle = WidgetSurfaceStyle.Standard;
        }
        if (!Enum.IsDefined(item.SystemMonitor.Style))
        {
            item.SystemMonitor.Style = SystemMonitorStyle.Meters;
        }
        if (!Enum.IsDefined(item.SystemMonitor.Focus))
        {
            item.SystemMonitor.Focus = SystemMonitorFocus.Cpu;
        }
        if (!Enum.IsDefined(item.SystemMonitor.SurfaceStyle))
        {
            item.SystemMonitor.SurfaceStyle = WidgetSurfaceStyle.Standard;
        }
        item.Width = item.Width;
        item.Height = item.Height;
        item.Opacity = item.Opacity;
        item.RotationDegrees = item.RotationDegrees;
    }

    // Keep valid entries when a future or damaged individual item cannot be read.
    private static CanvasLayout? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var rootElement = document.RootElement;
            if (rootElement.ValueKind != JsonValueKind.Object) return null;
            var properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in rootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, "items", StringComparison.OrdinalIgnoreCase)) properties[property.Name] = property.Value.Clone();
            }
            using var emptyItems = JsonDocument.Parse("[]");
            properties["items"] = emptyItems.RootElement.Clone();
            var layout = JsonSerializer.Deserialize<CanvasLayout>(JsonSerializer.Serialize(properties), JsonOptions) ?? new CanvasLayout();
            var items = rootElement.EnumerateObject().FirstOrDefault(property => string.Equals(property.Name, "items", StringComparison.OrdinalIgnoreCase)).Value;
            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in items.EnumerateArray())
                {
                    try
                    {
                        var item = entry.Deserialize<CanvasItem>(JsonOptions);
                        if (item is not null) layout.Items.Add(item);
                    }
                    catch (JsonException)
                    {
                        // Corrupt item is skipped; other placements remain usable.
                    }
                }
            }
            return layout;
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (SecurityException) { return null; }
        catch (NotSupportedException) { return null; }
    }

    private void TryCreateDirectories()
    {
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(backupDirectory);
    }

    private void TryBackupCurrent()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (lastBackupDay == today) return;
        try
        {
            File.Copy(layoutPath, Path.Combine(backupDirectory, $"layout-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json"), true);
            lastBackupDay = today;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException)
        {
            // A failed backup must not block the live write.
        }
    }

    private void PruneBackups()
    {
        try
        {
            foreach (var old in Directory.EnumerateFiles(backupDirectory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Skip(10))
            {
                try { File.Delete(old); }
                catch (IOException) { }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException)
        {
        }
    }
}
