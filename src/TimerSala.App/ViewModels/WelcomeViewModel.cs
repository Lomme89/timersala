using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using TimerSala.App.Interop;
using TimerSala.Core.Storage;

namespace TimerSala.App.ViewModels;

/// <summary>Primo avvio guidato: schermo della sala, orari, prova del telefono, collegamento sul desktop.</summary>
public sealed class WelcomeViewModel : ObservableObject
{
    static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    public const int LastStep = 3;

    readonly MainViewModel _main;

    public WelcomeViewModel(MainViewModel main)
    {
        _main = main;
        var s = main.Settings;
        Days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }
            .Select(d => new Choice<DayOfWeek>(d, It.TextInfo.ToTitleCase(It.DateTimeFormat.GetDayName(d)))).ToList();
        MidweekDay = s.MidweekDay;
        MidweekTime = s.MidweekTime;
        WeekendDay = s.WeekendDay;
        WeekendTime = s.WeekendTime;
        StartWithWindows = s.StartWithWindows;
        SelectedMonitor = main.TimerWindowVisible ? main.SelectedMonitor : null;
    }

    public IReadOnlyList<Choice<DayOfWeek>> Days { get; }
    public ObservableCollection<MonitorInfo> Monitors => _main.AvailableMonitors;
    public bool HasSecondScreen => Monitors.Count > 1;

    public int Step
    {
        get;
        set
        {
            if (!Set(ref field, Math.Clamp(value, 0, LastStep))) return;
            OnPropertyChanged(nameof(IsScreenStep));
            OnPropertyChanged(nameof(IsTimesStep));
            OnPropertyChanged(nameof(IsPhoneStep));
            OnPropertyChanged(nameof(IsDoneStep));
            OnPropertyChanged(nameof(IsFirstStep));
            OnPropertyChanged(nameof(NextText));
            OnPropertyChanged(nameof(StepText));
            // il timer in rete può essere partito nel frattempo
            OnPropertyChanged(nameof(WebUrl));
            OnPropertyChanged(nameof(HasWebUrl));
        }
    }

    public bool IsScreenStep => Step == 0;
    public bool IsTimesStep => Step == 1;
    public bool IsPhoneStep => Step == 2;
    public bool IsDoneStep => Step == LastStep;
    public bool IsFirstStep => Step == 0;
    public string NextText => IsDoneStep ? "Inizia" : "Avanti";
    public string StepText => IsDoneStep ? "Pronto" : $"Passo {Step + 1} di {LastStep}";

    /// <summary>Schermo della sala: si accende subito, così si vede dove va il timer.</summary>
    public MonitorInfo? SelectedMonitor
    {
        get;
        set
        {
            if (!Set(ref field, value)) return;
            OnPropertyChanged(nameof(NoScreen));
            if (value is not null)
            {
                _main.SelectedMonitor = value;
                _main.TimerWindowVisible = true;
            }
            else _main.TimerWindowVisible = false;
        }
    }

    public bool NoScreen { get => SelectedMonitor is null; set { if (value) SelectedMonitor = null; } }

    public DayOfWeek MidweekDay { get; set => Set(ref field, value); }
    public TimeOnly? MidweekTime { get; set => Set(ref field, value); }
    public DayOfWeek WeekendDay { get; set => Set(ref field, value); }
    public TimeOnly? WeekendTime { get; set => Set(ref field, value); }

    public string? WebUrl => _main.WebUrl;
    public bool HasWebUrl => _main.WebUrl is not null;

    public bool DesktopShortcut { get; set => Set(ref field, value); } = true;
    public bool StartWithWindows { get; set => Set(ref field, value); }

    /// <summary>Salva le scelte (anche se si chiude prima della fine: quello che si è scelto resta).</summary>
    public void Finish(bool completed)
    {
        var s = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(_main.Settings))!;
        s.MidweekDay = MidweekDay;
        if (MidweekTime is { } mt) s.MidweekTime = mt;
        s.WeekendDay = WeekendDay;
        if (WeekendTime is { } wt) s.WeekendTime = wt;
        s.TimerMonitor = _main.SelectedMonitor?.DeviceName ?? s.TimerMonitor;
        s.TimerWindowVisible = SelectedMonitor is not null;
        s.OnboardingDone = true;
        if (completed)
        {
            s.StartWithWindows = StartWithWindows;
            if (DesktopShortcut) Shortcut.CreateOnDesktop();
        }
        _main.ApplySettings(s);
    }
}
