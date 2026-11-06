using System.Runtime.InteropServices;
using System.Text;
using Accessibility;

namespace TimerSala.App.Interop;

// Preso da assistant_acustica. Zoom disegna da sé la sua interfaccia: UI Automation vede pannelli vuoti, MSAA ogni controllo.
// Mai il vero puntatore, la mano dell'operatore è sul mouse: Click manda il clic alla finestra, nella posizione del controllo.
sealed class Ctl(IAccessible owner, object id, string name, int role, int state)
{
    public const int List = 33, Row = 34, Tab = 37, Check = 44;
    public string Name => name;
    public int Role => role;
    public bool Checked => (state & 0x10) != 0;
    public bool Selected => (state & 0x02) != 0;

    public void Press() => owner.accDoDefaultAction(id);

    /// <summary>Lo stato riletto adesso, con una chiamata sola invece di scorrere di nuovo l'albero.</summary>
    public bool SelectedNow
    {
        get
        {
            try { return (Convert.ToInt32(owner.get_accState(id)) & 0x02) != 0; }
            catch (Exception e) when (e is COMException or InvalidComObjectException or ArgumentException) { return false; }
        }
    }

    /// <summary>Riletto adesso: un controllo ricordato da prima vale solo se Zoom non l'ha ricreato.</summary>
    public bool StillNamed(string text)
    {
        try { return (owner.get_accName(id) ?? "").StartsWith(text, StringComparison.OrdinalIgnoreCase); }
        catch (Exception e) when (e is COMException or InvalidComObjectException or ArgumentException) { return false; }
    }

    public void Click(IntPtr window)
    {
        owner.accLocation(out int x, out int y, out int w, out int h, id);
        Native.PostClick(window, x + w / 2, y + h / 2);
    }
}

static class Msaa
{
    const int Invisible = 0x8000;
    static Guid IID = new("618736E0-3C3D-11CF-810C-00AA00389B71");

    [DllImport("oleacc")] static extern int AccessibleObjectFromWindow(IntPtr h, uint id, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IAccessible o);
    [DllImport("oleacc")] static extern int AccessibleChildren(IAccessible p, int start, int n, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] object[] kids, out int got);

    /// <param name="clip">Salta i gruppi fuori dalla finestra: Condividi tiene tutte le sezioni in una lista che scorre, anche quelle non visibili.</param>
    public static List<Ctl> Controls(IntPtr hwnd, bool hidden = false, bool clip = false)
    {
        var found = new List<Ctl>();
        Visit(hwnd, hidden, clip, c => { found.Add(c); return false; });
        return found;
    }

    /// <summary>Il primo controllo che va bene: smette di scorrere l'albero appena lo trova (le anteprime di Condividi sono tante).</summary>
    public static Ctl? First(IntPtr hwnd, Func<Ctl, bool> match, bool hidden = false, bool clip = false)
    {
        Ctl? hit = null;
        Visit(hwnd, hidden, clip, c => { if (match(c)) hit = c; return hit != null; });
        return hit;
    }

    static void Visit(IntPtr hwnd, bool hidden, bool clip, Func<Ctl, bool> stop)
    {
        if (hwnd == 0 || AccessibleObjectFromWindow(hwnd, 0xFFFFFFFC, ref IID, out var root) != 0 || root == null) return;
        Walk(root, 0, hidden, clip && !hidden ? Area(root) : null, stop);
    }

    // true = trovato, si smette. Ogni lettura è un viaggio fino a Zoom: lo stato per primo, così i nodi nascosti costano una lettura sola
    static bool Walk(IAccessible parent, int depth, bool hidden, Box? area, Func<Ctl, bool> stop)
    {
        if (depth > 18) return false;
        int count;
        try { count = parent.accChildCount; } catch (COMException) { return false; }
        if (count <= 0 || (area is { } a && Outside(parent, a))) return false;
        var kids = new object[count];
        if (AccessibleChildren(parent, 0, count, kids, out int got) != 0) return false;
        for (int i = 0; i < got; i++)
        {
            var child = kids[i] as IAccessible;
            var owner = child ?? parent;
            object id = child != null ? 0 : kids[i];
            try
            {
                int state = Convert.ToInt32(owner.get_accState(id));
                if ((state & Invisible) != 0 && !hidden) continue;
                string name = owner.get_accName(id) ?? "";
                if (name != "" && stop(new Ctl(owner, id, name, Convert.ToInt32(owner.get_accRole(id)), state))) return true;
            }
            catch (Exception e) when (e is COMException or InvalidCastException or NotImplementedException) { continue; }
            if (child != null && Walk(child, depth + 1, hidden, area, stop)) return true;
        }
        return false;
    }

    record struct Box(int L, int T, int R, int B);

    // le posizioni le dà Zoom, finestra e controlli nello stesso sistema; null se illeggibile o vuota
    static Box? Area(IAccessible o)
    {
        try { o.accLocation(out int x, out int y, out int w, out int h, 0); return w > 0 && h > 0 ? new(x, y, x + w, y + h) : null; }
        catch (COMException) { return null; }
    }

    // nel dubbio si entra: saltare un gruppo buono costerebbe la condivisione
    static bool Outside(IAccessible o, Box a) => Area(o) is { } c && (c.L >= a.R || c.T >= a.B || c.R <= a.L || c.B <= a.T);
}

static class Native
{
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32")] static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32")] static extern bool EnumChildWindows(IntPtr r, EnumProc p, IntPtr l);
    [DllImport("user32")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32")] static extern bool ScreenToClient(IntPtr h, ref Point p);
    [DllImport("user32")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);

    struct Point { public int X, Y; }

    static string ClassOf(IntPtr h) { var s = new StringBuilder(256); GetClassName(h, s, 256); return s.ToString(); }

    public static IntPtr WindowOf(IReadOnlySet<uint> pids, string cls)
    {
        IntPtr hit = 0;
        EnumWindows((h, _) =>
        {
            GetWindowThreadProcessId(h, out uint pid);
            if (pids.Contains(pid) && IsWindowVisible(h) && ClassOf(h) == cls) { hit = h; return false; }
            return true;
        }, 0);
        return hit;
    }

    public static IntPtr ChildOf(IntPtr parent, string cls)
    {
        IntPtr hit = 0;
        EnumChildWindows(parent, (h, _) =>
        {
            if (ClassOf(h) == cls) { hit = h; return false; }
            return true;
        }, 0);
        return hit;
    }

    public static void Focus(IntPtr hwnd)
    {
        SetForegroundWindow(hwnd);
        Thread.Sleep(120);
    }

    public static void PostClick(IntPtr h, int screenX, int screenY)
    {
        var p = new Point { X = screenX, Y = screenY };
        ScreenToClient(h, ref p);
        IntPtr at = (p.Y << 16) | (p.X & 0xFFFF);
        PostMessage(h, 0x200, 0, at); // move
        PostMessage(h, 0x201, 1, at); // down
        PostMessage(h, 0x202, 0, at); // up
    }

    /// <summary>Raggiunge l'app che ha la sessione multimediale di Windows; tasto esteso, come quello di una tastiera vera.</summary>
    public static void MediaKey(byte key)
    {
        keybd_event(key, 0, 1, 0);
        keybd_event(key, 0, 3, 0);
    }

    public static void Keys(params byte[] chord)
    {
        foreach (var k in chord) keybd_event(k, 0, 0, 0);
        foreach (var k in chord.Reverse()) keybd_event(k, 0, 2, 0);
    }
}
