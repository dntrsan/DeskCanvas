using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Control;

namespace DeskCanvas.App.Services;

internal enum NowPlayingState { None, Playing, Paused, Stopped, Unknown }
internal enum NowPlayingCommand { Previous, PlayPause, Next, Seek }
internal sealed record NowPlayingSnapshot(bool HasSession, string SourceApp, string Title, string Artist, string Album, byte[]? Artwork, NowPlayingState State, TimeSpan Position, TimeSpan End, bool CanPrevious, bool CanPlayPause, bool CanNext, bool CanSeek, DateTimeOffset ObservedAt = default)
{
    internal static NowPlayingSnapshot Empty { get; } = new(false, "", "再生中のコンテンツはありません", "", "", null, NowPlayingState.None, TimeSpan.Zero, TimeSpan.Zero, false, false, false, false);
}
internal sealed record NowPlayingData(string? SourceApp, string? Title, string? Artist, string? Album, byte[]? Artwork, NowPlayingState State, TimeSpan Position, TimeSpan End, bool CanPrevious, bool CanPlayPause, bool CanNext, bool CanSeek);
internal interface INowPlayingManagerProvider { Task<INowPlayingManager> RequestAsync(); }
internal interface INowPlayingManager : IDisposable { INowPlayingSession? CurrentSession { get; } event EventHandler? CurrentSessionChanged; }
internal interface INowPlayingSession : IDisposable { event EventHandler? MediaPropertiesChanged; event EventHandler? PlaybackInfoChanged; event EventHandler? TimelinePropertiesChanged; Task<NowPlayingData> ReadAsync(); Task<bool> TryCommandAsync(NowPlayingCommand command, TimeSpan position = default); }
internal interface INowPlayingService : IDisposable { NowPlayingSnapshot Snapshot { get; } event EventHandler<NowPlayingSnapshot>? SnapshotChanged; IDisposable Acquire(); Task<bool> PreviousAsync(); Task<bool> PlayPauseAsync(); Task<bool> NextAsync(); Task<bool> SeekAsync(TimeSpan position); }

internal sealed class NowPlayingService : INowPlayingService
{
    private readonly INowPlayingManagerProvider provider; private readonly SemaphoreSlim gate = new(1, 1); private readonly object stateGate = new();
    private INowPlayingManager? manager; private INowPlayingSession? session; private int consumers; private bool disposed; private NowPlayingSnapshot snapshot = NowPlayingSnapshot.Empty; private string? mediaIdentity; private TimeSpan lastKnownEnd;
    internal NowPlayingService() : this(new GsmtcManagerProvider()) { }
    internal NowPlayingService(INowPlayingManagerProvider provider) => this.provider = provider;
    public NowPlayingSnapshot Snapshot => Project(snapshot, DateTimeOffset.UtcNow);
    public event EventHandler<NowPlayingSnapshot>? SnapshotChanged;
    public IDisposable Acquire() { var connect = false; lock (stateGate) { if (disposed) return EmptyLease.Instance; connect = ++consumers == 1; } if (connect) _ = ConnectAsync(); return new Lease(this); }
    private void Release() { var disconnect = false; lock (stateGate) { if (consumers > 0) consumers--; disconnect = consumers == 0; } if (disconnect) _ = DisconnectAsync(); }
    private bool ShouldRun { get { lock (stateGate) return !disposed && consumers > 0; } }
    private async Task ConnectAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false); try
        {
            if (!ShouldRun || manager is not null) return; INowPlayingManager? next = null;
            try { next = await provider.RequestAsync().ConfigureAwait(false); if (!ShouldRun) { next.Dispose(); return; } manager = next; manager.CurrentSessionChanged += Manager_CurrentSessionChanged; await SwitchSessionCoreAsync().ConfigureAwait(false); }
            catch (Exception) { next?.Dispose(); manager = null; Clear(); }
        }
        finally { gate.Release(); }
    }
    private async Task DisconnectAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false); try { if (ShouldRun) return; DetachSession(); if (manager is not null) { manager.CurrentSessionChanged -= Manager_CurrentSessionChanged; manager.Dispose(); manager = null; } Clear(); } finally { gate.Release(); }
    }
    private void Manager_CurrentSessionChanged(object? sender, EventArgs args) => _ = SwitchSessionAsync();
    private void Session_MediaPropertiesChanged(object? sender, EventArgs args) => _ = RefreshAsync();
    private void Session_PlaybackInfoChanged(object? sender, EventArgs args) => _ = RefreshAsync();
    private void Session_TimelinePropertiesChanged(object? sender, EventArgs args) => _ = RefreshAsync();
    private async Task SwitchSessionAsync() { await gate.WaitAsync().ConfigureAwait(false); try { if (ShouldRun && manager is not null) await SwitchSessionCoreAsync().ConfigureAwait(false); } catch (Exception) { /* Preserve last good same-session state during a reconnect. */ } finally { gate.Release(); } }
    private async Task SwitchSessionCoreAsync()
    {
        var next = manager?.CurrentSession; if (ReferenceEquals(next, session)) { await RefreshCoreAsync().ConfigureAwait(false); return; }
        DetachSession(); session = next; if (session is null) { Clear(); return; }
        // A new session is a real identity boundary; no stale duration may leak across it.
        mediaIdentity = null; lastKnownEnd = TimeSpan.Zero;
        session.MediaPropertiesChanged += Session_MediaPropertiesChanged; session.PlaybackInfoChanged += Session_PlaybackInfoChanged; session.TimelinePropertiesChanged += Session_TimelinePropertiesChanged;
        await RefreshCoreAsync().ConfigureAwait(false);
    }
    private async Task RefreshAsync() { await gate.WaitAsync().ConfigureAwait(false); try { if (ShouldRun && session is not null) await RefreshCoreAsync().ConfigureAwait(false); } catch (Exception) { /* GSMTC can throw transiently; retain the last valid display. */ } finally { gate.Release(); } }
    private async Task RefreshCoreAsync()
    {
        var current = session; if (current is null) { Clear(); return; }
        var data = await current.ReadAsync().ConfigureAwait(false); if (!ReferenceEquals(current, session) || !ShouldRun) return;
        var source = data.SourceApp?.Trim() ?? ""; var title = string.IsNullOrWhiteSpace(data.Title) ? "タイトル不明" : data.Title.Trim(); var artist = data.Artist?.Trim() ?? ""; var album = data.Album?.Trim() ?? "";
        var identity = string.Join("\u001f", source, title, artist, album); var rawEnd = data.End > TimeSpan.Zero ? data.End : TimeSpan.Zero;
        if (!string.Equals(identity, mediaIdentity, StringComparison.Ordinal)) { mediaIdentity = identity; lastKnownEnd = rawEnd; }
        else if (rawEnd > TimeSpan.Zero) lastKnownEnd = rawEnd;
        var end = rawEnd > TimeSpan.Zero ? rawEnd : lastKnownEnd;
        var position = data.Position < TimeSpan.Zero ? TimeSpan.Zero : (end > TimeSpan.Zero ? TimeSpan.FromTicks(Math.Min(data.Position.Ticks, end.Ticks)) : data.Position);
        Publish(new(true, source, title, artist, album, data.Artwork, data.State, position, end, data.CanPrevious, data.CanPlayPause, data.CanNext, data.CanSeek, DateTimeOffset.UtcNow));
    }
    private static NowPlayingSnapshot Project(NowPlayingSnapshot value, DateTimeOffset now)
    {
        if (!value.HasSession || value.State != NowPlayingState.Playing || value.End <= TimeSpan.Zero || value.ObservedAt == default) return value;
        var elapsed = now - value.ObservedAt; if (elapsed <= TimeSpan.Zero) return value; var position = value.Position + elapsed; if (position > value.End) position = value.End; return value with { Position = position };
    }
    public Task<bool> PreviousAsync() => InvokeAsync(NowPlayingCommand.Previous); public Task<bool> PlayPauseAsync() => InvokeAsync(NowPlayingCommand.PlayPause); public Task<bool> NextAsync() => InvokeAsync(NowPlayingCommand.Next); public Task<bool> SeekAsync(TimeSpan position) => InvokeAsync(NowPlayingCommand.Seek, position);
    private async Task<bool> InvokeAsync(NowPlayingCommand command, TimeSpan position = default)
    {
        var current = session; var currentSnapshot = Snapshot; if (current is null || !ReferenceEquals(current, session) || !IsEnabled(currentSnapshot, command)) return false;
        if (command == NowPlayingCommand.Seek) position = TimeSpan.FromTicks(Math.Clamp(position.Ticks, 0, currentSnapshot.End.Ticks));
        try { var ok = await current.TryCommandAsync(command, position).ConfigureAwait(false); if (ok && ReferenceEquals(current, session)) _ = RefreshAsync(); return ok; } catch (Exception) { return false; }
    }
    private static bool IsEnabled(NowPlayingSnapshot value, NowPlayingCommand command) => command switch { NowPlayingCommand.Previous => value.CanPrevious, NowPlayingCommand.PlayPause => value.CanPlayPause, NowPlayingCommand.Next => value.CanNext, NowPlayingCommand.Seek => value.CanSeek && value.End > TimeSpan.Zero, _ => false };
    private void DetachSession() { if (session is null) return; session.MediaPropertiesChanged -= Session_MediaPropertiesChanged; session.PlaybackInfoChanged -= Session_PlaybackInfoChanged; session.TimelinePropertiesChanged -= Session_TimelinePropertiesChanged; session.Dispose(); session = null; }
    private void Clear() { mediaIdentity = null; lastKnownEnd = TimeSpan.Zero; Publish(NowPlayingSnapshot.Empty); }
    private void Publish(NowPlayingSnapshot value) { snapshot = value; SnapshotChanged?.Invoke(this, Snapshot); }
    public void Dispose() { lock (stateGate) { if (disposed) return; disposed = true; consumers = 0; } gate.Wait(); try { DetachSession(); if (manager is not null) { manager.CurrentSessionChanged -= Manager_CurrentSessionChanged; manager.Dispose(); manager = null; } snapshot = NowPlayingSnapshot.Empty; mediaIdentity = null; lastKnownEnd = TimeSpan.Zero; } finally { gate.Release(); } }
    private sealed class Lease(NowPlayingService owner) : IDisposable { private NowPlayingService? owner = owner; public void Dispose() => Interlocked.Exchange(ref owner, null)?.Release(); }
    private sealed class EmptyLease : IDisposable { internal static readonly EmptyLease Instance = new(); public void Dispose() { } }
}

internal sealed class GsmtcManagerProvider : INowPlayingManagerProvider { public async Task<INowPlayingManager> RequestAsync() => new GsmtcManagerAdapter(await GlobalSystemMediaTransportControlsSessionManager.RequestAsync()); }
internal sealed class GsmtcManagerAdapter : INowPlayingManager
{
    private readonly GlobalSystemMediaTransportControlsSessionManager manager; private GlobalSystemMediaTransportControlsSession? nativeSession; private GsmtcSessionAdapter? adapter;
    internal GsmtcManagerAdapter(GlobalSystemMediaTransportControlsSessionManager manager) { this.manager = manager; manager.CurrentSessionChanged += Changed; }
    public INowPlayingSession? CurrentSession { get { var current = manager.GetCurrentSession(); if (ReferenceEquals(current, nativeSession)) return adapter; nativeSession = current; adapter = current is null ? null : new GsmtcSessionAdapter(current); return adapter; } }
    public event EventHandler? CurrentSessionChanged; private void Changed(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) => CurrentSessionChanged?.Invoke(this, EventArgs.Empty); public void Dispose() => manager.CurrentSessionChanged -= Changed;
}
internal sealed class GsmtcSessionAdapter : INowPlayingSession
{
    private readonly GlobalSystemMediaTransportControlsSession session;
    internal GsmtcSessionAdapter(GlobalSystemMediaTransportControlsSession session) { this.session = session; session.MediaPropertiesChanged += MediaChanged; session.PlaybackInfoChanged += PlaybackChanged; session.TimelinePropertiesChanged += TimelineChanged; }
    public event EventHandler? MediaPropertiesChanged; public event EventHandler? PlaybackInfoChanged; public event EventHandler? TimelinePropertiesChanged;
    private void MediaChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => MediaPropertiesChanged?.Invoke(this, EventArgs.Empty); private void PlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => PlaybackInfoChanged?.Invoke(this, EventArgs.Empty); private void TimelineChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args) => TimelinePropertiesChanged?.Invoke(this, EventArgs.Empty);
    public async Task<NowPlayingData> ReadAsync()
    {
        var media = await session.TryGetMediaPropertiesAsync(); var playback = session.GetPlaybackInfo(); var timeline = session.GetTimelineProperties(); var controls = playback.Controls; byte[]? artwork = null;
        try { if (media.Thumbnail is not null) { using var random = await media.Thumbnail.OpenReadAsync(); using var input = random.AsStreamForRead(); using var output = new MemoryStream(); await input.CopyToAsync(output).ConfigureAwait(false); artwork = output.ToArray(); } } catch (Exception) { }
        return new(session.SourceAppUserModelId, media.Title, media.Artist, media.AlbumTitle, artwork, playback.PlaybackStatus switch { GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => NowPlayingState.Playing, GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => NowPlayingState.Paused, GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => NowPlayingState.Stopped, _ => NowPlayingState.Unknown }, timeline.Position, timeline.EndTime, controls.IsPreviousEnabled, controls.IsPlayPauseToggleEnabled, controls.IsNextEnabled, controls.IsPlaybackPositionEnabled);
    }
    public async Task<bool> TryCommandAsync(NowPlayingCommand command, TimeSpan position = default) => command switch { NowPlayingCommand.Previous => await session.TrySkipPreviousAsync(), NowPlayingCommand.PlayPause => await session.TryTogglePlayPauseAsync(), NowPlayingCommand.Next => await session.TrySkipNextAsync(), NowPlayingCommand.Seek => await session.TryChangePlaybackPositionAsync(position.Ticks), _ => false };
    public void Dispose() { session.MediaPropertiesChanged -= MediaChanged; session.PlaybackInfoChanged -= PlaybackChanged; session.TimelinePropertiesChanged -= TimelineChanged; }
}
