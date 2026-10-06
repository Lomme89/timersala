using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

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
    /// Passa da una finestra all'altra (controller ↔ mini) con un'immagine che si restringe o si allarga
    /// dalla prima alla seconda. Senza animazioni, o la prima volta che la seconda si apre, il passaggio è immediato.
    /// </summary>
    public static void SwitchWindows(Window from, Window to, Action? done = null)
    {
        void Instant()
        {
            to.Show();
            if (to.WindowState == WindowState.Minimized) to.WindowState = WindowState.Normal;
            to.Activate();
            from.Hide();
            done?.Invoke();
        }

        bool known = new WindowInteropHelper(to).Handle != IntPtr.Zero && to.ActualWidth > 0 && to.ActualHeight > 0;
        if (Reduced || !from.IsVisible || from.WindowState != WindowState.Normal || !known)
        {
            Instant();
            return;
        }

        ImageSource? a, b;
        try
        {
            a = Snapshot(from);
            b = Snapshot(to);
        }
        catch
        {
            Instant();
            return;
        }
        if (a is null || b is null)
        {
            Instant();
            return;
        }

        var imgA = new Image { Source = a, Stretch = Stretch.Fill };
        var imgB = new Image { Source = b, Stretch = Stretch.Fill, Opacity = 0 };
        var ghost = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            ResizeMode = ResizeMode.NoResize,
            Left = from.Left,
            Top = from.Top,
            Width = from.ActualWidth,
            Height = from.ActualHeight,
            Content = new Border { CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = new Grid { Children = { imgA, imgB } } },
        };
        ghost.Show();
        from.Hide();

        var d = new Duration(TimeSpan.FromMilliseconds(260));
        var sb = new Storyboard();
        void Add(DependencyObject target, DependencyProperty prop, double fromValue, double toValue)
        {
            var anim = new DoubleAnimation(fromValue, toValue, d) { EasingFunction = Ease };
            Storyboard.SetTarget(anim, target);
            Storyboard.SetTargetProperty(anim, new PropertyPath(prop));
            sb.Children.Add(anim);
        }
        Add(ghost, Window.LeftProperty, from.Left, to.Left);
        Add(ghost, Window.TopProperty, from.Top, to.Top);
        Add(ghost, FrameworkElement.WidthProperty, from.ActualWidth, to.ActualWidth);
        Add(ghost, FrameworkElement.HeightProperty, from.ActualHeight, to.ActualHeight);
        Add(imgA, UIElement.OpacityProperty, 1, 0);
        Add(imgB, UIElement.OpacityProperty, 0, 1);
        sb.Completed += (_, _) =>
        {
            to.Show();
            to.Activate();
            ghost.Close();
            done?.Invoke();
        };
        sb.Begin();
    }

    static ImageSource? Snapshot(Window w)
    {
        if (w.Content is not FrameworkElement content || content.ActualWidth <= 0 || content.ActualHeight <= 0) return null;
        var dpi = VisualTreeHelper.GetDpi(content);
        var bmp = new RenderTargetBitmap((int)Math.Ceiling(w.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(w.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        // sfondo della finestra, poi il contenuto
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(w.Background, null, new Rect(0, 0, w.ActualWidth, w.ActualHeight));
            dc.DrawRectangle(new VisualBrush(content) { Stretch = Stretch.Fill }, null, new Rect(0, 0, w.ActualWidth, w.ActualHeight));
        }
        bmp.Render(dv);
        bmp.Freeze();
        return bmp;
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
