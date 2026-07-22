using DeskCanvas.Core;

var tests = new (string Name, Action Run)[]
{
    ("角度を-180..180へ正規化する", TestAngles),
    ("配置を回収可能な位置へ戻す", TestClamp),
    ("一部を画面外へ配置できる", TestPartialOffscreen),
    ("中心が移動したモニターへ所属を切り替える", TestMonitorSelection),
    ("設定を保存して再読込できる", TestRoundTrip),
    ("壊れた設定からバックアップへ戻る", TestRecovery)
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
