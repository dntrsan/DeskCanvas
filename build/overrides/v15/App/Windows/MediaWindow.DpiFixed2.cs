using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DeskCanvas.App.Services;
using DeskCanvas.Core;

namespace DeskCanvas.App.Windows;

public partial class MediaWindow : Window, IDisposable
{
    private const int WmNcHitTest = 0x0084;
    private const int WmMouseActivate = 0x0021;
    private static readonly IntPtr HtTransparent = new(-1);
    private static readonly IntPtr MaNoActivate = new(3);
    private const double ExtraSpace = 150;

    private readonly CanvasItem item;
    private readonly IDesktopItemContent content;
    private readonly DesktopWindowService desktop;
    private readonly Action<CanvasItem, bool> changed;
    private readonly Action<CanvasItem> delete;
    private readonly Action<CanvasItem> lockItem;
    private readonly Action<CanvasItem> hideItem;
    private readonly DropShadowEffect whiteOutlineEffect;
    private HwndSource? source;
    private IntPtr handle;
    private bool editEnabled;
    private bool effectivelyVisible;
    private bool disposed;
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
        IDesktopItemContent content,
        DesktopWindowService desktop,
        Action<CanvasItem, bool> changed,
        Action<CanvasItem> delete,
        Action<CanvasItem> lockItem,
        Action<CanvasItem> hideItem)
    {
        this.item = item;
        this.content = content;
        this.desktop = desktop;
        this.changed = changed;
        this.delete = delete;
        this.lockItem = lockItem;
        this.hideItem = hideItem;
        whiteOutlineEffect = new DropShadowEffect
        {
            Color = Colors.White,
            ShadowDepth = 0,
            BlurRadius = 3,
            Opacity = 1,
            RenderingBias = RenderingBias.Quality
        };
        whiteOutlineEffect.Freeze();

        InitializeComponent();
        DataContext = item;
        ItemContent.Content = content.View;
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
        PreviewMouseMove += Window_PreviewMouseMove;
        PreviewMouseLeftButtonUp += Window_PreviewMouseLeftButtonUp;
        item.PropertyChanged += Item_PropertyChanged;
        ApplyItem();
    }

    internal CanvasItem Item => item;

    internal void SetEditEnabled(bool enabled)
    {
        editEnabled = enabled;
        if (handle != IntPtr.Zero)
        {
            desktop.Configure(handle, clickThrough: !enabled && !content.HasInteractiveControls);
        }
        if (!enabled)
        {
            EndDrag(persist: true);
            HideChrome();
        }
        ApplyChromeVisibility();
    }

    internal void SetEffectivelyVisible(bool visible)
    {
        effectivelyVisible = visible;
        if (visible)
        {
            Reposition();
        }
        else
        {
            content.SetActive(false);
            EndDrag(persist: true);
            if (IsVisible)
            {
                Hide();
            }
        }
    }

    internal void Reposition()
    {
        if (!effectivelyVisible)
        {
            return;
        }
        if (handle == IntPtr.Zero)
        {
            _ = new WindowInteropHelper(this).EnsureHandle();
        }
        if (handle == IntPtr.Zero) return;
        var side = CalculateSide();
        var dpi = VisualTreeHelper.GetDpi(this);
        var width = Math.Ceiling(side * dpi.DpiScaleX);
        var height = Math.Ceiling(side * dpi.DpiScaleY);
        var left = item.CenterX - width / 2;
        var top = item.CenterY - height / 2;
        if (!desktop.PlaceAboveDesktop(handle, left, top, width, height, showWindow: false))
        {
            content.SetActive(false);
            if (IsVisible) Hide();
            return;
        }
        if (!IsVisible) Show();
        if (!desktop.PlaceAboveDesktop(handle, left, top, width, height, showWindow: false))
        {
            content.SetActive(false);
            desktop.Demote(handle);
            if (IsVisible) Hide();
            return;
        }
        content.SetActive(true);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        effectivelyVisible = false;
        content.SetActive(false);
        item.PropertyChanged -= Item_PropertyChanged;
        if (source is not null)
        {
            source.RemoveHook(WindowProc);
            source = null;
        }
        Close();
        content.Dispose();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyItem();
        if (effectivelyVisible)
        {
            Reposition();
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        handle = new WindowInteropHelper(this).Handle;
        source = HwndSource.FromHwnd(handle);
        source?.AddHook(WindowProc);
        desktop.Configure(handle, clickThrough: !editEnabled && !content.HasInteractiveControls);
    }

    private void OnClosing(object? sender, CancelEventArgs e) => content.SetActive(false);

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CanvasItem.Width) or
            nameof(CanvasItem.Height) or
            nameof(CanvasItem.RotationDegrees) or
            nameof(CanvasItem.Opacity) or
            nameof(CanvasItem.IsFlipped) or
            nameof(CanvasItem.CenterX) or
            nameof(CanvasItem.CenterY) or
            nameof(CanvasItem.DecorationMode))
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

        DecoratedContent.Opacity = item.Opacity;
        ItemContent.Effect = item.DecorationMode == DecorationModes.WhiteOutline ? whiteOutlineEffect : null;
        DecorationBorder.BorderThickness = item.DecorationMode == DecorationModes.OuterFrame ? new Thickness(2) : new Thickness(0);

        if (content is MediaItemContent mediaContent)
        {
            mediaContent.SetFlipped(item.IsFlipped);
            FlipButton.Visibility = Visibility.Visible;
        }
        else
        {
            FlipButton.Visibility = Visibility.Collapsed;
        }
        ApplyChromeVisibility();
    }

    private double CalculateSide() => Math.Ceiling(Math.Sqrt(item.Width * item.Width + item.Height * item.Height) + ExtraSpace);

    private void Visual_MouseEnter(object sender, MouseEventArgs e) => ApplyChromeVisibility(forceVisible: true);

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
        var value = visible ? Visibility.Visible : Visibility.Hidden;
        ChromeBorder.BorderThickness = visible ? new Thickness(2) : new Thickness(0);
        Toolbar.Visibility = value;
        ResizeHandle.Visibility = value;
        RotationHandle.Visibility = value;
        RotationStem.Visibility = value;
    }

    private void HideChrome()
    {
        ChromeBorder.BorderThickness = new Thickness(0);
        Toolbar.Visibility = Visibility.Hidden;
        ResizeHandle.Visibility = Visibility.Hidden;
        RotationHandle.Visibility = Visibility.Hidden;
        RotationStem.Visibility = Visibility.Hidden;
    }

    private void Move_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => BeginDrag(DragOperation.Move, e);
    private void Resize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => BeginDrag(DragOperation.Resize, e);
    private void Rotate_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => BeginDrag(DragOperation.Rotate, e);

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
        if (dragOperation == DragOperation.Move)
        {
            item.CenterX = startCenterX + dx;
            item.CenterY = startCenterY + dy;
        }
        else if (dragOperation == DragOperation.Rotate)
        {
            var angle = startRotation + PointerAngle(current) - startPointerAngle;
            item.RotationDegrees = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? Math.Round(angle / 15) * 15 : angle;
        }
        else
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            ResizeFromDelta(dx / dpi.DpiScaleX, dy / dpi.DpiScaleY, dpi.DpiScaleX, dpi.DpiScaleY);
        }
        changed(item, false);
    }

    private void ResizeFromDelta(double dx, double dy, double dpiX, double dpiY)
    {
        var radians = startRotation * Math.PI / 180;
        var localX = dx * Math.Cos(radians) + dy * Math.Sin(radians);
        var localY = -dx * Math.Sin(radians) + dy * Math.Cos(radians);
        var scale = Math.Max(48 / Math.Min(startWidth, startHeight), Math.Max((startWidth + localX) / startWidth, (startHeight + localY) / startHeight));
        var width = Math.Max(48, startWidth * scale);
        var height = Math.Max(48, startHeight * scale);
        item.Width = width;
        item.Height = height;
        var shift = MediaWindowDpiMath.ResizeCenterShift(width - startWidth, height - startHeight, radians, dpiX, dpiY);
        item.CenterX = startCenterX + shift.X;
        item.CenterY = startCenterY + shift.Y;
    }

    private double PointerAngle(Point point) => Math.Atan2(point.Y - item.CenterY, point.X - item.CenterX) * 180 / Math.PI;

    private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndDrag(persist: true);

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
        if (content is not MediaItemContent)
        {
            return;
        }
        item.IsFlipped = !item.IsFlipped;
        changed(item, true);
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => hideItem(item);
    private void Lock_Click(object sender, RoutedEventArgs e) => lockItem(item);
    private void Delete_Click(object sender, RoutedEventArgs e) => delete(item);

    private IntPtr WindowProc(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmMouseActivate)
        {
            handled = true;
            return MaNoActivate;
        }
        if (message != WmNcHitTest)
        {
            return IntPtr.Zero;
        }

        var packed = lParam.ToInt64();
        var screenX = unchecked((short)(packed & 0xffff));
        var screenY = unchecked((short)((packed >> 16) & 0xffff));
        var point = PointFromScreen(new Point(screenX, screenY));
        if (!editEnabled)
        {
            if (!content.HasInteractiveControls) { handled = true; return HtTransparent; }
            var contentPoint = TranslatePoint(point, content.View);
            if (!content.IsInteractiveHit(contentPoint)) { handled = true; return HtTransparent; }
            return IntPtr.Zero;
        }

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var radians = -item.RotationDegrees * Math.PI / 180;
        var translatedX = point.X - center.X;
        var translatedY = point.Y - center.Y;
        var localX = translatedX * Math.Cos(radians) - translatedY * Math.Sin(radians);
        var localY = translatedX * Math.Sin(radians) + translatedY * Math.Cos(radians);
        var hit = Math.Abs(localX) <= item.Width / 2 + 36 && Math.Abs(localY) <= item.Height / 2 + 38;
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
