using System.Runtime.CompilerServices;
using DeskCanvas.Core;

internal static class V39Beta12ContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        TestGpuMemoryMath();
        TestMetricHistory();
        TestSparkline();
        TestGlassMath();
        TestMonitorOptions();
        Console.WriteLine("PASS v1.2.0-beta.12 system monitor and glass math contract");
    }

    private static void TestGpuMemoryMath()
    {
        var value = GpuMemoryMath.FromBytes(8d * 1024 * 1024 * 1024, 16d * 1024 * 1024 * 1024);
        Check(value.Availability == MetricAvailability.Available, "vram available");
        Check(Math.Abs(value.Value!.Value - 50) < 0.01, "vram percent");
        Check(GpuMemoryMath.FromBytes(10, 0).Availability == MetricAvailability.Unavailable, "vram zero total");
        Check(GpuMemoryMath.FromBytes(double.NaN, 1024).Availability == MetricAvailability.Unavailable, "vram nan");
        Check(GpuMemoryMath.FromBytes(2048, 1024).Value!.Value == 100, "vram clamp");
    }

    private static void TestMetricHistory()
    {
        var history = new MetricHistory(3);
        history.Push(1);
        history.Push(double.NaN);
        history.Push(2);
        Equal(2, history.Count);
        Equal(1d, history.Snapshot()[0]);
        Equal(2d, history.Snapshot()[1]);
        history.Push(3);
        history.Push(4);
        var snapshot = history.Snapshot();
        Equal(3, snapshot.Length);
        Equal(2d, snapshot[0]);
        Equal(3d, snapshot[1]);
        Equal(4d, snapshot[2]);
        history.Clear();
        Equal(0, history.Count);
    }

    private static void TestSparkline()
    {
        Check(SparklineGeometry.Build([], 100, 40).Count == 0, "spark empty");
        var single = SparklineGeometry.Build([50], 100, 40, floorScale: 100);
        Equal(2, single.Count);
        Check(Math.Abs(single[0].Y - 20) < 0.01, "spark single y");
        Check(Math.Abs(single[1].X - 100) < 0.01, "spark single x");
        var saturated = SparklineGeometry.Build([100, 100], 80, 20, floorScale: 100, smooth: false);
        Check(Math.Abs(saturated[0].Y) < 0.01, "spark top");
        Check(Math.Abs(saturated[1].Y) < 0.01, "spark top 2");
        var floor = SparklineGeometry.Build([0, 0], 80, 20, floorScale: 100, smooth: false);
        Check(Math.Abs(floor[0].Y - 20) < 0.01, "spark bottom");
        var area = SparklineGeometry.Area(saturated, 20);
        Equal(4, area.Count);
        Check(Math.Abs(area[^1].Y - 20) < 0.01, "area base");
    }

    private static void TestGlassMath()
    {
        var inside = GlassMath.RoundedRectSdf(50, 40, 100, 80, 16);
        var outside = GlassMath.RoundedRectSdf(200, 40, 100, 80, 16);
        Check(inside < 0, "sdf inside");
        Check(outside > 0, "sdf outside");
        var left = GlassMath.Displacement(8, 40, 100, 80, 16, 12, 10);
        var right = GlassMath.Displacement(92, 40, 100, 80, 16, 12, 10);
        Check(left.Dx > 0, "displace inward left");
        Check(right.Dx < 0, "displace inward right");
        Check(Math.Abs(left.Dx + right.Dx) < 0.4, "displace symmetry");
        var fill = GlassMath.MapToImage(50, 50, 100, 100, 200, 100, WallpaperFit.Fill);
        Check(Math.Abs(fill.ImageX - 100) < 0.01, "fill x");
        Check(Math.Abs(fill.ImageY - 50) < 0.01, "fill y");
        var stretch = GlassMath.MapToImage(50, 0, 100, 100, 200, 50, WallpaperFit.Stretch);
        Check(Math.Abs(stretch.ImageX - 100) < 0.01, "stretch x");
        var fit = GlassMath.MapToImage(50, 50, 100, 100, 200, 100, WallpaperFit.Fit);
        Check(Math.Abs(fit.ImageX - 100) < 0.01, "fit x");
        Check(GlassMath.Fresnel(0, 8) > GlassMath.Fresnel(-20, 8), "fresnel rim");
    }

    private static void TestMonitorOptions()
    {
        var clone = new SystemMonitorOptions
        {
            Style = SystemMonitorStyle.Detail,
            Focus = SystemMonitorFocus.Gpu,
            SurfaceStyle = WidgetSurfaceStyle.LiquidGlass,
            ShowVram = false,
            ShowCpu = false
        }.Clone();
        Check(clone.Style == SystemMonitorStyle.Detail, "clone style");
        Check(clone.Focus == SystemMonitorFocus.Gpu, "clone focus");
        Check(clone.SurfaceStyle == WidgetSurfaceStyle.LiquidGlass, "clone surface");
        Check(!clone.ShowVram && !clone.ShowCpu, "clone flags");
        clone.Style = (SystemMonitorStyle)99;
        clone.Focus = (SystemMonitorFocus)99;
        clone.SurfaceStyle = (WidgetSurfaceStyle)99;
        var repaired = clone.Clone();
        Check(repaired.Style == SystemMonitorStyle.Meters, "repair style");
        Check(repaired.Focus == SystemMonitorFocus.Cpu, "repair focus");
        Check(repaired.SurfaceStyle == WidgetSurfaceStyle.Standard, "repair surface");

        WithTemp(root =>
        {
            File.WriteAllText(Path.Combine(root, "layout.json"), """
            {
              "version": 2,
              "items": [
                { "displayName": "old", "contentKind": "systemMonitor", "systemMonitor": { "showCpu": false, "showGpu": true } }
              ]
            }
            """);
            var loaded = new LayoutRepository(root).Load().Items.Single();
            Check(loaded.SystemMonitor.ShowCpu == false, "legacy cpu");
            Check(loaded.SystemMonitor.Style == SystemMonitorStyle.Meters, "legacy style");
            Check(loaded.SystemMonitor.ShowVram, "legacy vram default");
        });

        WithTemp(root =>
        {
            File.WriteAllText(Path.Combine(root, "layout.json"), """
            {
              "version": 2,
              "items": [
                { "displayName": "bad", "contentKind": "systemMonitor", "systemMonitor": { "style": 99, "focus": 42, "surfaceStyle": 7 } }
              ]
            }
            """);
            var loaded = new LayoutRepository(root).Load().Items.Single();
            Check(loaded.SystemMonitor.Style == SystemMonitorStyle.Meters, "invalid style");
            Check(loaded.SystemMonitor.Focus == SystemMonitorFocus.Cpu, "invalid focus");
            Check(loaded.SystemMonitor.SurfaceStyle == WidgetSurfaceStyle.Standard, "invalid surface");
        });
    }

    private static void WithTemp(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "DeskCanvasTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { action(root); }
        finally { Directory.Delete(root, true); }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"expected={expected}, actual={actual}");
        }
    }

    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException($"beta 12 contract failed: {name}");
    }
}
