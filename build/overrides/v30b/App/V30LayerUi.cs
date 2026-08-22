using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

internal static class V30LayerUiBootstrap
{
    [ModuleInitializer]
    internal static void Register() => EventManager.RegisterClassHandler(typeof(MainWindow), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
    private static void OnLoaded(object sender, RoutedEventArgs e) { if (sender is MainWindow window) window.AddLayerOrderControls(); }
}

public partial class MainWindow
{
    private WrapPanel? layerOrderPanel;
    internal void AddLayerOrderControls()
    {
        if (layerOrderPanel is not null) return;
        layerOrderPanel = new WrapPanel { Margin = new Thickness(0, 18, 0, 0), Tag = "v30b-layer-order" };
        AddLayerButton("最前面", "選択した項目を最前面へ", () => LayerOrderAction.Front(controller, SelectedItem));
        AddLayerButton("上へ", "選択した項目を一段上へ", () => LayerOrderAction.Up(controller, SelectedItem));
        AddLayerButton("下へ", "選択した項目を一段下へ", () => LayerOrderAction.Down(controller, SelectedItem));
        AddLayerButton("最背面", "選択した項目を最背面へ", () => LayerOrderAction.Back(controller, SelectedItem));
        EditorPanel.Children.Add(layerOrderPanel);
    }
    private void AddLayerButton(string text, string help, Action action)
    {
        var button = new Button { Content = text, ToolTip = help, IsTabStop = true };
        AutomationProperties.SetName(button, text); AutomationProperties.SetHelpText(button, help);
        button.Click += (_, _) => action(); layerOrderPanel!.Children.Add(button);
    }
}

internal static class LayerOrderAction
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static void Front(DeskCanvasController controller, CanvasItem? item) { if (item is not null) controller.BringToFront(item); }
    internal static void Back(DeskCanvasController controller, CanvasItem? item) { if (item is not null) controller.SendToBack(item); }
    internal static void Up(DeskCanvasController controller, CanvasItem? item) => Move(controller, item, true);
    internal static void Down(DeskCanvasController controller, CanvasItem? item) => Move(controller, item, false);
    private static void Move(DeskCanvasController controller, CanvasItem? item, bool up)
    {
        if (item is null) return;
        var items = controller.Items;
        var moved = up ? LayerOrderMath.MoveUp(items, item) : LayerOrderMath.MoveDown(items, item);
        if (!moved) return; // boundary is deliberately a no-op
        typeof(DeskCanvasController).GetMethod("NormalizeZOrder", PrivateInstance)?.Invoke(controller, null);
    }
}
