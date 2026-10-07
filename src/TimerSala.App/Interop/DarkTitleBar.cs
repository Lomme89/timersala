using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TimerSala.App.Interop;

/// <summary>Barra del titolo scura e intonata al programma (Windows 10 20H1+ / Windows 11).</summary>
public static class DarkTitleBar
{
    const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
    const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWA_BORDER_COLOR = 34;
    const int DWMWA_CAPTION_COLOR = 35;
    const int DWMWA_TEXT_COLOR = 36;
    const int DWMWCP_ROUND = 2;

    static bool Light => Theming.UiTheme.IsLight;

    // i colori vengono dalla palette del tema (sfondo, testo, linee); COLORREF = 0x00BBGGRR
    static int Ref(System.Windows.Media.Color c) => c.R | (c.G << 8) | (c.B << 16);
    static int Ref(string key) => Ref(Theming.UiTheme.ColorOf(key));
    static int Border => Ref("LineBrush");

    public static void Apply(Window window, bool roundCorners = false)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        try
        {
            int on = Light ? 0 : 1;
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref on, sizeof(int));

            // solo Windows 11: colori personalizzati (su Windows 10 restano quelli scuri di sistema)
            int caption = Ref("BgBrush"), text = Ref("TextBrush"), border = Border;
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref text, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
            if (roundCorners)
            {
                int round = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
            }
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    /// <summary>
    /// Finestre senza icona propria (impostazioni, editor, dialoghi): niente icona generica di Windows nella barra,
    /// resta solo il titolo nella cornice del colore del tema. Va fatto prima che la finestra compaia
    /// (dopo, Windows non ridisegna la barra): per questo si aggancia alla creazione della finestra.
    /// </summary>
    public static void HideIconWhenCreated(Window window) => window.SourceInitialized += (_, _) =>
    {
        if (window.Icon is not null || window.WindowStyle == WindowStyle.None) return;
        var hwnd = new WindowInteropHelper(window).Handle;
        // la cornice «da dialogo» non disegna l'icona di riserva di Windows
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, GetWindowLongPtr(hwnd, GWL_EXSTYLE) | WS_EX_DLGMODALFRAME);
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    };

    const int GWL_EXSTYLE = -20;
    const nint WS_EX_DLGMODALFRAME = 0x0001;
    const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    static extern nint GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    static extern nint SetWindowLongPtr(IntPtr hwnd, int index, nint value);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    /// <summary>Colore del bordo della finestra (Windows 11); null = colore normale.</summary>
    public static void SetBorder(Window window, System.Windows.Media.Color? color)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        int value = color is { } c ? Ref(c) : Border;
        try { DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref value, sizeof(int)); }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
