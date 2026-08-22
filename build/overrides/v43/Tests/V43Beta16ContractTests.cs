using System.Reflection;
using System.Runtime.CompilerServices;
using DeskCanvas.App;
using DeskCanvas.Core;

internal static class V43Beta16ContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var assembly = typeof(ApplicationVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var file = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        Check(ApplicationVersion.SemVer == "1.2.0-beta.16", "semver");
        Check(ApplicationVersion.Display == "v1.2.0-beta.16  BETA", "display");
        Check(ApplicationVersion.WindowTitle == "DeskCanvas v1.2.0-beta.16 Beta", "title");
        Check(informational == ApplicationVersion.SemVer, "informational");
        Check(file == "1.2.0.16", "file");
        TestLens();
        Console.WriteLine("PASS v1.2.0-beta.16 convex glass refraction contract");
    }

    private static void TestLens()
    {
        var still = GlassOptics.Displace(100, 50, 200, 100, 16);
        Check(Math.Abs(still.Dx) < 1e-9 && Math.Abs(still.Dy) < 1e-9, "center still");

        var left = GlassOptics.Displace(4, 50, 200, 100, 16);
        Check(left.Dx < 0, "rim wraps outward");

        var interior = GlassOptics.Displace(120, 50, 200, 100, 16);
        Check(interior.Dx < 0, "interior magnifies");

        var mid = GlassOptics.Displace(14, 50, 200, 100, 16);
        Check(Math.Abs(left.Dx) > Math.Abs(mid.Dx), "wrap stronger at rim");

        var sdf = GlassMath.RoundedRectSdf(100, 50, 200, 100, 16);
        Check(GlassOptics.Thickness(sdf, 200, 100) > 0.9, "center thickness");
        Check(GlassOptics.Thickness(0, 200, 100) < 0.15, "rim thickness");

        var pixel = GlassOptics.Composite(120, 80, 40, sdf, 100, 50, 200, 100, 16);
        Check(pixel.A == 255, "center opaque");
        Check(Math.Abs(pixel.R - 120) <= 12 && Math.Abs(pixel.G - 80) <= 12, "center wallpaper");
    }

    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException($"beta 16 contract failed: {name}");
    }
}
