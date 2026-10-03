using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TimerSala.App.Interop;
using TimerSala.App.ViewModels;

namespace TimerSala.App.Views;

/// <summary>Schermo del timer a tutto schermo, pensato per essere letto da lontano.</summary>
public partial class TimerWindow : Window
{
    public MonitorInfo? Monitor { get; set; }

    static readonly Duration ColorFade = new(TimeSpan.FromMilliseconds(450));
    static readonly IEasingFunction Ease = new CubicEase { EasingMode = EasingMode.EaseOut };

    readonly MainViewModel _vm;
    readonly SolidColorBrush _digits = new(Colors.White);
    readonly SolidColorBrush _background = new(Colors.Black);

    public TimerWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        DigitsText.Foreground = _digits;
        Background = _background;
        SetColor(_digits, vm.ScreenDigitsBrush, animate: false);
        SetColor(_background, vm.DisplayBackground, animate: false);
        SizeChanged += (_, _) => ApplyScale();
        vm.PropertyChanged += OnViewModelChanged;
        Closed += (_, _) => vm.PropertyChanged -= OnViewModelChanged;
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(PlaceOnMonitor);
    }

    void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.ShowScreenHeader):
                ApplyScale();
                break;
            case nameof(MainViewModel.ScreenDigitsBrush):
                SetColor(_digits, _vm.ScreenDigitsBrush, animate: true);
                break;
            case nameof(MainViewModel.DisplayBackground):
                SetColor(_background, _vm.DisplayBackground, animate: true);
                break;
            case nameof(MainViewModel.IsIdle):
                // passaggio morbido tra orologio e timer
                DigitsBox.BeginAnimation(OpacityProperty, new DoubleAnimation(0.15, 1, new Duration(TimeSpan.FromMilliseconds(350))) { EasingFunction = Ease });
                break;
            case nameof(MainViewModel.ScreenMessageFull) when _vm.ScreenMessageFull:
            {
                // entra con un piccolo «colpo»: si allarga appena e si assesta
                var f = new Duration(TimeSpan.FromMilliseconds(380));
                var pop = new DoubleAnimation(1.06, 1, f) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 } };
                MsgFullScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
                MsgFullScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
                MsgFull.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200))));
                break;
            }
            case nameof(MainViewModel.ShowMessageBand) when _vm.ShowMessageBand:
                // il messaggio entra scorrendo dal basso
                var d = new Duration(TimeSpan.FromMilliseconds(320));
                MsgShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(ActualHeight * 0.15, 0, d) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 } });
                MsgBanner.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, d));
                break;
        }
    }

    static void SetColor(SolidColorBrush target, Brush source, bool animate)
    {
        if (source is not SolidColorBrush s) return;
        if (!animate)
        {
            target.BeginAnimation(SolidColorBrush.ColorProperty, null);
            target.Color = s.Color;
            return;
        }
        target.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(s.Color, ColorFade) { EasingFunction = Ease });
    }

    public void PlaceOnMonitor()
    {
        if (Monitor is null) return;
        WindowState = WindowState.Normal;
        Monitors.PlaceOn(this, Monitor);
    }

    /// <summary>Dimensiona i testi in proporzione all'altezza dello schermo.</summary>
    void ApplyScale()
    {
        double h = ActualHeight, w = ActualWidth;
        if (h <= 0 || w <= 0) return;
        double margin = Math.Min(h, w) * 0.04;
        Root.Margin = new Thickness(margin * 1.2, margin * 0.6, margin * 1.2, margin * 0.4);

        TitleTb.FontSize = h * 0.07;
        TitleTb.MaxHeight = TitleTb.FontSize * 1.35 * 2;
        SectionTb.FontSize = h * 0.032;
        Chip.Width = Math.Max(6, h * 0.012);
        Bar.Height = Track.Height = Math.Max(8, h * 0.025);

        MsgText.FontSize = h * 0.08;
        MsgText.MaxHeight = MsgText.FontSize * 1.3 * 2;
        MsgBanner.Padding = new Thickness(h * 0.03, h * 0.02, h * 0.03, h * 0.02);
        MsgBanner.Margin = new Thickness(margin * 1.2, 0, margin * 1.2, margin * 0.6);

        // in "Solo cifre" le cifre occupano tutto lo schermo
        bool full = DataContext is MainViewModel { ShowScreenHeader: false };
        HeaderRow.Height = new GridLength(full ? 0 : 17, GridUnitType.Star);
        FooterRow.Height = new GridLength(full ? 0 : 10, GridUnitType.Star);
        FooterLeft.FontSize = h * 0.045;
        FooterRight.FontSize = h * 0.05;
    }
}
