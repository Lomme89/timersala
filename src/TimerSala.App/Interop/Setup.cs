using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using TimerSala.Core.Info;

namespace TimerSala.App.Interop;

/// <summary>
/// Installazione per l'utente, senza permessi da amministratore: il programma è un solo file, quindi installare vuol dire
/// copiarlo in una cartella sua, fare i collegamenti e farsi vedere in «App installate». I dati restano in %APPDATA%\TimerSala:
/// aggiornare o disinstallare non li tocca mai.
/// </summary>
static class Setup
{
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\TimerSala";
    const string AppKey = @"Software\TimerSala";
    public const string UninstallArg = "--disinstalla";
    public const string PortableArg = "--portatile";
    public const string ForceArg = "--installa"; // mostra l'installer anche nelle build di sviluppo (per provarlo)
    const string ExeName = "TimerSala.exe";

    public static string DefaultFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "TimerSala");

    /// <param name="Manual">Copiata a mano (per esempio in C:\TimerSala), senza voce in «App installate».</param>
    public sealed record InstalledCopy(string Folder, string Version, bool Manual = false)
    {
        public string Exe => Path.Combine(Folder, ExeName);
    }

    /// <summary>La copia installata, se la sua voce in «App installate» c'è e il file anche.</summary>
    public static InstalledCopy? Installed
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
                if (key?.GetValue("InstallLocation") is not string folder || !File.Exists(Path.Combine(folder, ExeName))) return null;
                return new(folder, key.GetValue("DisplayVersion") as string ?? "");
            }
            catch { return null; }
        }
    }

    // dove si trova una copia messa a mano: avvio con Windows, collegamenti, copie aperte, la cartella che si usa in sala
    static List<string> ManualCandidates()
    {
        var list = new List<string?> { AutoStart.RegisteredExe, Shortcut.TargetOf(Shortcut.DesktopLink), Shortcut.TargetOf(Shortcut.StartMenuLink) };
        foreach (var p in Process.GetProcessesByName("TimerSala"))
        {
            try { list.Add(p.MainModule?.FileName); } catch { /* processo di un altro utente o già chiuso */ }
        }
        list.Add(@"C:\TimerSala\TimerSala.exe");
        return list.OfType<string>()
            .Where(f => string.Equals(Path.GetFileName(f), ExeName, StringComparison.OrdinalIgnoreCase) && File.Exists(f))
            .ToList();
    }

    /// <summary>
    /// Una copia di TimerSala messa a mano (non registrata), diversa da questo file: è quella da aggiornare, nella sua cartella.
    /// </summary>
    public static InstalledCopy? FindManualCopy(string self)
    {
        var exe = ManualCandidates().FirstOrDefault(f => !SetupPlan.SamePath(f, self));
        if (exe is null) return null;
        string version = "";
        try { version = FileVersionInfo.GetVersionInfo(exe).ProductVersion?.Split('+')[0] ?? ""; } catch { }
        return new(Path.GetDirectoryName(exe)!, version is "1.0.0" ? "" : version, Manual: true);
    }

    /// <summary>Questo file è la copia che si usa già (avvio con Windows, collegamenti, C:\TimerSala): si registra dov'è.</summary>
    public static bool IsInUse(string self) => ManualCandidates().Any(f => SetupPlan.SamePath(f, self));

    /// <summary>La copia che si è scelto di usare senza installare: per lei l'installer non compare più.</summary>
    public static string? PortableExe
    {
        get { try { using var k = Registry.CurrentUser.OpenSubKey(AppKey); return k?.GetValue("Portatile") as string; } catch { return null; } }
        set
        {
            try
            {
                using var k = Registry.CurrentUser.CreateSubKey(AppKey);
                if (value is null) k.DeleteValue("Portatile", false); else k.SetValue("Portatile", value);
            }
            catch { /* registro non scrivibile: l'installer ricomparirà, nessun danno */ }
        }
    }

    /// <summary>Le altre copie di TimerSala aperte (non questa).</summary>
    public static bool OthersRunning => Process.GetProcessesByName("TimerSala").Any(p => p.Id != Environment.ProcessId);

    /// <summary>
    /// Chiede alle copie aperte di chiudersi, come se lo facesse l'utente: se una parte è in corso TimerSala chiede conferma,
    /// e se gli si dice di restare aperto l'installazione si ferma. Mai chiuso a forza.
    /// </summary>
    public static bool CloseOthers(TimeSpan wait)
    {
        if (!OthersRunning) return true;
        try { using var quit = EventWaitHandle.OpenExisting(App.CommandEventName("esci")); quit.Set(); }
        catch (WaitHandleCannotBeOpenedException) { } // una versione vecchia, senza il comando: ci si prova con la finestra
        foreach (var p in Process.GetProcessesByName("TimerSala").Where(p => p.Id != Environment.ProcessId)) p.CloseMainWindow();
        var sw = Stopwatch.StartNew();
        while (OthersRunning && sw.Elapsed < wait) Thread.Sleep(200);
        return !OthersRunning;
    }

    /// <summary>
    /// Copia questo file nella cartella, al posto di quello vecchio. Il vecchio si tiene da parte finché il nuovo non è al suo posto:
    /// se qualcosa va storto torna com'era.
    /// </summary>
    public static string Install(string folder, bool desktopLink, Action<string> step)
    {
        var source = Environment.ProcessPath ?? throw new InvalidOperationException("non trovo il file del programma");
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, ExeName);
        var fresh = target + ".nuovo";
        var old = target + ".vecchio";

        step("Copia del programma");
        // già al suo posto (copiato a mano e aperto da lì): niente da copiare, solo da registrare
        if (!SetupPlan.SamePath(source, target))
        {
            File.Copy(source, fresh, overwrite: true);
            bool hadOld = File.Exists(target);
            if (hadOld) File.Move(target, old, overwrite: true);
            try { File.Move(fresh, target); }
            catch
            {
                if (hadOld) File.Move(old, target, overwrite: true);
                TryDelete(fresh);
                throw;
            }
            TryDelete(old);
        }

        step("Collegamenti");
        Shortcut.Create(Shortcut.StartMenuLink, target);
        if (desktopLink) Shortcut.Create(Shortcut.DesktopLink, target);
        else if (SetupPlan.SamePath(Shortcut.TargetOf(Shortcut.DesktopLink), target)) TryDelete(Shortcut.DesktopLink);

        step("Registrazione in Windows");
        Register(folder, target);
        AutoStart.Retarget(target);
        return target;
    }

    static void Register(string folder, string exe)
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
        key.SetValue("DisplayName", "TimerSala");
        key.SetValue("DisplayVersion", AppInfo.Version is { Length: > 0 } v ? v : "sviluppo");
        key.SetValue("Publisher", "TimerSala");
        key.SetValue("DisplayIcon", exe + ",0");
        key.SetValue("InstallLocation", folder);
        key.SetValue("UninstallString", $"\"{exe}\" {UninstallArg}");
        key.SetValue("URLInfoAbout", "https://github.com/Lomme89/timersala");
        key.SetValue("InstallDate", DateTime.Today.ToString("yyyyMMdd"));
        key.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    /// <summary>
    /// Toglie collegamenti, voce in «App installate» e avvio con Windows; il file si cancella da solo appena il programma si chiude.
    /// I dati (impostazioni, schemi, congregazioni) restano: reinstallando si ritrova tutto.
    /// </summary>
    public static void Uninstall()
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("non trovo il file del programma");
        var folder = Path.GetDirectoryName(exe)!;
        foreach (var link in new[] { Shortcut.StartMenuLink, Shortcut.DesktopLink })
            if (SetupPlan.SamePath(Shortcut.TargetOf(link), exe)) TryDelete(link);
        AutoStart.Apply(false);
        try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
        // ponytail: un file in uso non si cancella da sé; lo fa cmd dopo due secondi. Solo i nostri file, e la cartella solo se è vuota.
        Process.Start(new ProcessStartInfo("cmd.exe",
            $"/c timeout /t 2 /nobreak >nul & del /f /q \"{exe}\" & del /f /q \"{exe}.vecchio\" & rmdir \"{folder}\"")
        { CreateNoWindow = true, UseShellExecute = false });
    }

    static void TryDelete(string file)
    {
        try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
