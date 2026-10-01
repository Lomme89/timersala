using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TimerSala.App.ViewModels;

namespace TimerSala.App.Views;

public partial class EditorWindow : Window
{
    readonly EditorViewModel _vm;
    Point _dragStart;
    EditablePart? _dragItem;

    public EditorWindow(MainViewModel main)
    {
        InitializeComponent();
        _vm = new EditorViewModel(main);
        DataContext = _vm;
    }

    void Save_Click(object sender, RoutedEventArgs e)
    {
        // conferma il valore del campo in modifica (es. minuti scritti a mano)
        if (Keyboard.FocusedElement is TextBox tb)
            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        _vm.Save();
        DialogResult = true;
    }

    // ── tastiera ──

    void List_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Delete) { _vm.RemoveCommand.Execute(null); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && key == Key.Up) { _vm.MoveUpCommand.Execute(null); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && key == Key.Down) { _vm.MoveDownCommand.Execute(null); e.Handled = true; }
        if (e.Handled && _vm.Selected is { } s) PartsList.ScrollIntoView(s);
    }

    // ── trascinamento per riordinare ──

    void List_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(PartsList);
        _dragItem = ItemAt(e.OriginalSource as DependencyObject);
    }

    void List_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem is null) return;
        var delta = e.GetPosition(PartsList) - _dragStart;
        if (Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var item = _dragItem;
        _dragItem = null;
        DragDrop.DoDragDrop(PartsList, item, DragDropEffects.Move);
    }

    void List_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(EditablePart)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    void List_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(EditablePart)) is not EditablePart dragged) return;
        int from = _vm.Parts.IndexOf(dragged);
        var target = ItemAt(e.OriginalSource as DependencyObject);
        int to = target is null ? _vm.Parts.Count - 1 : _vm.Parts.IndexOf(target);
        _vm.MoveItem(from, to);
    }

    static EditablePart? ItemAt(DependencyObject? d)
    {
        while (d is not null and not ListBoxItem)
            d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        return (d as ListBoxItem)?.DataContext as EditablePart;
    }
}
