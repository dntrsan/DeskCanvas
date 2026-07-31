using DeskCanvas.Core;
using DeskCanvas.App.Services;

var tests = new (string Name, Action Run)[]
{
    ("角度を-180..180へ正規化する", TestAngles),
    ("配置を回収可能な位置へ戻す", TestClamp),
    ("一部を画面外へ配置できる", TestPartialOffscreen),
    ("中心が移動したモニターへ所属を切り替える", TestMonitorSelection),
    ("設定を保存して再読込できる", TestRoundTrip),
    ("壊れた設定からバックアップへ戻る", TestRecovery),
    ("v1画像とGIFをv2へ移行する", TestV1Migration),
    ("v2設定を往復保存する", TestV2RoundTrip),
    ("時計の日付表示4組合せ", TestClockDateFormats),
    ("一時非表示の優先順位", TestVisibility),
    ("未知と壊れた項目を無視する", TestItemTolerance),
    ("一時非表示を保存せず実行中の値も壊さない", TestTransientState),
    ("重複と空の項目IDを修復する", TestItemIdentityRepair),
    ("ライブ項目のv2設定を往復する", TestLiveOptionsRoundTrip),
    ("CPUと通信量の初回・リセット・欠損を扱う", TestMetricMath),
    ("GPUを物理エンジン単位で合算する", TestGpuAggregation),
    ("物理NICだけを合算してtunnel二重計上を防ぐ", TestPhysicalNetworkSelection),
    ("GSMTCのセッションなしと共有lease寿命", TestNowPlayingLeaseLifecycle),
    ("GSMTCの欠損metadataとsession切替を反映する", TestNowPlayingSessionSwitch),
    ("GSMTC操作のcapability・false・例外を守る", TestNowPlayingCommands)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception error)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {error.Message}");
    }
}

return failed == 0 ? 0 : 1;

static void TestAngles()
{
    Equal(-170d, Geometry.NormalizeDegrees(190));
    Equal(170d, Geometry.NormalizeDegrees(-190));
    Equal(0d, Geometry.NormalizeDegrees(720));
}

static void TestClamp()
{
    var item = new CanvasItem
    {
        MonitorDevice = "missing",
        CenterX = 5000,
        CenterY = -100,
        Width = 400,
        Height = 200
    };
    Geometry.ClampToDisplays(item, [new DisplayArea("primary", 0, 0, 1920, 1040, true)]);
    Equal("primary", item.MonitorDevice);
    Equal(2056d, item.CenterX);
    Equal(-36d, item.CenterY);
}

static void TestPartialOffscreen()
{
    var item = new CanvasItem
    {
        MonitorDevice = "primary",
        CenterX = -120,
        CenterY = 500,
        Width = 400,
        Height = 200
    };
    Geometry.ClampToDisplays(item, [new DisplayArea("primary", 0, 0, 1920, 1040, true)]);
    Equal(-120d, item.CenterX);
    Equal(500d, item.CenterY);
}

static void TestMonitorSelection()
{
    var item = new CanvasItem
    {
        MonitorDevice = "primary",
        CenterX = 2500,
        CenterY = 500,
        Width = 400,
        Height = 200
    };
    Geometry.ClampToDisplays(item,
    [
        new DisplayArea("primary", 0, 0, 1920, 1040, true),
        new DisplayArea("secondary", 1920, 0, 1920, 1040, false)
    ]);
    Equal("secondary", item.MonitorDevice);
    Equal(2500d, item.CenterX);
}

static void TestRoundTrip()
{
    WithTemporaryDirectory(root =>
    {
        var repository = new LayoutRepository(root);
        var layout = new CanvasLayout();
        layout.Items.Add(new CanvasItem
        {
            DisplayName = "sample.gif",
            StoredFileName = "id.gif",
            MediaKind = "gif",
            CenterX = 300,
            CenterY = 200,
            RotationDegrees = 45,
            Opacity = 0.7,
            IsFlipped = true,
            IsLocked = true
        });
        repository.Save(layout);
        var loaded = repository.Load();
        Equal(1, loaded.Items.Count);
        Equal("sample.gif", loaded.Items[0].DisplayName);
        Equal(45d, loaded.Items[0].RotationDegrees);
        Equal(true, loaded.Items[0].IsLocked);
    });
}

static void TestRecovery()
{
    WithTemporaryDirectory(root =>
    {
        var repository = new LayoutRepository(root);
        var layout = new CanvasLayout();
        layout.Items.Add(new CanvasItem { DisplayName = "recover.png" });
        repository.Save(layout);
        repository.Save(layout);
        File.WriteAllText(Path.Combine(root, "layout.json"), "{broken");
        Equal("recover.png", repository.Load().Items.Single().DisplayName);
    });
}

static void WithTemporaryDirectory(Action<string> action)
{
    var root = Path.Combine(Path.GetTempPath(), "DeskCanvasTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        action(root);
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"expected={expected}, actual={actual}");
    }
}

static void TestV1Migration()
{
    WithTemporaryDirectory(root =>
    {
        File.WriteAllText(Path.Combine(root, "layout.json"), """
{
          "version": 1, "items": [
            { "id":"11111111-1111-1111-1111-111111111111", "displayName":"old.gif", "storedFileName":"old.gif", "mediaKind":"gif", "monitorDevice":"DISPLAY2", "centerX":11, "centerY":22, "width":123, "height":88, "rotationDegrees":13, "opacity":0.4, "isFlipped":true, "isLocked":true, "zIndex":9 },
            { "id":"22222222-2222-2222-2222-222222222222", "displayName":"old.png", "storedFileName":"old.png", "mediaKind":"image", "centerX":44, "centerY":55, "width":222, "height":111, "rotationDegrees":-17, "opacity":0.8, "isFlipped":false, "isLocked":false, "zIndex":3 }
          ] }
""");
        var loaded = new LayoutRepository(root).Load();
        Equal(2, loaded.Version);
        var gif = loaded.Items.Single(item => item.ContentKind == CanvasContentKinds.Gif);
        Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), gif.Id);
        Equal("old.gif", gif.StoredFileName); Equal("DISPLAY2", gif.MonitorDevice);
        Equal(11d, gif.CenterX); Equal(22d, gif.CenterY); Equal(123d, gif.Width); Equal(88d, gif.Height);
        Equal(13d, gif.RotationDegrees); Equal(0.4d, gif.Opacity); Equal(true, gif.IsFlipped); Equal(true, gif.IsLocked); Equal(9, gif.ZIndex);
        var image = loaded.Items.Single(item => item.ContentKind == CanvasContentKinds.Image);
        Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), image.Id);
        Equal("old.png", image.StoredFileName); Equal(44d, image.CenterX); Equal(55d, image.CenterY);
        Equal(222d, image.Width); Equal(111d, image.Height); Equal(-17d, image.RotationDegrees); Equal(0.8d, image.Opacity); Equal(3, image.ZIndex);
    });
}

static void TestV2RoundTrip()
{
    WithTemporaryDirectory(root =>
    {
        var layout = new CanvasLayout();
        layout.Settings.HideAll = true;
        layout.Items.Add(new CanvasItem { ContentKind = CanvasContentKinds.Clock, DecorationMode = DecorationModes.OuterFrame, Clock = new ClockOptions { Style = ClockStyle.Split, ShowYear = true, ShowMonthDay = false }, IsTemporarilyHidden = true });
        var repository = new LayoutRepository(root); repository.Save(layout);
        var loaded = repository.Load(); var item = loaded.Items.Single();
        Equal(2, loaded.Version); Equal(false, loaded.Settings.HideAll); Equal(false, item.IsTemporarilyHidden);
        Equal(ClockStyle.Split, item.Clock.Style); Equal(true, item.Clock.ShowYear); Equal(false, item.Clock.ShowMonthDay); Equal(DecorationModes.OuterFrame, item.DecorationMode);
    });
}

static void TestClockDateFormats()
{
    var date = new DateTime(2026, 7, 27);
    Equal("2026年 7月27日", ClockFormatting.FormatDate(date, new ClockOptions { ShowYear = true, ShowMonthDay = true }));
    Equal("2026年", ClockFormatting.FormatDate(date, new ClockOptions { ShowYear = true, ShowMonthDay = false }));
    Equal("7月27日", ClockFormatting.FormatDate(date, new ClockOptions { ShowYear = false, ShowMonthDay = true }));
    Equal("", ClockFormatting.FormatDate(date, new ClockOptions { ShowYear = false, ShowMonthDay = false }));
}

static void TestVisibility()
{
    var settings = new CanvasSettings(); var item = new CanvasItem();
    Equal(true, CanvasVisibility.IsEffectivelyVisible(settings, item));
    item.IsTemporarilyHidden = true; Equal(false, CanvasVisibility.IsEffectivelyVisible(settings, item));
    settings.HideAll = true; item.IsTemporarilyHidden = false; Equal(false, CanvasVisibility.IsEffectivelyVisible(settings, item));
    settings.HideAll = false; Equal(true, CanvasVisibility.IsEffectivelyVisible(settings, item));
}

static void TestItemTolerance()
{
    WithTemporaryDirectory(root =>
    {
        File.WriteAllText(Path.Combine(root, "layout.json"), """
{
          "version":2, "items":[
            {"displayName":"ok", "contentKind":"clock"},
            {"displayName":"future", "contentKind":"hologram"},
            {"displayName":"bad", "width":"not-a-number"}
          ] }
""");
        var loaded = new LayoutRepository(root).Load();
        Equal(1, loaded.Items.Count); Equal("ok", loaded.Items[0].DisplayName);
    });
}
static void TestTransientState()
{
    WithTemporaryDirectory(root =>
    {
        var item = new CanvasItem { ContentKind = CanvasContentKinds.Gif, IsTemporarilyHidden = true };
        var layout = new CanvasLayout { Settings = new CanvasSettings { HideAll = true }, Items = [item] };
        var repository = new LayoutRepository(root);
        repository.Save(layout);

        Equal(true, layout.Settings.HideAll);
        Equal(true, item.IsTemporarilyHidden);
        var json = File.ReadAllText(Path.Combine(root, "layout.json"));
        Equal(false, json.Contains("HideAll", StringComparison.Ordinal));
        Equal(false, json.Contains("IsTemporarilyHidden", StringComparison.Ordinal));
        Equal(false, json.Contains("mediaKind", StringComparison.OrdinalIgnoreCase));

        var loaded = repository.Load();
        Equal(false, loaded.Settings.HideAll);
        Equal(false, loaded.Items.Single().IsTemporarilyHidden);
    });
}

static void TestItemIdentityRepair()
{
    WithTemporaryDirectory(root =>
    {
        File.WriteAllText(Path.Combine(root, "layout.json"), """
{
  "version": 2,
  "items": [
    { "id":"00000000-0000-0000-0000-000000000000", "displayName":"empty", "contentKind":"clock" },
    { "id":"33333333-3333-3333-3333-333333333333", "displayName":"first", "contentKind":"clock" },
    { "id":"33333333-3333-3333-3333-333333333333", "displayName":"second", "contentKind":"clock", "clock": { "style": 99 } }
  ]
}
""");
        var items = new LayoutRepository(root).Load().Items;
        Equal(3, items.Count);
        Equal(3, items.Select(item => item.Id).Distinct().Count());
        Equal(false, items.Any(item => item.Id == Guid.Empty));
        Equal(ClockStyle.Digital, items.Single(item => item.DisplayName == "second").Clock.Style);
    });
}
static void TestLiveOptionsRoundTrip()
{
    WithTemporaryDirectory(root =>
    {
        var layout = new CanvasLayout { Items = [
            new CanvasItem { ContentKind = CanvasContentKinds.NowPlaying, NowPlaying = new NowPlayingOptions { ShowAlbumArt = false, ShowTimeline = true, ShowSourceApp = false } },
            new CanvasItem { ContentKind = CanvasContentKinds.SystemMonitor, SystemMonitor = new SystemMonitorOptions { ShowCpu = false, ShowMemory = true, ShowGpu = false, ShowNetwork = true } }
        ] };
        var repo = new LayoutRepository(root); repo.Save(layout); var loaded = repo.Load();
        var playing = loaded.Items.Single(item => item.ContentKind == CanvasContentKinds.NowPlaying); var monitor = loaded.Items.Single(item => item.ContentKind == CanvasContentKinds.SystemMonitor);
        Equal(false, playing.NowPlaying.ShowAlbumArt); Equal(false, playing.NowPlaying.ShowSourceApp); Equal(false, monitor.SystemMonitor.ShowCpu); Equal(false, monitor.SystemMonitor.ShowGpu);
    });
}

static void TestMetricMath()
{
    var now = DateTimeOffset.UtcNow;
    Equal(MetricAvailability.Loading, SystemMetricMath.Cpu(10, 100, 100, null, now).Availability);
    Equal(95d, Math.Round(SystemMetricMath.Cpu(20, 200, 200, (10, 100, 100, now - TimeSpan.FromSeconds(1)), now).Value!.Value));
    Equal(MetricAvailability.Loading, SystemMetricMath.Cpu(2, 2, 2, (10, 10, 10, now - TimeSpan.FromSeconds(1)), now).Availability);
    Equal(MetricAvailability.Loading, SystemMetricMath.Rate(1, 9, TimeSpan.FromSeconds(1), "秒").Availability);
    Equal(MetricAvailability.Loading, SystemMetricMath.Rate(20, 10, TimeSpan.FromSeconds(11), "秒").Availability);
    Equal("1 KB/秒", SystemMetricMath.Rate(1024, 0, TimeSpan.FromSeconds(1), "秒").Text);
}
static void TestPhysicalNetworkSelection()
{
    var samples = new[]
    {
        new NetworkCounterSample(true, true, 6, 1_000, 500),
        new NetworkCounterSample(false, true, 6, 1_000, 500),
        new NetworkCounterSample(true, true, 131, 1_000, 500),
        new NetworkCounterSample(true, true, 24, 1_000, 500),
        new NetworkCounterSample(true, false, 71, 1_000, 500),
        new NetworkCounterSample(true, true, 71, 2_000, 800)
    };
    Equal(true, SystemMetricMath.TrySumPhysicalNetwork(samples, out var received, out var sent));
    Equal(3_000L, received);
    Equal(1_300L, sent);
    Equal(false, SystemMetricMath.TrySumPhysicalNetwork(samples.Where(sample => !sample.IsHardware), out _, out _));
}
static void TestGpuAggregation()
{
    var samples = new[]
    {
        new GpuCounterSample("pid_10_luid_0x00000000_0x0000AAAA_phys_0_eng_1_engtype_3D", 30),
        new GpuCounterSample("pid_20_luid_0x00000000_0x0000AAAA_phys_0_eng_1_engtype_3D#1", 45),
        new GpuCounterSample("pid_30_luid_0x00000000_0x0000AAAA_phys_0_eng_2_engtype_Copy", 60),
        new GpuCounterSample("pid_40_luid_0x00000000_0x0000BBBB_phys_0_eng_1_engtype_3D", 70),
        new GpuCounterSample("not-a-gpu-engine", 999),
        new GpuCounterSample("pid_50_luid_0x0_0xAAAA_phys_0_eng_1_engtype_3D", 99, false)
    };
    var value = SystemMetricMath.BusiestGpuEngine(samples);
    Equal(MetricAvailability.Available, value.Availability);
    Equal(75d, value.Value!.Value);

    value = SystemMetricMath.BusiestGpuEngine(new[]
    {
        new GpuCounterSample("pid_1_luid_0x0_0x1_phys_0_eng_0_engtype_Compute_0", 80),
        new GpuCounterSample("pid_2_luid_0x0_0x1_phys_0_eng_0_engtype_Compute_0", 50)
    });
    Equal(100d, value.Value!.Value);
    Equal(MetricAvailability.Loading, SystemMetricMath.BusiestGpuEngine(new[] { new GpuCounterSample("bad", 1) }).Availability);
}

static void TestNowPlayingLeaseLifecycle()
{
    var manager = new FakeNowPlayingManager();
    var provider = new FakeNowPlayingProvider(manager);
    using var service = new NowPlayingService(provider);
    var first = service.Acquire();
    var second = service.Acquire();
    WaitUntil(() => provider.RequestCount == 1);
    Equal(false, service.Snapshot.HasSession);
    Equal(1, manager.SubscriberCount);
    first.Dispose();
    Thread.Sleep(30);
    Equal(false, manager.Disposed);
    second.Dispose();
    WaitUntil(() => manager.Disposed);
    Equal(0, manager.SubscriberCount);
    service.Dispose();
    service.Acquire().Dispose();
    Equal(1, provider.RequestCount);
}

static void TestNowPlayingSessionSwitch()
{
    var first = new FakeNowPlayingSession(new NowPlayingData("player.one", "  ", null, null, null, NowPlayingState.Paused, TimeSpan.FromSeconds(-2), TimeSpan.FromSeconds(100), true, true, true, true));
    var manager = new FakeNowPlayingManager { Current = first };
    using var service = new NowPlayingService(new FakeNowPlayingProvider(manager));
    var loadedOnce = false;
    var staleClearObserved = false;
    service.SnapshotChanged += (_, snapshot) => { if (loadedOnce && !snapshot.HasSession) staleClearObserved = true; };
    using var lease = service.Acquire();
    WaitUntil(() => service.Snapshot.HasSession);
    loadedOnce = true;
    Equal("タイトル不明", service.Snapshot.Title);
    Equal("", service.Snapshot.Artist);
    Equal(TimeSpan.Zero, service.Snapshot.Position);
    Equal(3, first.SubscriberCount);

    first.Data = first.Data with { Title = "更新後", Artist = "artist" };
    first.RaiseMedia();
    WaitUntil(() => service.Snapshot.Title == "更新後");
    first.Data = first.Data with { Position = TimeSpan.FromSeconds(42) };
    first.RaiseTimeline();
    WaitUntil(() => service.Snapshot.Position == TimeSpan.FromSeconds(42));

    var second = new FakeNowPlayingSession(new NowPlayingData("player.two", "次の曲", "second", "album", new byte[] { 1, 2 }, NowPlayingState.Playing, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(200), false, true, false, true));
    manager.Current = second;
    manager.RaiseCurrentSessionChanged();
    WaitUntil(() => service.Snapshot.Title == "次の曲");
    Equal(true, staleClearObserved);
    Equal(true, first.Disposed);
    Equal(0, first.SubscriberCount);
    Equal("player.two", service.Snapshot.SourceApp);

    manager.Current = null;
    manager.RaiseCurrentSessionChanged();
    WaitUntil(() => !service.Snapshot.HasSession && second.Disposed);
    Equal("再生中のコンテンツはありません", service.Snapshot.Title);
}

static void TestNowPlayingCommands()
{
    var session = new FakeNowPlayingSession(new NowPlayingData("player", "song", "artist", "album", null, NowPlayingState.Paused, TimeSpan.Zero, TimeSpan.FromSeconds(60), false, false, false, false));
    var manager = new FakeNowPlayingManager { Current = session };
    using var service = new NowPlayingService(new FakeNowPlayingProvider(manager), new UnavailableMediaCommandSender());
    using var lease = service.Acquire();
    WaitUntil(() => service.Snapshot.HasSession);

    Equal(false, service.PreviousAsync().GetAwaiter().GetResult());
    Equal(0, session.CommandCalls);

    session.Data = session.Data with { CanPrevious = true, CanPlayPause = true, CanNext = true, CanSeek = true };
    session.RaisePlayback();
    WaitUntil(() => service.Snapshot.CanPrevious && service.Snapshot.CanSeek);
    session.CommandResult = false;
    Equal(false, service.PreviousAsync().GetAwaiter().GetResult());
    Equal(1, session.CommandCalls);

    session.ThrowCommand = true;
    Equal(false, service.NextAsync().GetAwaiter().GetResult());
    session.ThrowCommand = false;
    session.CommandResult = true;
    Equal(true, service.SeekAsync(TimeSpan.FromSeconds(90)).GetAwaiter().GetResult());
    Equal(TimeSpan.FromSeconds(60), session.LastPosition);
}

static void WaitUntil(Func<bool> condition)
{
    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
    while (!condition())
    {
        if (DateTime.UtcNow >= deadline) throw new TimeoutException("condition was not reached");
        Thread.Sleep(10);
    }
}

sealed class FakeNowPlayingProvider(FakeNowPlayingManager manager) : INowPlayingManagerProvider
{
    public int RequestCount { get; private set; }
    public Task<INowPlayingManager> RequestAsync()
    {
        RequestCount++;
        return Task.FromResult<INowPlayingManager>(manager);
    }
}

sealed class FakeNowPlayingManager : INowPlayingManager
{
    private EventHandler? changed;
    public INowPlayingSession? CurrentSession => Current;
    public FakeNowPlayingSession? Current { get; set; }
    public int SubscriberCount { get; private set; }
    public bool Disposed { get; private set; }
    public event EventHandler? CurrentSessionChanged
    {
        add { changed += value; SubscriberCount++; }
        remove { changed -= value; SubscriberCount--; }
    }
    public void RaiseCurrentSessionChanged() => changed?.Invoke(this, EventArgs.Empty);
    public void Dispose() => Disposed = true;
}

sealed class FakeNowPlayingSession(NowPlayingData data) : INowPlayingSession
{
    private EventHandler? media;
    private EventHandler? playback;
    private EventHandler? timeline;
    public NowPlayingData Data { get; set; } = data;
    public bool Disposed { get; private set; }
    public int SubscriberCount { get; private set; }
    public int CommandCalls { get; private set; }
    public bool CommandResult { get; set; } = true;
    public bool ThrowCommand { get; set; }
    public TimeSpan LastPosition { get; private set; }
    public event EventHandler? MediaPropertiesChanged { add { media += value; SubscriberCount++; } remove { media -= value; SubscriberCount--; } }
    public event EventHandler? PlaybackInfoChanged { add { playback += value; SubscriberCount++; } remove { playback -= value; SubscriberCount--; } }
    public event EventHandler? TimelinePropertiesChanged { add { timeline += value; SubscriberCount++; } remove { timeline -= value; SubscriberCount--; } }
    public Task<NowPlayingData> ReadAsync() => Task.FromResult(Data);
    public Task<bool> TryCommandAsync(NowPlayingCommand command, TimeSpan position = default)
    {
        CommandCalls++;
        LastPosition = position;
        if (ThrowCommand) throw new InvalidOperationException("fake command failure");
        return Task.FromResult(CommandResult);
    }
    public void RaiseMedia() => media?.Invoke(this, EventArgs.Empty);
    public void RaisePlayback() => playback?.Invoke(this, EventArgs.Empty);
    public void RaiseTimeline() => timeline?.Invoke(this, EventArgs.Empty);
    public void Dispose() => Disposed = true;
}

sealed class UnavailableMediaCommandSender : IGlobalMediaCommandSender
{
    public bool IsAvailable => false;
    public bool TrySend(NowPlayingCommand command) => false;
}
