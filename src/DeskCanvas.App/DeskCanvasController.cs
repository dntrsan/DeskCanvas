using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using DeskCanvas.App.Media;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;
using Microsoft.Win32;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace DeskCanvas.App;

internal sealed class DeskCanvasController : IDisposable
{
    private readonly string root;
    private readonly LayoutRepository repository;
    private readonly MediaStore mediaStore;
    private readonly DesktopWindowService desktop = new();
    private readonly CanvasLayout layout;
    private readonly Dictionary<Guid, MediaWindow> mediaWindows = [];
    private readonly DispatcherTimer desktopTimer;
    private TrayService? tray;
    private HotkeyManager? hotkey;
    private MainWindow? mainWindow;
    private IntPtr lastDesktopHost;
    private bool disposed;

    internal DeskCanvasController()
    {
        root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeskCanvas");
        repository = new LayoutRepository(root);
        mediaStore = new MediaStore(root);
        layout = repository.Load();
        Items = new ObservableCollection<CanvasItem>(
            layout.Items.OrderBy(item => item.ZIndex));
        layout.Items = Items.ToList();
        desktopTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        desktopTimer.Tick += DesktopTimer_Tick;
    }

    internal ObservableCollection<CanvasItem> Items { get; }
    internal bool IsEditMode { get; private set; }
    internal bool IsExiting { get; private set; }
    internal bool StartWithWindows => layout.Settings.StartWithWindows;
    internal event Action<bool>? EditModeChanged;
    internal event Action<string>? StatusChanged;

    internal void Start()
    {
        layout.Settings.StartWithWindows = StartupService.IsEnabled();
        var displays = desktop.GetDisplays();
        foreach (var item in Items)
        {
            Geometry.ClampToDisplays(item, displays);
            TryCreateMediaWindow(item);
        }

        mainWindow = new MainWindow(this);
        tray = new TrayService(OpenMainWindow, AddFromDialog, ToggleEditMode, Exit);
        hotkey = new HotkeyManager(ToggleEditMode);
        if (!hotkey.IsRegistered)
        {
            tray.ShowMessage(
                "ショートカットを登録できませんでした",
                "Ctrl + Alt + L は別のアプリで使用されています。",
                ToolTipIcon.Warning);
        }
        ApplyEditMode();
        ReapplyZOrder();
        lastDesktopHost = desktop.CurrentDesktopHost;
        desktopTimer.Start();
        repository.Save(layout);

        if (Items.Count == 0)
        {
            mainWindow.OpenAndActivate();
        }
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
        if (dialog.ShowDialog(mainWindow) == true)
        {
            AddFiles(dialog.FileNames);
        }
    }

    internal void AddFiles(IEnumerable<string> paths)
    {
        var added = new List<CanvasItem>();
        var failures = new List<string>();
        var displays = desktop.GetDisplays();
        var primary = displays.FirstOrDefault(display => display.IsPrimary);
        if (string.IsNullOrWhiteSpace(primary.DeviceName) && displays.Count > 0)
        {
            primary = displays[0];
        }

        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string? storedName = null;
            try
            {
                if (!MediaStore.IsSupported(path))
                {
                    throw new NotSupportedException("対応していない形式です。");
                }
                storedName = mediaStore.Import(path);
                var decoded = MediaDecoder.Decode(mediaStore.GetPath(storedName));
                var maxWidth = 420d;
                var maxHeight = 320d;
                var scale = Math.Min(
                    1,
                    Math.Min(maxWidth / decoded.PixelWidth, maxHeight / decoded.PixelHeight));
                var item = new CanvasItem
                {
                    DisplayName = Path.GetFileName(path),
                    StoredFileName = storedName,
                    MediaKind = Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase)
                        ? "gif"
                        : "image",
                    MonitorDevice = primary.DeviceName ?? "",
                    CenterX = primary.Left + primary.Width / 2 + added.Count * 24,
                    CenterY = primary.Top + primary.Height / 2 + added.Count * 24,
                    Width = Math.Max(48, decoded.PixelWidth * scale),
                    Height = Math.Max(48, decoded.PixelHeight * scale),
                    ZIndex = Items.Count + added.Count
                };
                Items.Add(item);
                layout.Items.Add(item);
                var window = CreateMediaWindow(item, decoded);
                mediaWindows[item.Id] = window;
                window.Show();
                window.SetEditEnabled(IsEditMode && !item.IsLocked);
                added.Add(item);
            }
            catch (Exception error) when (
                error is IOException or
                UnauthorizedAccessException or
                InvalidDataException or
                NotSupportedException)
            {
                if (storedName is not null)
                {
                    mediaStore.Delete(storedName);
                }
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

    internal void ToggleEditMode()
    {
        IsEditMode = !IsEditMode;
        ApplyEditMode();
        EditModeChanged?.Invoke(IsEditMode);
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

    internal void SetItemLock(CanvasItem item, bool isLocked)
    {
        item.IsLocked = isLocked;
        if (mediaWindows.TryGetValue(item.Id, out var window))
        {
            window.SetEditEnabled(IsEditMode && !isLocked);
        }
        Save();
        SetStatus(isLocked
            ? $"「{item.DisplayName}」を個別ロックしました"
            : $"「{item.DisplayName}」の個別ロックを解除しました");
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
        var result = MessageBox.Show(
            $"「{item.DisplayName}」をDeskCanvasから削除しますか？\n元ファイルは削除されません。",
            "素材を削除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        if (mediaWindows.Remove(item.Id, out var window))
        {
            window.Dispose();
        }
        Items.Remove(item);
        layout.Items.Remove(item);
        try
        {
            mediaStore.Delete(item.StoredFileName);
        }
        catch (IOException)
        {
            // The layout should still forget the item even if cleanup must wait.
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
            Save();
            SetStatus(enabled ? "Windowsへの自動起動を有効にしました" : "Windowsへの自動起動を無効にしました");
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
        var displays = desktop.GetDisplays();
        if (persist)
        {
            Geometry.ClampToDisplays(item, displays);
            Save();
        }
        if (mediaWindows.TryGetValue(item.Id, out var window))
        {
            window.Reposition();
        }
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
        if (disposed)
        {
            return;
        }
        disposed = true;
        desktopTimer.Stop();
        hotkey?.Dispose();
        tray?.Dispose();
        foreach (var window in mediaWindows.Values.ToArray())
        {
            window.Dispose();
        }
        mediaWindows.Clear();
    }

    private void ApplyEditMode()
    {
        foreach (var pair in mediaWindows)
        {
            pair.Value.SetEditEnabled(IsEditMode && !pair.Value.Item.IsLocked);
        }
        tray?.SetEditMode(IsEditMode);
    }

    private void TryCreateMediaWindow(CanvasItem item)
    {
        try
        {
            var path = mediaStore.GetPath(item.StoredFileName);
            if (!File.Exists(path))
            {
                SetStatus($"素材が見つかりません: {item.DisplayName}");
                return;
            }
            var decoded = MediaDecoder.Decode(path);
            var window = CreateMediaWindow(item, decoded);
            mediaWindows[item.Id] = window;
            window.Show();
        }
        catch (Exception error) when (error is IOException or InvalidDataException)
        {
            SetStatus($"{item.DisplayName}を表示できません: {error.Message}");
        }
    }

    private MediaWindow CreateMediaWindow(CanvasItem item, DecodedMedia decoded) =>
        new(
            item,
            decoded,
            desktop,
            ItemChanged,
            RequestDelete,
            lockedItem => SetItemLock(lockedItem, true));

    private void NormalizeZOrder()
    {
        var ordered = Items.OrderBy(candidate => candidate.ZIndex).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].ZIndex = index;
        }
        Items.Clear();
        foreach (var item in ordered)
        {
            Items.Add(item);
        }
        layout.Items = Items.ToList();
        Save();
        ReapplyZOrder();
    }

    private void ReapplyZOrder()
    {
        foreach (var item in Items.OrderBy(candidate => candidate.ZIndex))
        {
            if (mediaWindows.TryGetValue(item.Id, out var window))
            {
                window.Reposition();
            }
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

    private void Save()
    {
        layout.Items = Items.OrderBy(item => item.ZIndex).ToList();
        repository.Save(layout);
    }

    private void SetStatus(string message)
    {
        StatusChanged?.Invoke(message);
    }
}
