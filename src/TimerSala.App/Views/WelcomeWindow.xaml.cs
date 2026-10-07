using System.Windows;
using TimerSala.App.ViewModels;
using TimerSala.Core.Web;

namespace TimerSala.App.Views;

/// <summary>Primo avvio guidato: tre passi e la fine. «Salta» o la chiusura tengono quello che si è già scelto.</summary>
public partial class WelcomeWindow : Window
{
    readonly WelcomeViewModel _vm;
    bool _finished;

    public WelcomeWindow(MainViewModel main)
    {
        InitializeComponent();
        Interop.DarkTitleBar.HideIconWhenCreated(this);
        _vm = new WelcomeViewModel(main);
        DataContext = _vm;
        _main = main;
        _vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(WelcomeViewModel.Step)) UpdateButtons(); };
        UpdateButtons();
    }

    readonly MainViewModel _main;

    public WelcomeViewModel ViewModel => _vm;

    void UpdateButtons()
    {
        BackButton.Visibility = _vm.IsFirstStep ? Visibility.Hidden : Visibility.Visible;
        if (!_vm.IsPhoneStep) return;
        // il QR si prepara quando serve: all'apertura il timer in rete potrebbe non essere ancora partito
        if (_main.WebUrl is { } url && QrImage.Source is null) QrImage.Source = QrWindow.ToImage(QrCodes.Png(url));
        NoWebText.Visibility = _main.WebUrl is null ? Visibility.Visible : Visibility.Collapsed;
    }

    void Next_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.IsDoneStep) { _vm.Step++; return; }
        _finished = true;
        _vm.Finish(completed: true);
        Close();
    }

    void Back_Click(object sender, RoutedEventArgs e) => _vm.Step--;

    void Skip_Click(object sender, RoutedEventArgs e) => Close();

    void Identify_Click(object sender, RoutedEventArgs e) => IdentifyWindow.ShowAll();

    protected override void OnClosed(EventArgs e)
    {
        if (!_finished) _vm.Finish(completed: false);
        base.OnClosed(e);
    }
}
