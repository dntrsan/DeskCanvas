using System.Runtime.CompilerServices;

internal static class V16TransportContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var source = File.ReadAllText(LiveSourcePath());
        Contract(
            "v1.6 transport keeps the Apple Music-style three-button geometry",
            source.Contains(
                "previous=Transport(PreviousGeometry(),40,\"前へ\");playPause=Transport(PlayGeometry(),46,\"再生 / 一時停止\",true);next=Transport(NextGeometry(),40,\"次へ\");",
                StringComparison.Ordinal));
        Contract(
            "v1.6 transport template is transparent vector-only and centers its content",
            source.Contains(
                "Padding=new Thickness(0),Background=Brushes.Transparent,BorderThickness=new Thickness(0)",
                StringComparison.Ordinal) &&
            source.Contains(
                "Content=Icon(g,play?19:17)",
                StringComparison.Ordinal) &&
            source.Contains(
                "HorizontalAlignment.Center",
                StringComparison.Ordinal) &&
            source.Contains(
                "VerticalAlignment.Center",
                StringComparison.Ordinal));
        Contract(
            "v1.6 transport applies press scale and disabled opacity",
            source.Contains(
                "PreviewMouseLeftButtonDown+=(_,_)=>Scale(b,.94)",
                StringComparison.Ordinal) &&
            source.Contains(
                "b.IsEnabledChanged+=(_,_)=>b.Opacity=b.IsEnabled?1:.32",
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
                "LiveItemContent.Spectrum.Accepted.cs"));

    private static void Contract(string name, bool passed)
    {
        if (!passed) throw new InvalidOperationException($"{name} failed");
        Console.WriteLine($"PASS {name}");
    }
}
