using TimerSala.Core.Storage;
using TimerSala.Core.Timing;

namespace TimerSala.App.ViewModels;

/// <summary>Conto alla rovescia prima dell'adunanza: valori per gli stili alternativi dello schermo.</summary>
public sealed partial class MainViewModel
{
    public bool ShowCountdownScreen { get; private set => Set(ref field, value); }
    public bool CdRing { get; private set => Set(ref field, value); }
    public bool CdBlocks { get; private set => Set(ref field, value); }
    public bool CdClock { get; private set => Set(ref field, value); }
    public bool CdTide { get; private set => Set(ref field, value); }
    public bool CdDial { get; private set => Set(ref field, value); }
    public bool CdWords { get; private set => Set(ref field, value); }

    /// <summary>Tempo all'inizio, senza zero iniziale («4:32»).</summary>
    public string CdDigits { get; private set => Set(ref field, value); } = "";
    /// <summary>Parte ancora da attendere: 1 all'apparire del countdown, 0 all'inizio.</summary>
    public double CdRemaining { get; private set => Set(ref field, value); } = 1;
    /// <summary>Livello della marea: 0 all'apparire del countdown, 1 all'inizio.</summary>
    public double CdLevel { get; private set => Set(ref field, value); }
    public double CdPhase { get; private set => Set(ref field, value); }
    public int CdBlockCount { get; private set => Set(ref field, value); } = 5;
    public string CdStart { get; private set => Set(ref field, value); } = "";
    public string CdStartLabel { get; private set => Set(ref field, value); } = "";
    public string CdClockSeconds { get; private set => Set(ref field, value); } = "";
    public string CdClockNote { get; private set => Set(ref field, value); } = "";
    public string CdDialBig { get; private set => Set(ref field, value); } = "";
    public string CdDialUnit { get; private set => Set(ref field, value); } = "";
    public int CdDialLit { get; private set => Set(ref field, value); }
    public string CdWordsBig { get; private set => Set(ref field, value); } = "";

    void RefreshCountdown(TimerSnapshot s)
    {
        var style = Settings.CountdownStyle;
        bool on = s.Mode == TimerMode.Countdown && style != CountdownStyle.Classic;
        ShowCountdownScreen = on;
        CdRing = on && style == CountdownStyle.Ring;
        CdBlocks = on && style == CountdownStyle.Blocks;
        CdClock = on && style == CountdownStyle.Clock;
        CdTide = on && style == CountdownStyle.Tide;
        CdDial = on && style == CountdownStyle.Dial;
        CdWords = on && style == CountdownStyle.Words;
        if (!on) return;

        double rem = Math.Max(0, s.RemainingSeconds);
        int secs = (int)Math.Ceiling(rem - 0.0001);
        CdDigits = $"{secs / 60}:{secs % 60:00}";
        CdRemaining = s.TargetSeconds <= 0 ? 0 : Math.Clamp(rem / s.TargetSeconds, 0, 1);
        CdLevel = Math.Max(0.03, 1 - CdRemaining);
        CdPhase = s.Now.TimeOfDay.TotalSeconds * 1.3;
        CdBlockCount = Math.Clamp((int)Math.Ceiling(s.TargetSeconds / 60.0), 1, 10);
        CdStart = s.StartsAt?.ToString("HH:mm") ?? "";
        CdStartLabel = CdStart.Length > 0 ? $"ALLE {CdStart}" : "";

        // a parole: minuti arrotondati per eccesso, i secondi solo nell'ultimo minuto
        CdWordsBig = secs > 60 ? $"{(secs + 59) / 60} MINUTI" : secs == 1 ? "1 SECONDO" : $"{secs} SECONDI";
        CdClockSeconds = ":" + s.Now.ToString("ss");
        CdClockNote = CdStart.Length > 0 ? $"si comincia alle {CdStart} · tra {CdWordsBig.ToLowerInvariant()}" : $"si comincia tra {CdWordsBig.ToLowerInvariant()}";

        // quadrante: minuti al centro, secondi del minuto in corso sulle tacche; nell'ultimo minuto solo i secondi
        if (secs > 60)
        {
            int m = secs / 60, r = secs % 60;
            CdDialBig = (r == 0 ? m - 1 : m).ToString();
            CdDialUnit = (r == 0 ? m - 1 : m) == 1 ? "MINUTO" : "MINUTI";
            CdDialLit = r == 0 ? 60 : r;
        }
        else
        {
            CdDialBig = secs.ToString();
            CdDialUnit = secs == 1 ? "SECONDO" : "SECONDI";
            CdDialLit = secs;
        }
    }

    /// <summary>Mostra per qualche secondo lo stile scelto, come se l'adunanza iniziasse tra 4:32.</summary>
    public void PreviewCountdown()
    {
        Timer.PreviewCountdown(TimeSpan.FromSeconds(272), TimeSpan.FromSeconds(12));
        RefreshDisplay();
    }
}
