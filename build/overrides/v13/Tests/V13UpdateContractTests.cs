using System.Runtime.CompilerServices;
using DeskCanvas.App.Services;

internal static class V13UpdateContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        Run("v1.3 semantic version comparison", TestSemanticVersions);
        Run("v1.3 release page allowlist", TestReleasePageAllowlist);
        Run("v1.3 update detection and duplicate suppression", TestUpdateDetection);
        Run("v1.3 update failures are harmless", TestFailureHandling);
        Run("v1.3 isolated preview update injection", TestPreviewInjection);
        Run("v1.3 release launcher injection", TestLauncherInjection);
    }

    private static void Run(string name, Action test)
    {
        test();
        Console.WriteLine($"PASS {name}");
    }

    private static void TestSemanticVersions()
    {
        True(SemanticVersion.TryParse("v1.2.3", out var stable));
        Equal(new Version(1, 2, 3, 0), stable);
        True(SemanticVersion.TryParse("1.2", out var shortVersion));
        Equal(new Version(1, 2, 0, 0), shortVersion);
        False(SemanticVersion.TryParse("v1.2.3-beta.1", out _));
        False(SemanticVersion.TryParse("not-a-version", out _));
    }

    private static void TestReleasePageAllowlist()
    {
        True(UpdateSafety.IsAllowedReleasePage(
            new Uri("https://github.com/dntrsan/DeskCanvas/releases/tag/v1.2.0")));
        True(UpdateSafety.IsAllowedReleasePage(
            new Uri("https://github.com/dntrsan/DeskCanvas/releases")));
        False(UpdateSafety.IsAllowedReleasePage(
            new Uri("http://github.com/dntrsan/DeskCanvas/releases/tag/v1.2.0")));
        False(UpdateSafety.IsAllowedReleasePage(
            new Uri("https://github.com/dntrsan/DeskCanvas/releases-malicious")));
        False(UpdateSafety.IsAllowedReleasePage(
            new Uri("https://example.com/dntrsan/DeskCanvas/releases")));
    }

    private static void TestUpdateDetection()
    {
        var feed = new QueueFeed(
            Release("v1.0.1"),
            Release("v1.2.0"),
            Release("v1.3.0"));
        var service = new UpdateCheckService(feed, new Version(1, 1, 0));
        Equal(null, service.CheckOnceAsync(CancellationToken.None).GetAwaiter().GetResult());
        Equal(
            "v1.2.0",
            service.CheckOnceAsync(CancellationToken.None).GetAwaiter().GetResult()?.Version);
        Equal(null, service.CheckOnceAsync(CancellationToken.None).GetAwaiter().GetResult());
    }

    private static void TestFailureHandling()
    {
        var canceled = new UpdateCheckService(
            new ThrowingFeed(new TaskCanceledException("timeout")),
            new Version(1, 1, 0));
        Equal(
            null,
            canceled.CheckOnceAsync(CancellationToken.None).GetAwaiter().GetResult());

        var malformed = new UpdateCheckService(
            new ThrowingFeed(new System.Text.Json.JsonException("bad json")),
            new Version(1, 1, 0));
        Equal(
            null,
            malformed.CheckOnceAsync(CancellationToken.None).GetAwaiter().GetResult());
    }

    private static void TestPreviewInjection()
    {
        var previous = Environment.GetEnvironmentVariable(
            "DESKCANVAS_PREVIEW_FAKE_UPDATE");
        try
        {
            Environment.SetEnvironmentVariable(
                "DESKCANVAS_PREVIEW_FAKE_UPDATE",
                "v9.9.9");
            var prompt = new FakePrompt();
            var coordinator = new UpdatePromptCoordinator(
                new UpdateCheckService(new QueueFeed(), new Version(1, 1, 0)),
                prompt);
            coordinator.CheckAfterStartupAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Equal("v9.9.9", prompt.Release?.Version);
            Equal(0, prompt.FeedWasCalled);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "DESKCANVAS_PREVIEW_FAKE_UPDATE",
                previous);
        }
    }

    private static void TestLauncherInjection()
    {
        var launcher = new FakeLauncher();
        var safe = new Uri(
            "https://github.com/dntrsan/DeskCanvas/releases/tag/v1.2.0");
        True(launcher.TryOpen(safe));
        Equal(safe, launcher.Last);
    }

    private static ReleaseInfo Release(string version) =>
        new(
            version,
            new Uri(
                $"https://github.com/dntrsan/DeskCanvas/releases/tag/{version}"));

    private static void True(bool value)
    {
        if (!value)
            throw new InvalidOperationException("expected true");
    }

    private static void False(bool value)
    {
        if (value)
            throw new InvalidOperationException("expected false");
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(
                $"expected {expected}, actual {actual}");
    }

    private sealed class QueueFeed(params ReleaseInfo?[] releases) : IReleaseFeed
    {
        private readonly Queue<ReleaseInfo?> values = new(releases);
        public Task<ReleaseInfo?> GetLatestStableAsync(CancellationToken cancellationToken) =>
            Task.FromResult(values.Count == 0 ? null : values.Dequeue());
    }

    private sealed class ThrowingFeed(Exception exception) : IReleaseFeed
    {
        public Task<ReleaseInfo?> GetLatestStableAsync(CancellationToken cancellationToken) =>
            Task.FromException<ReleaseInfo?>(exception);
    }

    private sealed class FakePrompt : IUpdatePrompt
    {
        internal ReleaseInfo? Release { get; private set; }
        internal int FeedWasCalled => 0;
        public void Show(ReleaseInfo release) => Release = release;
    }

    private sealed class FakeLauncher : IReleasePageLauncher
    {
        internal Uri? Last { get; private set; }
        public bool TryOpen(Uri releasePage)
        {
            Last = releasePage;
            return true;
        }
    }
}
