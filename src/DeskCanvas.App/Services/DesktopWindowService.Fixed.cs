using System.ComponentModel;
using System.Runtime.InteropServices;
using DeskCanvas.Core;

namespace DeskCanvas.App.Services;

internal sealed class DesktopWindowService
{
    private const int GwlpStyle = -16;
    private const int GwlpExStyle = -20;
    private const long WsChild = 0x40000000L;
    private const long WsPopup = 0x80000000L;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint GwHwndPrevious = 3;
    private const int MonitorDpiTypeEffective = 0;
    private static readonly IntPtr HwndBottom = new(1);

    internal static IntPtr FindDesktopHost()
    {
        var desktop = Locate();
        return desktop.IsValid ? desktop.Host : IntPtr.Zero;
    }

    internal IntPtr CurrentDesktopHost => FindDesktopHost();

    internal IReadOnlyList<DisplayArea> GetDisplays()
    {
        var result = new List<DisplayArea>();
        if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, CollectMonitor, IntPtr.Zero) && result.Count == 0)
        {
            return result;
        }
        return result;

        bool CollectMonitor(
            IntPtr monitor,
            IntPtr hdc,
            ref NativeRect bounds,
            IntPtr data)
        {
            var info = new MonitorInfoEx
            {
                Size = Marshal.SizeOf<MonitorInfoEx>(),
                Device = ""
            };
            if (GetMonitorInfo(monitor, ref info))
            {
                var scale = TryGetMonitorScale(monitor);
                result.Add(new DisplayArea(
                    info.Device,
                    info.Monitor.Left / scale,
                    info.Monitor.Top / scale,
                    (info.Monitor.Right - info.Monitor.Left) / scale,
                    (info.Monitor.Bottom - info.Monitor.Top) / scale,
                    (info.Flags & 1) != 0));
            }
            return true;
        }
    }

    private static double TryGetMonitorScale(IntPtr monitor)
    {
        try
        {
            if (GetDpiForMonitor(monitor, MonitorDpiTypeEffective, out var dpiX, out _) == 0 && dpiX > 0)
            {
                return dpiX / 96d;
            }
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        return 1;
    }

    internal void Configure(IntPtr window, bool clickThrough)
    {
        _ = SetParent(window, IntPtr.Zero);
        var currentStyle = GetWindowLongPtr(window, GwlpStyle).ToInt64();
        _ = SetWindowLongPtr(window, GwlpStyle, new IntPtr((currentStyle | WsPopup) & ~WsChild));

        var currentExStyle = GetWindowLongPtr(window, GwlpExStyle).ToInt64();
        currentExStyle |= WsExToolWindow | WsExNoActivate;
        currentExStyle = clickThrough
            ? currentExStyle | WsExTransparent
            : currentExStyle & ~WsExTransparent;
        _ = SetWindowLongPtr(window, GwlpExStyle, new IntPtr(currentExStyle));

        // Styles only. This must not touch the Z-order: Configure runs on every edit-mode
        // toggle, and sending the widget to HWND_BOTTOM here pushed it behind Progman/WorkerW,
        // i.e. behind the wallpaper, until the next Reposition happened to pull it back.
        _ = SetWindowPos(
            window,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SwpNoActivate | SwpFrameChanged | SwpNoMove | SwpNoSize | SwpNoZOrder);
    }

    internal bool PlaceAboveDesktop(
        IntPtr window,
        double left,
        double top,
        double width,
        double height,
        bool showWindow = true)
    {
        var desktop = Locate();
        if (!desktop.IsValid)
        {
            Demote(window);
            return false;
        }

        // A top-level window inserted directly above Progman/WorkerW remains
        // visible with desktop icons, but normal application windows cover it.
        //
        // hWndInsertAfter names the window that ends up ABOVE ours, so passing desktop.Host
        // would place the widget underneath the wallpaper. Target the sibling immediately
        // above the desktop host instead; HWND_TOP is the fallback when there is none.
        var aboveDesktop = GetWindow(desktop.Host, GwHwndPrevious);
        var flags = SwpNoActivate | (showWindow ? SwpShowWindow : 0);
        if (!SetWindowPos(
                window,
                aboveDesktop,
                checked((int)Math.Round(left)),
                checked((int)Math.Round(top)),
                Math.Max(1, checked((int)Math.Round(width))),
                Math.Max(1, checked((int)Math.Round(height))),
                flags))
        {
            Demote(window);
            return false;
        }
        return true;
    }

    internal void Demote(IntPtr window)
    {
        _ = SetWindowPos(
            window,
            HwndBottom,
            0,
            0,
            0,
            0,
            SwpNoActivate | SwpNoMove | SwpNoSize);
    }

    private static DesktopHandles Locate()
    {
        var programManager = FindWindow("Progman", null);
        var shellView = FindWindowEx(programManager, IntPtr.Zero, "SHELLDLL_DefView", null);
        var host = programManager;

        if (shellView == IntPtr.Zero)
        {
            var worker = IntPtr.Zero;
            while (true)
            {
                worker = FindWindowEx(IntPtr.Zero, worker, "WorkerW", null);
                if (worker == IntPtr.Zero)
                {
                    break;
                }
                shellView = FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    host = worker;
                    break;
                }
            }
        }
        return new DesktopHandles(host, shellView);
    }

    private readonly record struct DesktopHandles(IntPtr Host, IntPtr ShellView)
    {
        internal bool IsValid =>
            Host != IntPtr.Zero &&
            ShellView != IntPtr.Zero &&
            IsWindow(Host) &&
            IsWindow(ShellView);
    }

    private delegate bool MonitorEnumProc(
        IntPtr monitor,
        IntPtr hdc,
        ref NativeRect bounds,
        IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        internal int Size;
        internal NativeRect Monitor;
        internal NativeRect Work;
        internal uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string Device;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindowEx(
        IntPtr parent,
        IntPtr childAfter,
        string? className,
        string? windowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindow(IntPtr window, uint command);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr clip,
        MonitorEnumProc callback,
        IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern IntPtr GetWindowLong32(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr window, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern IntPtr SetWindowLong32(IntPtr window, int index, IntPtr value);

    private static IntPtr GetWindowLongPtr(IntPtr window, int index) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(window, index) : GetWindowLong32(window, index);

    private static IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(window, index, value)
            : SetWindowLong32(window, index, value);
}
