using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TimerSala.App.Theming;

/// <summary>
/// Movimento dell'interfaccia: solo dove aiuta a capire, mai continuo, e tutto spento se in Windows
/// gli effetti di animazione sono disattivati («riduci movimento»).
/// </summary>
public static class Motion
{
    static readonly IEasingFunction Ease = new CubicEase { EasingMode = EasingMode.EaseOut };

    /// <summary>Windows chiede di ridurre le animazioni (Impostazioni → Accessibilità → Effetti visivi).</summary>
    public static bool Reduced => !SystemParameters.ClientAreaAnimation;

    /// <summary>Un solo leggero «respiro» (si allarga e torna) per segnalare un cambio, per esempio verde → giallo.</summary>
    public static void Breathe(FrameworkElement element, double scale = 1.05)
    {
        if (Reduced) return;
        if (element.RenderTransform is not ScaleTransform st || st.IsFrozen)
        {
            st = new ScaleTransform(1, 1);
            element.RenderTransform = st;
            element.RenderTransformOrigin = new Point(0.5, 0.5);
        }
        var a = new DoubleAnimation(1, scale, new Duration(TimeSpan.FromMilliseconds(260)))
        {
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    /// <summary>Il pulsante Avvia/Ferma cambia colore sfumando e l'icona entra con un piccolo scatto.</summary>
    public static void FadeBackground(Control control, Color to)
    {
        if (control.Background is not SolidColorBrush { IsFrozen: false } brush)
        {
            var from = (control.Background as SolidColorBrush)?.Color ?? to;
            brush = new SolidColorBrush(from);
            control.Background = brush;
        }
        if (Reduced)
        {
            brush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            brush.Color = to;
            return;
        }
        brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, new Duration(TimeSpan.FromMilliseconds(220))) { EasingFunction = Ease });
    }

    public static void Pop(FrameworkElement element)
    {
        if (Reduced) return;
        var st = new ScaleTransform(0.6, 0.6);
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = st;
        var d = new Duration(TimeSpan.FromMilliseconds(200));
        var grow = new DoubleAnimation(0.6, 1, d) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 } };
        st.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, d));
    }

    /// <summary>
    /// Passa da una finestra all'altra (controller ↔ mini): la seconda compare prima che la prima sparisca,
    /// così non c'è mai un istante senza finestre; l'apertura e la chiusura le anima Windows.
    /// </summary>
    // ponytail: prima c'era un'immagine che si deformava da una finestra all'altra (contenuto senza barra del titolo
    // stirato, finestra trasparente ridimensionata a ogni fotogramma, istantanea vecchia della mini): sembrava strana
    public static void SwitchWindows(Window from, Window to)
    {
        to.Show();
        if (to.WindowState == WindowState.Minimized) to.WindowState = WindowState.Normal;
        to.Activate();
        from.Hide();
    }

    /// <summary>La finestra parte già nera (o del colore di sfondo): niente lampo bianco prima del primo disegno.</summary>
    public static void PaintBackgroundEarly(Window w, Color color)
    {
        w.SourceInitialized += (_, _) =>
        {
            try
            {
                var hwnd = new WindowInteropHelper(w).Handle;
                var brush = CreateSolidBrush(color.R | (color.G << 8) | (color.B << 16));
                if (brush != IntPtr.Zero) SetClassLongPtr(hwnd, GCLP_HBRBACKGROUND, brush);
            }
            catch { /* solo estetica */ }
        };
    }

    const int GCLP_HBRBACKGROUND = -10;

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateSolidBrush(int color);

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")]
    static extern IntPtr SetClassLongPtr(IntPtr hwnd, int index, IntPtr value);
}
