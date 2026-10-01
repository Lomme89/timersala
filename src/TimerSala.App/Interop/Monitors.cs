using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TimerSala.App.Interop;

/// <summary>Uno schermo collegato al PC (coordinate in pixel fisici).</summary>
public sealed record MonitorInfo(string DeviceName, Int32Rect Bounds, bool IsPrimary, int Number)
{
    public string Label => $"Schermo {Number} — {Bounds.Width}×{Bounds.Height}{(IsPrimary ? " (principale)" : "")}";

    public override string ToString() => Label;
}

public static class Monitors
{
    public static IReadOnlyList<MonitorInfo> GetAll()
    {
        var list = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMon, ref mi))
            {
                var r = mi.rcMonitor;
                list.Add(new MonitorInfo(mi.szDevice,
                    new Int32Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top),
                    (mi.dwFlags & MONITORINFOF_PRIMARY) != 0, 0));
            }
            return true;
        }, IntPtr.Zero);

        // numerazione come nelle impostazioni di Windows (\\.\DISPLAY3 -> 3), altrimenti per posizione
        var ordered = list.OrderBy(m => m.Bounds.X).ThenBy(m => m.Bounds.Y).ToList();
        return ordered.Select((m, i) =>
        {
            var digits = new string(m.DeviceName.Where(char.IsDigit).ToArray());
            return m with { Number = int.TryParse(digits, out var n) ? n : i + 1 };
        }).OrderBy(m => m.Number).ToList();
    }

    /// <summary>Posiziona la finestra a schermo intero sul monitor indicato.</summary>
    public static void PlaceOn(Window window, MonitorInfo monitor)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var b = monitor.Bounds;
        SetWindowPos(hwnd, IntPtr.Zero, b.X, b.Y, b.Width, b.Height, SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    /// <summary>Monitor su cui si trova attualmente la finestra.</summary>
    public static string? DeviceOf(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return null;
        var hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        return GetMonitorInfo(hMon, ref mi) ? mi.szDevice : null;
    }

    const int MONITORINFOF_PRIMARY = 1;
    const uint MONITOR_DEFAULTTONEAREST = 2;
    const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020;

    delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
    }

    [DllImport("user32.dll")]
    static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);

    [DllImport("user32.dll")]
    static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
