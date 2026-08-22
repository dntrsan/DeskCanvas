namespace DeskCanvas.App.Services;

internal static class BackgroundLoadPolicy
{
    internal static readonly TimeSpan DesktopHostConnectedPollInterval = TimeSpan.FromSeconds(2);
    internal static readonly TimeSpan DesktopHostRecoveryPollInterval = TimeSpan.FromMilliseconds(250);
    internal static readonly TimeSpan NowPlayingReconciliationInterval = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan PlaybackTimelineInterval = TimeSpan.FromMilliseconds(250);
    internal static readonly TimeSpan PlaybackSpectrumInterval = TimeSpan.FromMilliseconds(125);
    internal static readonly TimeSpan WaveAnimationInterval = TimeSpan.FromMilliseconds(100);
    internal static TimeSpan DesktopHostPollInterval(bool connected) =>
        connected ? DesktopHostConnectedPollInterval : DesktopHostRecoveryPollInterval;

    internal static bool NeedsPlaybackTicks(bool active, bool playing, bool showTimeline, bool showSpectrum) =>
        active && playing && (showTimeline || showSpectrum);

    internal static TimeSpan PlaybackInterval(bool showSpectrum) =>
        showSpectrum ? PlaybackSpectrumInterval : PlaybackTimelineInterval;
}
