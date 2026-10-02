using TimerSala.Core.Models;

namespace TimerSala.Core.Timing;

/// <summary>
/// Motore del timer. Thread-safe: il server web legge gli snapshot da altri thread.
/// Non ha un proprio timer: chi lo usa chiama <see cref="GetSnapshot"/> periodicamente.
/// </summary>
public sealed class MeetingTimer
{
    readonly object _lock = new();
    readonly TimeProvider _clock;

    Meeting _meeting = new();
    int _selected = -1;
    readonly Dictionary<int, TimeSpan> _actual = [];

    // timer corrente
    TimerMode _mode = TimerMode.Part;
    int _runningIndex = -1;
    DateTimeOffset? _startedAt;
    TimeSpan _carried;            // tempo già trascorso prima dell'ultimo avvio (ripresa)
    readonly HashSet<int> _adaptive = [];
    readonly Dictionary<int, int> _adaptedTargets = [];   // durata adattata fissata all'avvio della parte
    int _targetSeconds;
    string _manualTitle = "";

    public MeetingTimer(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    /// <summary>Soglia (secondi rimanenti) sotto la quale il timer diventa giallo.</summary>
    public int WarningSeconds { get; set; } = 60;

    public int CounselSeconds { get; set; } = 60;

    DateTimeOffset? _meetingStart;
    int _countdownLeadSeconds = 300;
    bool _forceCountdown;

    /// <summary>Orario di inizio dell'adunanza corrente (per il conto alla rovescia).</summary>
    public DateTimeOffset? MeetingStart
    {
        get { lock (_lock) return _meetingStart; }
        set { lock (_lock) _meetingStart = value; }
    }

    /// <summary>Quanti secondi prima dell'inizio compare il conto alla rovescia (0 = disattivato).</summary>
    public int CountdownLeadSeconds
    {
        get { lock (_lock) return _countdownLeadSeconds; }
        set { lock (_lock) _countdownLeadSeconds = Math.Max(0, value); }
    }

    /// <summary>Mostra il conto alla rovescia fino all'inizio anche prima del preavviso.</summary>
    public bool ForceCountdown
    {
        get { lock (_lock) return _forceCountdown; }
        set
        {
            lock (_lock) _forceCountdown = value;
            Raise();
        }
    }

    /// <summary>Alla fermata seleziona automaticamente la parte successiva.</summary>
    public bool AutoAdvance { get; set; } = true;

    /// <summary>Generato quando cambia lo stato (non ad ogni secondo).</summary>
    public event EventHandler? StateChanged;

    public Meeting Meeting { get { lock (_lock) return _meeting; } }

    public int SelectedIndex { get { lock (_lock) return _selected; } }

    public bool IsRunning { get { lock (_lock) return _startedAt is not null; } }

    public TimerMode Mode { get { lock (_lock) return _mode; } }

    public int RunningIndex { get { lock (_lock) return _startedAt is null ? -1 : _runningIndex; } }

    public TimeSpan? ActualFor(int index) { lock (_lock) return _actual.TryGetValue(index, out var t) ? t : null; }

    public void LoadMeeting(Meeting meeting, bool keepProgress = false)
    {
        lock (_lock)
        {
            _meeting = meeting;
            StopInternal(record: false);
            if (!keepProgress)
            {
                _actual.Clear();
                _adaptedTargets.Clear();
            }
            _selected = FirstTimedFrom(0);
        }
        Raise();
    }

    public void Select(int index)
    {
        lock (_lock)
        {
            if (index < 0 || index >= _meeting.Parts.Count || !_meeting.Parts[index].IsTimed) return;
            if (_startedAt is not null && _mode == TimerMode.Part && _runningIndex == index) return;
            _selected = index;
        }
        Raise();
    }

    public void SelectNext() => Move(+1);

    public void SelectPrevious() => Move(-1);

    void Move(int dir)
    {
        lock (_lock)
        {
            if (_startedAt is not null) return;
            for (int i = _selected + dir; i >= 0 && i < _meeting.Parts.Count; i += dir)
                if (_meeting.Parts[i].IsTimed) { _selected = i; break; }
        }
        Raise();
    }

    /// <summary>Avvia la parte selezionata (riprende se era già stata cronometrata).</summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_startedAt is not null) return;
            if (_selected < 0 || _selected >= _meeting.Parts.Count) return;
            var part = _meeting.Parts[_selected];
            _mode = TimerMode.Part;
            _runningIndex = _selected;
            _targetSeconds = TargetForUnlocked(_selected);
            if (_adaptive.Contains(_selected)) _adaptedTargets[_selected] = _targetSeconds;
            _carried = _actual.TryGetValue(_selected, out var t) ? t : TimeSpan.Zero;
            _startedAt = _clock.GetUtcNow();
            _forceCountdown = false;
        }
        Raise();
    }

    public void StartCounsel()
    {
        lock (_lock)
        {
            if (_startedAt is not null) StopInternal(record: true);
            _mode = TimerMode.Counsel;
            _targetSeconds = CounselSeconds;
            _carried = TimeSpan.Zero;
            _startedAt = _clock.GetUtcNow();
            _forceCountdown = false;
        }
        Raise();
    }

    public void StartManual(TimeSpan duration, string title)
    {
        lock (_lock)
        {
            if (_startedAt is not null) StopInternal(record: true);
            _mode = TimerMode.Manual;
            _manualTitle = title;
            _targetSeconds = (int)duration.TotalSeconds;
            _carried = TimeSpan.Zero;
            _startedAt = _clock.GetUtcNow();
        }
        Raise();
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_startedAt is null) return;
            StopInternal(record: true);
        }
        Raise();
    }

    public void Toggle()
    {
        if (IsRunning) Stop(); else Start();
    }

    /// <summary>Aggiunge/toglie tempo al timer in corso (o alla parte selezionata se fermo).</summary>
    public void AdjustTarget(int deltaSeconds)
    {
        lock (_lock)
        {
            if (_startedAt is not null)
            {
                _targetSeconds = Math.Max(0, _targetSeconds + deltaSeconds);
            }
            else if (_selected >= 0)
            {
                var p = _meeting.Parts[_selected];
                p.DurationSeconds = Math.Max(60, p.DurationSeconds + deltaSeconds);
            }
        }
        Raise();
    }

    /// <summary>Azzera il tempo registrato della parte selezionata.</summary>
    public void ResetSelected()
    {
        lock (_lock)
        {
            if (_startedAt is not null && _runningIndex == _selected && _mode == TimerMode.Part) return;
            _actual.Remove(_selected);
            _adaptedTargets.Remove(_selected);
        }
        Raise();
    }

    public void ResetAll()
    {
        lock (_lock)
        {
            StopInternal(record: false);
            _actual.Clear();
            _adaptedTargets.Clear();
            _selected = FirstTimedFrom(0);
        }
        Raise();
    }

    void StopInternal(bool record)
    {
        if (_startedAt is null) return;
        var elapsed = _carried + (_clock.GetUtcNow() - _startedAt.Value);
        _startedAt = null;
        if (_mode == TimerMode.Part && _runningIndex >= 0)
        {
            if (record) _actual[_runningIndex] = elapsed;
            if (AutoAdvance && record && _selected == _runningIndex)
            {
                int next = FirstTimedFrom(_runningIndex + 1);
                if (next >= 0) _selected = next;
            }
        }
        _mode = TimerMode.Part;
    }

    int FirstTimedFrom(int start)
    {
        for (int i = start; i < _meeting.Parts.Count; i++)
            if (_meeting.Parts[i].IsTimed) return i;
        return -1;
    }

    // ───── durate adattive (studio biblico / Torre di Guardia) ─────

    /// <summary>Parti la cui durata si adatta al ritardo accumulato per finire in orario.</summary>
    public void SetAdaptiveParts(IEnumerable<int> indices)
    {
        lock (_lock)
        {
            _adaptive.Clear();
            foreach (var i in indices) _adaptive.Add(i);
        }
        Raise();
    }

    /// <summary>Durata adattata della parte, o null se non è adattiva o coincide con quella prevista.</summary>
    public int? AdaptedTargetFor(int index)
    {
        lock (_lock)
        {
            if (!_adaptive.Contains(index) || index < 0 || index >= _meeting.Parts.Count) return null;
            int target = TargetForUnlocked(index);
            return target == _meeting.Parts[index].DurationSeconds ? null : target;
        }
    }

    /// <summary>
    /// Calcola la durata adattata: la parte si accorcia per recuperare il ritardo accumulato
    /// (mai più della metà), ma non si allunga mai oltre la durata prevista.
    /// </summary>
    public static int Adapt(int plannedSeconds, int delaySeconds)
    {
        int target = plannedSeconds - Math.Max(0, delaySeconds);
        target = Math.Clamp(target, plannedSeconds / 2, plannedSeconds);
        return (int)(Math.Round(target / 15.0) * 15); // a passi di 15 secondi
    }

    int TargetForUnlocked(int index)
    {
        int planned = _meeting.Parts[index].DurationSeconds;
        if (!_adaptive.Contains(index)) return planned;
        if (_adaptedTargets.TryGetValue(index, out var fixedTarget)) return fixedTarget;
        return Adapt(planned, RecordedDelayUnlocked());
    }

    int RecordedDelayUnlocked()
    {
        int delay = 0;
        foreach (var (idx, t) in _actual)
            if (idx < _meeting.Parts.Count)
                delay += (int)Math.Round(t.TotalSeconds) - _meeting.Parts[idx].DurationSeconds;
        return delay;
    }

    // ───── salvataggio e ripristino ─────

    public TimerState ExportState()
    {
        lock (_lock)
        {
            var state = new TimerState
            {
                SelectedIndex = _selected,
                ActualSeconds = _actual.ToDictionary(kv => kv.Key, kv => kv.Value.TotalSeconds),
                AdaptedTargets = new Dictionary<int, int>(_adaptedTargets),
                SavedAt = _clock.GetUtcNow(),
            };
            if (_startedAt is not null && _mode == TimerMode.Part)
            {
                state.Running = true;
                state.RunningIndex = _runningIndex;
                state.StartedAtUtc = _startedAt;
                state.CarriedSeconds = _carried.TotalSeconds;
                state.TargetSeconds = _targetSeconds;
            }
            return state;
        }
    }

    /// <summary>
    /// Ripristina uno stato salvato. Con <paramref name="countDowntime"/> il tempo in cui il programma è rimasto
    /// chiuso viene conteggiato (l'adunanza è andata avanti); altrimenti il timer riparte da dove era.
    /// </summary>
    public void ImportState(TimerState state, bool countDowntime)
    {
        lock (_lock)
        {
            StopInternal(record: false);
            _actual.Clear();
            foreach (var (i, sec) in state.ActualSeconds)
                if (i >= 0 && i < _meeting.Parts.Count) _actual[i] = TimeSpan.FromSeconds(sec);
            _adaptedTargets.Clear();
            foreach (var (i, t) in state.AdaptedTargets) _adaptedTargets[i] = t;
            if (state.SelectedIndex >= 0 && state.SelectedIndex < _meeting.Parts.Count) _selected = state.SelectedIndex;

            if (state.Running && state.StartedAtUtc is { } started && state.RunningIndex >= 0 && state.RunningIndex < _meeting.Parts.Count)
            {
                _mode = TimerMode.Part;
                _runningIndex = state.RunningIndex;
                _selected = state.RunningIndex;
                _targetSeconds = state.TargetSeconds;
                if (countDowntime)
                {
                    _carried = TimeSpan.FromSeconds(state.CarriedSeconds);
                    _startedAt = started;
                }
                else
                {
                    _carried = TimeSpan.FromSeconds(state.CarriedSeconds) + (state.SavedAt - started);
                    _startedAt = _clock.GetUtcNow();
                }
            }
        }
        Raise();
    }

    public TimerSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            var now = _clock.GetLocalNow();
            int delay = 0;
            foreach (var (idx, t) in _actual)
                if (idx < _meeting.Parts.Count && !(_startedAt is not null && _mode == TimerMode.Part && idx == _runningIndex))
                    delay += (int)Math.Round(t.TotalSeconds) - _meeting.Parts[idx].DurationSeconds;

            string? NextFrom(int from)
            {
                int n = FirstTimedFrom(from);
                return n >= 0 ? _meeting.Parts[n].Title : null;
            }

            if (_startedAt is null && _meetingStart is { } start && now < start &&
                (_forceCountdown || (_countdownLeadSeconds > 0 && start - now <= TimeSpan.FromSeconds(_countdownLeadSeconds))))
            {
                double remainingToStart = (start - now).TotalSeconds;
                int target = Math.Max(_countdownLeadSeconds, (int)Math.Ceiling(remainingToStart));
                return new TimerSnapshot
                {
                    Phase = remainingToStart <= 60 ? TimerPhase.Warning : TimerPhase.Normal,
                    Mode = TimerMode.Countdown,
                    Title = "L'adunanza inizia tra",
                    TargetSeconds = target,
                    ElapsedSeconds = target - remainingToStart,
                    NextTitle = _selected >= 0 ? _meeting.Parts[_selected].Title : null,
                    MeetingTitle = _meeting.Title,
                    DelaySeconds = delay,
                    Now = now,
                };
            }

            if (_startedAt is null)
            {
                return new TimerSnapshot
                {
                    Phase = TimerPhase.Idle,
                    Mode = TimerMode.Part,
                    Title = _selected >= 0 ? _meeting.Parts[_selected].Title : "",
                    TargetSeconds = _selected >= 0 ? TargetForUnlocked(_selected) : 0,
                    NextTitle = _selected >= 0 ? _meeting.Parts[_selected].Title : null,
                    MeetingTitle = _meeting.Title,
                    DelaySeconds = delay,
                    Now = now,
                };
            }

            var elapsed = (_carried + (_clock.GetUtcNow() - _startedAt.Value)).TotalSeconds;
            double remaining = _targetSeconds - elapsed;
            var phase = remaining < 0 ? TimerPhase.Overtime
                : remaining <= WarningSeconds ? TimerPhase.Warning
                : TimerPhase.Normal;

            if (_mode == TimerMode.Part && remaining < 0) delay += (int)Math.Round(-remaining);

            MeetingPart? part = _mode == TimerMode.Part ? _meeting.Parts[_runningIndex] : null;
            return new TimerSnapshot
            {
                Phase = phase,
                Mode = _mode,
                IsRunning = true,
                Title = _mode switch
                {
                    TimerMode.Counsel => "Consiglio",
                    TimerMode.Manual => _manualTitle,
                    _ => part!.Title,
                },
                Section = part?.Section.ToString(),
                TargetSeconds = _targetSeconds,
                ElapsedSeconds = elapsed,
                NextTitle = _mode == TimerMode.Part ? NextFrom(_runningIndex + 1) : (_selected >= 0 ? _meeting.Parts[_selected].Title : null),
                MeetingTitle = _meeting.Title,
                DelaySeconds = delay,
                Now = now,
            };
        }
    }

    void Raise() => StateChanged?.Invoke(this, EventArgs.Empty);
}
