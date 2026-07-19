using DeskCanvas.Core;

var tests = new (string Name, Action Run)[]
{
    ("角度を-180..180へ正規化する", TestAngles),
    ("配置を利用可能なモニターへ戻す", TestClamp),
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
    Equal(1720d, item.CenterX);
    Equal(100d, item.CenterY);
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
