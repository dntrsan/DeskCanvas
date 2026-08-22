using System.Reflection;
using System.Runtime.CompilerServices;
using DeskCanvas.App;
using DeskCanvas.Core;

internal static class V45V130ContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var assembly = typeof(ApplicationVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var file = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        Check(ApplicationVersion.SemVer == "1.3.0", "semver");
        Check(ApplicationVersion.Display == "v1.3.0", "display");
        Check(ApplicationVersion.WindowTitle == "DeskCanvas v1.3.0", "title");
        Check(informational == ApplicationVersion.SemVer, "informational");
        Check(file == "1.3.0.0", "file");
        TestBackdropMapping();
        TestPlateBlur();
        TestPassThrough();
        TestLens();
        Console.WriteLine("PASS v1.3.0 glass and monitor contract");
    }

    private static void TestBackdropMapping()
    {
        var mapped = BackdropMapping.ToImage(100, 50, 10, 20);
        Check(Near(mapped.ImageX, 90) && Near(mapped.ImageY, 30), "translate");
        Check(BackdropMapping.IsInside(90, 30, 100, 40), "inside");
        Check(!BackdropMapping.IsInside(100, 0, 100, 40), "edge exclusive");
        Check(!BackdropMapping.IsInside(-1, 10, 100, 40), "outside left");
    }

    private static void TestPlateBlur()
    {
        var pixels = new byte[] { 10, 20, 30, 255 };
        var scratch = new byte[4];
        PlateBlur.Box(pixels, 1, 1, 0, scratch);
        Check(pixels[0] == 10 && pixels[2] == 30, "radius zero identity");

        var fill = new byte[4 * 4 * 4];
        for (var i = 0; i < fill.Length; i += 4)
        {
            fill[i] = 128;
            fill[i + 1] = 128;
            fill[i + 2] = 128;
            fill[i + 3] = 255;
        }

        var before = SumRgb(fill);
        PlateBlur.Box(fill, 4, 4, 1, new byte[fill.Length]);
        Check(SumRgb(fill) == before, "brightness conserved");
        Check(fill[0] == 128 && fill[2] == 128, "uniform stays");
        PlateBlur.Box(new byte[16], 2, 2, 8, new byte[16]);
    }

    private static void TestPassThrough()
    {
        var pixel = GlassOptics.PassThrough(10, 20, 30);
        Check(pixel.R == 10 && pixel.G == 20 && pixel.B == 30 && pixel.A == 255, "passthrough");
    }

    private static void TestLens()
    {
        var still = GlassOptics.Displace(100, 50, 200, 100, 16);
        Check(Math.Abs(still.Dx) < 1e-9 && Math.Abs(still.Dy) < 1e-9, "center still");
        var left = GlassOptics.Displace(4, 50, 200, 100, 16);
        Check(left.Dx < 0, "rim wraps outward");
        var mid = GlassOptics.Displace(14, 50, 200, 100, 16);
        Check(Math.Abs(left.Dx) > Math.Abs(mid.Dx), "wrap stronger at rim");
        var interior = GlassOptics.Displace(120, 50, 200, 100, 16);
        Check(interior.Dx < 0, "interior magnifies");
    }

    private static int SumRgb(byte[] pixels)
    {
        var sum = 0;
        for (var i = 0; i < pixels.Length; i += 4) sum += pixels[i] + pixels[i + 1] + pixels[i + 2];
        return sum;
    }

    private static bool Near(double actual, double expected) => Math.Abs(actual - expected) < 1e-9;

    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException($"v1.3.0 contract failed: {name}");
    }
}
