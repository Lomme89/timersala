using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace TimerSala.App.Views;

/// <summary>
/// Numero con − e +: si può anche scrivere, ma un valore fuori dai limiti viene corretto
/// e uno non valido torna quello di prima. Frecce su/giù e rotellina cambiano di un passo.
/// </summary>
public sealed class NumberBox : UserControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(int), typeof(NumberBox),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((NumberBox)d).ShowValue(),
            (d, v) => ((NumberBox)d).Clamp((int)v)));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(int), typeof(NumberBox), new PropertyMetadata(0));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(int), typeof(NumberBox), new PropertyMetadata(999));
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(int), typeof(NumberBox), new PropertyMetadata(1));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(NumberBox),
        new PropertyMetadata("", (d, _) => ((NumberBox)d).ShowValue()));
    /// <summary>Testo al posto dello zero (per esempio «mai»).</summary>
    public static readonly DependencyProperty ZeroTextProperty = DependencyProperty.Register(nameof(ZeroText), typeof(string), typeof(NumberBox),
        new PropertyMetadata(null, (d, _) => ((NumberBox)d).ShowValue()));

    public int Value { get => (int)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int Minimum { get => (int)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public int Maximum { get => (int)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public int Step { get => (int)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public string? ZeroText { get => (string?)GetValue(ZeroTextProperty); set => SetValue(ZeroTextProperty, value); }

    readonly TextBox _text;

    public NumberBox()
    {
        _text = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            FontWeight = FontWeights.SemiBold,
            MinWidth = 64,
            Padding = new Thickness(2, 5, 2, 5),
        };
        _text.GotKeyboardFocus += (_, _) => { _text.Text = Value.ToString(); _text.SelectAll(); };
        _text.LostKeyboardFocus += (_, _) => Commit();
        _text.PreviewKeyDown += OnKey;
        _text.PreviewMouseWheel += (_, e) =>
        {
            Value = Clamp(Value + Math.Sign(e.Delta) * Step);
            e.Handled = true;
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var minus = StepButton("−", -1);
        var plus = StepButton("+", +1);
        Grid.SetColumn(_text, 1);
        Grid.SetColumn(plus, 2);
        grid.Children.Add(minus);
        grid.Children.Add(_text);
        grid.Children.Add(plus);

        var border = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Child = grid };
        border.SetResourceReference(Border.BackgroundProperty, "InputBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
        Content = border;
        HorizontalAlignment = HorizontalAlignment.Right;
        ShowValue();
    }

    Button StepButton(string text, int direction)
    {
        var b = new Button
        {
            Content = text,
            Width = 34,
            FontSize = 16,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Focusable = false,
            ToolTip = direction > 0 ? "Aumenta" : "Diminuisci",
        };
        b.SetResourceReference(StyleProperty, "FlatButton");
        b.SetResourceReference(ForegroundProperty, "MutedBrush");
        b.Click += (_, _) =>
        {
            Commit();
            Value = Clamp(Value + direction * Step);
        };
        return b;
    }

    void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up: Commit(); Value = Clamp(Value + Step); _text.Text = Value.ToString(); _text.SelectAll(); e.Handled = true; break;
            case Key.Down: Commit(); Value = Clamp(Value - Step); _text.Text = Value.ToString(); _text.SelectAll(); e.Handled = true; break;
            case Key.Enter: Commit(); _text.Text = Value.ToString(); _text.SelectAll(); e.Handled = true; break;
            case Key.Escape: _text.Text = Value.ToString(); _text.SelectAll(); break;
        }
    }

    void Commit()
    {
        if (!_text.IsKeyboardFocused) return;
        var digits = new string(_text.Text.Where(c => char.IsDigit(c) || c == '-').ToArray());
        if (int.TryParse(digits, out var v)) Value = Clamp(v);
    }

    int Clamp(int v) => Math.Clamp(v, Minimum, Math.Max(Minimum, Maximum));

    void ShowValue()
    {
        if (_text is null || _text.IsKeyboardFocused) return;
        _text.Text = Value == 0 && ZeroText is { } zero ? zero
            : string.IsNullOrEmpty(Unit) ? Value.ToString() : $"{Value} {Unit}";
    }
}

/// <summary>
/// Orario «19:00»: si scrive (anche 19, 19.00 o 1900) e si regola a passi di 5 minuti con frecce e rotellina.
/// Un testo non valido torna l'orario di prima; con <see cref="AllowEmpty"/> si può lasciare vuoto.
/// </summary>
public sealed class TimeBox : UserControl
{
    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(nameof(Time), typeof(TimeOnly?), typeof(TimeBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((TimeBox)d).ShowValue()));

    public static readonly DependencyProperty AllowEmptyProperty = DependencyProperty.Register(nameof(AllowEmpty), typeof(bool), typeof(TimeBox), new PropertyMetadata(false));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(TimeBox),
        new PropertyMetadata("", (d, _) => ((TimeBox)d).ShowValue()));

    public TimeOnly? Time { get => (TimeOnly?)GetValue(TimeProperty); set => SetValue(TimeProperty, value); }
    public bool AllowEmpty { get => (bool)GetValue(AllowEmptyProperty); set => SetValue(AllowEmptyProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    readonly TextBox _text;
    readonly TextBlock _placeholder;

    public TimeBox()
    {
        _text = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(2, 5, 6, 5),
            MinWidth = 56,
        };
        _text.LostKeyboardFocus += (_, _) => Commit();
        _text.GotKeyboardFocus += (_, _) => _text.SelectAll();
        _text.TextChanged += (_, _) => _placeholder!.Visibility = _text.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _text.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Up) { Commit(); Shift(+5); e.Handled = true; }
            else if (e.Key == Key.Down) { Commit(); Shift(-5); e.Handled = true; }
            else if (e.Key == Key.Enter) { Commit(); _text.SelectAll(); e.Handled = true; }
        };
        _text.PreviewMouseWheel += (_, e) => { Shift(Math.Sign(e.Delta) * 5); e.Handled = true; };

        var icon = new TextBlock { Text = "", FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 4, 0) };
        icon.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
        icon.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        _placeholder = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 6, 0), IsHitTestVisible = false, FontSize = 13 };
        _placeholder.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_text, 1);
        Grid.SetColumn(_placeholder, 1);
        grid.Children.Add(icon);
        grid.Children.Add(_text);
        grid.Children.Add(_placeholder);
        var border = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Child = grid };
        border.SetResourceReference(Border.BackgroundProperty, "InputBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
        Content = border;
        ToolTip = "Ora di inizio: scrivila (per esempio 19:00) o regolala con le frecce";
        ShowValue();
    }

    void Shift(int minutes)
    {
        var t = Time ?? new TimeOnly(19, 0);
        // al multiplo di 5 minuti successivo o precedente
        int now = t.Hour * 60 + t.Minute;
        int total = minutes > 0 ? (now / 5 + 1) * 5 : (now + 4) / 5 * 5 - 5;
        total = ((total % 1440) + 1440) % 1440;
        Time = new TimeOnly(total / 60, total % 60);
        if (_text.IsKeyboardFocused) _text.SelectAll();
    }

    void Commit()
    {
        var parsed = Parse(_text.Text);
        if (parsed is not null) Time = parsed;
        else if (AllowEmpty && string.IsNullOrWhiteSpace(_text.Text)) Time = null;
        ShowValue(force: true);
    }

    public static TimeOnly? Parse(string text)
    {
        var t = text.Trim().Replace('.', ':').Replace(',', ':');
        if (t.Length is 3 or 4 && t.All(char.IsDigit)) t = t[..^2] + ":" + t[^2..];
        if (t.Length is 1 or 2 && t.All(char.IsDigit)) t += ":00";
        return TimeOnly.TryParseExact(t, ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;
    }

    void ShowValue(bool force = false)
    {
        if (_text is null || (!force && _text.IsKeyboardFocused)) return;
        _text.Text = Time?.ToString("HH:mm") ?? "";
        _placeholder.Text = Placeholder;
        _placeholder.Visibility = _text.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}

/// <summary>true se il valore è diverso da null (per mostrare o nascondere un elemento).</summary>
public sealed class NotNullToVisibility : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null or "" ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
