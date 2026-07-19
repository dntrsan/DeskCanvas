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

    internal IntPtr CurrentDesktopHost => Locate().Host;

    internal IReadOnlyList<DisplayArea> GetDisplays()
    {
        var result = new List<DisplayArea>();
        _ = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, CollectMonitor, IntPtr.Zero);
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
                result.Add(new DisplayArea(
                    info.Device,
                    info.Work.Left,
                    info.Work.Top,
                    info.Work.Right - info.Work.Left,
                    info.Work.Bottom - info.Work.Top,
                    (info.Flags & 1) != 0));
            }
            return true;
        }
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
        _ = SetWindowPos(
            window,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SwpNoActivate | SwpFrameChanged | SwpNoMove | SwpNoSize | SwpNoZOrder);
    }

    internal void PlaceAboveDesktop(
        IntPtr window,
        double left,
        double top,
        double width,
        double height)
    {
        var desktop = Locate();
        if (!desktop.IsValid)
        {
            return;
        }

        // A top-level window inserted directly above Progman/WorkerW remains
        // visible with desktop icons, but normal application windows cover it.
        if (!SetWindowPos(
                window,
                desktop.Host,
                checked((int)Math.Round(left)),
                checked((int)Math.Round(top)),
                Math.Max(1, checked((int)Math.Round(width))),
                Math.Max(1, checked((int)Math.Round(height))),
                SwpNoActivate | SwpShowWindow))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "素材をデスクトップへ配置できませんでした。");
        }
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
