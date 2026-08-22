using System.Reflection;
using System.Runtime.CompilerServices;
using DeskCanvas.App;
using DeskCanvas.Core;

internal static class V42Beta15ContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var assembly = typeof(ApplicationVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var file = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        Check(ApplicationVersion.SemVer == "1.2.0-beta.15", "semver");
        Check(ApplicationVersion.Display == "v1.2.0-beta.15  BETA", "display");
        Check(ApplicationVersion.WindowTitle == "DeskCanvas v1.2.0-beta.15 Beta", "title");
        Check(informational == ApplicationVersion.SemVer, "informational");
        Check(file == "1.2.0.15", "file");
        TestGlassOptics();
        TestMeterLayout();
        Console.WriteLine("PASS v1.2.0-beta.15 continuous glass contract");
    }

    private static void TestGlassOptics()
    {
        Check(Near(GlassOptics.EdgeWeight(0, 14), 1), "edge weight rim");
        Check(Near(GlassOptics.EdgeWeight(-14, 14), 0), "edge weight deep");
        Check(GlassOptics.Falloff(1) > GlassOptics.Falloff(0.5), "falloff monotonic");
        Check(GlassOptics.Falloff(0) == 0, "falloff zero");

        var sdf = GlassMath.RoundedRectSdf(100, 50, 200, 100, 16);
        var pixel = GlassOptics.Composite(120, 80, 40, sdf, 100, 50, 200, 100, 16);
        Check(pixel.A == 255, "center opaque");
        Check(Math.Abs(pixel.R - 120) <= 12 && Math.Abs(pixel.G - 80) <= 12 && Math.Abs(pixel.B - 40) <= 12, "center wallpaper");

        var still = GlassOptics.Displace(100, 50, 200, 100, 16);
        Check(Math.Abs(still.Dx) < 1e-9 && Math.Abs(still.Dy) < 1e-9, "center still");

        var near = GlassOptics.Displace(4, 50, 200, 100, 16);
        var mid = GlassOptics.Displace(14, 50, 200, 100, 16);
        var far = GlassOptics.Displace(36, 50, 200, 100, 16);
        Check(near.Dx > 0, "edge displace inward");
        Check(Math.Abs(near.Dx) > Math.Abs(mid.Dx), "no refraction ridge");
        Check(Math.Abs(mid.Dx) >= Math.Abs(far.Dx) - 1e-9, "smooth falloff");
        Check(GlassOptics.Rim(0) > GlassOptics.Rim(-8), "rim falloff");
    }

    private static void TestMeterLayout()
    {
        Check(MeterLayoutMath.RowPadding(160, 100, 3) == 10, "spread");
        Check(MeterLayoutMath.RowPadding(400, 100, 3) == 14, "cap");
    }

    private static bool Near(double actual, double expected) => Math.Abs(actual - expected) < 1e-9;

    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException($"beta 15 contract failed: {name}");
    }
}
