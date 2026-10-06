using System.Reflection;

namespace TimerSala.App;

public static class AppInfo
{
    /// <summary>Versione del programma («2.1.0-beta.1»), vuota nelle build di sviluppo.</summary>
    public static string Version { get; } = ReadVersion();

    static string ReadVersion()
    {
        var v = typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        v = v.Split('+')[0];
        return v is "1.0.0" ? "" : v;
    }
}
