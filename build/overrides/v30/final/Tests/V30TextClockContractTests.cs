using System.Runtime.CompilerServices;
using System.Text.Json;
using DeskCanvas.Core;

internal static class V30TextClockContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var options = new ClockOptions
        {
            Backgroundless = true,
            FontFamily = "Segoe UI",
            TextColor = "#FF336699",
            FontWeight = ClockFontWeight.Bold
        };
        var clone = options.Clone();
        if (!clone.Backgroundless || clone.FontFamily != "Segoe UI" || clone.TextColor != "#FF336699" || clone.FontWeight != ClockFontWeight.Bold)
            throw new InvalidOperationException("文字時計のCloneに設定が残りません。");
        var fromJson = JsonSerializer.Deserialize<ClockOptions>(JsonSerializer.Serialize(options))!;
        if (!fromJson.Backgroundless || fromJson.FontWeight != ClockFontWeight.Bold)
            throw new InvalidOperationException("文字時計のJSON往復に設定が残りません。");
        var unknown = new ClockOptions { Style = (ClockStyle)999, FontWeight = (ClockFontWeight)999 }.Clone();
        if (unknown.Style != ClockStyle.Digital || unknown.FontWeight != ClockFontWeight.Regular)
            throw new InvalidOperationException("未知の時計設定が既定値へ戻りません。");
        Console.WriteLine("PASS v3.0 backgroundless clock options");
    }
}
