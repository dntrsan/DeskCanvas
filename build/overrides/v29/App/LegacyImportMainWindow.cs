using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinForms = System.Windows.Forms;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;

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
        WpfMessageBox.Show(result.JapaneseSummary(), "旧データを引き継ぐ", MessageBoxButton.OK, result.IsRejected ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }
}
