using System.IO;

namespace TimerSala.App.Interop;

/// <summary>Collegamenti a TimerSala (con lo shell di Windows, senza librerie aggiuntive).</summary>
public static class Shortcut
{
    public static string DesktopLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "TimerSala.lnk");
    public static string StartMenuLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "TimerSala.lnk");

    public static bool CreateOnDesktop() => Environment.ProcessPath is { } exe && Create(DesktopLink, exe);

    public static bool Create(string link, string exe)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return false;
            object shell = Activator.CreateInstance(shellType)!;
            object lnk = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [link])!;
            var t = lnk.GetType();
            void Set(string name, object value) => t.InvokeMember(name, System.Reflection.BindingFlags.SetProperty, null, lnk, [value]);
            Set("TargetPath", exe);
            Set("WorkingDirectory", Path.GetDirectoryName(exe)!);
            Set("IconLocation", exe + ",0");
            Set("Description", "TimerSala, il timer per le adunanze");
            t.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, lnk, null);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Dove punta un collegamento (null se non c'è o non si legge).</summary>
    public static string? TargetOf(string link)
    {
        if (!File.Exists(link)) return null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return null;
            object shell = Activator.CreateInstance(shellType)!;
            object lnk = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [link])!;
            return lnk.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, lnk, null) as string;
        }
        catch { return null; }
    }
}
