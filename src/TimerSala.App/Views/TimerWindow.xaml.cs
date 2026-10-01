using System.Windows;
using TimerSala.App.Interop;
using TimerSala.App.ViewModels;

namespace TimerSala.App.Views;

/// <summary>Schermo del timer a tutto schermo, pensato per essere letto da lontano.</summary>
public partial class TimerWindow : Window
{
    public MonitorInfo? Monitor { get; set; }

    public TimerWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        SizeChanged += (_, _) => ApplyScale();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.ShowScreenHeader)) ApplyScale();
        };
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(PlaceOnMonitor);
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

        // in "Solo cifre" le cifre occupano tutto lo schermo
        bool full = DataContext is MainViewModel { ShowScreenHeader: false };
        HeaderRow.Height = new GridLength(full ? 0 : 17, GridUnitType.Star);
        FooterRow.Height = new GridLength(full ? 0 : 10, GridUnitType.Star);
        FooterLeft.FontSize = h * 0.045;
        FooterRight.FontSize = h * 0.05;
    }
}
