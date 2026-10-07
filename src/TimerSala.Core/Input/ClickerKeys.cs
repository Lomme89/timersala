namespace TimerSala.Core.Input;

public enum ClickerAction { None, Forward, Back }

/// <summary>
/// Tasti dei telecomandi per presentazioni. Quasi tutti mandano Pagina giù / Pagina su; alcuni le frecce, Invio o
/// Backspace, altri + / − / Tab. «Avanti» fa quello che fa Avvia/Ferma; «Indietro» è sempre «Annulla», mai «parte precedente»:
/// un colpo per sbaglio si rimedia.
/// </summary>
public static class ClickerKeys
{
    // codici dei tasti virtuali di Windows
    const int Back = 0x08, Tab = 0x09, Enter = 0x0D, Escape = 0x1B, Space = 0x20, PageUp = 0x21, PageDown = 0x22, Left = 0x25, Up = 0x26, Right = 0x27, Down = 0x28, Add = 0x6B, Subtract = 0x6D;

    // alcuni telecomandi «su/giù/OK» mandano + (su), − (giù) e Tab (OK)
    static readonly int[] DefaultForward = [PageDown, Right, Down, Enter, Space, Tab, Subtract];
    static readonly int[] DefaultBack = [PageUp, Left, Up, Back, Add];

    /// <summary>
    /// Con il telecomando associato: i tasti impostati a mano, poi tutti i tasti «avanti» e «indietro» che i telecomandi usano.
    /// </summary>
    public static ClickerAction FromAssociatedDevice(int vkey, IReadOnlyCollection<int>? forward = null, IReadOnlyCollection<int>? back = null) =>
        forward?.Contains(vkey) == true ? ClickerAction.Forward
        : back?.Contains(vkey) == true ? ClickerAction.Back
        : Default(vkey);

    static ClickerAction Default(int vkey) =>
        DefaultForward.Contains(vkey) ? ClickerAction.Forward
        : DefaultBack.Contains(vkey) ? ClickerAction.Back
        : ClickerAction.None;

    /// <summary>Tutti i tasti che fanno <paramref name="action"/>: prima quelli scelti a mano, poi i predefiniti rimasti.</summary>
    public static IReadOnlyList<int> KeysFor(ClickerAction action, IReadOnlyCollection<int> forward, IReadOnlyCollection<int> back) =>
        (action == ClickerAction.Forward ? forward.Concat(DefaultForward) : back.Concat(DefaultBack))
            .Distinct()
            .Where(k => FromAssociatedDevice(k, forward, back) == action)
            .ToList();

    /// <summary>Nome del tasto da mostrare: «Pagina giù», «→», «F5»…</summary>
    public static string Name(int vkey) => vkey switch
    {
        Back => "Backspace",
        Tab => "Tab",
        Enter => "Invio",
        Escape => "Esc",
        Space => "Spazio",
        PageUp => "Pagina su",
        PageDown => "Pagina giù",
        Left => "←",
        Up => "↑",
        Right => "→",
        Down => "↓",
        Add => "+",
        Subtract => "−",
        0xBE => ".",
        >= 0x70 and <= 0x87 => $"F{vkey - 0x6F}",
        >= 0x41 and <= 0x5A => ((char)vkey).ToString(),
        _ => $"tasto {vkey:X2}",
    };

    /// <summary>Senza associazione, dalla tastiera del PC: solo Pagina giù / Pagina su (le frecce scelgono la parte).</summary>
    public static ClickerAction FromKeyboard(int vkey) => vkey switch
    {
        PageDown => ClickerAction.Forward,
        PageUp => ClickerAction.Back,
        _ => ClickerAction.None,
    };

    /// <summary>
    /// Un tasto che un telecomando non ha: lettere (tranne B, «schermo nero») e numeri. Se arriva dal dispositivo
    /// associato, quello è una tastiera vera e l'associazione va annullata, altrimenti scrivere in Zoom fermerebbe il timer.
    /// </summary>
    public static bool IsTypingKey(int vkey) => (vkey is >= 0x41 and <= 0x5A && vkey != 0x42) || vkey is >= 0x30 and <= 0x39;

    /// <summary>Nome breve dal percorso del dispositivo: «\\?\HID#VID_046D&amp;PID_C52B…» → «VID 046D · PID C52B».</summary>
    public static string ShortName(string devicePath)
    {
        string? Part(string key)
        {
            int i = devicePath.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            return i < 0 || i + key.Length + 4 > devicePath.Length ? null : devicePath.Substring(i + key.Length, 4).ToUpperInvariant();
        }
        var vid = Part("VID_");
        var pid = Part("PID_");
        return vid is not null && pid is not null ? $"VID {vid} · PID {pid}" : "dispositivo USB";
    }
}
