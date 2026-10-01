using System.Windows;
using System.Windows.Controls;
using TimerSala.App.ViewModels;

namespace TimerSala.App.Views;

public partial class EditorWindow : Window
{
    readonly EditorViewModel _vm;

    public EditorWindow(MainViewModel main)
    {
        InitializeComponent();
        _vm = new EditorViewModel(main);
        DataContext = _vm;
        SectionColumn.ItemsSource = _vm.Sections;
    }

    void Grid_CellEditEnding(object? sender, DataGridCellEditEndingEventArgs e) =>
        Dispatcher.BeginInvoke(_vm.NotifyTotal);

    void Save_Click(object sender, RoutedEventArgs e)
    {
        Grid.CommitEdit(DataGridEditingUnit.Row, true);
        _vm.Save();
        DialogResult = true;
    }
}
