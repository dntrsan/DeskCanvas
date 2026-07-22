using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

public partial class MainWindow : Window
{
    private readonly DeskCanvasController controller;
    private CanvasItem? observedItem;
    private bool updatingEditor = true;

    internal MainWindow(DeskCanvasController controller)
    {
        this.controller = controller;
        InitializeComponent();
        ItemsList.ItemsSource = controller.Items;
        StartupCheck.IsChecked = controller.StartWithWindows;
        controller.EditModeChanged += UpdateEditMode;
        controller.StatusChanged += SetStatus;
        Closing += MainWindow_Closing;
        UpdateEditMode(controller.IsEditMode);
        updatingEditor = false;
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

    private void EditMode_Click(object sender, RoutedEventArgs e) => controller.ToggleEditMode();

    private void UpdateEditMode(bool enabled)
    {
        EditModeButton.Content = enabled ? "編集を終了してロック" : "編集モードをON";
        EditModeButton.Background = enabled
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(178, 78, 96))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(81, 70, 165));
        SetStatus(enabled
            ? "編集中 — 個別ロックしていない素材へカーソルを載せると操作できます"
            : "ロック中 — Ctrl + Alt + L で編集モードを切り替え");
    }

    private void SetStatus(string message) => StatusText.Text = message;

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
            controller.SetOpacity(item, e.NewValue / 100);
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
            controller.SetRotation(item, e.NewValue);
        }
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
