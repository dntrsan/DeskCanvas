using System.Runtime.CompilerServices;
using System.Text.Json;
using DeskCanvas.Core;

internal static class V20ProgressStyleMigrationTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        foreach (var legacy in new[] { NowPlayingProgressStyle.Dots, NowPlayingProgressStyle.Hearts })
        {
            var options = new NowPlayingOptions { ProgressStyle = legacy };
            if (options.ProgressStyle != NowPlayingProgressStyle.Wave || options.Clone().ProgressStyle != NowPlayingProgressStyle.Wave)
                throw new InvalidOperationException("v2.0 did not migrate retired progress style to Wave");
        }
        var unknown = new NowPlayingOptions { ProgressStyle = (NowPlayingProgressStyle)93 };
        if (unknown.ProgressStyle != NowPlayingProgressStyle.Simple)
            throw new InvalidOperationException("v2.0 did not normalize an unknown progress style");
        var roundTripped = JsonSerializer.Deserialize<NowPlayingOptions>("{\"progressStyle\":3}");
        if (roundTripped?.ProgressStyle != NowPlayingProgressStyle.Wave)
            throw new InvalidOperationException("v2.0 JSON compatibility did not migrate Hearts to Wave");
        var missing = JsonSerializer.Deserialize<NowPlayingOptions>("{}");
        if (missing is null || !missing.ShowSpectrum || missing.ProgressStyle != NowPlayingProgressStyle.Simple)
            throw new InvalidOperationException("v2.0 missing JSON defaults changed");
        Console.WriteLine("PASS v2.0 progress-style migration");
    }
}
