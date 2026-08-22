namespace DeskCanvas.Core;

/// <summary>
/// One-pixel liquid-glass composite. The plate is a convex lens: the rim wraps
/// wallpaper from outside (water-drop refraction) and the interior slightly
/// magnifies what sits behind it. Lighting stays on a thin outer rim so the
/// sheet does not split into a frame and a window.
/// </summary>
public static class GlassOptics
{
    public const double Strength = 10;
    public const double Bulge = 0.02;
    public const double RimGain = 36;
    public const double Tint = 0.05;
    public const double ChromaticGreen = 0.16;
    public const double ChromaticBlue = 0.34;
    public const double RimBand = 5;
    public const int BlurRadius = 2;
    public const byte Alpha = 0xFF;
    public const byte TintRed = 28;
    public const byte TintGreen = 28;
    public const byte TintBlue = 30;

    public static bool IsPassThrough =>
        string.Equals(Environment.GetEnvironmentVariable("DESKCANVAS_GLASS_PASSTHROUGH"), "1", StringComparison.Ordinal);

    public static (byte R, byte G, byte B, byte A) PassThrough(byte sampleR, byte sampleG, byte sampleB) =>
        (sampleR, sampleG, sampleB, Alpha);

    public static double EdgeBand(double radius) => Math.Clamp(radius * 1.25, 18, 32);

    public static double EdgeWeight(double sdf, double band)
    {
        var width = Math.Max(1, band);
        return Math.Clamp(1 + sdf / width, 0, 1);
    }

    public static double Falloff(double edgeWeight)
    {
        var t = Math.Clamp(edgeWeight, 0, 1);
        return t * t;
    }

    public static double Thickness(double sdf, double width, double height)
    {
        var reach = Math.Max(1, Math.Min(width, height) * 0.42);
        return 1 - EdgeWeight(sdf, reach);
    }

    public static double Rim(double sdf)
    {
        var t = Math.Clamp(1 + sdf / RimBand, 0, 1);
        return t * t * t;
    }

    public static double Streak(double x, double y, double width, double height, double edgeWeight)
    {
        if (width <= 0 || height <= 0 || edgeWeight <= 0) return 0;
        var top = Math.Clamp(1 - y / Math.Max(8, height * 0.18), 0, 1);
        var leftBias = Math.Clamp(1 - x / Math.Max(8, width * 0.55), 0, 1);
        return top * top * (0.35 + 0.65 * leftBias) * Falloff(edgeWeight);
    }

    public static (double Dx, double Dy) Displace(double x, double y, double width, double height, double radius)
    {
        var (nx, ny, sdf) = GlassMath.NormalAndSdf(x, y, width, height, radius);
        var rim = Falloff(EdgeWeight(sdf, EdgeBand(radius)));
        var wrap = Strength * rim;
        var depth = Thickness(sdf, width, height);
        var dx = nx * wrap + (width / 2 - x) * Bulge * depth;
        var dy = ny * wrap + (height / 2 - y) * Bulge * depth;
        return (dx, dy);
    }

    public static (byte R, byte G, byte B, byte A) Composite(
        byte sampleR,
        byte sampleG,
        byte sampleB,
        double sdf,
        double x,
        double y,
        double width,
        double height,
        double radius)
    {
        var edge = EdgeWeight(sdf, EdgeBand(radius));
        var highlight = Math.Min(160, Rim(sdf) * RimGain + Streak(x, y, width, height, edge) * 90);
        return (
            Mix(sampleR, TintRed, Tint, highlight),
            Mix(sampleG, TintGreen, Tint, highlight),
            Mix(sampleB, TintBlue, Tint, highlight),
            Alpha);
    }

    public static (byte R, byte G, byte B) TintSample(byte sampleR, byte sampleG, byte sampleB) =>
        (
            Mix(sampleR, TintRed, Tint, 0),
            Mix(sampleG, TintGreen, Tint, 0),
            Mix(sampleB, TintBlue, Tint, 0));

    public static (byte R, byte G, byte B, byte A) AddRim(
        byte sampleR,
        byte sampleG,
        byte sampleB,
        double sdf,
        double x,
        double y,
        double width,
        double height,
        double radius)
    {
        var edge = EdgeWeight(sdf, EdgeBand(radius));
        var highlight = Math.Min(160, Rim(sdf) * RimGain + Streak(x, y, width, height, edge) * 90);
        return (
            Mix(sampleR, sampleR, 0, highlight),
            Mix(sampleG, sampleG, 0, highlight),
            Mix(sampleB, sampleB, 0, highlight),
            Alpha);
    }

    private static byte Mix(byte sample, byte tint, double tintAmount, double highlight) =>
        (byte)Math.Clamp(sample * (1 - tintAmount) + tint * tintAmount + highlight, 0, 255);
}
