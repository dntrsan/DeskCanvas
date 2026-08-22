using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;

internal static class V21ClockActualQaEntry
{
    private static readonly MethodInfo ApplyCompact = typeof(MediaWindow).GetMethod(
        "ApplyClockCompactVisuals",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(nameof(MediaWindow), "ApplyClockCompactVisuals");

    private static readonly FieldInfo RotatedVisualField = typeof(MediaWindow).GetField(
        "RotatedVisual",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(MediaWindow), "RotatedVisual");

    private static readonly FieldInfo ItemContentField = typeof(MediaWindow).GetField(
        "ItemContent",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(MediaWindow), "ItemContent");

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !Path.IsPathFullyQualified(args[0]))
            return 2;

        Directory.CreateDirectory(args[0]);
        var log = new List<string>
        {
            "v2.1 actual MediaWindow compact Clock render manifest",
            "Actual MediaWindow ApplyClockCompactVisuals path; no live app, registry, or user data opened."
        };

        foreach (var dpi in new[] { 1d, 1.5d })
        foreach (var dimensions in new[] { (64d, 32d), (96d, 48d), (128d, 64d), (192d, 96d) })
        foreach (var style in new[] { ClockStyle.Digital, ClockStyle.Split, ClockStyle.Analog })
        foreach (var date in new[] { (Year: false, MonthDay: false), (Year: true, MonthDay: true) })
            Render(args[0], log, dpi, dimensions.Item1, dimensions.Item2, style, date.Year, date.MonthDay);

        File.WriteAllLines(Path.Combine(args[0], "manifest.txt"), log);
        Console.WriteLine($"PASS v2.1 actual compact Clock QA {log.Count - 2} images");
        return 0;
    }

    private static void Render(
        string output,
        List<string> log,
        double dpi,
        double width,
        double height,
        ClockStyle style,
        bool year,
        bool monthDay)
    {
        var item = new CanvasItem
        {
            ContentKind = CanvasContentKinds.Clock,
            Width = width,
            Height = height,
            Theme = WidgetThemeKind.Ocean,
            Clock = new ClockOptions
            {
                Style = style,
                ShowYear = year,
                ShowMonthDay = monthDay,
                ShowSeconds = true
            }
        };

        using var content = new ClockItemContent(item);
        var window = new MediaWindow(
            item,
            content,
            new DesktopWindowService(),
            static (_, _) => { },
            static _ => { },
            static _ => { },
            static _ => { });

        content.SetActive(true);
        ApplyCompact.Invoke(window, null);
        window.Measure(new Size(window.Width, window.Height));
        window.Arrange(new Rect(0, 0, window.Width, window.Height));
        window.UpdateLayout();
        ApplyCompact.Invoke(window, null);

        var rotated = (FrameworkElement)RotatedVisualField.GetValue(window)!;
        rotated.Measure(new Size(width, height));
        rotated.Arrange(new Rect(0, 0, width, height));
        rotated.UpdateLayout();

        var card = (Border)content.View;
        var presenter = (ContentControl)ItemContentField.GetValue(window)!;
        var scale = presenter.LayoutTransform is ScaleTransform transform ? transform.ScaleX : 1d;
        var pixelWidth = Math.Max(1, (int)Math.Round(width * dpi));
        var pixelHeight = Math.Max(1, (int)Math.Round(height * dpi));
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * dpi, 96 * dpi, PixelFormats.Pbgra32);
        bitmap.Render(rotated);

        var file = $"v21-actual-clock-{style.ToString().ToLowerInvariant()}-{width:0}x{height:0}-y{year}-d{monthDay}-{(dpi == 1 ? 100 : 150)}.png";
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, file)))
            encoder.Save(stream);

        log.Add(
            $"{file} item={item.Width:0.##}x{item.Height:0.##} " +
            $"rotated={rotated.ActualWidth:0.##}x{rotated.ActualHeight:0.##} " +
            $"card={card.ActualWidth:0.##}x{card.ActualHeight:0.##} " +
            $"scale={scale:0.###} padding={card.Padding.Left:0.##} radius={card.CornerRadius.TopLeft:0.##}");

        content.SetActive(false);
    }
}
