using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

// The editor already owns its general labels.  These three captions belong to
// the injected clock group and follow the same visibility rule as its fields.
internal static class V30ClockEditorCaptionCleanup
{
    [ModuleInitializer]
    internal static void Initialize() => EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(Loaded));

    private static void Loaded(object sender, RoutedEventArgs _)
    {
        if (sender is not MainWindow window) return;
        window.ItemsList.SelectionChanged += (_, _) => window.UpdateV30ClockCaptions();
        window.UpdateV30ClockCaptions();
    }
}

public partial class MainWindow
{
    internal void UpdateV30ClockCaptions()
    {
        var visible = SelectedItem?.ContentKind == CanvasContentKinds.Clock;
        foreach (var caption in EditorPanel.Children.OfType<TextBlock>().Where(text => text.Text is "フォント" or "文字色" or "太さ"))
            caption.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
}
