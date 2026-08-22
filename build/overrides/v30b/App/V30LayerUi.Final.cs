using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using DeskCanvas.Core;
namespace DeskCanvas.App.Windows;
internal static class V30LayerUiBootstrap{[ModuleInitializer]internal static void Register()=>EventManager.RegisterClassHandler(typeof(MainWindow),FrameworkElement.LoadedEvent,new RoutedEventHandler((s,e)=>{if(s is MainWindow w)w.AddLayerOrderControls();}));}
public partial class MainWindow
{
 private WrapPanel? layerOrderPanel;
 internal void AddLayerOrderControls(){if(layerOrderPanel is not null)return;foreach(var panel in EditorPanel.Children.OfType<WrapPanel>())foreach(var old in panel.Children.OfType<Button>().Where(b=>b.Content?.ToString() is "前面へ" or "背面へ"))old.Visibility=Visibility.Collapsed;layerOrderPanel=new WrapPanel{Margin=new Thickness(0,18,0,0),Tag="v30b-layer-order"};AddLayerButton("最前面","選択した項目を最前面へ",()=>LayerOrderAction.Front(controller,SelectedItem));AddLayerButton("上へ","選択した項目を一段上へ",()=>LayerOrderAction.Up(controller,SelectedItem));AddLayerButton("下へ","選択した項目を一段下へ",()=>LayerOrderAction.Down(controller,SelectedItem));AddLayerButton("最背面","選択した項目を最背面へ",()=>LayerOrderAction.Back(controller,SelectedItem));EditorPanel.Children.Add(layerOrderPanel);}
 private void AddLayerButton(string text,string help,Action action){var button=new Button{Content=text,ToolTip=help,IsTabStop=true};AutomationProperties.SetName(button,text);AutomationProperties.SetHelpText(button,help);button.Click+=(_,__)=>action();layerOrderPanel!.Children.Add(button);}
}
internal static class LayerOrderAction
{
 private const BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
 internal static void Front(DeskCanvasController c,CanvasItem? i){if(i is not null)c.BringToFront(i);} internal static void Back(DeskCanvasController c,CanvasItem? i){if(i is not null)c.SendToBack(i);} internal static void Up(DeskCanvasController c,CanvasItem? i)=>Move(c,i,true);internal static void Down(DeskCanvasController c,CanvasItem? i)=>Move(c,i,false);
 private static void Move(DeskCanvasController c,CanvasItem? i,bool up){if(i is null)return;var moved=up?LayerOrderMath.MoveUp(c.Items,i):LayerOrderMath.MoveDown(c.Items,i);if(!moved)return;typeof(DeskCanvasController).GetMethod("NormalizeZOrder",PrivateInstance)?.Invoke(c,null);}
}
