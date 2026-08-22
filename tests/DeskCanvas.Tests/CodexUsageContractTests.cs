using System.Runtime.CompilerServices;
using System.Text.Json;
using DeskCanvas.App.Services;
using DeskCanvas.Core;

internal static class CodexUsageContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        ParseLiveResponse();
        SelectCodexBucket();
        ParseOpenHistoryFile();
        VerifyCanvasContract();
        Console.WriteLine("PASS Codex limit parsing, fallback, and canvas contract");
    }

    private static void SelectCodexBucket()
    {
        using var document = JsonDocument.Parse("""
        {
          "rateLimits":{"primary":{"usedPercent":99}},
          "rateLimitsByLimitId":{
            "premium":{"primary":{"usedPercent":80}},
            "codex":{"primary":{"usedPercent":25}}
          }
        }
        """);
        var selected = CodexUsageService.SelectRateLimits(document.RootElement);
        Check(selected.GetProperty("primary").GetProperty("usedPercent").GetInt32() == 25);
    }

    private static void ParseLiveResponse()
    {
        using var document = JsonDocument.Parse("""
        {
          "limitId":"codex",
          "primary":{"usedPercent":40,"windowDurationMins":300,"resetsAt":1787040000},
          "secondary":{"usedPercent":10,"windowDurationMins":10080,"resetsAt":1787600000},
          "credits":{"hasCredits":true,"unlimited":false,"balance":"12.5"},
          "planType":"plus"
        }
        """);
        var snapshot = CodexUsageService.ParseRateLimits(
            document.RootElement,
            DateTimeOffset.UnixEpoch,
            CodexUsageSource.Live,
            null,
            camelCase: true);
        Check(snapshot.Primary?.RemainingPercent == 60);
        Check(snapshot.Primary?.WindowDurationMinutes == 300);
        Check(snapshot.Secondary?.RemainingPercent == 90);
        Check(snapshot.PlanType == "plus");
        Check(snapshot.CreditBalance == "12.5");
    }

    private static void ParseOpenHistoryFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "DeskCanvasCodexUsageTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "active.jsonl");
        try
        {
            using var writer = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
            using (var text = new StreamWriter(writer, leaveOpen: true))
            {
                text.WriteLine("""
                {"timestamp":"2026-08-12T02:13:26Z","type":"event_msg","payload":{"type":"token_count","rate_limits":{"limit_id":"codex","primary":{"used_percent":93.0,"window_minutes":10080,"resets_at":1787031045},"secondary":null,"credits":{"has_credits":false,"unlimited":false,"balance":"0"},"plan_type":"plus"}}}
                """);
                text.Flush();
            }
            var snapshot = CodexUsageService.ReadLatestRateLimit(new FileInfo(path));
            Check(snapshot is not null);
            Check(snapshot!.Source == CodexUsageSource.LocalHistory);
            Check(snapshot.Primary?.RemainingPercent == 7);
            Check(snapshot.Primary?.WindowDurationMinutes == 10080);
            Check(snapshot.UpdatedAt?.UtcDateTime == new DateTime(2026, 8, 12, 2, 13, 26, DateTimeKind.Utc));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void VerifyCanvasContract()
    {
        Check(CanvasContentKinds.IsSupported(CanvasContentKinds.CodexUsage));
        var item = new CanvasItem { ContentKind = CanvasContentKinds.CodexUsage };
        Check(item.KindLabel == "Codexリミット");
    }

    private static void Check(bool value)
    {
        if (!value) throw new InvalidOperationException("Codex usage contract failed.");
    }
}
