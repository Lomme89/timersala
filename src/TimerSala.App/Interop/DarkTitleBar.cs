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

    // COLORREF = 0x00BBGGRR
    const int DarkCaption = 0x15110F;   // #0F1115
    const int DarkText = 0xF3F0EE;      // #EEF0F3
    const int DarkBorder = 0x382F2A;    // #2A2F38
    const int LightCaption = 0xF6F4F3;  // #F3F4F6
    const int LightText = 0x271811;     // #111827
    const int LightBorder = 0xE1DAD5;   // #D5DAE1

    /// <summary>Tema chiaro del controller: barra del titolo chiara.</summary>
    public static bool Light { get; set; }

    static int Border => Light ? LightBorder : DarkBorder;

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
            int caption = Light ? LightCaption : DarkCaption, text = Light ? LightText : DarkText, border = Border;
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

    /// <summary>Colore del bordo della finestra (Windows 11); null = colore normale.</summary>
    public static void SetBorder(Window window, System.Windows.Media.Color? color)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        int value = color is { } c ? c.R | (c.G << 8) | (c.B << 16) : Border;
        try { DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref value, sizeof(int)); }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
