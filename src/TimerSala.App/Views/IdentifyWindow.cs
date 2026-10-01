using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TimerSala.App.Interop;

namespace TimerSala.App.Views;

/// <summary>Mostra per qualche secondo il numero di ogni schermo.</summary>
public static class IdentifyWindow
{
    public static void ShowAll()
    {
        foreach (var m in Monitors.GetAll())
        {
            var w = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Topmost = true,
                Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x0F, 0x11, 0x15)),
                AllowsTransparency = true,
                Content = new Viewbox
                {
                    Margin = new Thickness(80),
                    Child = new TextBlock
                    {
                        Text = m.Number.ToString(),
                        Foreground = Brushes.White,
                        FontFamily = (FontFamily)Application.Current.FindResource("DigitsFont"),
                        FontSize = 200,
                    },
                },
            };
            w.Show();
            Monitors.PlaceOn(w, m);
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            t.Tick += (_, _) => { t.Stop(); w.Close(); };
            t.Start();
        }
    }
}
