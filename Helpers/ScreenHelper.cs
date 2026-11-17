using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SonarController.Helpers;

public readonly record struct PixelPoint(int X, int Y);

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

/// <summary>物理ピクセル単位のモニター情報。</summary>
public sealed record ScreenInfo(string DeviceName, PixelRect Bounds, PixelRect WorkingArea, bool IsPrimary);

/// <summary>System.Windows.Forms を読み込まずにカーソル位置とモニター情報を取得する。</summary>
public static class ScreenHelper
{
    private const uint MonitorDefaultToNearest = 2;
    private const uint MonitorInfoPrimary = 1;

    public static PixelPoint GetCursorPosition()
    {
        if (!GetCursorPos(out var point))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        return new PixelPoint(point.X, point.Y);
    }

    public static IReadOnlyList<ScreenInfo> GetAllScreens()
    {
        var screens = new List<ScreenInfo>();
        MonitorEnumProc callback = (monitor, _, _, _) =>
        {
            if (TryGetScreen(monitor, out var screen))
                screens.Add(screen);
            return true;
        };

        if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        GC.KeepAlive(callback);
        return screens;
    }

    /// <summary>指定位置を含むモニターを返す。どれにも含まれない場合は最も近いモニター。</summary>
    public static ScreenInfo FromPoint(PixelPoint point)
    {
        var monitor = MonitorFromPoint(new NativePoint { X = point.X, Y = point.Y }, MonitorDefaultToNearest);
        if (TryGetScreen(monitor, out var screen))
            return screen;

        return GetAllScreens().FirstOrDefault(s => s.IsPrimary)
            ?? throw new InvalidOperationException("No display monitor was found.");
    }

    private static bool TryGetScreen(IntPtr monitor, out ScreenInfo screen)
    {
        var info = new MonitorInfoEx { Size = (uint)Marshal.SizeOf<MonitorInfoEx>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            screen = null!;
            return false;
        }

        screen = new ScreenInfo(
            info.Device,
            new PixelRect(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom),
            new PixelRect(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom),
            (info.Flags & MonitorInfoPrimary) != 0);
        return true;
    }

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Device;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);
}
