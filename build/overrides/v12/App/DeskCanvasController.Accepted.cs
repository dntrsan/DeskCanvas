using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using DeskCanvas.App.Media;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;
using Application = System.Windows.Application;
using CoreGeometry = DeskCanvas.Core.Geometry;
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
    private readonly INowPlayingService nowPlaying = new NowPlayingService();
    private readonly ISystemMetricsService systemMetrics = new SystemMetricsService();
    private readonly ICodexUsageService codexUsage;
    private readonly bool previewMode;
    private TrayService? tray;
    private HotkeyManager? hotkey;
    private MainWindow? mainWindow;
    private IntPtr lastDesktopHost;
    private bool disposed;

    internal DeskCanvasController()
    {
        previewMode = string.Equals(
            Environment.GetEnvironmentVariable("DESKCANVAS_PREVIEW_MODE"),
            "1",
            StringComparison.Ordinal);
        codexUsage = new CodexUsageService(previewMode);
        var root = ResolveDataRoot();
        repository = new LayoutRepository(root);
        mediaStore = new MediaStore(root);
        layout = repository.Load();
        Items = new ObservableCollection<CanvasItem>(layout.Items.OrderBy(item => item.ZIndex));
        layout.Items = Items.ToList();
        desktopTimer = new DispatcherTimer
        {
            Interval = BackgroundLoadPolicy.DesktopHostConnectedPollInterval
        };
        desktopTimer.Tick += DesktopTimer_Tick;
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
        layout.Settings.StartWithWindows = !previewMode && StartupService.IsEnabled();
        var displays = desktop.GetDisplays();
        foreach (var item in Items)
        {
            CoreGeometry.ClampToDisplays(item, displays);
            TryCreateDesktopItemWindow(item);
        }
        mainWindow = new MainWindow(this);
        if (ShouldCreateDesktopIntegrations(previewMode))
        {
            tray = new TrayService(
                OpenMainWindow,
                AddFromDialog,
                OpenBuiltInPicker,
                ToggleEditMode,
                () => SetHideAll(!HideAll),
                Exit);
            hotkey = new HotkeyManager(ToggleEditMode);
            if (!hotkey.IsRegistered)
            {
                tray.ShowMessage(
                    "ショートカットを登録できませんでした",
                    "Ctrl + Alt + L は別のアプリで使用されています。",
                    System.Windows.Forms.ToolTipIcon.Warning);
            }
        }
        ApplyEditMode();
        ApplyVisibility();
        ReapplyZOrder();
        if (ShouldCreateDesktopIntegrations(previewMode))
        {
            lastDesktopHost = desktop.CurrentDesktopHost;
            desktopTimer.Interval = BackgroundLoadPolicy.DesktopHostPollInterval(lastDesktopHost != IntPtr.Zero);
            desktopTimer.Start();
        }
        repository.Save(layout);
        var suppressPreviewUi = string.Equals(
            Environment.GetEnvironmentVariable("DESKCANVAS_PREVIEW_SUPPRESS_STARTUP_UI"),
            "1",
            StringComparison.Ordinal);
        if (ForegroundSafetyPolicy.ShouldOpenMainWindowAtStartup(previewMode, suppressPreviewUi)) mainWindow.OpenAndActivate();
    }

    internal void OpenMainWindow() => mainWindow?.OpenAndActivate();

    internal static bool ShouldCreateDesktopIntegrations(bool preview) => !preview;

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

    internal void AddFiles(IEnumerable<string> paths)
    {
        var added = new List<CanvasItem>();
        var failures = new List<string>();
        var primary = PrimaryDisplay();
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string? storedName = null;
            try
            {
                if (!MediaStore.IsSupported(path))
                    throw new NotSupportedException("対応していない形式です。");
                storedName = mediaStore.Import(path);
                var decoded = MediaDecoder.Decode(mediaStore.GetPath(storedName));
                var scale = Math.Min(1, Math.Min(420d / decoded.PixelWidth, 320d / decoded.PixelHeight));
                var item = new CanvasItem
                {
                    DisplayName = Path.GetFileName(path),
                    StoredFileName = storedName,
                    ContentKind = Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase)
                        ? CanvasContentKinds.Gif
                        : CanvasContentKinds.Image,
                    MonitorDevice = primary.DeviceName ?? "",
                    CenterX = primary.Left + primary.Width / 2 + added.Count * 24,
                    CenterY = primary.Top + primary.Height / 2 + added.Count * 24,
                    Width = Math.Max(48, decoded.PixelWidth * scale),
                    Height = Math.Max(48, decoded.PixelHeight * scale),
                    ZIndex = NextZIndex()
                };
                Items.Add(item);
                layout.Items.Add(item);
                var window = CreateDesktopItemWindow(item, new MediaItemContent(item, decoded));
                mediaWindows[item.Id] = window;
                ConfigureWindow(window, item);
                added.Add(item);
            }
            catch (Exception error) when (
                error is IOException or UnauthorizedAccessException or
                InvalidDataException or NotSupportedException)
            {
                if (storedName is not null) mediaStore.Delete(storedName);
                failures.Add($"{Path.GetFileName(path)}: {error.Message}");
            }
        }
        if (added.Count > 0)
        {
            Save();
            ReapplyZOrder();
            mainWindow?.Select(added[^1]);
            SetStatus($"{added.Count}個の素材を追加しました");
        }
        if (failures.Count > 0)
        {
            MessageBox.Show(
                string.Join(Environment.NewLine, failures),
                "追加できなかった素材",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    internal void OpenBuiltInPicker()
    {
        var picker = new BuiltInContentPicker(mainWindow, nowPlaying, systemMetrics, codexUsage);
        if (picker.ShowDialog() != true) return;
        if (picker.SelectedKind == CanvasContentKinds.Clock)
            AddClock(picker.ClockOptions, picker.Theme);
        else if (picker.SelectedKind == CanvasContentKinds.NowPlaying)
            AddNowPlaying(picker.NowPlayingOptions, picker.Theme);
        else if (picker.SelectedKind == CanvasContentKinds.SystemMonitor)
            AddSystemMonitor(picker.SystemMonitorOptions, picker.Theme);
        else if (picker.SelectedKind == CanvasContentKinds.CodexUsage)
            AddCodexUsage();
    }

    internal void AddClock(ClockOptions options, WidgetThemeKind theme = WidgetThemeKind.Auto) =>
        AddBuiltIn(
            "時計",
            CanvasContentKinds.Clock,
            300,
            150,
            item =>
            {
                item.Clock = options.Clone();
                item.Theme = NormalizeTheme(theme);
            });

    internal void AddNowPlaying(
        NowPlayingOptions options,
        WidgetThemeKind theme = WidgetThemeKind.Auto) =>
        AddBuiltIn(
            "再生中",
            CanvasContentKinds.NowPlaying,
            360,
            220,
            item =>
            {
                item.NowPlaying = options.Clone();
                item.Theme = NormalizeTheme(theme);
            });

    internal void AddSystemMonitor(
        SystemMonitorOptions options,
        WidgetThemeKind theme = WidgetThemeKind.Auto) =>
        AddBuiltIn(
            "システムモニター",
            CanvasContentKinds.SystemMonitor,
            320,
            210,
            item =>
            {
                item.SystemMonitor = options.Clone();
                item.Theme = NormalizeTheme(theme);
            });

    internal void AddCodexUsage() =>
        AddBuiltIn(
            "Codexリミット",
            CanvasContentKinds.CodexUsage,
            360,
            160,
            _ => { });

    private void AddBuiltIn(
        string name,
        string kind,
        double width,
        double height,
        Action<CanvasItem> configure)
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
            ZIndex = NextZIndex()
        };
        configure(item);
        Items.Add(item);
        layout.Items.Add(item);
        var window = CreateDesktopItemWindow(item, CreateBuiltInContent(item));
        mediaWindows[item.Id] = window;
        ConfigureWindow(window, item);
        Save();
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
        if (mediaWindows.TryGetValue(item.Id, out var window))
            window.SetEffectivelyVisible(CanvasVisibility.IsEffectivelyVisible(layout.Settings, item));
        SetStatus(hidden
            ? $"「{item.DisplayName}」を一時的に隠しました"
            : $"「{item.DisplayName}」を表示しました");
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
        item.DecorationMode = DecorationModes.IsSupported(decoration)
            ? decoration
            : DecorationModes.None;
        Save();
    }

    internal void SetOpacity(CanvasItem item, double opacity)
    {
        item.Opacity = opacity;
        Save();
    }

    internal void SetRotation(CanvasItem item, double rotation)
    {
        item.RotationDegrees = rotation;
        Save();
    }

    internal void SetFlipped(CanvasItem item, bool flipped)
    {
        item.IsFlipped = flipped;
        Save();
    }

    internal void SetTheme(CanvasItem item, WidgetThemeKind theme)
    {
        if (item.ContentKind is not (
                CanvasContentKinds.Clock or
                CanvasContentKinds.NowPlaying or
                CanvasContentKinds.SystemMonitor))
            return;
        item.Theme = NormalizeTheme(theme);
        ReplaceBuiltInWindow(item);
        Save();
        SetStatus($"「{item.DisplayName}」のテーマを{item.Theme}へ変更しました");
    }

    internal void SetSurfaceStyle(CanvasItem item, WidgetSurfaceStyle style)
    {
        if (item.ContentKind is not (CanvasContentKinds.Clock or CanvasContentKinds.NowPlaying)) return;
        var normalized = Enum.IsDefined(style) ? style : WidgetSurfaceStyle.Standard;
        if (item.ContentKind == CanvasContentKinds.Clock) item.Clock.SurfaceStyle = normalized;
        else item.NowPlaying.SurfaceStyle = normalized;
        ReplaceBuiltInWindow(item);
        Save();
        SetStatus($"「{item.DisplayName}」のカードスタイルを{(normalized == WidgetSurfaceStyle.MinimalGlass ? "ミニマルガラス" : "標準")}へ変更しました");
    }

    internal void SetItemLock(CanvasItem item, bool isLocked)
    {
        item.IsLocked = isLocked;
        if (mediaWindows.TryGetValue(item.Id, out var window))
            window.SetEditEnabled(IsEditMode && !isLocked);
        Save();
        SetStatus(isLocked
            ? $"「{item.DisplayName}」を個別ロックしました"
            : $"「{item.DisplayName}」の個別ロックを解除しました");
    }

    internal void BringToFront(CanvasItem item)
    {
        item.ZIndex = NextZIndex();
        NormalizeZOrder();
    }

    internal void SendToBack(CanvasItem item)
    {
        item.ZIndex = Items.Count == 0 ? 0 : Items.Min(candidate => candidate.ZIndex) - 1;
        NormalizeZOrder();
    }

    internal void RequestDelete(CanvasItem item)
    {
        if (MessageBox.Show(
                $"「{item.DisplayName}」をDeskCanvasから削除しますか？\n元ファイルは削除されません。",
                "素材を削除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        if (mediaWindows.Remove(item.Id, out var window)) window.Dispose();
        Items.Remove(item);
        layout.Items.Remove(item);
        if (item.ContentKind is CanvasContentKinds.Image or CanvasContentKinds.Gif)
        {
            try
            {
                mediaStore.Delete(item.StoredFileName);
            }
            catch (IOException)
            {
            }
        }
        NormalizeZOrder();
        SetStatus($"「{item.DisplayName}」を削除しました");
    }

    internal void SetStartWithWindows(bool enabled)
    {
        if (previewMode)
        {
            layout.Settings.StartWithWindows = false;
            SetStatus("隔離プレビューでは自動起動を変更しません");
            return;
        }
        try
        {
            StartupService.SetEnabled(enabled);
            layout.Settings.StartWithWindows = enabled;
            Save();
            SetStatus(enabled
                ? "Windowsへの自動起動を有効にしました"
                : "Windowsへの自動起動を無効にしました");
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException)
        {
            layout.Settings.StartWithWindows = StartupService.IsEnabled();
            MessageBox.Show(
                $"自動起動設定を変更できませんでした。\n\n{error.Message}",
                "DeskCanvas",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    internal void ItemChanged(CanvasItem item, bool persist)
    {
        if (persist)
        {
            CoreGeometry.ClampToDisplays(item, desktop.GetDisplays());
            Save();
        }
        if (mediaWindows.TryGetValue(item.Id, out var window)) window.Reposition();
    }

    internal void Exit()
    {
        IsExiting = true;
        IsEditMode = false;
        Save();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
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

    private void ReplaceBuiltInWindow(CanvasItem item)
    {
        if (mediaWindows.Remove(item.Id, out var old)) old.Dispose();
        var replacement = CreateDesktopItemWindow(item, CreateBuiltInContent(item));
        mediaWindows[item.Id] = replacement;
        ConfigureWindow(replacement, item);
        ReapplyZOrder();
    }

    private void ConfigureWindow(MediaWindow window, CanvasItem item)
    {
        window.SetEditEnabled(IsEditMode && !item.IsLocked);
        window.SetEffectivelyVisible(CanvasVisibility.IsEffectivelyVisible(layout.Settings, item));
    }

    private DisplayArea PrimaryDisplay()
    {
        var displays = desktop.GetDisplays();
        var primary = displays.FirstOrDefault(display => display.IsPrimary);
        return string.IsNullOrWhiteSpace(primary.DeviceName) && displays.Count > 0
            ? displays[0]
            : primary;
    }

    private void ApplyEditMode()
    {
        foreach (var pair in mediaWindows)
            pair.Value.SetEditEnabled(IsEditMode && !pair.Value.Item.IsLocked);
        tray?.SetEditMode(IsEditMode);
    }

    private void ApplyVisibility()
    {
        foreach (var pair in mediaWindows)
            pair.Value.SetEffectivelyVisible(
                CanvasVisibility.IsEffectivelyVisible(layout.Settings, pair.Value.Item));
    }

    private void TryCreateDesktopItemWindow(CanvasItem item)
    {
        try
        {
            IDesktopItemContent content;
            if (item.ContentKind is
                CanvasContentKinds.Clock or
                CanvasContentKinds.NowPlaying or
                CanvasContentKinds.SystemMonitor or
                CanvasContentKinds.CodexUsage)
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
                content = new MediaItemContent(item, MediaDecoder.Decode(path));
            }
            else
            {
                SetStatus($"未実装の標準コンテンツです: {item.DisplayName}");
                return;
            }
            var window = CreateDesktopItemWindow(item, content);
            mediaWindows[item.Id] = window;
            ConfigureWindow(window, item);
        }
        catch (Exception error) when (error is IOException or InvalidDataException)
        {
            SetStatus($"{item.DisplayName}を表示できません: {error.Message}");
        }
    }

    private IDesktopItemContent CreateBuiltInContent(CanvasItem item) =>
        item.ContentKind switch
        {
            CanvasContentKinds.Clock => new ClockItemContent(item),
            CanvasContentKinds.NowPlaying => new NowPlayingItemContent(item, nowPlaying),
            CanvasContentKinds.SystemMonitor => new SystemMonitorItemContent(item, systemMetrics),
            CanvasContentKinds.CodexUsage => new CodexUsageItemContent(codexUsage),
            _ => throw new NotSupportedException(item.ContentKind)
        };

    private MediaWindow CreateDesktopItemWindow(CanvasItem item, IDesktopItemContent content) =>
        new(
            item,
            content,
            desktop,
            ItemChanged,
            RequestDelete,
            lockedItem => SetItemLock(lockedItem, true),
            hiddenItem => SetTemporaryHidden(hiddenItem, true));

    private void NormalizeZOrder()
    {
        var ordered = Items.OrderBy(candidate => candidate.ZIndex).ToList();
        for (var index = 0; index < ordered.Count; index++) ordered[index].ZIndex = index;
        Items.Clear();
        foreach (var item in ordered) Items.Add(item);
        layout.Items = Items.ToList();
        Save();
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
        desktopTimer.Interval = BackgroundLoadPolicy.DesktopHostPollInterval(current != IntPtr.Zero);
        if (current == lastDesktopHost) return;
        lastDesktopHost = current;
        ReapplyZOrder();
        if (current != IntPtr.Zero) SetStatus("Explorerへ再接続しました");
    }

    private int NextZIndex() =>
        Items.Count == 0 ? 0 : Items.Max(candidate => candidate.ZIndex) + 1;

    private void Save()
    {
        layout.Items = Items.OrderBy(item => item.ZIndex).ToList();
        repository.Save(layout);
    }

    private void SetStatus(string message) => StatusChanged?.Invoke(message);

    private static WidgetThemeKind NormalizeTheme(WidgetThemeKind theme) =>
        Enum.IsDefined(theme) ? theme : WidgetThemeKind.Auto;

    private static string ResolveDataRoot()
    {
        var configured = Environment.GetEnvironmentVariable("DESKCANVAS_DATA_ROOT");
        if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured))
            return Path.GetFullPath(configured);
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeskCanvas");
    }
}
