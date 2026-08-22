using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

public partial class MainWindow : Window
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    private readonly DeskCanvasController controller;
    private CanvasItem? observedItem;
    private bool updatingEditor = true;

    internal MainWindow(DeskCanvasController controller)
    {
        this.controller = controller;
        InitializeComponent();
        Title = ApplicationVersion.WindowTitle;
        ItemsList.ItemsSource = controller.Items;
        StartupCheck.IsChecked = controller.StartWithWindows;
        OpacitySlider.PreviewMouseLeftButtonUp += SliderCommitted;
        OpacitySlider.LostMouseCapture += SliderCommitted;
        RotationSlider.PreviewMouseLeftButtonUp += SliderCommitted;
        RotationSlider.LostMouseCapture += SliderCommitted;
        controller.EditModeChanged += UpdateEditMode;
        controller.HideAllChanged += UpdateHideAll;
        controller.StatusChanged += SetStatus;
        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;
        UpdateEditMode(controller.IsEditMode);
        UpdateHideAll(controller.HideAll);
        updatingEditor = false;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, in int value, int size);

    /// <summary>
    /// The caption bar is drawn by the OS, so it is switched to its dark variant to
    /// avoid a white strip above the dark content.
    /// </summary>
    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var enabled = 1;
        try { _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, in enabled, sizeof(int)); }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    internal void OpenAndActivate()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    internal void Select(CanvasItem item)
    {
        ItemsList.SelectedItem = item;
        ItemsList.ScrollIntoView(item);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!controller.IsExiting)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e) => controller.AddFromDialog();

    private void BuiltIn_Click(object sender, RoutedEventArgs e) => controller.OpenBuiltInPicker();

    private void EditMode_Click(object sender, RoutedEventArgs e) => controller.ToggleEditMode();

    private void UpdateEditMode(bool enabled)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => UpdateEditMode(enabled));
            return;
        }
        EditModeButton.Content = enabled ? "編集を終了" : "編集モード";
        EditModeButton.Background = enabled ? Design.DestructiveBrush : Design.AccentBrush;
        SetStatus(enabled
            ? "編集中 — 個別ロックしていない素材へカーソルを載せると操作できます"
            : "ロック中 — Ctrl + Alt + L で編集モードを切り替え");
    }

    private void SetStatus(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => SetStatus(message));
            return;
        }
        StatusText.Text = message;
    }

    private void UpdateHideAll(bool hidden)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => UpdateHideAll(hidden));
            return;
        }
        HideAllButton.Content = hidden ? "すべて表示" : "すべて隠す";
    }

    private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (observedItem is not null)
        {
            observedItem.PropertyChanged -= ObservedItem_PropertyChanged;
        }

        observedItem = SelectedItem;
        if (observedItem is not null)
        {
            observedItem.PropertyChanged += ObservedItem_PropertyChanged;
        }

        RefreshEditor();
    }

    private void ObservedItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, SelectedItem))
        {
            Dispatcher.InvokeAsync(RefreshEditor);
        }
    }

    private CanvasItem? SelectedItem => ItemsList.SelectedItem as CanvasItem;

    private void RefreshEditor()
    {
        var item = SelectedItem;
        EmptyMessage.Visibility = item is null ? Visibility.Visible : Visibility.Collapsed;
        EditorPanel.Visibility = item is null ? Visibility.Collapsed : Visibility.Visible;
        if (item is null)
        {
            return;
        }

        updatingEditor = true;
        SelectedName.Text = item.DisplayName;
        OpacitySlider.Value = item.Opacity * 100;
        RotationSlider.Value = item.RotationDegrees;
        OpacityValue.Text = $"{Math.Round(item.Opacity * 100):0}%";
        RotationValue.Text = $"{item.RotationDegrees:0.#}°";
        ItemLockCheck.IsChecked = item.IsLocked;
        FlipCheck.IsChecked = item.IsFlipped;
        TemporaryHideCheck.IsChecked = item.IsTemporarilyHidden;
        // Flipping only means something for bitmaps. Hiding the whole row keeps the
        // grouped list from ending on a dangling separator.
        var canFlip = item.ContentKind is CanvasContentKinds.Image or CanvasContentKinds.Gif;
        FlipRow.Visibility = canFlip ? Visibility.Visible : Visibility.Collapsed;
        TemporaryHideRow.BorderThickness = canFlip ? new Thickness(0, 0, 0, 1) : new Thickness(0);
        DecorationCombo.SelectedIndex = item.DecorationMode switch { DecorationModes.WhiteOutline => 1, DecorationModes.OuterFrame => 2, _ => 0 };
        UpdateAppearanceEditor();
        updatingEditor = false;
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValue is not null)
        {
            OpacityValue.Text = $"{Math.Round(e.NewValue):0}%";
        }
        if (!updatingEditor && SelectedItem is { } item)
        {
            controller.SetOpacity(item, e.NewValue / 100, persist: false);
        }
    }

    private void RotationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RotationValue is not null)
        {
            RotationValue.Text = $"{e.NewValue:0.#}°";
        }
        if (!updatingEditor && SelectedItem is { } item)
        {
            controller.SetRotation(item, e.NewValue, persist: false);
        }
    }

    private void SliderCommitted(object sender, MouseEventArgs e)
    {
        if (!updatingEditor) controller.PersistPendingChanges();
    }

    private void ItemLockCheck_Click(object sender, RoutedEventArgs e)
    {
        if (!updatingEditor && SelectedItem is { } item)
        {
            controller.SetItemLock(item, ItemLockCheck.IsChecked == true);
        }
    }

    private void FlipCheck_Click(object sender, RoutedEventArgs e)
    {
        if (!updatingEditor && SelectedItem is { } item)
        {
            controller.SetFlipped(item, FlipCheck.IsChecked == true);
        }
    }

    private void TemporaryHideCheck_Click(object sender, RoutedEventArgs e) { if (!updatingEditor && SelectedItem is { } item) controller.SetTemporaryHidden(item, TemporaryHideCheck.IsChecked == true); }

    // Tag carries the persisted mode so the visible label can be localised freely.
    private void DecorationCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!updatingEditor && SelectedItem is { } item && DecorationCombo.SelectedItem is ComboBoxItem choice) controller.SetDecoration(item, choice.Tag?.ToString() ?? DecorationModes.None); }

    private void HideAll_Click(object sender, RoutedEventArgs e) => controller.SetHideAll(!controller.HideAll);

    private void BringFront_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem is { } item)
        {
            controller.BringToFront(item);
        }
    }

    private void SendBack_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem is { } item)
        {
            controller.SendToBack(item);
        }
    }

    private void ResetRotation_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem is { } item)
        {
            controller.SetRotation(item, 0);
            RefreshEditor();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem is { } item)
        {
            controller.RequestDelete(item);
        }
    }

    private void StartupCheck_Click(object sender, RoutedEventArgs e)
    {
        controller.SetStartWithWindows(StartupCheck.IsChecked == true);
        StartupCheck.IsChecked = controller.StartWithWindows;
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            controller.AddFiles(paths);
        }
    }
}
