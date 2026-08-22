using System.Text.Json;

namespace DeskCanvas.Core;

// Reads v1 layouts without losing the built-in widgets that v1 called images.
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

    public LayoutRepository(string root)
    {
        this.root = root;
        layoutPath = Path.Combine(root, "layout.json");
        backupDirectory = Path.Combine(root, "Backups");
    }

    public CanvasLayout Load()
    {
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(backupDirectory);
        var current = TryRead(layoutPath);
        if (current is not null)
        {
            var migrated = current.Version < 2;
            if (migrated)
            {
                var knownGood = LatestV2Backup();
                if (knownGood is not null)
                    MergeLegacyBuiltIns(current, knownGood);
                else
                    InferLegacyBuiltIns(current);
            }
            return Normalize(current, migrated);
        }

        foreach (var path in Directory.EnumerateFiles(backupDirectory, "*.json")
                     .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            var recovered = TryRead(path);
            if (recovered is not null) return Normalize(recovered, recovered.Version < 2);
        }
        return new CanvasLayout();
    }

    public void Save(CanvasLayout layout)
    {
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(backupDirectory);
        var temporaryPath = layoutPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(Normalize(layout, false), JsonOptions));
        if (File.Exists(layoutPath))
            File.Copy(layoutPath, Path.Combine(backupDirectory, $"layout-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json"), true);
        File.Move(temporaryPath, layoutPath, true);
        PruneBackups();
    }

    private CanvasLayout? LatestV2Backup()
    {
        foreach (var path in Directory.EnumerateFiles(backupDirectory, "*.json")
                     .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            var candidate = TryRead(path);
            if (candidate?.Version >= 2) return candidate;
        }
        return null;
    }

    private static void MergeLegacyBuiltIns(CanvasLayout legacy, CanvasLayout knownGood)
    {
        var previous = knownGood.Items
            .Where(item => item.Id != Guid.Empty)
            .ToDictionary(item => item.Id);
        foreach (var item in legacy.Items)
        {
            if (!previous.TryGetValue(item.Id, out var source)) continue;
            // v1 current layout owns live geometry; v2 backup owns the widget identity/options.
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
            var noMedia = string.IsNullOrWhiteSpace(item.StoredFileName);
            if (noMedia)
            {
                item.ContentKind = item.DisplayName switch
                {
                    "再生中" => CanvasContentKinds.NowPlaying,
                    "時計" => CanvasContentKinds.Clock,
                    "システムモニター" or "システムステータス" => CanvasContentKinds.SystemMonitor,
                    _ => item.ContentKind
                };
            }
            if (item.ContentKind == CanvasContentKinds.Image &&
                Path.GetExtension(item.StoredFileName).Equals(".gif", StringComparison.OrdinalIgnoreCase))
                item.ContentKind = CanvasContentKinds.Gif;
        }
    }

    private static CanvasLayout Normalize(CanvasLayout layout, bool migrated)
    {
        layout.Version = 2;
        layout.Settings ??= new CanvasSettings();
        layout.Items ??= [];
        layout.Items = layout.Items.Where(item => item is not null && CanvasContentKinds.IsSupported(item.ContentKind)).ToList();
        var ids = new HashSet<Guid>();
        foreach (var item in layout.Items)
        {
            if (item.Id == Guid.Empty || !ids.Add(item.Id))
            {
                item.Id = Guid.NewGuid();
                ids.Add(item.Id);
            }
            item.DisplayName ??= "";
            item.StoredFileName ??= "";
            item.MonitorDevice ??= "";
            item.DecorationMode = DecorationModes.IsSupported(item.DecorationMode)
                ? item.DecorationMode : DecorationModes.None;
            item.Theme = Enum.IsDefined(item.Theme) ? item.Theme : WidgetThemeKind.Auto;
            item.Clock ??= new ClockOptions();
            item.NowPlaying ??= new NowPlayingOptions();
            item.SystemMonitor ??= new SystemMonitorOptions();
            if (!Enum.IsDefined(item.Clock.Style)) item.Clock.Style = ClockStyle.Digital;
            if (migrated && item.ContentKind == CanvasContentKinds.SystemMonitor && item.Height < 320)
            {
                item.Width = Math.Max(320, item.Width);
                item.Height = Math.Max(390, item.Height);
            }
            item.Width = item.Width;
            item.Height = item.Height;
            item.Opacity = item.Opacity;
            item.RotationDegrees = item.RotationDegrees;
        }
        return layout;
    }

    private static CanvasLayout? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            var properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
                if (!string.Equals(property.Name, "items", StringComparison.OrdinalIgnoreCase))
                    properties[property.Name] = property.Value.Clone();
            using var emptyItems = JsonDocument.Parse("[]");
            properties["items"] = emptyItems.RootElement.Clone();
            var layout = JsonSerializer.Deserialize<CanvasLayout>(JsonSerializer.Serialize(properties), JsonOptions)
                ?? new CanvasLayout();
            var items = document.RootElement.EnumerateObject()
                .FirstOrDefault(property => string.Equals(property.Name, "items", StringComparison.OrdinalIgnoreCase)).Value;
            if (items.ValueKind == JsonValueKind.Array)
                foreach (var entry in items.EnumerateArray())
                    try
                    {
                        var item = entry.Deserialize<CanvasItem>(JsonOptions);
                        if (item is not null) layout.Items.Add(item);
                    }
                    catch (JsonException) { }
            return layout;
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }

    private void PruneBackups()
    {
        foreach (var old in Directory.EnumerateFiles(backupDirectory, "*.json")
                     .OrderByDescending(File.GetLastWriteTimeUtc).Skip(10))
            try { File.Delete(old); } catch (IOException) { }
    }
}
