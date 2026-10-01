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
    int _targetSeconds;
    string _manualTitle = "";

    public MeetingTimer(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    /// <summary>Soglia (secondi rimanenti) sotto la quale il timer diventa giallo.</summary>
    public int WarningSeconds { get; set; } = 60;

    public int CounselSeconds { get; set; } = 60;

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
            if (!keepProgress) _actual.Clear();
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
            _targetSeconds = part.DurationSeconds;
            _carried = _actual.TryGetValue(_selected, out var t) ? t : TimeSpan.Zero;
            _startedAt = _clock.GetUtcNow();
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
        }
        Raise();
    }

    public void ResetAll()
    {
        lock (_lock)
        {
            StopInternal(record: false);
            _actual.Clear();
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

            if (_startedAt is null)
            {
                return new TimerSnapshot
                {
                    Phase = TimerPhase.Idle,
                    Mode = TimerMode.Part,
                    Title = _selected >= 0 ? _meeting.Parts[_selected].Title : "",
                    TargetSeconds = _selected >= 0 ? _meeting.Parts[_selected].DurationSeconds : 0,
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
