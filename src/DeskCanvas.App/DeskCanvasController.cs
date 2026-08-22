using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using DeskCanvas.App.Media;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace DeskCanvas.App;

internal sealed class DeskCanvasController : IDisposable
{
    private readonly LayoutRepository repository;
    private readonly MediaStore mediaStore;
    private readonly DesktopWindowService desktop = new();
    private readonly CanvasLayout layout;
    private readonly Dictionary<Guid, MediaWindow> mediaWindows = [];
    private readonly DispatcherTimer desktopTimer;
    private readonly DispatcherTimer persistTimer;
    private readonly INowPlayingService nowPlaying = new NowPlayingService();
    private readonly ISystemMetricsService systemMetrics = new SystemMetricsService();
    private readonly ICodexUsageService codexUsage;
    private readonly bool previewMode;
    private TrayService? tray;
    private HotkeyManager? hotkey;
    private MainWindow? mainWindow;
    private IntPtr lastDesktopHost;
    private bool disposed;

    internal DeskCanvasController(bool previewMode = false, string? dataRoot = null)
    {
        this.previewMode = previewMode;
        var root = dataRoot
            ?? Environment.GetEnvironmentVariable("DESKCANVAS_DATA_ROOT")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskCanvas");
        repository = new LayoutRepository(root);
        mediaStore = new MediaStore(root);
        layout = repository.Load();
        Items = new ObservableCollection<CanvasItem>(layout.Items.OrderBy(item => item.ZIndex));
        layout.Items = Items.ToList();
        codexUsage = new CodexUsageService(previewMode);
        desktopTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        desktopTimer.Tick += DesktopTimer_Tick;
        persistTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        persistTimer.Tick += (_, _) =>
        {
            persistTimer.Stop();
            SaveNow();
        };
    }

    internal ObservableCollection<CanvasItem> Items { get; }
    internal bool IsEditMode { get; private set; }
    internal bool IsExiting { get; private set; }
    internal bool StartWithWindows => layout.Settings.StartWithWindows;
    internal bool HideAll => layout.Settings.HideAll;
    internal event Action<bool>? EditModeChanged;
    internal event Action<bool>? HideAllChanged;
    internal event Action<string>? StatusChanged;

    internal void Start()
    {
        layout.Settings.StartWithWindows = StartupService.IsEnabled();
        mediaStore.Reclaim(Items.Select(item => item.StoredFileName));
        mainWindow = new MainWindow(this);
        tray = new TrayService(OpenMainWindow, AddFromDialog, OpenBuiltInPicker, ToggleEditMode, () => SetHideAll(!HideAll), Exit);
        hotkey = new HotkeyManager(ToggleEditMode);
        if (!hotkey.IsRegistered)
        {
            tray.ShowMessage("ショートカットを登録できませんでした", "Ctrl + Alt + L は別のアプリで使用されています。", ToolTipIcon.Warning);
        }
        lastDesktopHost = desktop.CurrentDesktopHost;
        desktopTimer.Start();
        if (ForegroundSafetyPolicy.ShouldOpenMainWindowAtStartup(previewMode))
        {
            mainWindow.OpenAndActivate();
        }
        _ = LoadDesktopItemsAsync();
    }

    internal void OpenMainWindow() => mainWindow?.OpenAndActivate();

    internal void AddFromDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = "デスクトップへ飾る画像 / GIFを選択",
            Filter = "対応画像|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.gif|すべてのファイル|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog(mainWindow) == true) AddFiles(dialog.FileNames);
    }

    internal void AddFiles(IEnumerable<string> paths) => _ = AddFilesAsync(paths.ToArray());

    private async Task AddFilesAsync(string[] paths)
    {
        var added = new List<CanvasItem>();
        var failures = new List<string>();
        var primary = PrimaryDisplay();
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string? storedName = null;
            try
            {
                if (!MediaStore.IsSupported(path)) throw new NotSupportedException("対応していない形式です。");
                storedName = mediaStore.Import(path);
                var storedPath = mediaStore.GetPath(storedName);
                var decoded = await Task.Run(() => MediaDecoder.Decode(storedPath)).ConfigureAwait(true);
                var scale = Math.Min(1, Math.Min(420d / decoded.PixelWidth, 320d / decoded.PixelHeight));
                var item = new CanvasItem
                {
                    DisplayName = Path.GetFileName(path),
                    StoredFileName = storedName,
                    ContentKind = Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase) ? CanvasContentKinds.Gif : CanvasContentKinds.Image,
                    MonitorDevice = primary.DeviceName ?? "",
                    CenterX = primary.Left + primary.Width / 2 + added.Count * 24,
                    CenterY = primary.Top + primary.Height / 2 + added.Count * 24,
                    Width = Math.Max(48, decoded.PixelWidth * scale),
                    Height = Math.Max(48, decoded.PixelHeight * scale),
                    ZIndex = Items.Count + added.Count
                };
                Items.Add(item);
                layout.Items.Add(item);
                var window = CreateDesktopItemWindow(item, new MediaItemContent(item, decoded));
                mediaWindows[item.Id] = window;
                window.SetEditEnabled(IsEditMode && !item.IsLocked);
                window.SetEffectivelyVisible(CanvasVisibility.IsEffectivelyVisible(layout.Settings, item));
                added.Add(item);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
            {
                if (storedName is not null) mediaStore.Delete(storedName);
                failures.Add($"{Path.GetFileName(path)}: {error.Message}");
            }
        }
        if (added.Count > 0)
        {
            Save(immediate: true);
            ReapplyZOrder();
            mainWindow?.Select(added[^1]);
            SetStatus($"{added.Count}個の素材を追加しました");
        }
        if (failures.Count > 0) MessageBox.Show(string.Join(Environment.NewLine, failures), "追加できなかった素材", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    internal void OpenBuiltInPicker()
    {
        var picker = new BuiltInContentPicker(mainWindow, nowPlaying, systemMetrics, codexUsage);
        if (picker.ShowDialog() != true) return;
        if (picker.SelectedKind == CanvasContentKinds.Clock) AddClock(picker.ClockOptions);
        else if (picker.SelectedKind == CanvasContentKinds.NowPlaying) AddNowPlaying(picker.NowPlayingOptions);
        else if (picker.SelectedKind == CanvasContentKinds.SystemMonitor) AddSystemMonitor(picker.SystemMonitorOptions);
        else if (picker.SelectedKind == CanvasContentKinds.CodexUsage) AddCodexUsage();
    }

    internal void AddClock(ClockOptions options)
    {
        var primary = PrimaryDisplay();
        var item = new CanvasItem
        {
            DisplayName = "時計",
            ContentKind = CanvasContentKinds.Clock,
            Clock = options.Clone(),
            MonitorDevice = primary.DeviceName ?? "",
            CenterX = primary.Left + primary.Width / 2,
            CenterY = primary.Top + primary.Height / 2,
            Width = 300,
            Height = 150,
            ZIndex = Items.Count == 0 ? 0 : Items.Max(candidate => candidate.ZIndex) + 1
        };
        Items.Add(item);
        layout.Items.Add(item);
        var window = CreateDesktopItemWindow(item, new ClockItemContent(item));
        mediaWindows[item.Id] = window;
        window.SetEditEnabled(IsEditMode && !item.IsLocked);
        window.SetEffectivelyVisible(CanvasVisibility.IsEffectivelyVisible(layout.Settings, item));
        Save(immediate: true);
        ReapplyZOrder();
        mainWindow?.Select(item);
        SetStatus("時計を追加しました");
    }

    internal void AddNowPlaying(NowPlayingOptions options)
    {
        AddBuiltIn("再生中", CanvasContentKinds.NowPlaying, 360, 220, item => item.NowPlaying = options.Clone());
    }

    internal void AddSystemMonitor(SystemMonitorOptions options)
    {
        AddBuiltIn("システムモニター", CanvasContentKinds.SystemMonitor, 360, 200, item => item.SystemMonitor = options.Clone());
    }

    internal void AddCodexUsage()
    {
        AddBuiltIn("Codexリミット", CanvasContentKinds.CodexUsage, 300, 180, _ => { });
    }

    private void AddBuiltIn(string name, string kind, double width, double height, Action<CanvasItem> configure)
    {
        var primary = PrimaryDisplay();
        var item = new CanvasItem
        {
            DisplayName = name,
            ContentKind = kind,
            MonitorDevice = primary.DeviceName ?? "",
            CenterX = primary.Left + primary.Width / 2,
            CenterY = primary.Top + primary.Height / 2,
            Width = width,
            Height = height,
            ZIndex = Items.Count == 0 ? 0 : Items.Max(candidate => candidate.ZIndex) + 1
        };
        configure(item);
        Items.Add(item);
        layout.Items.Add(item);
        var window = CreateDesktopItemWindow(item, CreateBuiltInContent(item));
        mediaWindows[item.Id] = window;
        window.SetEditEnabled(IsEditMode && !item.IsLocked);
        window.SetEffectivelyVisible(CanvasVisibility.IsEffectivelyVisible(layout.Settings, item));
        Save(immediate: true);
        ReapplyZOrder();
        mainWindow?.Select(item);
        SetStatus($"{name}を追加しました");
    }

    internal void ToggleEditMode()
    {
        IsEditMode = !IsEditMode;
        ApplyEditMode();
        EditModeChanged?.Invoke(IsEditMode);
    }

    internal void SetTemporaryHidden(CanvasItem item, bool hidden)
    {
        item.IsTemporarilyHidden = hidden;
        if (mediaWindows.TryGetValue(item.Id, out var window)) window.SetEffectivelyVisible(CanvasVisibility.IsEffectivelyVisible(layout.Settings, item));
        SetStatus(hidden ? $"「{item.DisplayName}」を一時的に隠しました" : $"「{item.DisplayName}」を表示しました");
    }

    internal void SetHideAll(bool hidden)
    {
        layout.Settings.HideAll = hidden;
        ApplyVisibility();
        tray?.SetHideAll(hidden);
        HideAllChanged?.Invoke(hidden);
        SetStatus(hidden ? "すべての項目を一時的に隠しました" : "一括非表示を解除しました");
    }

    internal void SetDecoration(CanvasItem item, string decoration)
    {
        item.DecorationMode = DecorationModes.IsSupported(decoration) ? decoration : DecorationModes.None;
        Save(immediate: true);
    }

    internal void SetTheme(CanvasItem item, WidgetThemeKind theme)
    {
        item.Theme = theme;
        Save(immediate: true);
    }

    internal void SetSurfaceStyle(CanvasItem item, WidgetSurfaceStyle style)
    {
        var value = Enum.IsDefined(style) ? style : WidgetSurfaceStyle.Standard;
        if (item.ContentKind == CanvasContentKinds.Clock) item.Clock.SurfaceStyle = value;
        else if (item.ContentKind == CanvasContentKinds.NowPlaying) item.NowPlaying.SurfaceStyle = value;
        else if (item.ContentKind == CanvasContentKinds.SystemMonitor) item.SystemMonitor.SurfaceStyle = value;
        RefreshBuiltIn(item, recreate: false);
    }

    internal void RefreshBuiltIn(CanvasItem item, bool recreate = true)
    {
        Save(immediate: true);
        if (!mediaWindows.TryGetValue(item.Id, out var window)) return;
        if (recreate)
        {
            RecreateBuiltIn(item);
            return;
        }
        window.RefreshContent();
    }

    private void RecreateBuiltIn(CanvasItem item)
    {
        if (!mediaWindows.Remove(item.Id, out var window)) return;
        var visible = CanvasVisibility.IsEffectivelyVisible(layout.Settings, item);
        window.Dispose();
        var created = CreateDesktopItemWindow(item, CreateBuiltInContent(item));
        mediaWindows[item.Id] = created;
        created.SetEditEnabled(IsEditMode && !item.IsLocked);
        created.SetEffectivelyVisible(visible);
        ReapplyZOrder();
    }

    internal void SetOpacity(CanvasItem item, double opacity, bool persist = true)
    {
        item.Opacity = opacity;
        if (persist) Save(immediate: true);
    }

    internal void SetRotation(CanvasItem item, double rotation, bool persist = true)
    {
        item.RotationDegrees = rotation;
        if (persist) Save(immediate: true);
    }

    internal void PersistPendingChanges() => Save(immediate: true);

    internal void SetFlipped(CanvasItem item, bool flipped)
    {
        item.IsFlipped = flipped;
        Save(immediate: true);
    }

    internal void SetItemLock(CanvasItem item, bool isLocked)
    {
        item.IsLocked = isLocked;
        if (mediaWindows.TryGetValue(item.Id, out var window)) window.SetEditEnabled(IsEditMode && !isLocked);
        Save(immediate: true);
        SetStatus(isLocked ? $"「{item.DisplayName}」を個別ロックしました" : $"「{item.DisplayName}」の個別ロックを解除しました");
    }

    internal void BringToFront(CanvasItem item)
    {
        item.ZIndex = Items.Count == 0 ? 0 : Items.Max(candidate => candidate.ZIndex) + 1;
        NormalizeZOrder();
    }

    internal void SendToBack(CanvasItem item)
    {
        item.ZIndex = Items.Count == 0 ? 0 : Items.Min(candidate => candidate.ZIndex) - 1;
        NormalizeZOrder();
    }

    internal void RequestDelete(CanvasItem item)
    {
        if (MessageBox.Show($"「{item.DisplayName}」をDeskCanvasから削除しますか？\n元ファイルは削除されません。", "素材を削除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        if (mediaWindows.Remove(item.Id, out var window)) window.Dispose();
        Items.Remove(item);
        layout.Items.Remove(item);
        if (item.ContentKind is CanvasContentKinds.Image or CanvasContentKinds.Gif)
        {
            mediaStore.Delete(item.StoredFileName);
        }
        NormalizeZOrder();
        SetStatus($"「{item.DisplayName}」を削除しました");
    }

    internal void SetStartWithWindows(bool enabled)
    {
        try
        {
            StartupService.SetEnabled(enabled);
            layout.Settings.StartWithWindows = enabled;
            Save(immediate: true);
            SetStatus(enabled ? "Windowsへの自動起動を有効にしました" : "Windowsへの自動起動を無効にしました");
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException)
        {
            layout.Settings.StartWithWindows = StartupService.IsEnabled();
            MessageBox.Show($"自動起動設定を変更できませんでした。\n\n{error.Message}", "DeskCanvas", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    internal void ItemChanged(CanvasItem item, bool persist)
    {
        if (persist)
        {
            DeskCanvas.Core.Geometry.ClampToDisplays(item, desktop.GetDisplays());
            Save(immediate: true);
        }
        if (mediaWindows.TryGetValue(item.Id, out var window)) window.Reposition();
    }

    internal void Exit()
    {
        IsExiting = true;
        IsEditMode = false;
        Save(immediate: true);
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        persistTimer.Stop();
        desktopTimer.Stop();
        desktopTimer.Tick -= DesktopTimer_Tick;
        hotkey?.Dispose();
        tray?.Dispose();
        foreach (var window in mediaWindows.Values.ToArray()) window.Dispose();
        mediaWindows.Clear();
        nowPlaying.Dispose();
        systemMetrics.Dispose();
        codexUsage.Dispose();
    }

    private DisplayArea PrimaryDisplay()
    {
        var displays = desktop.GetDisplays();
        if (displays.Count == 0) return new DisplayArea("fallback", 0, 0, 1920, 1080, true);
        var primary = displays.FirstOrDefault(display => display.IsPrimary);
        return string.IsNullOrWhiteSpace(primary.DeviceName) ? displays[0] : primary;
    }

    private void ApplyEditMode()
    {
        foreach (var pair in mediaWindows) pair.Value.SetEditEnabled(IsEditMode && !pair.Value.Item.IsLocked);
        tray?.SetEditMode(IsEditMode);
    }

    private void ApplyVisibility()
    {
        foreach (var pair in mediaWindows) pair.Value.SetEffectivelyVisible(CanvasVisibility.IsEffectivelyVisible(layout.Settings, pair.Value.Item));
    }

    private async Task LoadDesktopItemsAsync()
    {
        var displays = desktop.GetDisplays();
        foreach (var item in Items.ToArray())
        {
            DeskCanvas.Core.Geometry.ClampToDisplays(item, displays);
            await TryCreateDesktopItemWindowAsync(item).ConfigureAwait(true);
        }
        ApplyEditMode();
        ApplyVisibility();
        ReapplyZOrder();
        Save(immediate: true);
    }

    private async Task TryCreateDesktopItemWindowAsync(CanvasItem item)
    {
        try
        {
            IDesktopItemContent content;
            if (item.ContentKind is CanvasContentKinds.Clock or CanvasContentKinds.NowPlaying or CanvasContentKinds.SystemMonitor or CanvasContentKinds.CodexUsage)
            {
                content = CreateBuiltInContent(item);
            }
            else if (item.ContentKind is CanvasContentKinds.Image or CanvasContentKinds.Gif)
            {
                var path = mediaStore.GetPath(item.StoredFileName);
                if (!File.Exists(path))
                {
                    SetStatus($"素材が見つかりません: {item.DisplayName}");
                    return;
                }
                var decoded = await Task.Run(() => MediaDecoder.Decode(path)).ConfigureAwait(true);
                content = new MediaItemContent(item, decoded);
            }
            else
            {
                SetStatus($"未実装の標準コンテンツです: {item.DisplayName}");
                return;
            }
            var window = CreateDesktopItemWindow(item, content);
            mediaWindows[item.Id] = window;
            window.SetEffectivelyVisible(CanvasVisibility.IsEffectivelyVisible(layout.Settings, item));
        }
        catch (Exception error) when (error is IOException or InvalidDataException)
        {
            SetStatus($"{item.DisplayName}を表示できません: {error.Message}");
        }
    }

    private IDesktopItemContent CreateBuiltInContent(CanvasItem item) => item.ContentKind switch
    {
        CanvasContentKinds.Clock => new ClockItemContent(item),
        CanvasContentKinds.NowPlaying => new NowPlayingItemContent(item, nowPlaying),
        CanvasContentKinds.SystemMonitor => new SystemMonitorItemContent(item, systemMetrics),
        CanvasContentKinds.CodexUsage => new CodexUsageItemContent(codexUsage),
        _ => throw new NotSupportedException(item.ContentKind)
    };

    private MediaWindow CreateDesktopItemWindow(CanvasItem item, IDesktopItemContent content)
    {
        if (content is SystemMonitorItemContent monitor)
        {
            monitor.Persist = () => Save(immediate: false);
        }

        return new(item, content, desktop, ItemChanged, RequestDelete, lockedItem => SetItemLock(lockedItem, true), hiddenItem => SetTemporaryHidden(hiddenItem, true));
    }

    private void NormalizeZOrder()
    {
        var ordered = Items.OrderBy(candidate => candidate.ZIndex).ToList();
        for (var index = 0; index < ordered.Count; index++) ordered[index].ZIndex = index;
        Items.Clear();
        foreach (var item in ordered) Items.Add(item);
        layout.Items = Items.ToList();
        Save(immediate: true);
        ReapplyZOrder();
    }

    private void ReapplyZOrder()
    {
        foreach (var item in Items.OrderBy(candidate => candidate.ZIndex))
        {
            if (mediaWindows.TryGetValue(item.Id, out var window)) window.Reposition();
        }
    }

    private void DesktopTimer_Tick(object? sender, EventArgs e)
    {
        var current = desktop.CurrentDesktopHost;
        if (current != IntPtr.Zero && current != lastDesktopHost)
        {
            lastDesktopHost = current;
            ReapplyZOrder();
            SetStatus("Explorerへ再接続しました");
        }
    }

    private void Save(bool immediate = false)
    {
        layout.Items = Items.OrderBy(item => item.ZIndex).ToList();
        if (immediate)
        {
            persistTimer.Stop();
            SaveNow();
            return;
        }
        persistTimer.Stop();
        persistTimer.Start();
    }

    private void SaveNow()
    {
        if (!repository.TrySave(layout, out var error))
        {
            SetStatus($"配置の保存に失敗しました: {error}");
        }
    }

    private void SetStatus(string message)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(() => StatusChanged?.Invoke(message));
            return;
        }
        StatusChanged?.Invoke(message);
    }
}
