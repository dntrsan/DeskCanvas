using System.Reflection;
using System.Runtime.CompilerServices;
using DeskCanvas.App;

internal static class V31Beta4VersionContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var assembly = typeof(ApplicationVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var file = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        if (ApplicationVersion.SemVer != "1.2.0-beta.4" ||
            ApplicationVersion.Display != "v1.2.0-beta.4  BETA" ||
            ApplicationVersion.WindowTitle != "DeskCanvas v1.2.0-beta.4 Beta" ||
            informational != ApplicationVersion.SemVer ||
            file != "1.2.0.4")
            throw new InvalidOperationException("beta 4 version metadata and management-window label diverged");
        Console.WriteLine("PASS v1.2.0-beta.4 assembly and management-window version label");
    }
}
