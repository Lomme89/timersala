using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace TimerSala.App.Views;

/// <summary>
/// Testo disegnato come forma e ritagliato sul contorno reale dei caratteri: riempie tutto lo spazio
/// disponibile senza il margine che i font riservano ad accenti e lettere discendenti.
/// </summary>
public sealed class GlyphText : FrameworkElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(GlyphText),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((GlyphText)d)._geometry = null));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(GlyphText),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.Register(nameof(FontFamily), typeof(FontFamily), typeof(GlyphText),
        new FrameworkPropertyMetadata(new FontFamily("Segoe UI"), FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((GlyphText)d)._geometry = null));

    /// <summary>Testo di riferimento per l'altezza: tutte le cifre hanno la stessa altezza del riferimento.</summary>
    const string HeightReference = "0123456789";

    Geometry? _geometry;
    Rect _referenceBounds;
    double _referenceWidth;

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    Geometry Build(string text)
    {
        var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 100, Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        return ft.BuildGeometry(new Point(0, 0));
    }

    protected override void OnRender(DrawingContext dc)
    {
        var text = Text ?? "";
        if (text.Length == 0 || ActualWidth <= 0 || ActualHeight <= 0) return;
        if (_geometry is null)
        {
            _geometry = Build(text);
            _referenceBounds = Build(HeightReference).Bounds; // altezza delle cifre, uguale per ogni valore
            // larghezza con tutte le cifre a "0": la grandezza non cambia quando cambiano le cifre
            var normalized = new string(text.Select(c => char.IsDigit(c) ? '0' : c).ToArray());
            _referenceWidth = Math.Max(Build(normalized).Bounds.Width, 1);
        }
        var b = _geometry.Bounds;
        if (b.IsEmpty || _referenceBounds.IsEmpty) return;

        // scala: l'altezza delle cifre riempie lo spazio, ma senza superare la larghezza disponibile
        double scale = Math.Min(ActualHeight / _referenceBounds.Height, ActualWidth / Math.Max(_referenceWidth, b.Width));
        double offsetY = (ActualHeight - _referenceBounds.Height * scale) / 2 - _referenceBounds.Top * scale;
        double offsetX = -b.Left * scale;

        dc.PushTransform(new TranslateTransform(offsetX, offsetY));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawGeometry(Fill, null, _geometry);
        dc.Pop();
        dc.Pop();
    }
}
