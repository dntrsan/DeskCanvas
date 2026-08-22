using System.Security.Cryptography;
using System.Text.Json;

namespace DeskCanvas.Core;

/// <summary>Imports an explicitly selected legacy root by merging items; it never replaces a layout.</summary>
public static class LegacyLayoutImportService
{
    public static LegacyLayoutImportResult Import(string selectedPath, string destinationRoot, IReadOnlyCollection<CanvasItem> currentItems)
    {
        if (string.IsNullOrWhiteSpace(selectedPath) || string.IsNullOrWhiteSpace(destinationRoot)) return LegacyLayoutImportResult.Rejected("引き継ぎ元を選択してください。");
        try
        {
            var layoutPath = ResolveLayoutPath(selectedPath);
            var sourceRoot = Path.GetDirectoryName(layoutPath)!;
            var destination = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), destination, StringComparison.OrdinalIgnoreCase)) return LegacyLayoutImportResult.Rejected("現在使用中のデータは引き継ぎ元に選べません。");
            if (!File.Exists(layoutPath)) return LegacyLayoutImportResult.Rejected("選択した場所に layout.json がありません。");
            using (var document = JsonDocument.Parse(File.ReadAllText(layoutPath)))
            {
                var items = document.RootElement.ValueKind == JsonValueKind.Object
                    ? document.RootElement.EnumerateObject().FirstOrDefault(property => string.Equals(property.Name, "items", StringComparison.OrdinalIgnoreCase)).Value
                    : default;
                if (items.ValueKind != JsonValueKind.Array) return LegacyLayoutImportResult.Rejected("layout.json の形式が正しくありません。");
            }
            var source = new LayoutRepository(sourceRoot).Load();
            var mediaDirectory = Path.Combine(destination, "Media"); Directory.CreateDirectory(mediaDirectory);
            var ids = currentItems.Select(item => item.Id).Where(id => id != Guid.Empty).ToHashSet();
            var builtIns = currentItems.Where(item => IsBuiltIn(item.ContentKind)).Select(item => item.ContentKind).ToHashSet(StringComparer.Ordinal);
            var hashes = KnownHashes(currentItems, mediaDirectory); var result = new LegacyLayoutImportResult();
            foreach (var item in source.Items)
            {
                if (!CanvasContentKinds.IsSupported(item.ContentKind)) { result.SkippedInvalid++; continue; }
                if (item.Id != Guid.Empty && ids.Contains(item.Id)) { result.SkippedDuplicate++; continue; }
                if (IsBuiltIn(item.ContentKind))
                {
                    if (builtIns.Contains(item.ContentKind)) { result.SkippedDuplicate++; continue; }
                    var added = Clone(item); RepairId(added, ids); added.ZIndex = NextZ(currentItems, result.Items); result.Items.Add(added); ids.Add(added.Id); builtIns.Add(added.ContentKind); continue;
                }
                if (item.ContentKind is not (CanvasContentKinds.Image or CanvasContentKinds.Gif) || string.IsNullOrWhiteSpace(item.StoredFileName)) { result.SkippedInvalid++; continue; }
                var sourceMedia = Path.Combine(sourceRoot, "Media", Path.GetFileName(item.StoredFileName));
                if (!File.Exists(sourceMedia)) { result.MissingMedia++; continue; }
                if (!IsSupportedMedia(sourceMedia)) { result.SkippedInvalid++; continue; }
                var hash = Hash(sourceMedia);
                if (hashes.Contains(hash)) { result.SkippedDuplicate++; continue; }
                var addedMedia = Clone(item); addedMedia.StoredFileName = CopyMedia(sourceMedia, mediaDirectory, hash); RepairId(addedMedia, ids); addedMedia.ZIndex = NextZ(currentItems, result.Items);
                result.Items.Add(addedMedia); ids.Add(addedMedia.Id); hashes.Add(hash); result.CopiedMedia++;
            }
            return result;
        }
        catch (JsonException) { return LegacyLayoutImportResult.Rejected("layout.json を読み取れませんでした。"); }
        catch (IOException) { return LegacyLayoutImportResult.Rejected("旧データのファイルを読み取れませんでした。"); }
        catch (UnauthorizedAccessException) { return LegacyLayoutImportResult.Rejected("旧データへアクセスできませんでした。"); }
    }
    private static string ResolveLayoutPath(string selected) { var path = Path.GetFullPath(selected); return Directory.Exists(path) ? Path.Combine(path, "layout.json") : path; }
    private static bool IsBuiltIn(string kind) => kind is CanvasContentKinds.Clock or CanvasContentKinds.NowPlaying or CanvasContentKinds.SystemMonitor;
    private static bool IsSupportedMedia(string path) => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" or ".gif";
    private static int NextZ(IEnumerable<CanvasItem> current, IEnumerable<CanvasItem> added) => current.Concat(added).Select(item => item.ZIndex).DefaultIfEmpty(-1).Max() + 1;
    private static HashSet<string> KnownHashes(IEnumerable<CanvasItem> items, string mediaDirectory)
    {
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items.Where(item => item.ContentKind is CanvasContentKinds.Image or CanvasContentKinds.Gif))
        { var path = Path.Combine(mediaDirectory, Path.GetFileName(item.StoredFileName)); if (File.Exists(path)) hashes.Add(Hash(path)); }
        return hashes;
    }
    private static string CopyMedia(string source, string destinationDirectory, string sourceHash)
    {
        var preferred = Path.GetFileName(source); var candidate = preferred; var stem = Path.GetFileNameWithoutExtension(preferred); var extension = Path.GetExtension(preferred); var number = 1;
        while (true) { var destination = Path.Combine(destinationDirectory, candidate); if (!File.Exists(destination)) { File.Copy(source, destination); return candidate; } if (string.Equals(Hash(destination), sourceHash, StringComparison.Ordinal)) return candidate; candidate = $"{stem}-legacy-{number++}{extension}"; }
    }
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static void RepairId(CanvasItem item, HashSet<Guid> ids) { if (item.Id == Guid.Empty || ids.Contains(item.Id)) item.Id = Guid.NewGuid(); }
    private static CanvasItem Clone(CanvasItem item) => new() { Id = item.Id, DisplayName = item.DisplayName, StoredFileName = item.StoredFileName, ContentKind = item.ContentKind, DecorationMode = item.DecorationMode, Theme = item.Theme, Clock = item.Clock?.Clone() ?? new(), NowPlaying = item.NowPlaying?.Clone() ?? new(), SystemMonitor = item.SystemMonitor?.Clone() ?? new(), MonitorDevice = item.MonitorDevice, CenterX = item.CenterX, CenterY = item.CenterY, Width = item.Width, Height = item.Height, RotationDegrees = item.RotationDegrees, Opacity = item.Opacity, IsFlipped = item.IsFlipped, IsLocked = item.IsLocked, ZIndex = item.ZIndex };
}

public sealed class LegacyLayoutImportResult
{
    public List<CanvasItem> Items { get; } = []; public int CopiedMedia { get; internal set; } public int SkippedDuplicate { get; internal set; } public int MissingMedia { get; internal set; } public int SkippedInvalid { get; internal set; } public string? Error { get; private init; } public bool IsRejected => Error is not null;
    public static LegacyLayoutImportResult Rejected(string message) => new() { Error = message };
    public string JapaneseSummary() => IsRejected ? Error! : $"旧データを引き継ぎました。追加 {Items.Count}件、素材コピー {CopiedMedia}件、重複をスキップ {SkippedDuplicate}件、見つからない素材 {MissingMedia}件、不正な項目 {SkippedInvalid}件。";
}
