using System.IO;
using System.Windows;
using System.Windows.Controls;
using TimerSala.Core.Info;

namespace TimerSala.App.Views;

/// <summary>Finestra di testo: la guida («?») e le novità dopo un aggiornamento.</summary>
public partial class DocWindow : Window
{
    public DocWindow(string title, string subtitle)
    {
        InitializeComponent();
        Interop.DarkTitleBar.HideIconWhenCreated(this);
        Title = title;
        Heading.Text = title;
        Subheading.Text = subtitle;
        Subheading.Visibility = subtitle.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public DocWindow AddAction(string label, Action action)
    {
        var b = new Button { Content = label, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 5, 12, 5) };
        b.SetResourceReference(StyleProperty, "FlatButton");
        b.Click += (_, _) => action();
        Actions.Children.Add(b);
        return this;
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    static string Resource(string name)
    {
        using var s = typeof(DocWindow).Assembly.GetManifestResourceStream(name);
        return s is null ? "" : new StreamReader(s).ReadToEnd();
    }

    public static IReadOnlyList<ChangelogEntry> ChangelogEntries() => Changelog.Parse(Resource("CHANGELOG.md"));

    /// <summary>Guida rapida dentro il programma.</summary>
    public static DocWindow Help()
    {
        var w = new DocWindow("Guida rapida", "Le funzioni principali e i tasti. Per il resto passa il mouse sui pulsanti: c'è sempre una spiegazione.");
        MarkdownView.Render(w.Body, Resource("guida.md"));
        return w;
    }

    /// <summary>Le novità delle versioni indicate (dalla più recente).</summary>
    public static DocWindow News(IReadOnlyList<ChangelogEntry> entries, bool afterUpdate)
    {
        var current = entries.FirstOrDefault();
        var w = new DocWindow(afterUpdate ? "Cosa c'è di nuovo" : "Novità",
            afterUpdate && current is not null ? $"TimerSala è stato aggiornato alla {current.Version}." : "Le ultime versioni di TimerSala.");
        foreach (var e in entries)
            MarkdownView.Render(w.Body, $"## {e.Heading}\n\n{e.Body}\n", firstHeadingBig: true);
        return w;
    }
}
