using System.Globalization;
using System.Windows.Input;
using TimerSala.Core.Models;
using TimerSala.Core.Timing;

namespace TimerSala.App.ViewModels;

/// <summary>Controller 2.0: settimana in una riga, riquadro del tempo più ricco, rete nell'intestazione.</summary>
public sealed partial class MainViewModel
{
    static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    /// <summary>Riquadro completo della settimana (frecce, download, adunanza, sorvegliante). Si chiude all'avvio di una parte.</summary>
    public bool WeekPanelOpen { get; set => Set(ref field, value); }

    public ICommand ToggleWeekPanelCommand => field ??= new RelayCommand(() => WeekPanelOpen = !WeekPanelOpen);

    /// <summary>«Dom 4 ott» · «Fine settimana» · «Geremia 38-39».</summary>
    public string MeetingDayText { get; private set => Set(ref field, value); } = "";
    public string MeetingLineText { get; private set => Set(ref field, value); } = "";

    // riquadro del tempo
    public string CardStartText { get; private set => Set(ref field, value); } = "";
    public string CardEndText { get; private set => Set(ref field, value); } = "";
    public string? CardNextTitle { get; private set => Set(ref field, value); }
    public string CardNextLabel { get; private set => Set(ref field, value); } = "Dopo";

    // rete
    public int ConnectedDevices { get; private set => Set(ref field, value); }
    public string ConnectedDevicesText { get; private set => Set(ref field, value); } = "";

    bool _wasRunning;

    void UpdateMeetingLine()
    {
        var date = Settings.StartOf(Kind, Week.WeekStart);
        var day = date.ToString("ddd d MMM", It).Replace(".", "");
        MeetingDayText = char.ToUpper(day[0], It) + day[1..];
        var bits = new List<string> { Kind == MeetingKind.Midweek ? "Infrasettimanale" : "Fine settimana" };
        if (!string.IsNullOrWhiteSpace(Week.BibleReading)) bits.Add(It.TextInfo.ToTitleCase(Week.BibleReading!.ToLower(It)));
        if (Week.CircuitOverseerVisit) bits.Add("sorvegliante");
        MeetingLineText = string.Join(" · ", bits);
    }

    /// <summary>Chiamata a ogni aggiornamento del display.</summary>
    void RefreshControllerCard(TimerSnapshot s)
    {
        if (s.IsRunning && !_wasRunning) WeekPanelOpen = false;
        _wasRunning = s.IsRunning;

        if (Timer.MeetingStart is { } start)
        {
            var end = start.AddMinutes(Settings.MeetingLengthMinutes).AddSeconds(Math.Max(0, s.DelaySeconds));
            CardStartText = $"Inizio {start:HH:mm}";
            CardEndText = end.ToString("HH:mm");
        }
        if (s.Mode == TimerMode.Countdown)
        {
            CardNextLabel = "Prima parte";
            CardNextTitle = s.NextTitle;
        }
        else if (s.IsRunning)
        {
            CardNextLabel = "Dopo";
            CardNextTitle = s.NextTitle;
        }
        else
        {
            CardNextLabel = "Prossima";
            CardNextTitle = null;
        }

        int devices = _web.ConnectedClients;
        if (devices != ConnectedDevices || ConnectedDevicesText.Length == 0)
        {
            ConnectedDevices = devices;
            ConnectedDevicesText = devices switch
            {
                0 => "Nessun dispositivo collegato",
                1 => "1 dispositivo collegato",
                _ => $"{devices} dispositivi collegati",
            };
        }
    }

    /// <summary>Orari previsti delle parti, dallo schema.</summary>
    void UpdatePlannedStarts()
    {
        if (Timer.MeetingStart is not { } start) return;
        var starts = MeetingPlan.PlannedStarts(Timer.Meeting, start.DateTime, Settings.MeetingLengthMinutes);
        foreach (var p in Parts)
            p.PlannedStartText = p.Index < starts.Count ? starts[p.Index].ToString("HH:mm") : "";
    }
}
