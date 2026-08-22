using System.Runtime.CompilerServices;
using DeskCanvas.Core;

internal static class V29LegacyImportContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        Case("merge preserves current and imports media", TestMerge);
        Case("rerun and built-ins are deduplicated", TestRerunAndBuiltIns);
        Case("ID and media name collisions are repaired", TestCollisions);
        Case("invalid self and missing media are safe", TestFailures);
    }

    private static void TestMerge()
    {
        InTemp((source, destination) =>
        {
            WriteLayout(source, new CanvasItem { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), ContentKind = CanvasContentKinds.Image, DisplayName = "old", StoredFileName = "old.png" });
            File.WriteAllText(Path.Combine(source, "Media", "old.png"), "old-image");
            var current = new[] { new CanvasItem { ContentKind = CanvasContentKinds.Clock, DisplayName = "時計" } };
            var result = LegacyLayoutImportService.Import(source, destination, current);
            Equal(false, result.IsRejected); Equal(1, result.Items.Count); Equal("old.png", result.Items[0].StoredFileName); Equal(1, result.CopiedMedia);
            Equal(true, File.Exists(Path.Combine(destination, "Media", "old.png")));
        });
    }

    private static void TestRerunAndBuiltIns()
    {
        InTemp((source, destination) =>
        {
            var image = new CanvasItem { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), ContentKind = CanvasContentKinds.Image, StoredFileName = "same.png" };
            WriteLayout(source, image, new CanvasItem { ContentKind = CanvasContentKinds.Clock, DisplayName = "時計" });
            File.WriteAllText(Path.Combine(source, "Media", "same.png"), "same");
            var first = LegacyLayoutImportService.Import(source, destination, []);
            var current = first.Items.Concat([new CanvasItem { ContentKind = CanvasContentKinds.Clock }]).ToArray();
            var second = LegacyLayoutImportService.Import(source, destination, current);
            Equal(0, second.Items.Count); Equal(2, second.SkippedDuplicate);
        });
    }

    private static void TestCollisions()
    {
        InTemp((source, destination) =>
        {
            var shared = Guid.Parse("33333333-3333-3333-3333-333333333333");
            WriteLayout(source, new CanvasItem { Id = shared, ContentKind = CanvasContentKinds.Image, StoredFileName = "same.png" });
            File.WriteAllText(Path.Combine(source, "Media", "same.png"), "legacy-bytes");
            Directory.CreateDirectory(Path.Combine(destination, "Media"));
            File.WriteAllText(Path.Combine(destination, "Media", "same.png"), "current-bytes");
            var current = new[] { new CanvasItem { Id = shared, ContentKind = CanvasContentKinds.Image, StoredFileName = "current.png" } };
            var result = LegacyLayoutImportService.Import(source, destination, current);
            Equal(0, result.Items.Count); // ID is a stable duplicate identity.
            var collision = LegacyLayoutImportService.Import(source, destination, []);
            Equal(1, collision.Items.Count); Equal("same-legacy-1.png", collision.Items.Single().StoredFileName); Equal(true, File.Exists(Path.Combine(destination, "Media", "same-legacy-1.png")));
        });
    }

    private static void TestFailures()
    {
        InTemp((source, destination) =>
        {
            WriteLayout(source, new CanvasItem { ContentKind = CanvasContentKinds.Image, StoredFileName = "gone.png" });
            var missing = LegacyLayoutImportService.Import(source, destination, []);
            Equal(0, missing.Items.Count); Equal(1, missing.MissingMedia);
            var self = LegacyLayoutImportService.Import(destination, destination, []);
            Equal(true, self.IsRejected);
            File.WriteAllText(Path.Combine(source, "layout.json"), "{ broken");
            Equal(true, LegacyLayoutImportService.Import(source, destination, []).IsRejected);
        });
    }

    private static void WriteLayout(string root, params CanvasItem[] items)
    {
        Directory.CreateDirectory(Path.Combine(root, "Media"));
        new LayoutRepository(root).Save(new CanvasLayout { Items = items.ToList() });
    }

    private static void InTemp(Action<string, string> action)
    {
        var basePath = Path.Combine(Path.GetTempPath(), "DeskCanvasImportTests", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(basePath, "legacy"); var destination = Path.Combine(basePath, "current");
        Directory.CreateDirectory(source); Directory.CreateDirectory(destination);
        try { action(source, destination); }
        finally { Directory.Delete(basePath, true); }
    }

    private static void Case(string name, Action action) { action(); Console.WriteLine($"PASS v1.2 import {name}"); }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"expected={expected}, actual={actual}"); }
}
