using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Media;
using TimerSala.App.ViewModels;

namespace TimerSala.App.Views;

public partial class EditorWindow : Window
{
    readonly EditorViewModel _vm;

    public EditorWindow(MainViewModel main)
    {
        InitializeComponent();
        WindowSizing.FitToScreen(this);
        WindowSizing.Remember(this, main, "editor");
        _vm = new EditorViewModel(main);
        DataContext = _vm;
        Closed += (_, _) => Mouse.OverrideCursor = null;
        PreviewKeyDown += OnUndoKeys;
    }

    // Ctrl+Z / Ctrl+Y (o Ctrl+Maiusc+Z) annullano e ripetono le modifiche allo schema, anche dentro i campi di testo
    void OnUndoKeys(object sender, KeyEventArgs e)
    {
        if (_dragItem is not null || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (e.Key == Key.Z && !shift) { CommitFocusedText(); _vm.Undo(); e.Handled = true; }
        else if (e.Key == Key.Y || (e.Key == Key.Z && shift)) { CommitFocusedText(); _vm.Redo(); e.Handled = true; }
    }

    static void CommitFocusedText()
    {
        if (Keyboard.FocusedElement is TextBox tb)
            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
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
        if (_dragItem is not null)
        {
            if (key == Key.Escape) EndDrag(commit: false);
            e.Handled = true;
            return;
        }
        if (key == Key.Delete) { _vm.RemoveCommand.Execute(null); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && key == Key.Up) { _vm.MoveUpCommand.Execute(null); e.Handled = true; }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && key == Key.Down) { _vm.MoveDownCommand.Execute(null); e.Handled = true; }
        if (e.Handled && _vm.Selected is { } s) PartsList.ScrollIntoView(s);
    }

    // ── trascinamento per riordinare ──
    // La riga trascinata "si solleva" e segue il mouse; le altre scorrono con un'animazione
    // per farle spazio, così si vede sempre dove andrà a finire. Esc annulla.

    static readonly IEasingFunction SlideEase = new CubicEase { EasingMode = EasingMode.EaseOut };

    EditablePart? _pressItem;
    Point _pressPos;
    EditablePart? _dragItem;
    DragGhost? _ghost;
    double _grabOffsetY;
    int _originalIndex;

    void List_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressItem = ItemAt(e.OriginalSource as DependencyObject);
        _pressPos = e.GetPosition(PartsList);
    }

    void List_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(PartsList);
        if (_dragItem is not null)
        {
            UpdateDrag(p);
            return;
        }
        if (e.LeftButton != MouseButtonState.Pressed || _pressItem is null) return;
        if (Math.Abs(p.Y - _pressPos.Y) < SystemParameters.MinimumVerticalDragDistance + 2) return;
        BeginDrag(_pressItem);
    }

    void List_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        _pressItem = null;
        if (_dragItem is not null)
        {
            EndDrag(commit: true);
            e.Handled = true;
        }
    }

    void List_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_dragItem is not null) EndDrag(commit: true);
    }

    void BeginDrag(EditablePart item)
    {
        if (Container(item) is not { } c) return;
        var origin = c.TranslatePoint(new Point(0, 0), PartsList);
        _grabOffsetY = _pressPos.Y - origin.Y;
        _originalIndex = _vm.Parts.IndexOf(item);
        _dragItem = item;
        _vm.Selected = item;
        PartsList.UpdateLayout();

        _ghost = new DragGhost(PartsList, Snapshot(c), origin.X, origin.Y, c.ActualWidth, c.ActualHeight);
        AdornerLayer.GetAdornerLayer(PartsList)?.Add(_ghost);
        _ghost.Lift();
        SetPlaceholder(item, true);
        PartsList.CaptureMouse();
        Mouse.OverrideCursor = Cursors.SizeNS;
    }

    void UpdateDrag(Point p)
    {
        if (_dragItem is null || _ghost is null) return;
        double y = Math.Clamp(p.Y - _grabOffsetY, -_ghost.GhostHeight / 2, PartsList.ActualHeight - _ghost.GhostHeight / 2);
        _ghost.MoveTo(y);
        AutoScroll(p.Y);

        // posizione d'arrivo: quante righe (esclusa quella trascinata) hanno il centro sopra il mouse
        int target = 0;
        foreach (var part in _vm.Parts)
        {
            if (part == _dragItem || Container(part) is not { } c) continue;
            double center = c.TranslatePoint(new Point(0, c.ActualHeight / 2), PartsList).Y;
            if (center < p.Y) target++;
        }
        int current = _vm.Parts.IndexOf(_dragItem);
        if (target != current) AnimatedMove(current, target);
    }

    void EndDrag(bool commit)
    {
        var item = _dragItem;
        var ghost = _ghost;
        _dragItem = null;
        _ghost = null;
        Mouse.OverrideCursor = null;
        if (PartsList.IsMouseCaptured) PartsList.ReleaseMouseCapture();
        if (item is null) return;

        if (!commit)
        {
            int current = _vm.Parts.IndexOf(item);
            if (current != _originalIndex) AnimatedMove(current, _originalIndex);
        }
        _vm.Selected = item;
        PartsList.UpdateLayout();

        void Finish()
        {
            if (ghost is not null) AdornerLayer.GetAdornerLayer(PartsList)?.Remove(ghost);
            SetPlaceholder(item, false);
        }

        if (ghost is not null && Container(item) is { } c)
            ghost.Settle(c.TranslatePoint(new Point(0, 0), PartsList).Y, Finish);
        else
            Finish();
    }

    /// <summary>Sposta la riga e fa scorrere le altre dalla vecchia alla nuova posizione.</summary>
    void AnimatedMove(int from, int to)
    {
        var before = _vm.Parts.ToDictionary(p => p, p => Container(p)?.TranslatePoint(new Point(0, 0), PartsList).Y);
        _vm.MoveItem(from, to);
        PartsList.UpdateLayout();
        foreach (var part in _vm.Parts)
        {
            if (Container(part) is not { } c) continue;
            if (part == _dragItem)
            {
                SetPlaceholder(part, true);
                continue;
            }
            if (before.GetValueOrDefault(part) is not { } oldY) continue;
            double delta = oldY - c.TranslatePoint(new Point(0, 0), PartsList).Y;
            if (Math.Abs(delta) < 0.5) continue;
            var shift = c.RenderTransform as TranslateTransform ?? new TranslateTransform();
            c.RenderTransform = shift;
            shift.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(delta, 0, new Duration(TimeSpan.FromMilliseconds(160))) { EasingFunction = SlideEase });
        }
    }

    /// <summary>La riga trascinata resta al suo posto come sagoma trasparente.</summary>
    void SetPlaceholder(EditablePart part, bool on)
    {
        if (Container(part) is not { } c) return;
        var d = new Duration(TimeSpan.FromMilliseconds(on ? 90 : 200));
        c.BeginAnimation(OpacityProperty, new DoubleAnimation(on ? 0.25 : 1, d));
    }

    void AutoScroll(double y)
    {
        if (FindScrollViewer(PartsList) is not { } sv) return;
        const double edge = 36;
        if (y < edge) sv.ScrollToVerticalOffset(sv.VerticalOffset - (edge - y) / 3);
        else if (y > PartsList.ActualHeight - edge) sv.ScrollToVerticalOffset(sv.VerticalOffset + (y - (PartsList.ActualHeight - edge)) / 3);
    }

    ListBoxItem? Container(EditablePart part) => PartsList.ItemContainerGenerator.ContainerFromItem(part) as ListBoxItem;

    static ImageSource Snapshot(FrameworkElement element)
    {
        var dpi = VisualTreeHelper.GetDpi(element);
        double w = element.ActualWidth, h = element.ActualHeight;
        var bmp = new RenderTargetBitmap((int)Math.Ceiling(w * dpi.DpiScaleX), (int)Math.Ceiling(h * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            dc.DrawRectangle(new VisualBrush(element), null, new Rect(0, 0, w, h));
        bmp.Render(dv);
        bmp.Freeze();
        return bmp;
    }

    static ScrollViewer? FindScrollViewer(DependencyObject d)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
        {
            var child = VisualTreeHelper.GetChild(d, i);
            if (child is ScrollViewer sv) return sv;
            if (FindScrollViewer(child) is { } found) return found;
        }
        return null;
    }

    static EditablePart? ItemAt(DependencyObject? d)
    {
        while (d is not null and not ListBoxItem)
            d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        return (d as ListBoxItem)?.DataContext as EditablePart;
    }
}
