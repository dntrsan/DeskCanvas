using System.Reflection;
using System.Runtime.CompilerServices;
using DeskCanvas.App;

internal static class V35Beta8ContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var assembly = typeof(ApplicationVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var file = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        Check(ApplicationVersion.SemVer == "1.2.0-beta.8");
        Check(ApplicationVersion.Display == "v1.2.0-beta.8  BETA");
        Check(ApplicationVersion.WindowTitle == "DeskCanvas v1.2.0-beta.8 Beta");
        Check(informational == ApplicationVersion.SemVer);
        Check(file == "1.2.0.8");
        Console.WriteLine("PASS v1.2.0-beta.8 foreground safety contract");
    }

    private static void Check(bool value)
    {
        if (!value) throw new InvalidOperationException("beta 8 contract failed");
    }
}
