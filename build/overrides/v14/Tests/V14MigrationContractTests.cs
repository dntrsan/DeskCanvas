using System.Runtime.CompilerServices;
using System.Text.Json;
using DeskCanvas.Core;

internal static class V14MigrationContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        Test("v1 built-ins recover from v2 backup", BackupMerge);
        Test("v1 built-ins fall back without backup", HeuristicFallback);
    }

    private static void BackupMerge()
    {
        var folder = NewFolder();
        try
        {
            var id = Guid.NewGuid();
            Directory.CreateDirectory(Path.Combine(folder, "Backups"));
            Write(Path.Combine(folder, "layout.json"), new
            {
                Version = 1,
                Items = new[] { new { Id = id, DisplayName = "再生中", MediaKind = "image", StoredFileName = "", Width = 360, Height = 220, Opacity = .75 } }
            });
            Write(Path.Combine(folder, "Backups", "layout-v2.json"), new
            {
                Version = 2,
                Items = new[]
                {
                    new { Id = id, DisplayName = "再生中", ContentKind = "nowPlaying", Theme = 5, NowPlaying = new { ShowAlbumArt = false, ShowTimeline = true, ShowSourceApp = false } }
                }
            });
            var item = new LayoutRepository(folder).Load().Items.Single();
            Equal(CanvasContentKinds.NowPlaying, item.ContentKind);
            Equal(WidgetThemeKind.Mint, item.Theme);
            Equal(false, item.NowPlaying.ShowAlbumArt);
            Equal(.75, item.Opacity);
        }
        finally { Directory.Delete(folder, true); }
    }

    private static void HeuristicFallback()
    {
        var folder = NewFolder();
        try
        {
            Write(Path.Combine(folder, "layout.json"), new
            {
                Version = 1,
                Items = new object[]
                {
                    new { Id = Guid.NewGuid(), DisplayName = "時計", MediaKind = "image", StoredFileName = "" },
                    new { Id = Guid.NewGuid(), DisplayName = "システムステータス", MediaKind = "image", StoredFileName = "", Width = 300, Height = 180 },
                    new { Id = Guid.NewGuid(), DisplayName = "loop", MediaKind = "image", StoredFileName = "loop.gif" }
                }
            });
            var items = new LayoutRepository(folder).Load().Items;
            Equal(CanvasContentKinds.Clock, items[0].ContentKind);
            Equal(CanvasContentKinds.SystemMonitor, items[1].ContentKind);
            Equal(320d, items[1].Width);
            Equal(390d, items[1].Height);
            Equal(CanvasContentKinds.Gif, items[2].ContentKind);
        }
        finally { Directory.Delete(folder, true); }
    }

    private static string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DeskCanvas-v14-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
    private static void Write(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value));
    private static void Test(string name, Action action) { action(); Console.WriteLine("PASS " + name); }
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"expected {expected}, actual {actual}");
    }
}
