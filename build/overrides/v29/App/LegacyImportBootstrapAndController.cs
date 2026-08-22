using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;

namespace DeskCanvas.App;

internal static class LegacyImportUiBootstrap
{
    [ModuleInitializer]
    internal static void Register() => EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
    private static void OnLoaded(object sender, RoutedEventArgs e) { if (sender is MainWindow window) window.EnsureLegacyImportAction(); }
}

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
            try { create?.Invoke(controller, [item]); } catch { }
        }
        typeof(DeskCanvasController).GetMethod("Save", PrivateInstance)?.Invoke(controller, null);
        typeof(DeskCanvasController).GetMethod("ReapplyZOrder", PrivateInstance)?.Invoke(controller, null);
        return result;
    }
}
