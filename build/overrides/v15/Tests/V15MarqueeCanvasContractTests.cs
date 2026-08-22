using System.Runtime.CompilerServices;

internal static class V15MarqueeCanvasContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var source = File.ReadAllText(LiveSourcePath());
        if (!source.Contains(
                "private readonly Canvas titleViewport",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "marquee viewport must not constrain the title child width");
        }
        Console.WriteLine("PASS v1.5 marquee viewport keeps the complete title visual");
    }

    private static string LiveSourcePath(
        [CallerFilePath] string testSourcePath = "") =>
        Path.GetFullPath(
            Path.Combine(
                Path.GetDirectoryName(testSourcePath)
                    ?? throw new InvalidOperationException("test source path missing"),
                "..",
                "App",
                "Windows",
                "LiveItemContent.Accepted6.cs"));
}
