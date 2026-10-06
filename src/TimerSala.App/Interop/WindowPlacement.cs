using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TimerSala.Core.Storage;

namespace TimerSala.App.Interop;

/// <summary>
/// Ricorda posizione e dimensioni delle finestre con le funzioni di Windows, che lavorano in pixel reali
/// e funzionano anche con più monitor a scale diverse. Se il monitor salvato non c'è più la finestra
/// si apre nella posizione predefinita.
/// </summary>
public static class WindowPlacement
{
    /// <summary>
    /// Da chiamare nel costruttore, prima di mostrare la finestra. Con <paramref name="keepSize"/> si ripristina
    /// solo la posizione (finestre che si dimensionano da sole, come la mini).
    /// </summary>
    public static bool Restore(Window window, WindowBounds? saved, bool keepSize = false)
    {
        if (saved is null || saved.Right - saved.Left < 50 || saved.Bottom - saved.Top < 50) return false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.SourceInitialized += (_, _) => Apply(window, saved, keepSize);
        return true;
    }

    static void Apply(Window window, WindowBounds saved, bool keepSize)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!GetWindowPlacement(hwnd, ref wp)) return;

        var r = new RECT { Left = saved.Left, Top = saved.Top, Right = saved.Right, Bottom = saved.Bottom };
        if (keepSize)
        {
            r.Right = r.Left + (wp.rcNormalPosition.Right - wp.rcNormalPosition.Left);
            r.Bottom = r.Top + (wp.rcNormalPosition.Bottom - wp.rcNormalPosition.Top);
        }
        // la parte alta della finestra (con la barra del titolo) deve stare su uno schermo collegato
        var top = new POINT { X = (r.Left + r.Right) / 2, Y = r.Top + 10 };
        if (MonitorFromPoint(top, MONITOR_DEFAULTTONULL) == IntPtr.Zero) return;

        wp.rcNormalPosition = r;
        wp.flags = 0;
        wp.showCmd = saved.Maximized && window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip
            ? SW_SHOWMAXIMIZED : SW_SHOWNORMAL;
        SetWindowPlacement(hwnd, ref wp);
    }

    /// <summary>Posizione e dimensioni attuali (quelle «normali» se la finestra è ingrandita), anche se è nascosta.</summary>
    public static WindowBounds? Capture(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return null;
        var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!GetWindowPlacement(hwnd, ref wp)) return null;
        var r = wp.rcNormalPosition;
        return new WindowBounds(r.Left, r.Top, r.Right, r.Bottom, wp.showCmd == SW_SHOWMAXIMIZED);
    }

    const int SW_SHOWNORMAL = 1, SW_SHOWMAXIMIZED = 3;
    const uint MONITOR_DEFAULTTONULL = 0;

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [DllImport("user32.dll")]
    static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
}
