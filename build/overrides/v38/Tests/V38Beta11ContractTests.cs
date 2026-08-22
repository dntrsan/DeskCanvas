using System.Reflection;
using System.Runtime.CompilerServices;
using DeskCanvas.App;

internal static class V38Beta11ContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var assembly = typeof(ApplicationVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var file = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        Check(ApplicationVersion.SemVer == "1.2.0-beta.11");
        Check(ApplicationVersion.Display == "v1.2.0-beta.11  BETA");
        Check(ApplicationVersion.WindowTitle == "DeskCanvas v1.2.0-beta.11 Beta");
        Check(informational == ApplicationVersion.SemVer);
        Check(file == "1.2.0.11");
        Console.WriteLine("PASS v1.2.0-beta.11 bugfix and release contract");
    }

    private static void Check(bool value)
    {
        if (!value) throw new InvalidOperationException("beta 11 contract failed");
    }
}
