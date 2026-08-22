using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DeskCanvas.App;
using DeskCanvas.Core;

internal static class V40Beta13ContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var assembly = typeof(ApplicationVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var file = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        Check(ApplicationVersion.SemVer == "1.2.0-beta.13", "semver");
        Check(ApplicationVersion.Display == "v1.2.0-beta.13  BETA", "display");
        Check(ApplicationVersion.WindowTitle == "DeskCanvas v1.2.0-beta.13 Beta", "title");
        Check(informational == ApplicationVersion.SemVer, "informational");
        Check(file == "1.2.0.13", "file");
        Check(Enum.IsDefined(WidgetSurfaceStyle.LiquidGlass), "liquid enum");
        var json = JsonSerializer.Serialize(new CanvasItem
        {
            ContentKind = CanvasContentKinds.SystemMonitor,
            SystemMonitor = new SystemMonitorOptions { SurfaceStyle = WidgetSurfaceStyle.LiquidGlass, Style = SystemMonitorStyle.Detail }
        });
        var clone = JsonSerializer.Deserialize<CanvasItem>(json) ?? throw new InvalidOperationException("clone");
        Check(clone.SystemMonitor.SurfaceStyle == WidgetSurfaceStyle.LiquidGlass, "liquid json");
        Check(clone.SystemMonitor.Style == SystemMonitorStyle.Detail, "detail json");
        var rim = GlassMath.Fresnel(0, 12);
        var deep = GlassMath.Fresnel(-40, 12);
        Check(rim > deep, "fresnel depth");
        Console.WriteLine("PASS v1.2.0-beta.13 liquid glass contract");
    }

    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException($"beta 13 contract failed: {name}");
    }
}
