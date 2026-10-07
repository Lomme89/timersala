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

    /// <summary>«3 min oltre» se lo schema supera la durata dell'adunanza, altrimenti vuoto.</summary>
    public string OverrunText { get; private set => Set(ref field, value); } = "";
    public bool HasOverrun { get; private set => Set(ref field, value); }

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
        if (Settings.IsAssemblyWeek(Week.WeekStart)) bits.Add("assemblea: nessuna adunanza");
        else if (MeetingTemplates.IsMemorial(CurrentMeeting)) bits.Add("Commemorazione");
        if (Kind == MeetingKind.Weekend && Settings.IsSpecialTalkWeek(Week.WeekStart)) bits.Add("discorso speciale");
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
            // a riposo: «Inizio» a sinistra, a specchio di «Fine prevista» (e non ripetuto sopra)
            CardNextLabel = "Inizio";
            CardNextTitle = Timer.MeetingStart?.ToString("HH:mm");
            CardStartText = "";
        }

        int devices = _web.ConnectedClients, controllers = _web.ConnectedControllers;
        if (devices != ConnectedDevices || controllers != _connectedControllers || ConnectedDevicesText.Length == 0)
        {
            ConnectedDevices = devices;
            _connectedControllers = controllers;
            ConnectedDevicesText = devices switch
            {
                0 => "Nessun dispositivo collegato",
                1 => "1 dispositivo collegato",
                _ => $"{devices} dispositivi collegati",
            } + (controllers > 0 ? $" · {controllers} con il controllo" : "");
        }
    }

    int _connectedControllers;

    /// <summary>Orari previsti delle parti, dallo schema.</summary>
    void UpdatePlannedStarts()
    {
        int over = MeetingPlan.OverrunMinutes(Timer.Meeting, Settings.MeetingLengthMinutes);
        OverrunText = over > 0 ? $"{over} min oltre" : "";
        HasOverrun = over > 0;
        if (Timer.MeetingStart is not { } start) return;
        var starts = MeetingPlan.PlannedStarts(Timer.Meeting, start.DateTime, Settings.MeetingLengthMinutes);
        foreach (var p in Parts)
            p.PlannedStartText = p.Index < starts.Count ? starts[p.Index].ToString("HH:mm") : "";
    }
}
