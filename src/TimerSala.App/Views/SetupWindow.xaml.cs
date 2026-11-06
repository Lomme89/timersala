using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using TimerSala.App.Interop;
using TimerSala.Core.Info;

namespace TimerSala.App.Views;

/// <summary>
/// Installer: compare aprendo il file scaricato. Una schermata, un pulsante; cartella e collegamenti in «Opzioni».
/// Riconosce la copia installata e propone l'aggiornamento con le novità, oppure la disinstallazione (da «App installate»).
/// </summary>
public partial class SetupWindow : Window
{
    readonly SetupKind? _kind;           // null = disinstallazione
    readonly string _version;
    readonly Setup.InstalledCopy? _installed;
    string? _launch;                     // il programma da aprire con «Apri TimerSala»
    readonly string? _here;              // questo file è già la copia in uso: si registra dov'è

    /// <param name="kind">Cosa proporre; null per disinstallare.</param>
    /// <param name="folder">Dove installare, se diverso dal solito (la cartella di una copia messa a mano).</param>
    internal SetupWindow(SetupKind? kind, string version, Setup.InstalledCopy? installed, bool othersRunning, string? folder = null)
    {
        InitializeComponent();
        _kind = kind;
        _version = version.Length > 0 ? version : "sviluppo";
        _installed = installed;
        HeroVersion.Text = "v" + _version;
        FolderBox.Text = folder ?? installed?.Folder ?? Setup.DefaultFolder;
        _here = folder;
        // la cartella si sceglie alla prima installazione; dopo si aggiorna dov'è
        FolderBox.IsEnabled = BrowseButton.IsEnabled = kind == SetupKind.Install;
        DesktopBox.IsChecked = installed is null || File.Exists(Shortcut.DesktopLink);
        RunningNote.Visibility = othersRunning && kind is not null ? Visibility.Visible : Visibility.Collapsed;
        if (RunningNote.Visibility == Visibility.Visible) NewsScroll.MaxHeight = 104; // lo spazio per l'avviso
        ShowChoice();
    }

    void ShowChoice()
    {
        var installed = _installed?.Version is { Length: > 0 } v ? v : "?";
        (string title, string lead, string primary, string secondary) = _kind switch
        {
            SetupKind.Install => ("Installa TimerSala",
                "Lo installo su questo PC e lo trovi nel menu Start.", "Installa", "Apri senza installare"),
            SetupKind.Upgrade => ("Aggiorna TimerSala",
                "C'è una versione nuova. Impostazioni, schemi e congregazioni restano come sono.", "Aggiorna", "Apri senza aggiornare"),
            SetupKind.Reinstall => ("TimerSala è già installato",
                $"Questa versione ({_version}) è già installata. Puoi aprirla, oppure reinstallarla se qualcosa non va.", "Apri TimerSala", "Reinstalla"),
            SetupKind.Downgrade => ("C'è una versione più recente",
                $"È installata la {installed}, più nuova di questo file ({_version}). Puoi comunque tornare a questa versione senza perdere impostazioni e schemi.",
                "Apri la versione installata", "Installa comunque questa"),
            _ => ("Disinstalla TimerSala",
                "Tolgo il programma, i collegamenti e la voce in «App installate». Impostazioni e schemi restano sul PC, e reinstallando ritrovi tutto.",
                "Disinstalla", "Annulla"),
        };
        // copie messe a mano (per esempio in C:\TimerSala): si aggiornano o registrano lì, senza lasciarne una vecchia in giro
        if (_kind == SetupKind.Install && _here is not null)
            lead = $"TimerSala è in «{_here}». Lo lascio lì e lo aggiungo al menu Start e ad «App installate».";
        if (_kind == SetupKind.Upgrade && _installed is { Manual: true } m)
            lead = $"C'è una copia di TimerSala in «{m.Folder}». La aggiorno lì e da ora la trovi anche in «App installate». Impostazioni e schemi restano come sono.";
        ChooseTitle.Text = title;
        ChooseLead.Text = lead;
        PrimaryButton.Content = primary;
        SecondaryButton.Content = secondary;

        Points.Visibility = _kind == SetupKind.Install ? Visibility.Visible : Visibility.Collapsed;
        OptionsToggle.Visibility = _kind is SetupKind.Install or SetupKind.Upgrade ? Visibility.Visible : Visibility.Collapsed;
        if (_kind == SetupKind.Upgrade)
        {
            VersionsRow.Visibility = Visibility.Visible;
            FromVersion.Text = installed;
            ToVersion.Text = _version;
            ShowNews(_installed?.Version);
        }
        if (_kind is null) PrimaryButton.Background = (System.Windows.Media.Brush)FindResource("StopBrush");
    }

    // le novità tra la versione installata e questa, come nella finestra «Novità»
    void ShowNews(string? from)
    {
        var entries = Changelog.Since(DocWindow.ChangelogEntries(), from is { Length: > 0 } ? from : null, _version);
        if (entries.Count == 0) return;
        foreach (var e in entries.Take(3))
            MarkdownView.Render(NewsPanel, $"### {e.Heading}\n\n{e.Body}", firstHeadingBig: false);
        NewsBox.Visibility = Visibility.Visible;
    }

    void Primary_Click(object sender, RoutedEventArgs e)
    {
        switch (_kind)
        {
            case SetupKind.Reinstall:
            case SetupKind.Downgrade:
                Open(_installed!.Exe);
                break;
            case null:
                Uninstall();
                break;
            default:
                Install();
                break;
        }
    }

    void Secondary_Click(object sender, RoutedEventArgs e)
    {
        switch (_kind)
        {
            case SetupKind.Install:
            case SetupKind.Upgrade:
                // questa copia si userà così com'è: l'installer non comparirà più per lei
                Setup.PortableExe = Environment.ProcessPath;
                if (Environment.ProcessPath is { } exe) Open(exe);
                break;
            case SetupKind.Reinstall:
            case SetupKind.Downgrade:
                Install();
                break;
            default:
                Close();
                break;
        }
    }

    async void Install()
    {
        var folder = FolderBox.Text.Trim();
        if (folder.Length == 0 || !Path.IsPathFullyQualified(folder))
        {
            OptionsToggle.IsChecked = true;
            FolderBox.Focus();
            return;
        }
        bool desktop = DesktopBox.IsChecked == true;
        bool upgrade = _installed is not null;
        ShowWork(upgrade ? "Aggiornamento…" : "Installazione…");
        try
        {
            if (Setup.OthersRunning)
            {
                WorkStep.Text = "Chiusura di TimerSala";
                if (!await Task.Run(() => Setup.CloseOthers(TimeSpan.FromSeconds(20))))
                    throw new InvalidOperationException("TimerSala è ancora aperto. Chiudilo e riprova, non ho cambiato niente.");
            }
            var exe = await Task.Run(() => Setup.Install(folder, desktop, s => Dispatcher.Invoke(() => { WorkStep.Text = s; WorkBar.Value++; })));
            Setup.PortableExe = null;
            _launch = exe;
            ShowDone(true,
                upgrade ? $"Aggiornato alla {_version}" : "TimerSala è installato",
                upgrade ? "Impostazioni e schemi sono quelli di prima." : "Lo trovi nel menu Start" + (desktop ? " e sul desktop." : "."),
                "Apri TimerSala");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowDone(false, "Non è andata", ex is UnauthorizedAccessException
                ? $"Non posso scrivere in «{folder}». Scegli un'altra cartella in «Opzioni»."
                : ex.Message + (upgrade ? " La versione installata è rimasta quella di prima." : ""), "Riprova");
        }
    }

    async void Uninstall()
    {
        ShowWork("Disinstallazione…");
        try
        {
            if (Setup.OthersRunning && !await Task.Run(() => Setup.CloseOthers(TimeSpan.FromSeconds(20))))
                throw new InvalidOperationException("TimerSala è ancora aperto. Chiudilo e riprova, non ho cambiato niente.");
            Setup.Uninstall();
            ShowDone(true, "TimerSala è stato disinstallato", "Impostazioni e schemi restano in %APPDATA%\\TimerSala, e reinstallando ritrovi tutto.", "Fine");
            DoneSecondary.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowDone(false, "Non è andata", ex.Message, "Riprova");
        }
    }

    internal void ShowWork(string title)
    {
        ChoosePage.Visibility = Visibility.Collapsed;
        DonePage.Visibility = Visibility.Collapsed;
        WorkPage.Visibility = Visibility.Visible;
        WorkTitle.Text = title;
        WorkStep.Text = "";
        WorkBar.Value = 0;
        // la barretta del marchio corre come un timer finché si lavora
        if (!Theming.Motion.Reduced)
            HeroBar.BeginAnimation(WidthProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 210, TimeSpan.FromSeconds(1.2))
            { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
    }

    bool _failed;

    internal void ShowDone(bool ok, string title, string lead, string primary)
    {
        _failed = !ok;
        HeroBar.BeginAnimation(WidthProperty, null);
        HeroBar.Width = ok ? 210 : 0;
        WorkPage.Visibility = Visibility.Collapsed;
        ChoosePage.Visibility = Visibility.Collapsed;
        DonePage.Visibility = Visibility.Visible;
        DoneBadge.Background = (System.Windows.Media.Brush)FindResource(ok ? "StartBrush" : "StopBrush");
        DoneGlyph.Text = ok ? "" : "";
        DoneTitle.Text = title;
        DoneLead.Text = lead;
        DonePrimary.Content = primary;
    }

    void DonePrimary_Click(object sender, RoutedEventArgs e)
    {
        if (_failed)
        {
            DonePage.Visibility = Visibility.Collapsed;
            ChoosePage.Visibility = Visibility.Visible;
            return;
        }
        if (_launch is { } exe) Open(exe); else Close();
    }

    void Open(string exe)
    {
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe)! });
        Close();
    }

    void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Dove installare TimerSala?" };
        if (Directory.Exists(Path.GetDirectoryName(FolderBox.Text))) dialog.InitialDirectory = Path.GetDirectoryName(FolderBox.Text);
        if (dialog.ShowDialog(this) == true) FolderBox.Text = SetupPlan.FolderFor(dialog.FolderName);
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    void Drag_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase) DragMove();
    }

    /// <summary>
    /// All'avvio: l'installer al posto del programma? Solo aprendo un file scaricato (o con --disinstalla dalla voce in
    /// «App installate»). Null = si apre TimerSala come sempre.
    /// </summary>
    internal static SetupWindow? ForStartup(string[] args)
    {
        if (Environment.ProcessPath is not { } exe) return null;
        bool uninstall = args.Contains(Setup.UninstallArg);
        if (args.Contains(Setup.PortableArg) || args.Contains("--avvio-windows")) return null;
        var installed = Setup.Installed;
        if (!uninstall && !args.Contains(Setup.ForceArg)
            && !SetupPlan.Offer(exe, installed?.Folder, Setup.PortableExe, devBuild: AppInfo.Version.Length == 0)) return null;
        // mai installato con l'installer: forse c'è una copia messa a mano (in sala, C:\TimerSala), o è proprio questa
        string? here = null;
        if (installed is null && !uninstall)
        {
            if (Setup.IsInUse(exe)) here = Path.GetDirectoryName(exe);
            else installed = Setup.FindManualCopy(exe);
        }
        var kind = uninstall ? (SetupKind?)null : SetupPlan.Kind(installed?.Version, AppInfo.Version);
        return new SetupWindow(kind, AppInfo.Version, installed, Setup.OthersRunning, here);
    }
}
