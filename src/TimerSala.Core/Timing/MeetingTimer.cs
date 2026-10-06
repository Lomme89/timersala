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

    // ultima fermata, per «Annulla» nei secondi successivi
    sealed record StopUndo(TimerMode Mode, int RunningIndex, int Selected, DateTimeOffset StartedAt, TimeSpan Carried,
        int TargetSeconds, string ManualTitle, TimeSpan? PreviousActual, DateTimeOffset StoppedAt);
    StopUndo? _lastStop;

    /// <summary>Per quanto tempo dopo Ferma si può annullare la fermata.</summary>
    public static readonly TimeSpan UndoStopWindow = TimeSpan.FromSeconds(5);

    public MeetingTimer(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    /// <summary>Soglia (secondi rimanenti) sotto la quale il timer diventa giallo.</summary>
    public int WarningSeconds { get; set; } = 60;

    public int CounselSeconds { get; set; } = 60;

    DateTimeOffset? _meetingStart;
    int _countdownLeadSeconds = 300;
    bool _forceCountdown;
    DateTimeOffset? _previewStart, _previewUntil;

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

    /// <summary>
    /// Anteprima dello stile: per <paramref name="duration"/> mostra un conto alla rovescia
    /// come se l'adunanza iniziasse tra <paramref name="startsIn"/>. Non tocca l'orario vero.
    /// </summary>
    public void PreviewCountdown(TimeSpan startsIn, TimeSpan duration)
    {
        lock (_lock)
        {
            var now = _clock.GetUtcNow();
            _previewStart = now + startsIn;
            _previewUntil = now + duration;
        }
        Raise();
    }

    public bool IsPreviewingCountdown { get { lock (_lock) return _previewUntil is { } u && _clock.GetUtcNow() < u; } }

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
            _lastStop = null;
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

    /// <summary>
    /// Avvia la parte selezionata (riprende se era già stata cronometrata).
    /// <paramref name="startedAt"/> retrodata l'avvio, per esempio all'istante in cui è iniziata la voce.
    /// </summary>
    public void Start(DateTimeOffset? startedAt = null)
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
            var now = _clock.GetUtcNow();
            _startedAt = startedAt is { } at && at < now ? at : now;
            _forceCountdown = false;
            _lastStop = null;
        }
        Raise();
    }

    /// <summary>Annulla l'avvio della parte in corso: si ferma senza registrare il tempo.</summary>
    public void CancelStart()
    {
        lock (_lock)
        {
            if (_startedAt is null || _mode != TimerMode.Part) return;
            int index = _runningIndex;
            StopInternal(record: false);
            _lastStop = null;
            if (!_actual.ContainsKey(index)) _adaptedTargets.Remove(index);
        }
        Raise();
    }

    public void StartCounsel()
    {
        lock (_lock)
        {
            if (_startedAt is not null) StopInternal(record: true);
            _lastStop = null;
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
            _lastStop = null;
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
            if (_startedAt is not { } started) return;
            var now = _clock.GetUtcNow();
            TimeSpan? previous = _mode == TimerMode.Part && _actual.TryGetValue(_runningIndex, out var t) ? t : null;
            _lastStop = new StopUndo(_mode, _runningIndex, _selected, started, _carried, _targetSeconds, _manualTitle, previous, now);
            StopInternal(record: true);
        }
        Raise();
    }

    /// <summary>Nei secondi dopo Ferma si può annullare la fermata (pressione sbagliata).</summary>
    public bool CanUndoStop { get { lock (_lock) return CanUndoStopUnlocked(); } }

    /// <summary>Quanto resta per annullare l'ultima fermata, o null se non si può.</summary>
    public TimeSpan? UndoStopRemaining
    {
        get { lock (_lock) return CanUndoStopUnlocked() ? UndoStopWindow - (_clock.GetUtcNow() - _lastStop!.StoppedAt) : null; }
    }

    bool CanUndoStopUnlocked() =>
        _lastStop is { } u && _startedAt is null && _clock.GetUtcNow() - u.StoppedAt < UndoStopWindow;

    /// <summary>
    /// Annulla l'ultima fermata: il timer riprende come se non fosse mai stato fermato,
    /// contando anche i secondi trascorsi nel frattempo (l'oratore è andato avanti).
    /// </summary>
    public bool UndoStop()
    {
        lock (_lock)
        {
            if (!CanUndoStopUnlocked()) return false;
            var u = _lastStop!;
            _lastStop = null;
            if (u.Mode == TimerMode.Part)
            {
                if (u.PreviousActual is { } prev) _actual[u.RunningIndex] = prev;
                else _actual.Remove(u.RunningIndex);
            }
            _mode = u.Mode;
            _runningIndex = u.RunningIndex;
            _selected = u.Selected;
            _startedAt = u.StartedAt;
            _carried = u.Carried;
            _targetSeconds = u.TargetSeconds;
            _manualTitle = u.ManualTitle;
        }
        Raise();
        return true;
    }

    /// <summary>Rinuncia alla possibilità di annullare l'ultima fermata (è seguita un'altra azione).</summary>
    public void ForgetStop()
    {
        lock (_lock) _lastStop = null;
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
            _lastStop = null;
        }
        Raise();
    }

    public void ResetAll()
    {
        lock (_lock)
        {
            StopInternal(record: false);
            _lastStop = null;
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

    // ───── imprevisti durante l'adunanza (menu della parte) ─────

    /// <summary>Istantanea di schema e tempi, per annullare una modifica fatta durante l'adunanza.</summary>
    public sealed record EditSnapshot(List<MeetingPart> Parts, TimerState State, int Selected);

    public EditSnapshot Snapshot()
    {
        lock (_lock) return new EditSnapshot(_meeting.Parts.Select(p => p.Clone()).ToList(), ExportStateUnlocked(), _selected);
    }

    /// <summary>Riporta schema e tempi all'istantanea; una parte in corso continua senza perdere tempo.</summary>
    public void Restore(EditSnapshot snapshot)
    {
        lock (_lock)
        {
            _meeting.Parts.Clear();
            _meeting.Parts.AddRange(snapshot.Parts.Select(p => p.Clone()));
        }
        ImportState(snapshot.State, countDowntime: true);
        lock (_lock)
        {
            if (_startedAt is null && snapshot.Selected >= 0 && snapshot.Selected < _meeting.Parts.Count) _selected = snapshot.Selected;
        }
        Raise();
    }

    /// <summary>Le modifiche dal menu della parte valgono solo con le parti normali (non durante consiglio o timer libero).</summary>
    public bool CanEditParts { get { lock (_lock) return _startedAt is null || _mode == TimerMode.Part; } }

    /// <summary>Cambia titolo e durata di una parte (anche quella in corso: il tempo assegnato si aggiorna).</summary>
    public bool EditPart(int index, string title, int durationSeconds)
    {
        lock (_lock)
        {
            if (!ValidTimed(index) || !CanEditPartsUnlocked() || durationSeconds <= 0) return false;
            var part = _meeting.Parts[index];
            part.Title = string.IsNullOrWhiteSpace(title) ? part.Title : title.Trim();
            part.DurationSeconds = durationSeconds;
            _adaptedTargets.Remove(index);
            if (_startedAt is not null && _runningIndex == index) _targetSeconds = TargetForUnlocked(index);
        }
        Raise();
        return true;
    }

    /// <summary>La parte successiva cronometrata, o -1.</summary>
    public int NextTimedAfter(int index) { lock (_lock) return FirstTimedFrom(index + 1); }

    /// <summary>Scambia la parte con la successiva (per esempio se un oratore non è ancora pronto). I cantici in mezzo restano dove sono.</summary>
    public bool MoveAfterNext(int index)
    {
        lock (_lock)
        {
            if (!ValidTimed(index) || !CanEditPartsUnlocked()) return false;
            int next = FirstTimedFrom(index + 1);
            if (next < 0 || IsRunningUnlocked(index) || IsRunningUnlocked(next)) return false;
            (_meeting.Parts[index], _meeting.Parts[next]) = (_meeting.Parts[next], _meeting.Parts[index]);
            Swap(_actual, index, next);
            Swap(_adaptedTargets, index, next);
            bool a = _adaptive.Remove(index), b = _adaptive.Remove(next);
            if (a) _adaptive.Add(next);
            if (b) _adaptive.Add(index);
            // la selezione resta sulla posizione: si parte con quella che ora viene prima
        }
        Raise();
        return true;
    }

    /// <summary>
    /// Salta una parte (per esempio uno studente assente): conta come durata zero, così il ritardo
    /// ne tiene conto, e si passa alla successiva.
    /// </summary>
    public bool Skip(int index)
    {
        lock (_lock)
        {
            if (!ValidTimed(index) || !CanEditPartsUnlocked() || IsRunningUnlocked(index)) return false;
            _actual[index] = TimeSpan.Zero;
            _adaptedTargets.Remove(index);
            if (_selected == index && _startedAt is null)
            {
                int next = FirstTimedFrom(index + 1);
                if (next >= 0) _selected = next;
            }
            _lastStop = null;
        }
        Raise();
        return true;
    }

    /// <summary>La parte è stata saltata (tempo registrato zero).</summary>
    public bool IsSkipped(int index) { lock (_lock) return _actual.TryGetValue(index, out var t) && t == TimeSpan.Zero; }

    bool ValidTimed(int index) => index >= 0 && index < _meeting.Parts.Count && _meeting.Parts[index].IsTimed;

    bool CanEditPartsUnlocked() => _startedAt is null || _mode == TimerMode.Part;

    bool IsRunningUnlocked(int index) => _startedAt is not null && _mode == TimerMode.Part && _runningIndex == index;

    static void Swap<T>(Dictionary<int, T> d, int a, int b)
    {
        bool hasA = d.Remove(a, out var va), hasB = d.Remove(b, out var vb);
        if (hasA) d[b] = va!;
        if (hasB) d[a] = vb!;
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
        lock (_lock) return ExportStateUnlocked();
    }

    TimerState ExportStateUnlocked()
    {
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
            _lastStop = null;
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

            bool preview = _previewUntil is { } until && now < until && _previewStart is not null;
            var countdownStart = preview ? _previewStart : _meetingStart;
            if (_startedAt is null && countdownStart is { } start && now < start &&
                (preview || _forceCountdown || (_countdownLeadSeconds > 0 && start - now <= TimeSpan.FromSeconds(_countdownLeadSeconds))))
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
                    StartsAt = TimeZoneInfo.ConvertTime(start, _clock.LocalTimeZone),
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
