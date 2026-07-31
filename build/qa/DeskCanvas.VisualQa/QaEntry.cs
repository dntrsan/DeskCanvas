using System.Reflection;

internal static class QaEntry
{
    [STAThread]
    private static int Main(string[] args)
    {
        var generatedMain = typeof(QaEntry).Assembly
            .GetType("Program", throwOnError: true)!
            .GetMethod(
                "<Main>$",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException("Generated Visual QA main was not found.");
        try
        {
            return (int)(generatedMain.Invoke(null, [args]) ?? 1);
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(error.InnerException)
                .Throw();
            throw;
        }
    }
}
