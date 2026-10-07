using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using TimerSala.App.ViewModels;
using TimerSala.Core.Web;

namespace TimerSala.App.Views;

/// <summary>Codici QR per aprire il timer (o il controllo remoto) da telefono o tablet.</summary>
public partial class QrWindow : Window
{
    readonly MainViewModel _vm;

    public QrWindow(MainViewModel vm)
    {
        InitializeComponent();
        Interop.DarkTitleBar.HideIconWhenCreated(this);
        _vm = vm;
        Refresh();
    }

    void Refresh()
    {
        ViewUrl.Text = _vm.WebUrl;
        ViewQr.Source = ToImage(QrCodes.Png(_vm.WebUrl!));
        if (_vm.ControlUrl is { } url)
        {
            ControlQr.Source = ToImage(QrCodes.Png(url));
            PinRun.Text = _vm.Settings.RemotePin;
        }
        else
        {
            ControlCard.Visibility = Visibility.Collapsed;
            ControlOff.Visibility = Visibility.Visible;
        }
    }

    void ShowControl_Click(object sender, RoutedEventArgs e)
    {
        ControlHidden.Visibility = Visibility.Collapsed;
        ControlShown.Visibility = Visibility.Visible;
    }

    void HideControl_Click(object sender, RoutedEventArgs e)
    {
        ControlShown.Visibility = Visibility.Collapsed;
        ControlHidden.Visibility = Visibility.Visible;
    }

    void NewPin_Click(object sender, RoutedEventArgs e)
    {
        _vm.RegeneratePin();
        Refresh();
    }

    internal static BitmapImage ToImage(byte[] png)
    {
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = new MemoryStream(png);
        img.EndInit();
        img.Freeze();
        return img;
    }
}
