namespace DeskCanvas.App.Services;

internal static class ArtworkPaletteMathV18
{
    internal static bool TryResolve(IReadOnlyList<PaletteRgb> pixels, out ArtworkPalette palette)
    {
        palette = default;
        if (pixels.Count < 12) return false;
        var bins = new Dictionary<int, (long r, long g, long b, int count)>();
        foreach (var p in pixels)
        {
            var key = (p.R >> 3) << 10 | (p.G >> 3) << 5 | (p.B >> 3);
            bins.TryGetValue(key, out var bin); bins[key] = (bin.r + p.R, bin.g + p.G, bin.b + p.B, bin.count + 1);
        }
        var candidates = bins.Values.Select(bin =>
        {
            var color = new PaletteRgb((byte)(bin.r / bin.count), (byte)(bin.g / bin.count), (byte)(bin.b / bin.count));
            return (color, bin.count, saturation: Saturation(color), lightness: Lightness(color));
        }).Where(x => x.saturation >= .12 && x.lightness is >= .08 and <= .92).ToArray();
        if (candidates.Length == 0) return false;
        var dominant = candidates.OrderByDescending(x => x.count * (1 + x.saturation * .25)).First().color;
        var accent = Hsl(dominant, .45, .72, .55, .72);
        var surfaceA = Hsl(dominant, .18, .30, .16, .22);
        var surfaceB = Hsl(dominant, .18, .30, .12, .18);
        var foreground = ArtworkPaletteMath.ContrastRatio(surfaceA, new PaletteRgb(250,248,246)) >= 4.5 ? new PaletteRgb(250,248,246) : new PaletteRgb(30,30,37);
        palette = new ArtworkPalette(surfaceA, surfaceB, accent, foreground, false);
        return true;
    }

    internal static IReadOnlyList<(double X1, double Y1, double X2, double Y2, double X, double Y)> Bezier(IReadOnlyList<NetworkGraphPoint> points, double height = 16)
    {
        var result = new List<(double,double,double,double,double,double)>();
        for (var i=1;i<points.Count;i++) { var previous=points[i-1]; var point=points[i]; var mid=(previous.X+point.X)/2; result.Add((mid,Math.Clamp(previous.Y,0,height),mid,Math.Clamp(point.Y,0,height),point.X,Math.Clamp(point.Y,0,height))); }
        return result;
    }

    private static double Saturation(PaletteRgb c) => (Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B))) / 255d;
    private static double Lightness(PaletteRgb c) => (Math.Max(c.R, Math.Max(c.G, c.B)) + Math.Min(c.R, Math.Min(c.G, c.B))) / 510d;
    private static PaletteRgb Hsl(PaletteRgb c, double minS, double maxS, double minL, double maxL)
    {
        var r=c.R/255d;var g=c.G/255d;var b=c.B/255d;var max=Math.Max(r,Math.Max(g,b));var min=Math.Min(r,Math.Min(g,b));var d=max-min;var l=(max+min)/2;var s=d==0?0:d/(1-Math.Abs(2*l-1));var h=d==0?0:max==r?((g-b)/d+(g<b?6:0))/6:max==g?((b-r)/d+2)/6:((r-g)/d+4)/6;
        s=Math.Clamp(s,minS,maxS);l=Math.Clamp(l,minL,maxL);var q=l<.5?l*(1+s):l+s-l*s;var p=2*l-q;
        static double Hue(double p,double q,double t){if(t<0)t+=1;if(t>1)t-=1;return t<1d/6?p+(q-p)*6*t:t<.5?q:t<2d/3?p+(q-p)*(2d/3-t)*6:p;}
        return new PaletteRgb((byte)Math.Round(255*Hue(p,q,h+1d/3)),(byte)Math.Round(255*Hue(p,q,h)),(byte)Math.Round(255*Hue(p,q,h-1d/3)));
    }
}
