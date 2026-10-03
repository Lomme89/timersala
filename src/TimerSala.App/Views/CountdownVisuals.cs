using System.Windows;
using System.Windows.Media;

namespace TimerSala.App.Views;

// Elementi grafici del conto alla rovescia prima dell'adunanza. Disegnati a mano: pochi elementi, aggiornati 10 volte al secondo.

/// <summary>Anello che si chiude: <see cref="Fraction"/> è la parte ancora da percorrere (1 = pieno, 0 = vuoto).</summary>
public sealed class RingArc : FrameworkElement
{
    public static readonly DependencyProperty FractionProperty = DependencyProperty.Register(nameof(Fraction), typeof(double), typeof(RingArc),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(RingArc),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(nameof(Track), typeof(Brush), typeof(RingArc),
        new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(nameof(Thickness), typeof(double), typeof(RingArc),
        new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Fraction { get => (double)GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    public Brush Track { get => (Brush)GetValue(TrackProperty); set => SetValue(TrackProperty, value); }
    public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;
        double r = (size - Thickness) / 2;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        dc.DrawEllipse(null, new Pen(Track, Thickness), c, r, r);

        double f = Math.Clamp(Fraction, 0, 1);
        if (f <= 0) return;
        var pen = new Pen(Stroke, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (f >= 0.9999) { dc.DrawEllipse(null, pen, c, r, r); return; }
        // dall'alto in senso orario
        double a0 = -Math.PI / 2, a1 = a0 + f * 2 * Math.PI;
        var start = new Point(c.X + r * Math.Cos(a0), c.Y + r * Math.Sin(a0));
        var end = new Point(c.X + r * Math.Cos(a1), c.Y + r * Math.Sin(a1));
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(end, new Size(r, r), 0, f > 0.5, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        dc.DrawGeometry(null, pen, g);
    }
}

/// <summary>Quadrante a 60 tacche: le prime <see cref="Lit"/> (in senso orario dall'alto) sono accese.</summary>
public sealed class TickDial : FrameworkElement
{
    public static readonly DependencyProperty LitProperty = DependencyProperty.Register(nameof(Lit), typeof(int), typeof(TickDial),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty OnBrushProperty = DependencyProperty.Register(nameof(OnBrush), typeof(Brush), typeof(TickDial),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty OffBrushProperty = DependencyProperty.Register(nameof(OffBrush), typeof(Brush), typeof(TickDial),
        new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public int Lit { get => (int)GetValue(LitProperty); set => SetValue(LitProperty, value); }
    public Brush OnBrush { get => (Brush)GetValue(OnBrushProperty); set => SetValue(OnBrushProperty, value); }
    public Brush OffBrush { get => (Brush)GetValue(OffBrushProperty); set => SetValue(OffBrushProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        double r = size / 2;
        var on = new Pen(OnBrush, size * 0.016) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var onMajor = new Pen(OnBrush, size * 0.024) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var off = new Pen(OffBrush, size * 0.016) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var offMajor = new Pen(OffBrush, size * 0.024) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        for (int i = 0; i < 60; i++)
        {
            double a = -Math.PI / 2 + i * Math.PI / 30;
            bool major = i % 5 == 0, lit = i < Lit;
            double r1 = r * (major ? 0.84 : 0.9), r2 = r * 0.98;
            dc.DrawLine(lit ? (major ? onMajor : on) : (major ? offMajor : off),
                new Point(c.X + r1 * Math.Cos(a), c.Y + r1 * Math.Sin(a)), new Point(c.X + r2 * Math.Cos(a), c.Y + r2 * Math.Sin(a)));
        }
    }
}

/// <summary>Fila di blocchi, uno per minuto: la parte accesa (<see cref="Fraction"/>) si consuma da destra.</summary>
public sealed class BlockBar : FrameworkElement
{
    public static readonly DependencyProperty CountProperty = DependencyProperty.Register(nameof(Count), typeof(int), typeof(BlockBar),
        new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FractionProperty = DependencyProperty.Register(nameof(Fraction), typeof(double), typeof(BlockBar),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty OnBrushProperty = DependencyProperty.Register(nameof(OnBrush), typeof(Brush), typeof(BlockBar),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty OffBrushProperty = DependencyProperty.Register(nameof(OffBrush), typeof(Brush), typeof(BlockBar),
        new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public int Count { get => (int)GetValue(CountProperty); set => SetValue(CountProperty, value); }
    public double Fraction { get => (double)GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public Brush OnBrush { get => (Brush)GetValue(OnBrushProperty); set => SetValue(OnBrushProperty, value); }
    public Brush OffBrush { get => (Brush)GetValue(OffBrushProperty); set => SetValue(OffBrushProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        int n = Math.Max(1, Count);
        double gap = ActualHeight * 0.6, w = (ActualWidth - gap * (n - 1)) / n, h = ActualHeight;
        if (w <= 0) return;
        double lit = Math.Clamp(Fraction, 0, 1) * n;
        for (int i = 0; i < n; i++)
        {
            double x = i * (w + gap);
            dc.DrawRectangle(OffBrush, null, new Rect(x, 0, w, h));
            double k = Math.Clamp(lit - i, 0, 1);
            if (k > 0) dc.DrawRectangle(OnBrush, null, new Rect(x, 0, w * k, h));
        }
    }
}

/// <summary>Riempimento dal basso con la superficie che ondeggia: <see cref="Level"/> 0 = vuoto, 1 = pieno.</summary>
public sealed class TideFill : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(nameof(Level), typeof(double), typeof(TideFill),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(nameof(Phase), typeof(double), typeof(TideFill),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(TideFill),
        new FrameworkPropertyMetadata(Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CrestProperty = DependencyProperty.Register(nameof(Crest), typeof(Brush), typeof(TideFill),
        new FrameworkPropertyMetadata(Brushes.LightSteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Level { get => (double)GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    public double Phase { get => (double)GetValue(PhaseProperty); set => SetValue(PhaseProperty, value); }
    public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public Brush Crest { get => (Brush)GetValue(CrestProperty); set => SetValue(CrestProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        double level = Math.Clamp(Level, 0, 1);
        if (level <= 0) return;
        double amp = h * 0.012, top = h * (1 - level);
        // due onde sovrapposte: la cresta chiara dietro, il corpo davanti
        dc.DrawGeometry(Crest, null, Wave(w, h, top - amp * 0.6, amp, Phase * 0.7 + 1.3));
        dc.DrawGeometry(Fill, null, Wave(w, h, top, amp, Phase));
    }

    static Geometry Wave(double w, double h, double top, double amp, double phase)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(0, h), true, true);
            const int steps = 48;
            for (int i = 0; i <= steps; i++)
            {
                double x = w * i / steps;
                ctx.LineTo(new Point(x, top + amp * Math.Sin(phase + x / w * Math.PI * 3)), true, false);
            }
            ctx.LineTo(new Point(w, h), true, false);
        }
        g.Freeze();
        return g;
    }
}
