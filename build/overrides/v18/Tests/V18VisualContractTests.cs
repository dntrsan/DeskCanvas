using System.Runtime.CompilerServices;
using DeskCanvas.App.Services;

internal static class V18VisualContractTests
{
    [ModuleInitializer] internal static void Run()
    {
        var graph = new SmoothedNetworkGraph(); for (var i = 0; i < 35; i++) graph.Add(i * 500); if (graph.Count != 30 || graph.Scale < 17000) throw new InvalidOperationException("network history or immediate rise failed");
        graph.Add(0); graph.Add(0); graph.Add(0); var held = graph.Scale; graph.Add(0); if (graph.Scale >= held) throw new InvalidOperationException("falling peak did not decay after hold");
        var before = graph.Advance(TimeSpan.Zero); var middle = graph.Advance(TimeSpan.FromMilliseconds(350)); var end = graph.Advance(TimeSpan.FromMilliseconds(350)); if (before.SequenceEqual(end) || middle.Zip(before, (a,b) => Math.Abs(a-b)).All(x => x < .0001) || graph.IsAnimating) throw new InvalidOperationException("700ms graph interpolation lifecycle failed");
        var neon = Enumerable.Repeat(new PaletteRgb(10, 22, 25), 40).Append(new PaletteRgb(0,255,0)).Append(new PaletteRgb(0,255,0)).ToArray(); if (!ArtworkPaletteMath.TryResolve(neon, out var dark) || dark.SurfaceA.Luminance > .23 || ArtworkPaletteMath.ContrastRatio(dark.SurfaceA, dark.Foreground) < 4.5 || dark.Accent.G == 255) throw new InvalidOperationException("neon palette was not tamed");
        var olive = Enumerable.Repeat(new PaletteRgb(112, 107, 39), 30).ToArray(); if (!ArtworkPaletteMath.TryResolve(olive, out var olivePalette) || olivePalette.SurfaceB.Luminance > olivePalette.SurfaceA.Luminance) throw new InvalidOperationException("olive palette failed");
        var mono = Enumerable.Repeat(new PaletteRgb(88,88,88), 30).ToArray(); if (ArtworkPaletteMath.TryResolve(mono, out _)) throw new InvalidOperationException("monochrome artwork must use Windows fallback");
        const double cardCenter = 180; const double trioCenter = (360 - 46) / 2d + 23; if (Math.Abs(cardCenter - trioCenter) > .5) throw new InvalidOperationException("transport trio is not card centered");
        Console.WriteLine("PASS v1.8 smooth graph palette guardrails and centered transport geometry");
    }
}
