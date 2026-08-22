using System.Text.Json;

namespace DeskCanvas.Core;

public sealed class LayoutRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string root, layoutPath, backupDirectory;
    public LayoutRepository(string root) { this.root = root; layoutPath = Path.Combine(root, "layout.json"); backupDirectory = Path.Combine(root, "Backups"); }
    public CanvasLayout Load()
    {
        Directory.CreateDirectory(root); Directory.CreateDirectory(backupDirectory);
        var current = TryRead(layoutPath); if (current is not null) return Normalize(current);
        foreach (var backup in Directory.EnumerateFiles(backupDirectory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc)) { var recovered = TryRead(backup); if (recovered is not null) return Normalize(recovered); }
        return new CanvasLayout();
    }
    public void Save(CanvasLayout layout)
    {
        Directory.CreateDirectory(root); Directory.CreateDirectory(backupDirectory);
        var temporaryPath = layoutPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(Normalize(layout), JsonOptions));
        if (File.Exists(layoutPath)) File.Copy(layoutPath, Path.Combine(backupDirectory, $"layout-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json"), true);
        File.Move(temporaryPath, layoutPath, true); PruneBackups();
    }
    private static CanvasLayout Normalize(CanvasLayout layout)
    {
        layout.Version = 2; layout.Settings ??= new CanvasSettings(); layout.Items ??= [];
        layout.Items = layout.Items.Where(item => item is not null && CanvasContentKinds.IsSupported(item.ContentKind)).ToList();
        var ids = new HashSet<Guid>();
        foreach (var item in layout.Items)
        {
            if (item.Id == Guid.Empty || !ids.Add(item.Id)) { item.Id = Guid.NewGuid(); ids.Add(item.Id); }
            item.DisplayName ??= ""; item.StoredFileName ??= ""; item.MonitorDevice ??= "";
            item.DecorationMode = DecorationModes.IsSupported(item.DecorationMode) ? item.DecorationMode : DecorationModes.None;
            item.Clock ??= new ClockOptions(); item.NowPlaying ??= new NowPlayingOptions(); item.SystemMonitor ??= new SystemMonitorOptions();
            if (!Enum.IsDefined(item.Clock.Style)) item.Clock.Style = ClockStyle.Digital;
            item.Width = item.Width; item.Height = item.Height; item.Opacity = item.Opacity; item.RotationDegrees = item.RotationDegrees;
        }
        layout.Items = LayerOrderMath.Normalize(layout.Items);
        return layout;
    }
    private static CanvasLayout? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path)); var rootElement = document.RootElement;
            if (rootElement.ValueKind != JsonValueKind.Object) return null;
            var properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in rootElement.EnumerateObject()) if (!string.Equals(property.Name, "items", StringComparison.OrdinalIgnoreCase)) properties[property.Name] = property.Value.Clone();
            using var emptyItems = JsonDocument.Parse("[]"); properties["items"] = emptyItems.RootElement.Clone();
            var layout = JsonSerializer.Deserialize<CanvasLayout>(JsonSerializer.Serialize(properties), JsonOptions) ?? new CanvasLayout();
            var items = rootElement.EnumerateObject().FirstOrDefault(property => string.Equals(property.Name, "items", StringComparison.OrdinalIgnoreCase)).Value;
            if (items.ValueKind == JsonValueKind.Array) foreach (var entry in items.EnumerateArray()) try { var item = entry.Deserialize<CanvasItem>(JsonOptions); if (item is not null) layout.Items.Add(item); } catch (JsonException) { }
            return layout;
        }
        catch (JsonException) { return null; } catch (IOException) { return null; }
    }
    private void PruneBackups() { foreach (var old in Directory.EnumerateFiles(backupDirectory, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Skip(10)) try { File.Delete(old); } catch (IOException) { } }
}
