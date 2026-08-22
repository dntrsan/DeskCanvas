using System.Runtime.CompilerServices;
using DeskCanvas.App.Services;

internal static class BackgroundLoadPolicyTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        Equal(TimeSpan.FromSeconds(2), BackgroundLoadPolicy.DesktopHostPollInterval(connected: true));
        Equal(TimeSpan.FromMilliseconds(250), BackgroundLoadPolicy.DesktopHostPollInterval(connected: false));
        Equal(TimeSpan.FromSeconds(5), BackgroundLoadPolicy.NowPlayingReconciliationInterval);
        Equal(false, BackgroundLoadPolicy.NeedsPlaybackTicks(true, true, false, false));
        Equal(false, BackgroundLoadPolicy.NeedsPlaybackTicks(false, true, true, true));
        Equal(true, BackgroundLoadPolicy.NeedsPlaybackTicks(true, true, true, false));
        Equal(true, BackgroundLoadPolicy.NeedsPlaybackTicks(true, true, false, true));
        Equal(TimeSpan.FromMilliseconds(125), BackgroundLoadPolicy.PlaybackInterval(showSpectrum: true));
        Equal(TimeSpan.FromMilliseconds(250), BackgroundLoadPolicy.PlaybackInterval(showSpectrum: false));
        Console.WriteLine("PASS background load policy avoids unnecessary polling and rendering");
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"expected={expected}, actual={actual}");
    }
}
