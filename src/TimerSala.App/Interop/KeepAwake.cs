using System.Runtime.InteropServices;

namespace TimerSala.App.Interop;

/// <summary>Impedisce a Windows di sospendere il PC o spegnere gli schermi (anche il salvaschermo) finché serve.</summary>
public static class KeepAwake
{
    const uint ES_CONTINUOUS = 0x80000000, ES_SYSTEM_REQUIRED = 0x1, ES_DISPLAY_REQUIRED = 0x2;

    static bool _on;

    /// <summary>Da chiamare sempre dallo stesso thread (quello dell'interfaccia): lo stato vale per il thread chiamante.</summary>
    public static void Set(bool on)
    {
        if (on == _on) return;
        _on = on;
        SetThreadExecutionState(on ? ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED : ES_CONTINUOUS);
    }

    [DllImport("kernel32.dll")]
    static extern uint SetThreadExecutionState(uint flags);
}
