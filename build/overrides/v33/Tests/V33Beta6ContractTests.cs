using System.Reflection;
using System.Runtime.CompilerServices;
using DeskCanvas.App;

internal static class V33Beta6ContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var assembly = typeof(ApplicationVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var file = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        Check(ApplicationVersion.SemVer == "1.2.0-beta.6");
        Check(ApplicationVersion.Display == "v1.2.0-beta.6  BETA");
        Check(ApplicationVersion.WindowTitle == "DeskCanvas v1.2.0-beta.6 Beta");
        Check(informational == ApplicationVersion.SemVer);
        Check(file == "1.2.0.6");
        Check(!DeskCanvasController.ShouldCreateDesktopIntegrations(preview: true));
        Check(DeskCanvasController.ShouldCreateDesktopIntegrations(preview: false));
        Console.WriteLine("PASS v1.2.0-beta.6 version and desktop integration contract");
    }

    private static void Check(bool value)
    {
        if (!value) throw new InvalidOperationException("beta 6 contract failed");
    }
}
