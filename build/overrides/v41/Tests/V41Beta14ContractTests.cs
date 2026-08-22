using System.Reflection;
using System.Runtime.CompilerServices;
using DeskCanvas.App;
using DeskCanvas.Core;

internal static class V41Beta14ContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var assembly = typeof(ApplicationVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var file = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        Check(ApplicationVersion.SemVer == "1.2.0-beta.14", "semver");
        Check(ApplicationVersion.Display == "v1.2.0-beta.14  BETA", "display");
        Check(ApplicationVersion.WindowTitle == "DeskCanvas v1.2.0-beta.14 Beta", "title");
        Check(informational == ApplicationVersion.SemVer, "informational");
        Check(file == "1.2.0.14", "file");
        TestGlassOptics();
        TestMeterLayout();
        TestSparklineFill();
        Console.WriteLine("PASS v1.2.0-beta.14 glass refine contract");
    }

    private static void TestGlassOptics()
    {
        Check(Near(GlassOptics.EdgeWeight(0, 14), 1), "edge weight rim");
        Check(Near(GlassOptics.EdgeWeight(-14, 14), 0), "edge weight deep");
        Check(Near(GlassOptics.EdgeWeight(-7, 14), 0.5), "edge weight mid");
        Check(GlassOptics.EdgeWeight(-40, 14) == 0, "edge weight center");
        var sdf = GlassMath.RoundedRectSdf(100, 50, 200, 100, 16);
        var pixel = GlassOptics.Composite(120, 80, 40, sdf, 100, 50, 200, 100, 16);
        Check(pixel.R == 120 && pixel.G == 80 && pixel.B == 40 && pixel.A == 255, "center passthrough");
        var still = GlassOptics.Displace(100, 50, 200, 100, 16);
        Check(Math.Abs(still.Dx) < 1e-9 && Math.Abs(still.Dy) < 1e-9, "center still");
        var left = GlassOptics.Displace(6, 50, 200, 100, 16);
        Check(left.Dx > 0, "edge displace inward");
        var rim = GlassOptics.Composite(10, 10, 10, 0, 4, 4, 200, 100, 16);
        Check(rim.R > 80 && rim.A == 255, "rim highlight");
        Check(GlassOptics.Rim(0) > GlassOptics.Rim(-8), "rim falloff");
    }

    private static void TestMeterLayout()
    {
        Check(MeterLayoutMath.RowPadding(100, 120, 3) == 0, "no leftover");
        Check(MeterLayoutMath.RowPadding(160, 100, 3) == 10, "spread");
        Check(MeterLayoutMath.RowPadding(400, 100, 3) == 14, "cap");
        Check(MeterLayoutMath.RowPadding(200, 100, 0) == 0, "no rows");
        Check(MeterLayoutMath.RowPadding(double.NaN, 100, 3) == 0, "nan");
        Check(Near(MeterLayoutMath.NaturalHeight(1, 3, true), 127), "natural");
        Check(MeterLayoutMath.NaturalHeight(0, 3, true) == 0, "zero scale");
    }

    private static void TestSparklineFill()
    {
        var line = SparklineGeometry.Build([50, 50], 80, 20, floorScale: 100, smooth: false);
        var area = SparklineGeometry.Area(line, 20);
        Check(area.Count == 4, "area count");
        Check(Near(area[^1].Y, 20) && Near(area[^2].Y, 20), "area base height");
        Check(area.All(point => point.Y <= 20 + 1e-9), "area below height");
    }

    private static bool Near(double actual, double expected) => Math.Abs(actual - expected) < 1e-9;

    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException($"beta 14 contract failed: {name}");
    }
}
