using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace TimerSala.App.Views;

/// <summary>Copia "sollevata" della riga trascinata che segue il mouse sopra l'elenco.</summary>
sealed class DragGhost : Adorner
{
    static readonly IEasingFunction Ease = new CubicEase { EasingMode = EasingMode.EaseOut };

    readonly Border _visual;
    readonly TranslateTransform _pos = new();
    readonly ScaleTransform _scale = new(1, 1);

    public double GhostHeight { get; }

    public DragGhost(UIElement adorned, ImageSource snapshot, double x, double y, double width, double height) : base(adorned)
    {
        IsHitTestVisible = false;
        GhostHeight = height;
        _visual = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(8),
            Background = (Brush)Application.Current.FindResource("CardHoverBrush"),
            BorderBrush = (Brush)Application.Current.FindResource("AccentBrush"),
            BorderThickness = new Thickness(1),
            Child = new Image { Source = snapshot, Stretch = Stretch.Fill },
            Effect = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 6, Direction = 270, Opacity = 0.55, Color = Colors.Black },
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
        var group = new TransformGroup();
        group.Children.Add(_scale);
        group.Children.Add(_pos);
        _visual.RenderTransform = group;
        _pos.X = x;
        _pos.Y = y;
        AddVisualChild(_visual);
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    protected override Size MeasureOverride(Size constraint)
    {
        _visual.Measure(constraint);
        return base.MeasureOverride(constraint);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _visual.Arrange(new Rect(0, 0, _visual.Width, _visual.Height));
        return finalSize;
    }

    /// <summary>Segue il mouse (senza animazione, per essere reattivo).</summary>
    public void MoveTo(double y)
    {
        _pos.BeginAnimation(TranslateTransform.YProperty, null);
        _pos.Y = y;
    }

    /// <summary>Effetto "presa": la riga si solleva leggermente.</summary>
    public void Lift()
    {
        var d = new Duration(TimeSpan.FromMilliseconds(140));
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 1.03, d) { EasingFunction = Ease });
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1.06, d) { EasingFunction = Ease });
    }

    /// <summary>Effetto "rilascio": scivola nella posizione finale e si appoggia.</summary>
    public void Settle(double targetY, Action done)
    {
        var d = new Duration(TimeSpan.FromMilliseconds(170));
        var move = new DoubleAnimation(_pos.Y, targetY, d) { EasingFunction = Ease };
        move.Completed += (_, _) => done();
        _pos.BeginAnimation(TranslateTransform.YProperty, move);
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, d) { EasingFunction = Ease });
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, d) { EasingFunction = Ease });
    }
}
