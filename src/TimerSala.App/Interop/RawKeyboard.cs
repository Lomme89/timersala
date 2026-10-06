using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace TimerSala.App.Interop;

/// <summary>
/// Tasti di tutte le tastiere collegate, anche con TimerSala in secondo piano (Raw Input con RIDEV_INPUTSINK),
/// con il dispositivo da cui arrivano: serve per il telecomando per presentazioni associato.
/// Non blocca i tasti: arrivano comunque anche al programma in primo piano.
/// </summary>
public sealed class RawKeyboard : IDisposable
{
    const int WM_INPUT = 0x00FF;
    const uint RIDEV_INPUTSINK = 0x00000100, RIDEV_REMOVE = 0x00000001;
    const uint RID_INPUT = 0x10000003, RIDI_DEVICENAME = 0x20000007;
    const uint RIM_TYPEKEYBOARD = 1;
    const ushort RI_KEY_BREAK = 1;

    readonly HwndSource _window;
    readonly Dictionary<IntPtr, string> _names = [];

    /// <summary>Tasto premuto (non rilasciato): percorso del dispositivo e codice del tasto virtuale.</summary>
    public event Action<string, int>? KeyDown;

    public RawKeyboard()
    {
        // finestra solo-messaggi: esiste sempre, anche con il controller nascosto (modalità mini)
        _window = new HwndSource(new HwndSourceParameters("TimerSala.Telecomando") { ParentWindow = new IntPtr(-3), Width = 0, Height = 0 });
        _window.AddHook(Hook);
        var device = new RAWINPUTDEVICE { UsagePage = 0x01, Usage = 0x06, Flags = RIDEV_INPUTSINK, Target = _window.Handle };
        if (!RegisterRawInputDevices([device], 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
            throw new InvalidOperationException("Registrazione dell'input non riuscita.");
    }

    IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_INPUT) return IntPtr.Zero;
        uint size = 0, header = (uint)Marshal.SizeOf<RAWINPUTHEADER>();
        GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, header);
        if (size == 0) return IntPtr.Zero;
        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(lParam, RID_INPUT, buffer, ref size, header) != size) return IntPtr.Zero;
            var h = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);
            if (h.Type != RIM_TYPEKEYBOARD || h.Device == IntPtr.Zero) return IntPtr.Zero;
            var k = Marshal.PtrToStructure<RAWKEYBOARD>(buffer + (int)header);
            if ((k.Flags & RI_KEY_BREAK) != 0) return IntPtr.Zero;
            KeyDown?.Invoke(NameOf(h.Device), k.VKey);
        }
        finally { Marshal.FreeHGlobal(buffer); }
        return IntPtr.Zero;
    }

    string NameOf(IntPtr device)
    {
        if (_names.TryGetValue(device, out var name)) return name;
        uint chars = 0;
        GetRawInputDeviceInfo(device, RIDI_DEVICENAME, IntPtr.Zero, ref chars);
        var buffer = Marshal.AllocHGlobal((int)chars * 2 + 2);
        try
        {
            GetRawInputDeviceInfo(device, RIDI_DEVICENAME, buffer, ref chars);
            name = Marshal.PtrToStringUni(buffer) ?? "";
        }
        finally { Marshal.FreeHGlobal(buffer); }
        return _names[device] = name;
    }

    public void Dispose()
    {
        var device = new RAWINPUTDEVICE { UsagePage = 0x01, Usage = 0x06, Flags = RIDEV_REMOVE, Target = IntPtr.Zero };
        RegisterRawInputDevices([device], 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());
        _window.RemoveHook(Hook);
        _window.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTDEVICE { public ushort UsagePage; public ushort Usage; public uint Flags; public IntPtr Target; }

    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTHEADER { public uint Type; public uint Size; public IntPtr Device; public IntPtr WParam; }

    [StructLayout(LayoutKind.Sequential)]
    struct RAWKEYBOARD { public ushort MakeCode; public ushort Flags; public ushort Reserved; public ushort VKey; public uint Message; public uint ExtraInformation; }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);

    [DllImport("user32.dll")]
    static extern uint GetRawInputData(IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, IntPtr data, ref uint size);
}
