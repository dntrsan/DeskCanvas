using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DeskCanvas.App.Media;
using DeskCanvas.App.Services;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

internal partial class MediaWindow : Window, IDisposable
{
    private const int WmNcHitTest = 0x0084;
    private static readonly IntPtr HtTransparent = new(-1);
    private const double ExtraSpace = 150;

    private readonly CanvasItem item;
    private readonly DecodedMedia media;
    private readonly DesktopWindowService desktop;
    private readonly Action<CanvasItem, bool> changed;
    private readonly Action<CanvasItem> delete;
    private readonly Action<CanvasItem> lockItem;
    private DispatcherTimer? animationTimer;
    private HwndSource? source;
    private IntPtr handle;
    private bool editEnabled;
    private bool disposed;
    private int frameIndex;
    private DragOperation dragOperation;
    private Point startScreen;
    private double startCenterX;
    private double startCenterY;
    private double startWidth;
    private double startHeight;
    private double startRotation;
    private double startPointerAngle;

    internal MediaWindow(
        CanvasItem item,
        DecodedMedia media,
        DesktopWindowService desktop,
        Action<CanvasItem, bool> changed,
        Action<CanvasItem> delete,
        Action<CanvasItem> lockItem)
    {
        this.item = item;
        this.media = media;
        this.desktop = desktop;
        this.changed = changed;
        this.delete = delete;
        this.lockItem = lockItem;
        InitializeComponent();
        DataContext = item;
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
        PreviewMouseMove += Window_PreviewMouseMove;
        PreviewMouseLeftButtonUp += Window_PreviewMouseLeftButtonUp;
        item.PropertyChanged += Item_PropertyChanged;
        ApplyItem();
    }

    internal IntPtr Handle => handle;
    internal CanvasItem Item => item;

    internal void SetEditEnabled(bool enabled)
    {
        editEnabled = enabled;
        if (handle != IntPtr.Zero)
        {
            desktop.Configure(handle, clickThrough: !enabled);
        }
        if (!enabled)
        {
            HideChrome();
            EndDrag(persist: true);
        }
        ApplyChromeVisibility();
    }

    internal void Reposition()
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }
        var side = CalculateSide();
        desktop.PlaceAboveDesktop(
            handle,
            item.CenterX - side / 2,
            item.CenterY - side / 2,
            side,
            side);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        animationTimer?.Stop();
        item.PropertyChanged -= Item_PropertyChanged;
        if (source is not null)
        {
            source.RemoveHook(WindowProc);
        }
        Close();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        MediaImage.Source = media.Frames[0].Image;
        if (media.IsAnimated)
        {
            animationTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = media.Frames[0].Duration
            };
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();
        }
        ApplyItem();
        Reposition();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        handle = new WindowInteropHelper(this).Handle;
        source = HwndSource.FromHwnd(handle);
        source?.AddHook(WindowProc);
        desktop.Configure(handle, clickThrough: !editEnabled);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        animationTimer?.Stop();
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        frameIndex = (frameIndex + 1) % media.Frames.Count;
        MediaImage.Source = media.Frames[frameIndex].Image;
        if (animationTimer is not null)
        {
            animationTimer.Interval = media.Frames[frameIndex].Duration;
        }
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CanvasItem.Width) or
            nameof(CanvasItem.Height) or
            nameof(CanvasItem.RotationDegrees) or
            nameof(CanvasItem.Opacity) or
            nameof(CanvasItem.IsFlipped) or
            nameof(CanvasItem.CenterX) or
            nameof(CanvasItem.CenterY))
        {
            Dispatcher.InvokeAsync(() =>
            {
                ApplyItem();
                Reposition();
            });
        }
    }

    private void ApplyItem()
    {
        var side = CalculateSide();
        Width = side;
        Height = side;
        Root.Width = side;
        Root.Height = side;
        RotatedVisual.Width = item.Width;
        RotatedVisual.Height = item.Height;
        RotatedVisual.RenderTransform = new RotateTransform(item.RotationDegrees);
        MediaImage.Opacity = item.Opacity;
        MediaImage.RenderTransformOrigin = new Point(0.5, 0.5);
        MediaImage.RenderTransform = new ScaleTransform(item.IsFlipped ? -1 : 1, 1);
        ApplyChromeVisibility();
    }

    private double CalculateSide() =>
        Math.Ceiling(Math.Sqrt(item.Width * item.Width + item.Height * item.Height) + ExtraSpace);

    private void Visual_MouseEnter(object sender, MouseEventArgs e)
    {
        ApplyChromeVisibility(forceVisible: true);
    }

    private void Visual_MouseLeave(object sender, MouseEventArgs e)
    {
        if (dragOperation == DragOperation.None)
        {
            HideChrome();
        }
    }

    private void ApplyChromeVisibility(bool forceVisible = false)
    {
        var visible = editEnabled && (forceVisible || RotatedVisual.IsMouseOver || dragOperation != DragOperation.None);
        var visibility = visible ? Visibility.Visible : Visibility.Hidden;
        ChromeBorder.BorderThickness = visible ? new Thickness(2) : new Thickness(0);
        Toolbar.Visibility = visibility;
        ResizeHandle.Visibility = visibility;
        RotationHandle.Visibility = visibility;
        RotationStem.Visibility = visibility;
    }

    private void HideChrome()
    {
        ChromeBorder.BorderThickness = new Thickness(0);
        Toolbar.Visibility = Visibility.Hidden;
        ResizeHandle.Visibility = Visibility.Hidden;
        RotationHandle.Visibility = Visibility.Hidden;
        RotationStem.Visibility = Visibility.Hidden;
    }

    private void Move_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginDrag(DragOperation.Move, e);
    }

    private void Resize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginDrag(DragOperation.Resize, e);
    }

    private void Rotate_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginDrag(DragOperation.Rotate, e);
    }

    private void BeginDrag(DragOperation operation, MouseButtonEventArgs e)
    {
        if (!editEnabled)
        {
            return;
        }
        dragOperation = operation;
        startScreen = PointToScreen(e.GetPosition(this));
        startCenterX = item.CenterX;
        startCenterY = item.CenterY;
        startWidth = item.Width;
        startHeight = item.Height;
        startRotation = item.RotationDegrees;
        startPointerAngle = PointerAngle(startScreen);
        Mouse.Capture(this);
        ApplyChromeVisibility(forceVisible: true);
        e.Handled = true;
    }

    private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (dragOperation == DragOperation.None || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = PointToScreen(e.GetPosition(this));
        var dx = current.X - startScreen.X;
        var dy = current.Y - startScreen.Y;
        switch (dragOperation)
        {
            case DragOperation.Move:
                item.CenterX = startCenterX + dx;
                item.CenterY = startCenterY + dy;
                break;
            case DragOperation.Resize:
                ResizeFromDelta(dx, dy);
                break;
            case DragOperation.Rotate:
                var angle = startRotation + PointerAngle(current) - startPointerAngle;
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    angle = Math.Round(angle / 15) * 15;
                }
                item.RotationDegrees = angle;
                break;
        }

        changed(item, false);
    }

    private void ResizeFromDelta(double dx, double dy)
    {
        var radians = startRotation * Math.PI / 180;
        var localX = dx * Math.Cos(radians) + dy * Math.Sin(radians);
        var localY = -dx * Math.Sin(radians) + dy * Math.Cos(radians);
        var scaleFromX = (startWidth + localX) / startWidth;
        var scaleFromY = (startHeight + localY) / startHeight;
        var scale = Math.Max(48 / Math.Min(startWidth, startHeight), Math.Max(scaleFromX, scaleFromY));
        var newWidth = Math.Max(48, startWidth * scale);
        var newHeight = Math.Max(48, startHeight * scale);
        var widthChange = newWidth - startWidth;
        var heightChange = newHeight - startHeight;
        item.Width = newWidth;
        item.Height = newHeight;
        item.CenterX = startCenterX +
            (widthChange / 2 * Math.Cos(radians) - heightChange / 2 * Math.Sin(radians));
        item.CenterY = startCenterY +
            (widthChange / 2 * Math.Sin(radians) + heightChange / 2 * Math.Cos(radians));
    }

    private double PointerAngle(Point screenPoint) =>
        Math.Atan2(screenPoint.Y - item.CenterY, screenPoint.X - item.CenterX) * 180 / Math.PI;

    private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndDrag(persist: true);
    }

    private void EndDrag(bool persist)
    {
        if (dragOperation == DragOperation.None)
        {
            return;
        }
        dragOperation = DragOperation.None;
        if (Mouse.Captured == this)
        {
            Mouse.Capture(null);
        }
        changed(item, persist);
        ApplyChromeVisibility();
    }

    private void Flip_Click(object sender, RoutedEventArgs e)
    {
        item.IsFlipped = !item.IsFlipped;
        changed(item, true);
    }

    private void Lock_Click(object sender, RoutedEventArgs e)
    {
        lockItem(item);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        delete(item);
    }

    private IntPtr WindowProc(
        IntPtr window,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message != WmNcHitTest || !editEnabled)
        {
            return IntPtr.Zero;
        }

        var x = unchecked((short)(long)lParam);
        var y = unchecked((short)((long)lParam >> 16));
        var point = PointFromScreen(new Point(x, y));
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radians = -item.RotationDegrees * Math.PI / 180;
        var translatedX = point.X - center.X;
        var translatedY = point.Y - center.Y;
        var localX = translatedX * Math.Cos(radians) - translatedY * Math.Sin(radians);
        var localY = translatedX * Math.Sin(radians) + translatedY * Math.Cos(radians);
        var hit = Math.Abs(localX) <= item.Width / 2 + 36 &&
                  Math.Abs(localY) <= item.Height / 2 + 38;
        if (!hit)
        {
            handled = true;
            return HtTransparent;
        }

        return IntPtr.Zero;
    }

    private enum DragOperation
    {
        None,
        Move,
        Resize,
        Rotate
    }
}
