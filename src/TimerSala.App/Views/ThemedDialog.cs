using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TimerSala.App.Views;

/// <summary>
/// Al posto di <see cref="MessageBox"/>: stessa firma, ma nei colori del tema e con la barra del titolo del programma.
/// Per una domanda «pericolosa» (avviso con Sì/No) il pulsante di conferma è rosso; le etichette si possono cambiare.
/// </summary>
static class ThemedDialog
{
    public static MessageBoxResult Show(Window? owner, string text, string caption,
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None,
        string? yes = null, string? no = null)
    {
        var result = buttons is MessageBoxButton.YesNo ? MessageBoxResult.No : MessageBoxResult.OK;
        owner ??= Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var win = new Window
        {
            Title = caption,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            WindowStartupLocation = owner is { IsVisible: true } ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            Owner = owner is { IsVisible: true } ? owner : null,
            FontFamily = (FontFamily)Application.Current.FindResource("UiFont"),
        };
        win.SetResourceReference(Control.BackgroundProperty, "BgBrush");
        win.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        Interop.DarkTitleBar.HideIconWhenCreated(win);

        // icona a sinistra, testo, pulsanti in basso a destra
        var (glyph, color) = image switch
        {
            MessageBoxImage.Warning => ("", "AmberBrush"),
            MessageBoxImage.Error => ("", "RedBrush"),
            MessageBoxImage.Question => ("", "AccentBrush"),
            MessageBoxImage.Information => ("", "AccentBrush"),
            _ => (null, null),
        };
        var body = new DockPanel { Margin = new Thickness(0, 0, 0, 22) };
        if (glyph is not null)
        {
            var icon = new TextBlock { Text = glyph, FontSize = 22, Margin = new Thickness(0, 1, 14, 0), VerticalAlignment = VerticalAlignment.Top,
                FontFamily = (FontFamily)Application.Current.FindResource("IconFont") };
            icon.SetResourceReference(TextBlock.ForegroundProperty, color);
            DockPanel.SetDock(icon, Dock.Left);
            body.Children.Add(icon);
        }
        body.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 420, MinWidth = 260, FontSize = 14.5, LineHeight = 21 });

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Button Make(string label, MessageBoxResult value, bool primary)
        {
            var b = new Button { Content = label, MinWidth = 96, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(16, 7, 16, 7),
                Style = (Style)Application.Current.FindResource("FlatButton") };
            if (primary)
            {
                b.SetResourceReference(Control.BackgroundProperty, image == MessageBoxImage.Warning && buttons == MessageBoxButton.YesNo ? "StopBrush" : "AccentBrush");
                b.BorderThickness = new Thickness(0);
                b.Foreground = Brushes.White;
                b.FontWeight = FontWeights.SemiBold;
            }
            b.Click += (_, _) => { result = value; win.Close(); };
            row.Children.Add(b);
            return b;
        }
        if (buttons == MessageBoxButton.YesNo)
        {
            Make(no ?? "No", MessageBoxResult.No, primary: false).IsCancel = true;
            Make(yes ?? "Sì", MessageBoxResult.Yes, primary: true).IsDefault = true;
        }
        else
        {
            var ok = Make("OK", MessageBoxResult.OK, primary: true);
            ok.IsDefault = ok.IsCancel = true;
        }

        win.Content = new StackPanel { Margin = new Thickness(22, 20, 22, 18), Children = { body, row } };
        win.Loaded += (_, _) => (row.Children[^1] as UIElement)?.Focus();
        win.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) win.Close(); };
        win.ShowDialog();
        return result;
    }

    public static MessageBoxResult Show(string text, string caption, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
        => Show(null, text, caption, buttons, image);
}
