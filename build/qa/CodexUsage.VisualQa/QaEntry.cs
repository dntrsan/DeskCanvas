using System.Reflection;

internal static class QaEntry
{
    [STAThread]
    private static int Main(string[] args)
    {
        var generated = typeof(QaEntry).Assembly
            .GetType("Program", throwOnError: true)!
            .GetMethod("<Main>$", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException("Generated QA entry point was not found.");
        return (int)(generated.Invoke(null, [args]) ?? 1);
    }
}
