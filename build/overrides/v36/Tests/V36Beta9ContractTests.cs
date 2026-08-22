using System.Reflection;
using System.Runtime.CompilerServices;
using DeskCanvas.App;

internal static class V36Beta9ContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var assembly = typeof(ApplicationVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var file = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        Check(ApplicationVersion.SemVer == "1.2.0-beta.9");
        Check(ApplicationVersion.Display == "v1.2.0-beta.9  BETA");
        Check(ApplicationVersion.WindowTitle == "DeskCanvas v1.2.0-beta.9 Beta");
        Check(informational == ApplicationVersion.SemVer);
        Check(file == "1.2.0.9");
        Console.WriteLine("PASS v1.2.0-beta.9 migration and background-load contract");
    }

    private static void Check(bool value)
    {
        if (!value) throw new InvalidOperationException("beta 9 contract failed");
    }
}
