using Microsoft.Win32;

namespace TimerSala.App.Interop;

/// <summary>Avvio di TimerSala all'accesso a Windows (chiave Run dell'utente, senza permessi da amministratore).</summary>
public static class AutoStart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "TimerSala";
    public const string Argument = "--avvio-windows";

    static string? Command => Environment.ProcessPath is { } exe ? $"\"{exe}\" {Argument}" : null;

    /// <summary>Il programma che Windows apre all'accesso, se l'avvio è attivo.</summary>
    public static string? RegisteredExe
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                if (key?.GetValue(ValueName) is not string cmd) return null;
                cmd = cmd.Trim();
                return cmd.StartsWith('"') ? cmd[1..cmd.IndexOf('"', 1)] : cmd.Split(' ')[0];
            }
            catch { return null; }
        }
    }

    /// <summary>Dopo un'installazione: se l'avvio con Windows era attivo, ora apre la copia installata.</summary>
    public static void Retarget(string exe)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) is not null) key.SetValue(ValueName, $"\"{exe}\" {Argument}");
        }
        catch { /* come sopra */ }
    }

    /// <summary>Attiva o disattiva l'avvio; se è attivo aggiorna il percorso (il programma può essere stato spostato).</summary>
    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (!enabled)
            {
                if (key.GetValue(ValueName) is not null) key.DeleteValue(ValueName, false);
                return;
            }
            if (Command is { } cmd && key.GetValue(ValueName) as string != cmd) key.SetValue(ValueName, cmd);
        }
        catch { /* criteri aziendali o registro non scrivibile: l'opzione resta senza effetto */ }
    }
}
