using System.Security.Cryptography;
using System.Text.Json;

namespace DeskCanvas.Core;

/// <summary>
/// Imports a deliberately selected DeskCanvas data root without replacing the current layout.
/// The service is filesystem-only so it can be exercised without WPF or a live desktop instance.
/// </summary>
public static class LegacyLayoutImportService
{
    public static LegacyLayoutImportResult Import(string selectedPath, string destinationRoot, IReadOnlyCollection<CanvasItem> currentItems)
    {
        if (string.IsNullOrWhiteSpace(selectedPath) || string.IsNullOrWhiteSpace(destinationRoot))
            return LegacyLayoutImportResult.Rejected("引き継ぎ元を選択してください。");

        try
        {
            var layoutPath = ResolveLayoutPath(selectedPath);
            var sourceRoot = Path.GetDirectoryName(layoutPath)!;
            var destination = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), destination, StringComparison.OrdinalIgnoreCase))
                return LegacyLayoutImportResult.Rejected("現在使用中のデータは引き継ぎ元に選べません。");
            if (!File.Exists(layoutPath)) return LegacyLayoutImportResult.Rejected("選択した場所に layout.json がありません。");
            using (var document = JsonDocument.Parse(File.ReadAllText(layoutPath)))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                    return LegacyLayoutImportResult.Rejected("layout.json の形式が正しくありません。");
            }

            var source = new LayoutRepository(sourceRoot).Load();
            var destinationMedia = Path.Combine(destination, "Media");
            Directory.CreateDirectory(destinationMedia);
            var knownIds = currentItems.Select(item => item.Id).Where(id => id != Guid.Empty).ToHashSet();
            var knownBuiltIns = currentItems.Where(item => IsBuiltIn(item.ContentKind)).Select(item => item.ContentKind).ToHashSet(StringComparer.Ordinal);
            var knownMediaHashes = BuildKnownMediaHashes(currentItems, destinationMedia);
            var result = new LegacyLayoutImportResult();

            foreach (var sourceItem in source.Items)
            {
                if (!CanvasContentKinds.IsSupported(sourceItem.ContentKind)) { result.SkippedInvalid++; continue; }
                if (sourceItem.Id != Guid.Empty && knownIds.Contains(sourceItem.Id)) { result.SkippedDuplicate++; continue; }

                if (IsBuiltIn(sourceItem.ContentKind))
                {
                    if (knownBuiltIns.Contains(sourceItem.ContentKind)) { result.SkippedDuplicate++; continue; }
                    var importedBuiltIn = Clone(sourceItem);
                    RepairId(importedBuiltIn, knownIds);
                    importedBuiltIn.ZIndex = NextZIndex(currentItems, result.Items);
                    result.Items.Add(importedBuiltIn);
                    knownIds.Add(importedBuiltIn.Id);
                    knownBuiltIns.Add(importedBuiltIn.ContentKind);
                    continue;
                }

                if (sourceItem.ContentKind is not (CanvasContentKinds.Image or CanvasContentKinds.Gif) || string.IsNullOrWhiteSpace(sourceItem.StoredFileName))
                {
                    result.SkippedInvalid++;
                    continue;
                }

                var sourceMedia = Path.Combine(sourceRoot, "Media", Path.GetFileName(sourceItem.StoredFileName));
                if (!File.Exists(sourceMedia)) { result.MissingMedia++; continue; }
                if (!IsSupportedMedia(sourceMedia)) { result.SkippedInvalid++; continue; }

                var hash = Hash(sourceMedia);
                if (knownMediaHashes.Contains(hash)) { result.SkippedDuplicate++; continue; }
                var destinationName = CopyMedia(sourceMedia, destinationMedia, hash);
                var importedMedia = Clone(sourceItem);
                importedMedia.StoredFileName = destinationName;
                RepairId(importedMedia, knownIds);
                importedMedia.ZIndex = NextZIndex(currentItems, result.Items);
                result.Items.Add(importedMedia);
                knownIds.Add(importedMedia.Id);
                knownMediaHashes.Add(hash);
                result.CopiedMedia++;
            }
            return result;
        }
        catch (JsonException) { return LegacyLayoutImportResult.Rejected("layout.json を読み取れませんでした。"); }
        catch (IOException) { return LegacyLayoutImportResult.Rejected("旧データのファイルを読み取れませんでした。"); }
        catch (UnauthorizedAccessException) { return LegacyLayoutImportResult.Rejected("旧データへアクセスできませんでした。"); }
    }

    private static string ResolveLayoutPath(string selectedPath)
    {
        var full = Path.GetFullPath(selectedPath);
        return Directory.Exists(full) ? Path.Combine(full, "layout.json") : full;
    }

    private static bool IsBuiltIn(string kind) => kind is CanvasContentKinds.Clock or CanvasContentKinds.NowPlaying or CanvasContentKinds.SystemMonitor;
    private static bool IsSupportedMedia(string path) => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" or ".gif";
    private static int NextZIndex(IReadOnlyCollection<CanvasItem> current, IReadOnlyCollection<CanvasItem> imported) =>
        current.Concat(imported).Select(item => item.ZIndex).DefaultIfEmpty(-1).Max() + 1;

    private static HashSet<string> BuildKnownMediaHashes(IEnumerable<CanvasItem> items, string mediaDirectory)
    {
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items.Where(item => item.ContentKind is CanvasContentKinds.Image or CanvasContentKinds.Gif))
        {
            var path = Path.Combine(mediaDirectory, Path.GetFileName(item.StoredFileName));
            if (File.Exists(path)) hashes.Add(Hash(path));
        }
        return hashes;
    }

    private static string CopyMedia(string source, string destinationDirectory, string sourceHash)
    {
        var preferred = Path.GetFileName(source);
        var candidate = preferred;
        var baseName = Path.GetFileNameWithoutExtension(preferred);
        var extension = Path.GetExtension(preferred);
        var ordinal = 1;
        while (true)
        {
            var destination = Path.Combine(destinationDirectory, candidate);
            if (!File.Exists(destination)) { File.Copy(source, destination); return candidate; }
            if (string.Equals(Hash(destination), sourceHash, StringComparison.Ordinal)) return candidate;
            candidate = $"{baseName}-legacy-{ordinal++}{extension}";
        }
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void RepairId(CanvasItem item, HashSet<Guid> knownIds)
    {
        if (item.Id == Guid.Empty || knownIds.Contains(item.Id)) item.Id = Guid.NewGuid();
    }

    private static CanvasItem Clone(CanvasItem source) => new()
    {
        Id = source.Id, DisplayName = source.DisplayName, StoredFileName = source.StoredFileName, ContentKind = source.ContentKind,
        DecorationMode = source.DecorationMode, Theme = source.Theme, Clock = source.Clock?.Clone() ?? new ClockOptions(),
        NowPlaying = source.NowPlaying?.Clone() ?? new NowPlayingOptions(), SystemMonitor = source.SystemMonitor?.Clone() ?? new SystemMonitorOptions(),
        MonitorDevice = source.MonitorDevice, CenterX = source.CenterX, CenterY = source.CenterY, Width = source.Width, Height = source.Height,
        RotationDegrees = source.RotationDegrees, Opacity = source.Opacity, IsFlipped = source.IsFlipped, IsLocked = source.IsLocked, ZIndex = source.ZIndex
    };
}

public sealed class LegacyLayoutImportResult
{
    public List<CanvasItem> Items { get; } = [];
    public int CopiedMedia { get; internal set; }
    public int SkippedDuplicate { get; internal set; }
    public int MissingMedia { get; internal set; }
    public int SkippedInvalid { get; internal set; }
    public string? Error { get; private init; }
    public bool IsRejected => Error is not null;
    public static LegacyLayoutImportResult Rejected(string message) => new() { Error = message };
    public string JapaneseSummary() => IsRejected ? Error! : $"旧データを引き継ぎました。追加 {Items.Count}件、素材コピー {CopiedMedia}件、重複をスキップ {SkippedDuplicate}件、見つからない素材 {MissingMedia}件、不正な項目 {SkippedInvalid}件。";
}
