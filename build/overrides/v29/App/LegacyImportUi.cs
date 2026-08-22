using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;
using WinForms = System.Windows.Forms;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace DeskCanvas.App;

internal static class LegacyImportUiBootstrap
{
    [ModuleInitializer]
    internal static void Register() => EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded));

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is MainWindow window) window.EnsureLegacyImportAction();
    }
}

namespace DeskCanvas.App.Windows;

internal partial class MainWindow
{
    private Button? legacyImportButton;

    internal void EnsureLegacyImportAction()
    {
        if (legacyImportButton is not null) return;
        var addButton = FindButton(this, "＋ 画像 / GIFを追加");
        if (addButton?.Parent is not StackPanel host) return;
        legacyImportButton = new Button { Content = "旧データを引き継ぐ…", ToolTip = "旧DeskCanvasのデータフォルダーまたはlayout.jsonを選択" };
        legacyImportButton.Click += LegacyImport_Click;
        host.Children.Insert(Math.Min(1, host.Children.Count), legacyImportButton);
    }

    private static Button? FindButton(DependencyObject root, string content)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is Button { Content: string text } button && text == content) return button;
            if (FindButton(child, content) is { } found) return found;
        }
        return null;
    }

    private void LegacyImport_Click(object sender, RoutedEventArgs e)
    {
        var choice = WpfMessageBox.Show("旧DeskCanvasのデータフォルダーを選ぶなら「はい」、layout.jsonを直接選ぶなら「いいえ」を押してください。\n現在の配置は消えず、重複する素材は追加しません。", "旧データを引き継ぐ", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        string? selected = null;
        if (choice == MessageBoxResult.Yes)
        {
            using var dialog = new WinForms.FolderBrowserDialog { Description = "旧DeskCanvasのデータフォルダー（layout.jsonとMediaがある場所）を選択" };
            if (dialog.ShowDialog() == WinForms.DialogResult.OK) selected = dialog.SelectedPath;
        }
        else if (choice == MessageBoxResult.No)
        {
            var dialog = new WpfOpenFileDialog { Title = "旧DeskCanvasのlayout.jsonを選択", Filter = "DeskCanvas layout.json|layout.json|JSONファイル|*.json" };
            if (dialog.ShowDialog(this) == true) selected = dialog.FileName;
        }
        if (string.IsNullOrWhiteSpace(selected)) return;
        var result = controller.ImportLegacyData(selected);
        SetStatus(result.JapaneseSummary());
        var icon = result.IsRejected ? MessageBoxImage.Warning : MessageBoxImage.Information;
        WpfMessageBox.Show(result.JapaneseSummary(), "旧データを引き継ぐ", MessageBoxButton.OK, icon);
    }
}

namespace DeskCanvas.App;

internal static class LegacyImportControllerExtensions
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static LegacyLayoutImportResult ImportLegacyData(this DeskCanvasController controller, string selectedPath)
    {
        var mediaStore = (MediaStore?)typeof(DeskCanvasController).GetField("mediaStore", PrivateInstance)?.GetValue(controller);
        if (mediaStore is null) return LegacyLayoutImportResult.Rejected("引き継ぎ先を準備できませんでした。");
        var mediaDirectory = Path.GetDirectoryName(mediaStore.GetPath("import-probe.bin"));
        var root = mediaDirectory is null ? null : Directory.GetParent(mediaDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(root)) return LegacyLayoutImportResult.Rejected("引き継ぎ先を準備できませんでした。");
        var result = LegacyLayoutImportService.Import(selectedPath, root, controller.Items);
        if (result.IsRejected || result.Items.Count == 0) return result;

        var create = typeof(DeskCanvasController).GetMethod("TryCreateDesktopItemWindow", PrivateInstance);
        foreach (var item in result.Items)
        {
            controller.Items.Add(item);
            try { create?.Invoke(controller, [item]); }
            catch { /* A bad decoder must not cancel the rest of the imported layout. */ }
        }
        typeof(DeskCanvasController).GetMethod("Save", PrivateInstance)?.Invoke(controller, null);
        typeof(DeskCanvasController).GetMethod("ReapplyZOrder", PrivateInstance)?.Invoke(controller, null);
        return result;
    }
}
