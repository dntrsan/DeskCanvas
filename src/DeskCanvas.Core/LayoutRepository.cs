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
            return Normalize(current);
        }

        foreach (var backup in Directory.EnumerateFiles(backupDirectory, "*.json")
                     .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            var recovered = TryRead(backup);
            if (recovered is not null)
            {
                return Normalize(recovered);
            }
        }

        return new CanvasLayout();
    }

    public void Save(CanvasLayout layout)
    {
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(backupDirectory);
        var temporaryPath = layoutPath + ".tmp";
        var json = JsonSerializer.Serialize(Normalize(layout), JsonOptions);
        File.WriteAllText(temporaryPath, json);

        if (File.Exists(layoutPath))
        {
            var backupName = $"layout-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json";
            File.Copy(layoutPath, Path.Combine(backupDirectory, backupName), overwrite: true);
        }

        File.Move(temporaryPath, layoutPath, overwrite: true);
        PruneBackups();
    }

    private static CanvasLayout Normalize(CanvasLayout layout)
    {
        layout.Version = 1;
        layout.Settings ??= new CanvasSettings();
        layout.Items ??= [];
        foreach (var item in layout.Items)
        {
            item.DisplayName ??= "";
            item.StoredFileName ??= "";
            item.MediaKind = item.MediaKind == "gif" ? "gif" : "image";
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
            return File.Exists(path)
                ? JsonSerializer.Deserialize<CanvasLayout>(File.ReadAllText(path), JsonOptions)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private void PruneBackups()
    {
        foreach (var old in Directory.EnumerateFiles(backupDirectory, "*.json")
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .Skip(10))
        {
            try
            {
                File.Delete(old);
            }
            catch (IOException)
            {
                // A backup being held by another process is harmless.
            }
        }
    }
}
