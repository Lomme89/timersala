using System.Text.Json.Serialization;
using TimerSala.Core.Wol;

namespace TimerSala.Core.Storage;

public sealed class AppSettings
{
    public string WolCode { get; set; } = WolLanguage.Italian.Code;
    public string WolRsconf { get; set; } = WolLanguage.Italian.Rsconf;
    public string WolLib { get; set; } = WolLanguage.Italian.Lib;

    /// <summary>Nome dispositivo dello schermo del timer (es. \\.\DISPLAY3). Vuoto = nessuno.</summary>
    public string? TimerMonitor { get; set; }

    public bool TimerWindowVisible { get; set; } = true;

    public bool WebServerEnabled { get; set; } = true;
    public int WebServerPort { get; set; } = 8090;

    public int WarningSeconds { get; set; } = 60;
    public int CounselSeconds { get; set; } = 60;

    public bool AutoDownload { get; set; } = true;
    public bool ShowClockWhenIdle { get; set; } = true;
    public bool ShowNextPartWhenIdle { get; set; } = true;
    public bool ShowDelayOnDisplay { get; set; } = false;
    public bool ControllerTopmost { get; set; } = false;

    public double? ControllerLeft { get; set; }
    public double? ControllerTop { get; set; }
    public double? ControllerWidth { get; set; }
    public double? ControllerHeight { get; set; }

    [JsonIgnore]
    public WolLanguage Language
    {
        get => WolLanguage.Presets.FirstOrDefault(p => p.Code == WolCode && p.Rsconf == WolRsconf && p.Lib == WolLib)
               ?? new WolLanguage(WolCode, WolCode, WolRsconf, WolLib);
        set { WolCode = value.Code; WolRsconf = value.Rsconf; WolLib = value.Lib; }
    }
}
