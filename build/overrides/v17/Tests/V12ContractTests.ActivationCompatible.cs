using System.Runtime.CompilerServices;
using System.Text.Json;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;

internal static class V12ContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        Run("v1.2 CPU clock formatting", TestProcessorClock);
        Run("v1.2 theme JSON and normalization", TestThemePersistence);
        Run("v1.2 opaque theme surfaces", TestOpaqueThemeSurfaces);
        Run("v1.2 activation routing", TestActivationRouting);
        Run("v1.2 GSMTC continuity and reset", TestNowPlayingContinuity);
        Run("v1.2 playing projection", TestProjection);
    }

    private static void Run(string name, Action test)
    {
        test();
        Console.WriteLine($"PASS {name}");
    }

    private static void TestProcessorClock()
    {
        Equal("2.50 GHz", SystemMetricMath.FormatProcessorClock([2400, 2600]));
        Equal("-- GHz", SystemMetricMath.FormatProcessorClock([0, 0]));
        Equal("2.40 GHz", SystemMetricMath.FormatProcessorClock([0, 2400, 0]));
    }

    private static void TestThemePersistence()
    {
        var original = new CanvasLayout
        {
            Items =
            [
                new CanvasItem
                {
                    ContentKind = CanvasContentKinds.NowPlaying,
                    Theme = WidgetThemeKind.Ocean
                }
            ]
        };
        var json = JsonSerializer.Serialize(original);
        var clone = JsonSerializer.Deserialize<CanvasLayout>(json)
            ?? throw new InvalidOperationException("theme JSON clone was null");
        Equal(WidgetThemeKind.Ocean, clone.Items.Single().Theme);

        var unknown = JsonSerializer.Deserialize<CanvasItem>("""{"Theme":999}""")
            ?? throw new InvalidOperationException("unknown theme JSON was null");
        Equal(WidgetThemeKind.Auto, unknown.Theme);

        var direct = new CanvasItem { Theme = (WidgetThemeKind)999 };
        Equal(WidgetThemeKind.Auto, direct.Theme);
    }

    private static void TestOpaqueThemeSurfaces()
    {
        foreach (var kind in Enum.GetValues<WidgetThemeKind>())
        {
            var palette = WidgetTheme.Resolve(new CanvasItem { Theme = kind });
            var brush = WidgetTheme.Gradient(palette.SurfaceA, palette.SurfaceB);
            if (brush.GradientStops.Any(stop => stop.Color.A != byte.MaxValue))
                throw new InvalidOperationException($"{kind} contains a translucent surface");
        }
    }

    private static void TestActivationRouting()
    {
        var fake = new FakeActivationPlatform();
        var service = new ActivationService(fake);

        if (!service.TryActivate("Contoso.Player_123!App"))
            throw new InvalidOperationException("valid AUMID was rejected");
        Equal("Contoso.Player_123!App", fake.LastAumid);
        Equal(null, fake.LastProcess);

        fake.Reset();
        if (!service.TryActivate("MusicPlayer.exe"))
            throw new InvalidOperationException("valid process identity was rejected");
        Equal("MusicPlayer.exe", fake.LastProcess);
        Equal(null, fake.LastAumid);

        fake.Reset();
        if (service.TryActivate("powershell -command nope") ||
            service.TryActivate(@"C:\Windows\notepad.exe") ||
            service.TryActivate("https://example.invalid"))
            throw new InvalidOperationException("unsafe activation input was accepted");
        Equal(0, fake.CallCount);
    }

    private static void TestNowPlayingContinuity()
    {
        var first = new FakeSession(Data(
            title: "Sixty Seconds",
            end: TimeSpan.FromSeconds(60),
            position: TimeSpan.FromSeconds(10),
            state: NowPlayingState.Playing,
            canSeek: true));
        var manager = new FakeManager(first);
        var service = new NowPlayingService(new FakeProvider(manager));
        var lease = service.Acquire();

        WaitUntil(() => service.Snapshot.End == TimeSpan.FromSeconds(60));
        Equal("Sixty Seconds", service.Snapshot.Title);

        first.Value = Data(
            title: "Sixty Seconds",
            end: TimeSpan.Zero,
            position: TimeSpan.FromSeconds(12),
            state: NowPlayingState.Playing,
            canSeek: false);
        first.RaiseTimeline();
        WaitUntil(() => !service.Snapshot.CanSeek &&
                        service.Snapshot.Position >= TimeSpan.FromSeconds(12));
        Equal(TimeSpan.FromSeconds(60), service.Snapshot.End);
        if (service.SeekAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult())
            throw new InvalidOperationException("seek succeeded while CanSeek was false");
        Equal(0, first.SeekCalls);

        var readsBeforeException = first.ReadCount;
        first.ThrowOnRead = true;
        first.RaiseTimeline();
        WaitUntil(() => first.ReadCount > readsBeforeException);
        Equal("Sixty Seconds", service.Snapshot.Title);
        Equal(TimeSpan.FromSeconds(60), service.Snapshot.End);

        var reconnect = new FakeSession(Data(
            title: "Sixty Seconds",
            end: TimeSpan.Zero,
            position: TimeSpan.FromSeconds(13),
            state: NowPlayingState.Paused,
            canSeek: false));
        manager.Current = reconnect;
        manager.RaiseCurrentSessionChanged();
        WaitUntil(() => reconnect.ReadCount > 0 &&
                        service.Snapshot.Position == TimeSpan.FromSeconds(13));
        Equal(TimeSpan.FromSeconds(60), service.Snapshot.End);

        reconnect.Value = Data(
            title: "Another Song",
            end: TimeSpan.Zero,
            position: TimeSpan.Zero,
            state: NowPlayingState.Paused,
            canSeek: false);
        reconnect.RaiseMedia();
        WaitUntil(() => service.Snapshot.Title == "Another Song");
        Equal(TimeSpan.Zero, service.Snapshot.End);

        manager.Current = null;
        manager.RaiseCurrentSessionChanged();
        WaitUntil(() => !service.Snapshot.HasSession);
        Equal(TimeSpan.Zero, service.Snapshot.End);

        service.Dispose();
        lease.Dispose();
        if (!manager.Disposed || !first.Disposed || !reconnect.Disposed)
            throw new InvalidOperationException("manager/session lifetime leaked");
        Equal(0, manager.HandlerCount);
        Equal(0, first.HandlerCount);
        Equal(0, reconnect.HandlerCount);
    }

    private static void TestProjection()
    {
        var observed = new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero);
        var playing = new NowPlayingSnapshot(
            true,
            "player.exe",
            "Song",
            "Artist",
            "Album",
            null,
            NowPlayingState.Playing,
            TimeSpan.FromSeconds(58),
            TimeSpan.FromSeconds(60),
            true,
            true,
            true,
            true,
            observed);
        Equal(
            TimeSpan.FromSeconds(60),
            NowPlayingService.Project(playing, observed.AddSeconds(5)).Position);

        var paused = playing with
        {
            State = NowPlayingState.Paused,
            Position = TimeSpan.FromSeconds(11)
        };
        Equal(
            TimeSpan.FromSeconds(11),
            NowPlayingService.Project(paused, observed.AddSeconds(5)).Position);
    }

    private static NowPlayingData Data(
        string title,
        TimeSpan end,
        TimeSpan position,
        NowPlayingState state,
        bool canSeek) =>
        new(
            "MusicPlayer.exe",
            title,
            "Artist",
            "Album",
            null,
            state,
            position,
            end,
            true,
            true,
            true,
            canSeek);

    private static void WaitUntil(Func<bool> condition)
    {
        if (!SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(5)))
            throw new TimeoutException("asynchronous contract did not settle");
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"expected {expected}, actual {actual}");
    }

    private sealed class FakeActivationPlatform : IActivationPlatform
    {
        internal string? LastAumid { get; private set; }
        internal string? LastProcess { get; private set; }
        internal int CallCount { get; private set; }

        public bool ActivateAumid(string appUserModelId)
        {
            LastAumid = appUserModelId;
            CallCount++;
            return true;
        }

        public bool ActivateProcess(string executableName)
        {
            LastProcess = executableName;
            CallCount++;
            return true;
        }

        public bool ActivateUniqueTitleMatch(string title, string artist) => false;

        internal void Reset()
        {
            LastAumid = null;
            LastProcess = null;
            CallCount = 0;
        }
    }

    private sealed class FakeProvider(FakeManager manager) : INowPlayingManagerProvider
    {
        public Task<INowPlayingManager> RequestAsync() => Task.FromResult<INowPlayingManager>(manager);
    }

    private sealed class FakeManager(INowPlayingSession? current) : INowPlayingManager
    {
        private EventHandler? changed;
        internal bool Disposed { get; private set; }
        internal int HandlerCount => changed?.GetInvocationList().Length ?? 0;
        internal INowPlayingSession? Current { get; set; } = current;
        public INowPlayingSession? CurrentSession => Current;
        public event EventHandler? CurrentSessionChanged
        {
            add => changed += value;
            remove => changed -= value;
        }
        internal void RaiseCurrentSessionChanged() => changed?.Invoke(this, EventArgs.Empty);
        public void Dispose() => Disposed = true;
    }

    private sealed class FakeSession(NowPlayingData value) : INowPlayingSession
    {
        private EventHandler? media;
        private EventHandler? playback;
        private EventHandler? timeline;
        private int readCount;

        internal NowPlayingData Value { get; set; } = value;
        internal bool ThrowOnRead { get; set; }
        internal bool Disposed { get; private set; }
        internal int ReadCount => Volatile.Read(ref readCount);
        internal int SeekCalls { get; private set; }
        internal int HandlerCount =>
            (media?.GetInvocationList().Length ?? 0) +
            (playback?.GetInvocationList().Length ?? 0) +
            (timeline?.GetInvocationList().Length ?? 0);

        public event EventHandler? MediaPropertiesChanged
        {
            add => media += value;
            remove => media -= value;
        }
        public event EventHandler? PlaybackInfoChanged
        {
            add => playback += value;
            remove => playback -= value;
        }
        public event EventHandler? TimelinePropertiesChanged
        {
            add => timeline += value;
            remove => timeline -= value;
        }

        internal void RaiseMedia() => media?.Invoke(this, EventArgs.Empty);
        internal void RaiseTimeline() => timeline?.Invoke(this, EventArgs.Empty);

        public Task<NowPlayingData> ReadAsync()
        {
            Interlocked.Increment(ref readCount);
            return ThrowOnRead
                ? Task.FromException<NowPlayingData>(new InvalidOperationException("transient read"))
                : Task.FromResult(Value);
        }

        public Task<bool> TryCommandAsync(NowPlayingCommand command, TimeSpan position = default)
        {
            if (command == NowPlayingCommand.Seek) SeekCalls++;
            return Task.FromResult(true);
        }

        public void Dispose() => Disposed = true;
    }
}
