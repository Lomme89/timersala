using TimerSala.Core.Audio;
using TimerSala.Core.Models;
using TimerSala.Core.Timing;

namespace TimerSala.Tests;

public class VoiceStartTests
{
    const int Rate = 48000;
    static readonly DateTimeOffset T0 = new(2026, 10, 7, 19, 0, 0, TimeSpan.Zero);

    /// <summary>Costruisce un segnale a pezzi e lo fa passare per rilevatore e trigger.</summary>
    sealed class Scene
    {
        readonly List<float> _samples = [];
        readonly Random _rng = new(42);

        public double Seconds => (double)_samples.Count / Rate;

        /// <summary>Parlato simulato: armoniche di 140 Hz modulate a ritmo di sillaba.</summary>
        public Scene Speech(double seconds, double amplitude = 0.2)
        {
            int n = (int)(seconds * Rate);
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                double syllable = 0.6 + 0.4 * Math.Sin(2 * Math.PI * 4 * t);
                double v = 0;
                for (int h = 1; h <= 20; h++) v += Math.Sin(2 * Math.PI * 140 * h * t + h) / h;
                _samples.Add((float)(amplitude * syllable * v / 2 + Noise()));
            }
            return this;
        }

        public Scene Silence(double seconds)
        {
            int n = (int)(seconds * Rate);
            for (int i = 0; i < n; i++) _samples.Add((float)Noise());
            return this;
        }

        /// <summary>Colpo sul microfono: forte, breve e quasi tutto sotto i 100 Hz.</summary>
        public Scene Bump(double seconds = 0.08)
        {
            int n = (int)(seconds * Rate);
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                _samples.Add((float)(0.8 * Math.Exp(-t * 30) * Math.Sin(2 * Math.PI * 45 * t) + Noise()));
            }
            return this;
        }

        /// <summary>Schiocco a banda larga, breve.</summary>
        public Scene Click()
        {
            for (int i = 0; i < Rate / 50; i++) _samples.Add((float)((_rng.NextDouble() * 2 - 1) * 0.6));
            return this;
        }

        double Noise() => (_rng.NextDouble() * 2 - 1) * 0.0008; // circa −62 dBFS

        /// <summary>Esegue il segnale; <paramref name="armAt"/> in secondi. Restituisce l'istante di avvio in secondi.</summary>
        public double? Run(double armAt, VoiceTrigger? trigger = null)
        {
            var detector = new VoiceDetector(Rate);
            trigger ??= new VoiceTrigger();
            int armSample = (int)(armAt * Rate);
            bool armed = false;
            const int block = 480 * 4; // come i buffer di WASAPI
            for (int offset = 0; offset < _samples.Count; offset += block)
            {
                if (!armed && offset >= armSample) { trigger.Arm(); armed = true; }
                var chunk = _samples.Skip(offset).Take(block).ToArray();
                int frameStart = offset;
                double? result = null;
                detector.Process(chunk, (frame, end) =>
                {
                    var start = T0 + TimeSpan.FromSeconds((double)frameStart / Rate);
                    frameStart = offset + end;
                    if (result is null && trigger.Feed(frame.IsVoice, start, detector.FrameDuration) is { } at)
                        result = (at - T0).TotalSeconds;
                });
                if (result is not null) return result;
            }
            return null;
        }
    }

    [Fact]
    public void Starts_at_the_first_voice_after_the_pause()
    {
        // il presidente parla, si mette in attesa mentre parla, pausa di 5 s, poi l'oratore
        var at = new Scene().Speech(3).Silence(0.3).Speech(2).Silence(5).Speech(3).Run(armAt: 1.5);
        Assert.NotNull(at);
        Assert.InRange(at!.Value, 10.3 - 0.06, 10.3 + 0.06); // retrodatato all'inizio della voce
    }

    [Fact]
    public void Short_pauses_of_the_chairman_do_not_start_the_part()
    {
        var at = new Scene().Speech(2).Silence(1).Speech(2).Silence(1.2).Speech(2).Run(armAt: 0.5);
        Assert.Null(at);
    }

    [Fact]
    public void Bumps_and_clicks_are_ignored()
    {
        var scene = new Scene().Speech(2).Silence(1.5).Bump().Silence(1).Click().Silence(2).Speech(2);
        var at = scene.Run(armAt: 1);
        Assert.NotNull(at);
        Assert.InRange(at!.Value, 6.62 - 0.06, 6.62 + 0.06);
    }

    [Fact]
    public void Pressing_after_a_long_silence_still_waits_for_a_full_pause()
    {
        // microfono muto da tempo, si preme e si parla subito: quella voce vale come presidente
        var scene = new Scene().Silence(5).Speech(1.5).Silence(2.5).Speech(2);
        var at = scene.Run(armAt: 4.8);
        Assert.NotNull(at);
        Assert.InRange(at!.Value, 9 - 0.06, 9 + 0.06); // parte solo dopo la pausa successiva
    }

    [Fact]
    public void Pressing_in_silence_and_staying_silent_then_speaking_starts_after_the_pause()
    {
        var at = new Scene().Silence(5.5).Speech(2).Run(armAt: 3);
        Assert.NotNull(at);
        Assert.InRange(at!.Value, 5.5 - 0.06, 5.5 + 0.06);
    }

    [Fact]
    public void Arming_in_a_brief_silence_of_the_chairman_measures_the_pause_from_the_press()
    {
        // il presidente tace 0,5 s, si preme, tace altri 1,7 s (2,2 s in tutto) e riprende a parlare:
        // dal momento della pressione sono solo 1,7 s, meno della pausa di 2 s
        var scene = new Scene().Speech(2).Silence(2.2).Speech(2).Silence(3).Speech(2);
        var at = scene.Run(armAt: 2.5);
        Assert.NotNull(at);
        Assert.InRange(at!.Value, 9.2 - 0.06, 9.2 + 0.06); // parte l'oratore, non il presidente
    }

    [Fact]
    public void Quiet_voice_below_threshold_does_not_start()
    {
        var at = new Scene().Silence(3).Speech(2, amplitude: 0.002).Run(armAt: 0);
        Assert.Null(at);
    }

    [Fact]
    public void Pause_length_is_configurable()
    {
        var scene = new Scene().Speech(2).Silence(3).Speech(2);
        Assert.NotNull(scene.Run(0, new VoiceTrigger { Pause = TimeSpan.FromSeconds(2.5) }));
        Assert.Null(scene.Run(0, new VoiceTrigger { Pause = TimeSpan.FromSeconds(4) }));
    }

    [Fact]
    public void Level_reflects_the_signal()
    {
        var detector = new VoiceDetector(Rate);
        var levels = new List<double>();
        var samples = new float[Rate / 10];
        for (int i = 0; i < samples.Length; i++) samples[i] = (float)(0.5 * Math.Sin(2 * Math.PI * 1000 * i / Rate));
        detector.Process(samples, (f, _) => levels.Add(f.LevelDb));
        Assert.InRange(levels[^1], -7.5, -4.5); // seno a 0,5 ≈ −6 dBFS
    }

    [Fact]
    public void Timer_start_can_be_backdated_and_cancelled()
    {
        var clock = new FakeClock(T0);
        var t = new MeetingTimer(clock) { AutoAdvance = true };
        t.LoadMeeting(MeetingTemplates.DefaultMidweek());
        t.Select(1);

        t.Start(T0 - TimeSpan.FromSeconds(1.5));
        Assert.Equal("09:59", t.GetSnapshot().Display);

        t.CancelStart();
        Assert.False(t.IsRunning);
        Assert.Equal(1, t.SelectedIndex);
        Assert.Null(t.ActualFor(1));

        t.Start(T0 + TimeSpan.FromSeconds(5)); // nel futuro: si usa l'ora attuale
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("09:59", t.GetSnapshot().Display);
    }
}
