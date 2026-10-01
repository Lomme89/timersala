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
    const int Caption = 0x15110F;   // #0F1115
    const int Text = 0xF3F0EE;      // #EEF0F3
    const int Border = 0x382F2A;    // #2A2F38

    public static void Apply(Window window, bool roundCorners = false)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        try
        {
            int on = 1;
            if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref on, sizeof(int));

            // solo Windows 11: colori personalizzati (su Windows 10 restano quelli scuri di sistema)
            int caption = Caption, text = Text, border = Border;
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

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
