using System.Diagnostics;

namespace TimerSala.App.Interop;

sealed class ZoomFailed(string message) : Exception(message);

/// <summary>
/// Il client Zoom installato, pilotato dall'accessibilità di Windows (preso da assistant_acustica, provato su Zoom 7.2.1 in italiano).
/// Riconosce finestre e pulsanti per nome: se Zoom li cambia, vanno aggiornati qui.
/// Ogni metodo blocca fino a qualche secondo: va chiamato fuori dal thread dell'interfaccia.
/// </summary>
static class ZoomClient
{
    const string Proc = "Zoom";
    const byte Alt = 0x12, S = 0x53, U = 0x55;
    const string MeetingWindow = "ConfMultiTabContentWndClass", ToolbarWindow = "ZPControlPanelClass", MuteDialog = "zChangeNameWndClass",
        ShareWindow = "ZPShareEntranceClass", SharingToolbar = "ZPFloatToolbarClass";
    const string Participants = "Partecipanti,", Share = "Condividi, Alt+S", MuteAllButton = "Disattiva audio per tutti",
        AllowUnmute = "Consenti ai partecipanti di attivare il proprio audio", Yes = "Sì", ScreensTab = "Schermi", OtherTab = "Altro",
        AudioRow = "audio del computer", VideoBox = "Ottimizza per condivisione video", ConfirmScreen = "Condividi Schermo", ConfirmAudio = "Condividi Audio",
        SharingAudio = "Condivisione audio computer", Self = "(Organizzatore, io)";

    enum Sharing { None, Screen, Audio }

    // l'elenco dei processi costa ~6 ms e servirebbe a ogni controllo: si tiene 2 secondi,
    // e si rilegge subito se una finestra non si trova (Zoom appena riavviato)
    sealed record ZoomPids(IReadOnlySet<uint> Ids, long At);
    static volatile ZoomPids _pids = new(new HashSet<uint>(), long.MinValue / 2);

    static ZoomPids ReadPids() => _pids = new(Process.GetProcessesByName(Proc).Select(p => (uint)p.Id).ToHashSet(), Environment.TickCount64);

    static IntPtr Win(string cls)
    {
        var pids = _pids;
        if (Environment.TickCount64 - pids.At > 2000) pids = ReadPids();
        var h = Native.WindowOf(pids.Ids, cls);
        if (h == 0 && Environment.TickCount64 - pids.At > 250) h = Native.WindowOf(ReadPids().Ids, cls);
        return h;
    }
    static IntPtr Meeting => Win(MeetingWindow);
    static IntPtr Toolbar => Meeting == 0 ? 0 : Native.ChildOf(Meeting, ToolbarWindow);

    // mentre si condivide lo schermo Zoom nasconde la finestra della riunione e mostra solo la barra flottante
    static bool InMeeting => Meeting != 0 || Win(SharingToolbar) != 0;

    static Ctl? Find(IntPtr h, string text, int role = 0, bool hidden = false) =>
        Msaa.First(h, c => (role == 0 || c.Role == role) && c.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase), hidden);

    static T Until<T>(Func<T?> probe, string what, int ms = 8000) where T : class
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            // mentre Zoom ricostruisce le sue pagine l'accessibilità fallisce: vuol dire «non ancora»
            try { if (probe() is { } hit) return hit; }
            catch (System.Runtime.InteropServices.COMException) { }
            if (sw.ElapsedMilliseconds > ms) throw new ZoomFailed($"non trovo «{what}»");
            Thread.Sleep(50);
        }
    }

    static void WaitFor(Func<bool> done, string what) => Until(() => done() ? "" : null, what);

    static void EnsureMeeting()
    {
        if (!InMeeting) throw new ZoomFailed("Zoom non è in riunione");
    }

    // la barra si nasconde da sola, ma i suoi pulsanti funzionano anche nascosti
    static Ctl Tool(string text) => Until(() => Find(Toolbar, text, hidden: true), text);

    static void ShowParticipants()
    {
        // il pulsante dice sempre «pannello aperto», aperto o no: la prova è solo il pannello
        Func<Ctl?> panel = () => Find(Meeting, MuteAllButton);
        if (panel() != null) return;
        Tool(Participants).Press();
        try { Until(panel, MuteAllButton, 3000); }
        catch (ZoomFailed)
        {
            Native.Focus(Meeting);
            Native.Keys(Alt, U);
            Until(panel, MuteAllButton);
        }
    }

    /// <summary>Muta tutti; il dialogo è lo stesso a inizio e fine, cambia solo la casella della riattivazione.</summary>
    public static void MuteAll(bool allowUnmute)
    {
        EnsureMeeting();
        ShowParticipants();
        Find(Meeting, MuteAllButton)!.Press();
        var box = Until(() => Find(Win(MuteDialog), AllowUnmute, Ctl.Check), AllowUnmute);
        if (box.Checked != allowUnmute) box.Press();
        Until(() => Find(Win(MuteDialog), Yes), Yes).Press();
    }

    // schede e righe ignorano accSelect: solo un clic sposta il fuoco di Zoom, e ogni clic si verifica rileggendo lo stato
    // Zoom riapre Condividi sull'ultima scheda e riga usate: se sono già quelle giuste non si clicca
    static void PickTab(IntPtr w, string tab)
    {
        var t = Until(() => Find(w, tab, Ctl.Tab), tab);
        if (t.Selected) return;
        t.Click(w);
        Until(() => Find(w, tab, Ctl.Tab) is { Selected: true } c ? c : null, tab);
    }

    static void PickRow(IntPtr w, Func<Ctl, bool> target, string what)
    {
        var row = Until(() => Msaa.First(w, c => c.Role == Ctl.Row && target(c)), what);
        if (row.Selected) return;
        row.Click(w);
        Until(() => Msaa.First(w, c => c.Role == Ctl.Row && c.Selected && target(c)), what);
    }

    static IntPtr OpenShareWindow(string tab)
    {
        var w = ShareWindowHandle();
        PickTab(w, tab);
        return w;
    }

    // il pulsante Condividi della barra: ricordato, così non si scorre la barra a ogni condivisione
    static Ctl? _shareButton;

    static IntPtr ShareWindowHandle()
    {
        // già aperta (a mano o da un tentativo fallito): si usa quella, premere di nuovo Condividi potrebbe chiuderla
        var w = Win(ShareWindow);
        if (w != 0) return w;
        if (_shareButton is not { } b || !b.StillNamed(Share)) _shareButton = b = Tool(Share);
        b.Press();
        return (IntPtr)Until<object>(() => Win(ShareWindow) is var h && h != 0 ? h : null, "la finestra Condividi");
    }

    /// <summary>Le righe della scheda «Schermi» di Condividi: i nomi cambiano da PC a PC.</summary>
    public static List<string> Screens()
    {
        EnsureMeeting();
        var w = OpenShareWindow(ScreensTab);
        try { return Until(() => Msaa.Controls(w).Where(c => c.Role == Ctl.Row).Select(c => c.Name).ToList() is { Count: > 0 } r ? r : null, "gli schermi"); }
        finally { CloseShareWindow(); }
    }

    static void CloseShareWindow()
    {
        if (Win(ShareWindow) is var w && w != 0) Native.PostMessage(w, 0x10, 0, 0); // WM_CLOSE
    }

    /// <summary>
    /// Condivide lo schermo scelto, con il suono del computer. Se un passo non riesce (Zoom lento, un clic perso)
    /// chiude la finestra Condividi rimasta a metà e riprova da capo una volta; se non va nemmeno così, la lascia chiusa.
    /// </summary>
    public static void ShareScreen(string screen) => TwoTries(() => ShareScreenOnce(screen), () => SharingScreen);

    static void TwoTries(Action once, Func<bool> done)
    {
        EnsureMeeting();
        try { once(); }
        catch (Exception e) when (e is ZoomFailed or System.Runtime.InteropServices.COMException)
        {
            CloseShareWindow();
            if (done()) return; // partita lo stesso, solo in ritardo
            Thread.Sleep(300);
            try { once(); }
            catch { CloseShareWindow(); throw; }
        }
    }

    static void ShareScreenOnce(string screen)
    {
        StopShare();
        var w = ShareWindowHandle();

        // di solito Zoom riapre Condividi già pronto (scheda, schermo e spunta dell'ultima volta):
        // una sola lettura della finestra lo verifica e si conferma subito; altrimenti passo per passo
        var all = Until(() => Msaa.Controls(w) is var c && c.Any(x => x.Role == Ctl.Tab) ? c : null, "la finestra Condividi");
        bool Is(Ctl c, int role, string text) => c.Role == role && c.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase);
        if (all.Any(c => Is(c, Ctl.Tab, ScreensTab) && c.Selected)
            && all.Any(c => Is(c, Ctl.Row, screen) && c.Selected)
            && all.Any(c => Is(c, Ctl.Check, VideoBox) && c.Checked)
            && all.FirstOrDefault(c => c.Name.StartsWith(ConfirmScreen, StringComparison.OrdinalIgnoreCase)) is { } ready)
        {
            ready.Press();
            WaitFor(() => SharingScreen, "l'avvio della condivisione");
            return;
        }

        PickTab(w, ScreensTab);
        PickRow(w, c => c.Name.StartsWith(screen), screen);
        // i cantici sono video: più fluidi a casa, e Zoom con questa spunta condivide anche il suono
        var video = Until(() => Find(w, VideoBox, Ctl.Check), VideoBox);
        if (!video.Checked) video.Press();
        Until(() => Find(w, ConfirmScreen), ConfirmScreen).Press();
        WaitFor(() => SharingScreen, "l'avvio della condivisione");
    }

    /// <summary>Condivide solo l'audio del computer: a casa arriva la musica di JW Library.</summary>
    public static void ShareAudio() => TwoTries(ShareAudioOnce, () => Status() == Sharing.Audio && (_audioShared = true));

    static void ShareAudioOnce()
    {
        StopShare();
        var w = OpenShareWindow(OtherTab);
        PickRow(w, c => c.Name.Contains(AudioRow), AudioRow);
        Until(() => Find(w, ConfirmAudio), ConfirmAudio).Press();
        WaitFor(() => Status() == Sharing.Audio, "l'avvio dell'audio condiviso");
        _audioShared = true;
    }

    // l'audio condiviso si legge solo dal pannello dei partecipanti, lento da scorrere: si guarda lì solo se l'abbiamo avviato noi
    static bool _audioShared;

    public static bool SharingScreen => Win(SharingToolbar) != 0;

    /// <summary>Interrompe la condivisione in corso; con <paramref name="audioOnly"/> solo quella dell'audio (la musica), mai lo schermo.</summary>
    public static void StopShare(bool audioOnly = false)
    {
        bool screen = SharingScreen;
        if (screen && audioOnly) return;
        if (screen) Native.Focus(Win(SharingToolbar));                          // il pulsante della barra non ha azione predefinita
        else if (_audioShared && Status() == Sharing.Audio) Native.Focus(Meeting); // l'audio da solo non ha barra flottante
        else { _audioShared = false; return; }
        Native.Keys(Alt, S);
        WaitFor(() => screen ? !SharingScreen : Status() == Sharing.None, "la fine della condivisione");
        if (!screen) _audioShared = false;
    }

    // l'audio condiviso si legge solo dalla propria riga nel pannello dei partecipanti, che va tenuto aperto
    static Sharing Status()
    {
        if (SharingScreen) return Sharing.Screen;
        ShowParticipants();
        return Msaa.First(Meeting, c => c.Role == Ctl.Row && c.Name.Contains(Self) && c.Name.Contains(SharingAudio)) != null ? Sharing.Audio : Sharing.None;
    }
}

/// <summary>JW Library pilotata solo da fuori: link ai cantici e tasto multimediale di Windows (preso da assistant_acustica).</summary>
static class JwMusic
{
    const int LastSong = 162; // ultimo della raccolta dei cantici

    // ponytail: un cantico a caso, poi JW prosegue da sola con i successivi. Uno shuffle vero vorrebbe una coda di link.
    // Mai altri link JW: in assistant_acustica `pub=sjjm` senza traccia e `alias=meetings` hanno fatto chiudere l'app.
    public static void Start() => Process.Start(new ProcessStartInfo(
        $"jwlibrary:///finder?srcid=jwlshare&wtlocale=I&prefer=lang&lank=pub-sjjm_{Random.Shared.Next(1, LastSong + 1)}_AUDIO") { UseShellExecute = true });

    /// <summary>Play/pausa: un interruttore, quindi va mandato solo se la musica suona.</summary>
    public static void Pause() => Native.MediaKey(0xB3);
}
