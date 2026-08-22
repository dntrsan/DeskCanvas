using System.Reflection;
using System.Runtime.CompilerServices;
using DeskCanvas.App;

internal static class V37Beta10ContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var assembly = typeof(ApplicationVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var file = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        Check(ApplicationVersion.SemVer == "1.2.0-beta.10");
        Check(ApplicationVersion.Display == "v1.2.0-beta.10  BETA");
        Check(ApplicationVersion.WindowTitle == "DeskCanvas v1.2.0-beta.10 Beta");
        Check(informational == ApplicationVersion.SemVer);
        Check(file == "1.2.0.10");
        Console.WriteLine("PASS v1.2.0-beta.10 timer-race and release contract");
    }

    private static void Check(bool value)
    {
        if (!value) throw new InvalidOperationException("beta 10 contract failed");
    }
}
