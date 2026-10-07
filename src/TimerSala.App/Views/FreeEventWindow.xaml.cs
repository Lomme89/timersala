using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TimerSala.App.ViewModels;

namespace TimerSala.App.Views;

public partial class FreeEventWindow : Window
{
    readonly MainViewModel _main;
    readonly FreeEventViewModel _vm;

    public FreeEventWindow(MainViewModel main)
    {
        InitializeComponent();
        Interop.DarkTitleBar.HideIconWhenCreated(this);
        _main = main;
        _vm = new FreeEventViewModel(main);
        DataContext = _vm;
        Loaded += (_, _) => TitleBox.Focus();
    }

    public FreeEventViewModel ViewModel => _vm;

    void Start_Click(object sender, RoutedEventArgs e)
    {
        // conferma il campo in modifica
        if (Keyboard.FocusedElement is TextBox tb) tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (_vm.Start is not { } start)
        {
            ThemedDialog.Show(this, "Indica l'ora d'inizio, per esempio 16:00.", "TimerSala", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (_main.StartFreeEvent(_vm.ToEvent(), start) is { } why)
        {
            ThemedDialog.Show(this, why, "TimerSala", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
