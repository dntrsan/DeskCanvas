using System.Runtime.CompilerServices;

internal static class V15SolReviewContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var source = File.ReadAllText(LiveSourcePath());
        Contract(
            "v1.5 marquee clears stale explicit clip",
            source.Contains(
                "titleTransform.X=0;titleViewport.Clip=null;",
                StringComparison.Ordinal));
        Contract(
            "v1.5 system card uses DPI layout rounding",
            source.Contains(
                "root.UseLayoutRounding=true;root.SnapsToDevicePixels=true;",
                StringComparison.Ordinal));
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
                "LiveItemContent.Accepted5.cs"));

    private static void Contract(string name, bool passed)
    {
        if (!passed) throw new InvalidOperationException($"{name} failed");
        Console.WriteLine($"PASS {name}");
    }
}
