using System.IO;

namespace TimerSala.App.Interop;

/// <summary>Collegamento a TimerSala sul desktop (con lo shell di Windows, senza librerie aggiuntive).</summary>
public static class Shortcut
{
    public static bool CreateOnDesktop()
    {
        try
        {
            if (Environment.ProcessPath is not { } exe) return false;
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var path = Path.Combine(desktop, "TimerSala.lnk");
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return false;
            object shell = Activator.CreateInstance(shellType)!;
            object link = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [path])!;
            var t = link.GetType();
            void Set(string name, object value) => t.InvokeMember(name, System.Reflection.BindingFlags.SetProperty, null, link, [value]);
            Set("TargetPath", exe);
            Set("WorkingDirectory", Path.GetDirectoryName(exe)!);
            Set("IconLocation", exe + ",0");
            Set("Description", "TimerSala, il timer per le adunanze");
            t.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, link, null);
            return true;
        }
        catch { return false; }
    }
}
