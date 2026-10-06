using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TimerSala.App.Views;

/// <summary>
/// Mostra il Markdown semplice di CHANGELOG.md e della guida: titoli (## e ###), elenchi a due livelli,
/// paragrafi, **grassetto**, *corsivo* e `codice`. Niente di più: basta per i testi del programma.
/// </summary>
public static partial class MarkdownView
{
    [GeneratedRegex(@"\*\*(.+?)\*\*|\*(.+?)\*|`(.+?)`")]
    private static partial Regex InlineRegex();

    public static void Render(Panel target, string markdown, bool firstHeadingBig = true)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var paragraph = new List<string>();
        bool first = target.Children.Count == 0;

        void FlushParagraph()
        {
            if (paragraph.Count == 0) return;
            target.Children.Add(Text(string.Join(" ", paragraph), 13.5, new Thickness(0, 0, 0, 8)));
            paragraph.Clear();
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) { FlushParagraph(); continue; }
            if (line.StartsWith("## "))
            {
                FlushParagraph();
                var h = Text(line[3..], firstHeadingBig ? 18 : 16, new Thickness(0, first ? 0 : 18, 0, 6));
                h.FontWeight = FontWeights.SemiBold;
                target.Children.Add(h);
            }
            else if (line.StartsWith("### "))
            {
                FlushParagraph();
                var h = Text(line[4..], 14, new Thickness(0, 10, 0, 4));
                h.FontWeight = FontWeights.SemiBold;
                target.Children.Add(h);
            }
            else if (line.TrimStart().StartsWith("- "))
            {
                FlushParagraph();
                int indent = line.Length - line.TrimStart().Length;
                target.Children.Add(Bullet(line.TrimStart()[2..], indent >= 2 ? 1 : 0));
            }
            else if (paragraph.Count == 0 && target.Children.Count > 0 && target.Children[^1] is FrameworkElement { Tag: "bullet" } prev
                     && raw.StartsWith("  "))
            {
                // continuazione di una voce dell'elenco
                if (prev is Grid g && g.Children[1] is TextBlock tb) AddInlines(tb, " " + line.Trim());
            }
            else
            {
                paragraph.Add(line.Trim());
            }
            first = false;
        }
        FlushParagraph();
    }

    static TextBlock Text(string text, double size, Thickness margin)
    {
        var tb = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = size, Margin = margin, LineHeight = size * 1.4 };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        AddInlines(tb, text);
        return tb;
    }

    static Grid Bullet(string text, int level)
    {
        var g = new Grid { Margin = new Thickness(level * 18, 0, 0, 5), Tag = "bullet" };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        var dot = new TextBlock { Text = level == 0 ? "•" : "–", FontSize = 13.5 };
        dot.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        var body = Text(text, 13.5, new Thickness(0));
        Grid.SetColumn(body, 1);
        g.Children.Add(dot);
        g.Children.Add(body);
        return g;
    }

    static void AddInlines(TextBlock tb, string text)
    {
        int at = 0;
        foreach (Match m in InlineRegex().Matches(text))
        {
            if (m.Index > at) tb.Inlines.Add(new Run(text[at..m.Index]));
            if (m.Groups[1].Success) tb.Inlines.Add(new Bold(new Run(m.Groups[1].Value)));
            else if (m.Groups[2].Success) tb.Inlines.Add(new Italic(new Run(m.Groups[2].Value)));
            else tb.Inlines.Add(new Run(m.Groups[3].Value) { FontFamily = new FontFamily("Consolas") });
            at = m.Index + m.Length;
        }
        if (at < text.Length) tb.Inlines.Add(new Run(text[at..]));
    }
}
