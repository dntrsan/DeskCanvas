using System.Runtime.CompilerServices;
using System.Text.Json;
using DeskCanvas.Core;

internal static class V19StableContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var defaults = new NowPlayingOptions();
        if (!defaults.ShowSpectrum || defaults.ProgressStyle != NowPlayingProgressStyle.Simple)
            throw new InvalidOperationException("v19 Now Playing defaults changed");
        var custom = new NowPlayingOptions { ShowSpectrum = false, ProgressStyle = NowPlayingProgressStyle.Hearts };
        var clone = custom.Clone();
        if (clone.ShowSpectrum || clone.ProgressStyle != NowPlayingProgressStyle.Hearts)
            throw new InvalidOperationException("v19 option clone changed values");
        custom.ProgressStyle = (NowPlayingProgressStyle)999;
        if (custom.Clone().ProgressStyle != NowPlayingProgressStyle.Simple)
            throw new InvalidOperationException("v19 clone did not normalize unknown style");
        var root = Path.Combine(Path.GetTempPath(), "DeskCanvas-v19-stable-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var first = Guid.NewGuid(); var second = Guid.NewGuid();
            var json = "{\"version\":2,\"settings\":{},\"items\":[{\"id\":\"" + first + "\",\"contentKind\":\"nowPlaying\",\"nowPlaying\":{\"showSpectrum\":false,\"progressStyle\":999}},{\"id\":\"" + second + "\",\"contentKind\":\"nowPlaying\",\"nowPlaying\":{}}]}";
            File.WriteAllText(Path.Combine(root, "layout.json"), json);
            var layout = new LayoutRepository(root).Load();
            if (layout.Items.Count != 2 || layout.Items[0].NowPlaying.ShowSpectrum || layout.Items[0].NowPlaying.ProgressStyle != NowPlayingProgressStyle.Simple || !layout.Items[1].NowPlaying.ShowSpectrum || layout.Items[1].NowPlaying.ProgressStyle != NowPlayingProgressStyle.Simple)
                throw new InvalidOperationException("v19 persisted defaults or normalization failed");
            var roundTrip = JsonSerializer.Serialize(layout);
            if (!roundTrip.Contains("ShowSpectrum", StringComparison.Ordinal) || !roundTrip.Contains("ProgressStyle", StringComparison.Ordinal))
                throw new InvalidOperationException("v19 options missing from JSON round trip");
        }
        finally { try { Directory.Delete(root, true); } catch { } }
        Console.WriteLine("PASS v1.9 options clone JSON defaults and normalization");
    }
}
