using TimerSala.Core.Audio;

namespace TimerSala.Tests;

public class VoiceCalibrationTests
{
    static List<double> Levels(double center, double spread, int n, int seed)
    {
        var r = new Random(seed);
        return Enumerable.Range(0, n).Select(_ => center + (r.NextDouble() * 2 - 1) * spread).ToList();
    }

    [Fact]
    public void Threshold_sits_between_room_noise_and_voice()
    {
        var silence = Levels(-58, 3, 100, 1);
        var voice = Levels(-24, 4, 100, 2).Concat(Levels(-58, 3, 30, 3)).ToList(); // con qualche pausa tra le parole
        var r = VoiceCalibration.Compute(silence, voice);
        Assert.NotNull(r.ThresholdDb);
        Assert.InRange(r.ThresholdDb!.Value, -45, -35);
    }

    [Fact]
    public void Too_close_or_no_audio_gives_an_explanation_instead_of_a_threshold()
    {
        Assert.Null(VoiceCalibration.Compute(Levels(-40, 2, 100, 1), Levels(-34, 2, 100, 2)).ThresholdDb);
        Assert.Null(VoiceCalibration.Compute(Levels(-58, 2, 100, 1), Levels(-58, 2, 100, 2)).ThresholdDb);
        Assert.Contains("audio", VoiceCalibration.Compute([], []).Message);
    }
}
