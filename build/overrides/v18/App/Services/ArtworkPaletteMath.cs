namespace DeskCanvas.App.Services;

internal readonly record struct PaletteRgb(byte R, byte G, byte B)
{
    internal double Luminance => (0.2126 * R + 0.7152 * G + 0.0722 * B) / 255d;
}

internal readonly record struct ArtworkPalette(PaletteRgb SurfaceA, PaletteRgb SurfaceB, PaletteRgb Accent, PaletteRgb Foreground, bool IsFallback);

/// <summary>Pure, deterministic artwork palette selection.  It deliberately selects a frequent colour cluster,
/// never a single saturated pixel such as a neon logo or a JPEG artefact.</summary>
internal static class ArtworkPaletteMath
{
    internal static bool TryResolve(IReadOnlyList<PaletteRgb> pixels, out ArtworkPalette palette)
    {
        palette = default;
        if (pixels.Count < 12) return false;

        var bins = new Dictionary<int, (long r, long g, long b, int count)>();
        var chromatic = 0;
        foreach (var p in pixels)
        {
            var max = Math.Max(p.R, Math.Max(p.G, p.B));
            var min = Math.Min(p.R, Math.Min(p.G, p.B));
            if (max - min >= 18) chromatic++;
            // Five bits per channel combines nearby JPEG/image-resize samples into a real cluster.
            var key = (p.R >> 3) << 10 | (p.G >> 3) << 5 | (p.B >> 3);
            if (!bins.TryGetValue(key, out var bin)) bin = default;
            bins[key] = (bin.r + p.R, bin.g + p.G, bin.b + p.B, bin.count + 1);
        }
        if (chromatic < pixels.Count / 12) return false;

        var chosen = bins.Values
            .OrderByDescending(x => x.count * (1 + Saturation(new PaletteRgb((byte)(x.r / x.count), (byte)(x.g / x.count), (byte)(x.b / x.count))) * .45))
            .First();
        var dominant = new PaletteRgb((byte)(chosen.r / chosen.count), (byte)(chosen.g / chosen.count), (byte)(chosen.b / chosen.count));
        var accent = Hsl(dominant, 0.45, 0.72, 0.55, 0.72);
        var surfaceA = Hsl(dominant, 0.18, 0.30, 0.16, 0.22);
        var surfaceB = Hsl(dominant, 0.18, 0.30, 0.12, 0.18);
        var foreground = ContrastForeground(surfaceA);
        palette = new ArtworkPalette(surfaceA, surfaceB, accent, foreground, false);
        return true;
    }

    internal static double ContrastRatio(PaletteRgb a, PaletteRgb b)
    {
        static double Linear(byte c) { var x = c / 255d; return x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4); }
        static double L(PaletteRgb c) => .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        var x = L(a); var y = L(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
    }

    private static PaletteRgb ContrastForeground(PaletteRgb surface) => ContrastRatio(surface, new(250, 248, 246)) >= 4.5 ? new(250, 248, 246) : new(30, 30, 37);
    private static double Saturation(PaletteRgb c) => (Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B))) / 255d;
    private static PaletteRgb Hsl(PaletteRgb c, double minS, double maxS, double minL, double maxL)
    {
        var r = c.R / 255d; var g = c.G / 255d; var b = c.B / 255d;
        var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b)); var d = max - min;
        var l = (max + min) / 2; var s = d == 0 ? 0 : d / (1 - Math.Abs(2 * l - 1));
        var h = d == 0 ? 0 : max == r ? ((g - b) / d + (g < b ? 6 : 0)) / 6 : max == g ? ((b - r) / d + 2) / 6 : ((r - g) / d + 4) / 6;
        s = Math.Clamp(s, minS, maxS); l = Math.Clamp(l, minL, maxL);
        var q = l + s * (1 - Math.Abs(2 * l - 1)); var p = 2 * l - q;
        static double Hue(double p, double q, double t) { t = t < 0 ? t + 1 : t > 1 ? t - 1 : t; return t < 1d / 6 ? p + (q - p) * 6 * t : t < .5 ? q : t < 2d / 3 ? p + (q - p) * (2d / 3 - t) * 6 : p; }
        return new((byte)Math.Round(255 * Hue(p, q, h + 1d / 3)), (byte)Math.Round(255 * Hue(p, q, h)), (byte)Math.Round(255 * Hue(p, q, h - 1d / 3)));
    }
}
