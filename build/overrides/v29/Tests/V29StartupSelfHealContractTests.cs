using System.Runtime.CompilerServices;
using DeskCanvas.App.Services;

internal static class V29StartupSelfHealContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string current = "C:\\Program Files\\DeskCanvas\\DeskCanvas.exe";
        Equal(true, StartupRegistrationPolicy.ShouldWrite(true, null, current));
        Equal(true, StartupRegistrationPolicy.ShouldWrite(true, "\"C:\\Old\\DeskCanvas.exe\"", current));
        Equal(false, StartupRegistrationPolicy.ShouldWrite(true, "\"C:\\Program Files\\DeskCanvas\\DeskCanvas.exe\"", current));
        Equal(false, StartupRegistrationPolicy.ShouldWrite(false, null, current));
        Console.WriteLine("PASS v1.2 startup self-heal policy");
    }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"expected={expected}, actual={actual}"); }
}
