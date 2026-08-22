using System.Runtime.CompilerServices;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;
using SkiaSharp;

internal static class V14VisualContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var source = File.ReadAllText(LiveSourcePath());
        Contract(
            "v1.4 system reference rows and default size",
            source.Contains(
                "if(item.Height<320){item.Width=Math.Max(320,item.Width);item.Height=390;}",
                StringComparison.Ordinal) &&
            source.Contains("rows.Children.Add(cpu.View)", StringComparison.Ordinal) &&
            source.Contains("rows.Children.Add(ram.View)", StringComparison.Ordinal) &&
            source.Contains("rows.Children.Add(gpu.View)", StringComparison.Ordinal) &&
            source.Contains("rows.Children.Add(down.View)", StringComparison.Ordinal) &&
            source.Contains("rows.Children.Add(up.View)", StringComparison.Ordinal) &&
            source.Contains("Data=Geometry.Parse(\"M0,12 C5,12", StringComparison.Ordinal));
        Contract(
            "v1.4 Apple-style transport corners",
            source.Contains("new CornerRadius(11)", StringComparison.Ordinal) &&
            !source.Contains("new CornerRadius(99)", StringComparison.Ordinal));
        Contract(
            "v1.4 same-media updates do not restart marquee",
            source.Contains(
                "if(mediaChanged){StopMarquee();root.Dispatcher.BeginInvoke(RecalculateMarquee",
                StringComparison.Ordinal) &&
            !source.Contains("else{RecalculateMarquee();}", StringComparison.Ordinal));
        Contract(
            "v1.4 system theme subscription lifetime",
            source.Contains(
                "SystemEvents.UserPreferenceChanged+=PreferenceChanged",
                StringComparison.Ordinal) &&
            source.Contains(
                "SystemEvents.UserPreferenceChanged-=PreferenceChanged",
                StringComparison.Ordinal));
        ArtworkAccent();
    }

    private static void ArtworkAccent()
    {
        using var bitmap = new SKBitmap(2, 1);
        bitmap.SetPixel(0, 0, new SKColor(128, 128, 128));
        bitmap.SetPixel(1, 0, new SKColor(255, 0, 0));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var palette = WidgetTheme.Resolve(
            new CanvasItem { Theme = WidgetThemeKind.Auto },
            encoded.ToArray());
        if (palette.Accent.R != 255 ||
            palette.Accent.G != 0 ||
            palette.Accent.B != 0 ||
            palette.SurfaceA.A != 255 ||
            palette.SurfaceB.A != 255)
        {
            throw new InvalidOperationException("artwork palette contract failed");
        }
        Console.WriteLine("PASS v1.4 artwork palette keeps the vivid accent");
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
                "LiveItemContent.Accepted.cs"));

    private static void Contract(string name, bool passed)
    {
        if (!passed)
            throw new InvalidOperationException($"{name} failed");
        Console.WriteLine($"PASS {name}");
    }
}
