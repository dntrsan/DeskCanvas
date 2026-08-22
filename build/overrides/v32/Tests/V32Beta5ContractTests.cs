using System.Reflection;
using System.Runtime.CompilerServices;
using DeskCanvas.App;
using DeskCanvas.App.Windows;

internal static class V32Beta5ContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var assembly = typeof(ApplicationVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var file = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        Check(ApplicationVersion.SemVer == "1.2.0-beta.5");
        Check(ApplicationVersion.Display == "v1.2.0-beta.5  BETA");
        Check(ApplicationVersion.WindowTitle == "DeskCanvas v1.2.0-beta.5 Beta");
        Check(informational == ApplicationVersion.SemVer);
        Check(file == "1.2.0.5");
        Check(!DeskCanvasController.ShouldCreateDesktopIntegrations(preview: true));
        Check(DeskCanvasController.ShouldCreateDesktopIntegrations(preview: false));
        var normal = SystemMonitorLayoutMath.Values(317.34072022160666, 361, 5, true);
        var compact = SystemMonitorLayoutMath.Values(240, 260, 5, true);
        Check(normal.Row > 14 && !normal.Dense);
        Check(compact.Row >= 14 && compact.Dense);
        Console.WriteLine("PASS v1.2.0-beta.5 version, preview tray, and stable system layout contract");
    }

    private static void Check(bool value)
    {
        if (!value) throw new InvalidOperationException("beta 5 contract failed");
    }
}
