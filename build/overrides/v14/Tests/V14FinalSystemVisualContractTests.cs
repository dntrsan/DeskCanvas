using System.Runtime.CompilerServices;

internal static class V14FinalSystemVisualContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var source = File.ReadAllText(LiveSourcePath());
        Contract(
            "v1.4 system uses three distinct accent-derived metric colors",
            source.Contains(
                "WidgetTheme.Blend(p.Accent,WpfColor.FromRgb(105,188,255),.72)",
                StringComparison.Ordinal) &&
            source.Contains(
                "WidgetTheme.Blend(p.Accent,WpfColor.FromRgb(111,219,174),.72)",
                StringComparison.Ordinal) &&
            source.Contains(
                "WidgetTheme.Blend(p.Accent,WpfColor.FromRgb(195,145,255),.72)",
                StringComparison.Ordinal));
        Contract(
            "v1.4 system brand and labels follow foreground contrast",
            source.Contains(
                "brandMark.Stroke=new SolidColorBrush(colors[0])",
                StringComparison.Ordinal) &&
            source.Contains(
                "heading.Foreground=new SolidColorBrush(p.Muted)",
                StringComparison.Ordinal) &&
            source.Contains(
                "label.Foreground=text.Foreground=new SolidColorBrush(p.Muted)",
                StringComparison.Ordinal));
        Contract(
            "v1.4 network arrows retain circular outlines",
            source.Contains(
                "circle.BorderBrush=b;arrow.Fill=b;spark.Stroke=b",
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
                "LiveItemContent.Accepted2.cs"));

    private static void Contract(string name, bool passed)
    {
        if (!passed)
            throw new InvalidOperationException($"{name} failed");
        Console.WriteLine($"PASS {name}");
    }
}
