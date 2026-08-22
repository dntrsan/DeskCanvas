using System.Security.Cryptography;
using System.Text.Json;

namespace DeskCanvas.Core;

public static class LegacyLayoutImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    public static LegacyLayoutImportResult Import(string selectedPath, string destinationRoot, IReadOnlyCollection<CanvasItem> currentItems)
    {
        if (string.IsNullOrWhiteSpace(selectedPath) || string.IsNullOrWhiteSpace(destinationRoot)) return LegacyLayoutImportResult.Rejected("引き継ぎ元を選択してください。");
        try
        {
            var layoutPath = ResolveLayoutPath(selectedPath); var sourceRoot = DataRootForLayout(layoutPath); var destination = Clean(destinationRoot);
            if (string.Equals(Clean(sourceRoot), destination, StringComparison.OrdinalIgnoreCase)) return LegacyLayoutImportResult.Rejected("現在使用中のデータは引き継ぎ元に選べません。");
            var source = ReadSelectedLayout(layoutPath); var mediaDirectory = Path.Combine(destination, "Media"); Directory.CreateDirectory(mediaDirectory);
            var ids = currentItems.Select(item => item.Id).Where(id => id != Guid.Empty).ToHashSet(); var builtIns = currentItems.Where(item => BuiltIn(item.ContentKind)).Select(item => item.ContentKind).ToHashSet(StringComparer.Ordinal); var hashes = KnownHashes(currentItems, mediaDirectory); var result = new LegacyLayoutImportResult();
            foreach (var item in source.Items ?? [])
            {
                if (!CanvasContentKinds.IsSupported(item.ContentKind)) { result.SkippedInvalid++; continue; }
                if (BuiltIn(item.ContentKind)) { if (builtIns.Contains(item.ContentKind)) { result.SkippedDuplicate++; continue; } Add(result, Clone(item), ids, currentItems); builtIns.Add(item.ContentKind); continue; }
                if (item.ContentKind is not (CanvasContentKinds.Image or CanvasContentKinds.Gif) || string.IsNullOrWhiteSpace(item.StoredFileName)) { result.SkippedInvalid++; continue; }
                var sourceMedia = Path.Combine(sourceRoot, "Media", Path.GetFileName(item.StoredFileName)); if (!File.Exists(sourceMedia)) { result.MissingMedia++; continue; } if (!Supported(sourceMedia)) { result.SkippedInvalid++; continue; }
                var hash = Hash(sourceMedia); if (hashes.Contains(hash)) { result.SkippedDuplicate++; continue; }
                var imported = Clone(item); imported.StoredFileName = CopyMedia(sourceMedia, mediaDirectory, hash); Add(result, imported, ids, currentItems); hashes.Add(hash); result.CopiedMedia++;
            }
            return result;
        }
        catch (JsonException) { return LegacyLayoutImportResult.Rejected("layout.json を読み取れませんでした。"); }
        catch (IOException) { return LegacyLayoutImportResult.Rejected("旧データのファイルを読み取れませんでした。"); }
        catch (UnauthorizedAccessException) { return LegacyLayoutImportResult.Rejected("旧データへアクセスできませんでした。"); }
    }
    private static string ResolveLayoutPath(string selected) { var path = Path.GetFullPath(selected); return Directory.Exists(path) ? Path.Combine(path, "layout.json") : path; }
    private static string DataRootForLayout(string layoutPath) { var parent = Path.GetDirectoryName(layoutPath)!; return string.Equals(Path.GetFileName(parent), "Backups", StringComparison.OrdinalIgnoreCase) ? Directory.GetParent(parent)?.FullName ?? parent : parent; }
    private static string Clean(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    private static CanvasLayout ReadSelectedLayout(string path) { if (!File.Exists(path)) throw new IOException(); var json = File.ReadAllText(path); using var document = JsonDocument.Parse(json); if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.EnumerateObject().Any(property => string.Equals(property.Name, "items", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Array)) throw new JsonException(); return JsonSerializer.Deserialize<CanvasLayout>(json, JsonOptions) ?? throw new JsonException(); }
    private static bool BuiltIn(string kind) => kind is CanvasContentKinds.Clock or CanvasContentKinds.NowPlaying or CanvasContentKinds.SystemMonitor or CanvasContentKinds.CodexUsage;
    private static bool Supported(string path) => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" or ".gif";
    private static void Add(LegacyLayoutImportResult result, CanvasItem item, HashSet<Guid> ids, IReadOnlyCollection<CanvasItem> current) { if (item.Id == Guid.Empty || ids.Contains(item.Id)) item.Id = Guid.NewGuid(); item.ZIndex = current.Concat(result.Items).Select(candidate => candidate.ZIndex).DefaultIfEmpty(-1).Max() + 1; result.Items.Add(item); ids.Add(item.Id); }
    private static HashSet<string> KnownHashes(IEnumerable<CanvasItem> items, string directory) { var hashes = new HashSet<string>(StringComparer.Ordinal); foreach (var item in items.Where(item => item.ContentKind is CanvasContentKinds.Image or CanvasContentKinds.Gif)) { var path = Path.Combine(directory, Path.GetFileName(item.StoredFileName)); if (File.Exists(path)) hashes.Add(Hash(path)); } return hashes; }
    private static string CopyMedia(string source, string directory, string hash) { var name = Path.GetFileName(source); var candidate = name; var stem = Path.GetFileNameWithoutExtension(name); var extension = Path.GetExtension(name); var suffix = 1; while (true) { var destination = Path.Combine(directory, candidate); if (!File.Exists(destination)) { File.Copy(source, destination); return candidate; } if (string.Equals(Hash(destination), hash, StringComparison.Ordinal)) return candidate; candidate = $"{stem}-legacy-{suffix++}{extension}"; } }
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    private static CanvasItem Clone(CanvasItem item) => new() { Id = item.Id, DisplayName = item.DisplayName, StoredFileName = item.StoredFileName, ContentKind = item.ContentKind, DecorationMode = item.DecorationMode, Theme = item.Theme, Clock = item.Clock?.Clone() ?? new(), NowPlaying = item.NowPlaying?.Clone() ?? new(), SystemMonitor = item.SystemMonitor?.Clone() ?? new(), MonitorDevice = item.MonitorDevice, CenterX = item.CenterX, CenterY = item.CenterY, Width = item.Width, Height = item.Height, RotationDegrees = item.RotationDegrees, Opacity = item.Opacity, IsFlipped = item.IsFlipped, IsLocked = item.IsLocked, ZIndex = item.ZIndex };
}

public sealed class LegacyLayoutImportResult
{
    public List<CanvasItem> Items { get; } = []; public int CopiedMedia { get; internal set; } public int SkippedDuplicate { get; internal set; } public int MissingMedia { get; internal set; } public int SkippedInvalid { get; internal set; } public string? Error { get; private init; } public bool IsRejected => Error is not null;
    public static LegacyLayoutImportResult Rejected(string message) => new() { Error = message };
    public string JapaneseSummary() => IsRejected ? Error! : $"旧データを引き継ぎました。追加 {Items.Count}件、素材コピー {CopiedMedia}件、重複をスキップ {SkippedDuplicate}件、見つからない素材 {MissingMedia}件、不正な項目 {SkippedInvalid}件。";
}
