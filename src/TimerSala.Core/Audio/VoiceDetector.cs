namespace TimerSala.Core.Audio;

/// <summary>Analisi di un blocco di 20 ms di audio.</summary>
/// <param name="LevelDb">Livello nella banda della voce, in dBFS (da circa −90 a 0).</param>
/// <param name="IsVoice">Il blocco sembra parlato: abbastanza forte e con l'energia nelle frequenze della voce.</param>
public readonly record struct VoiceFrame(double LevelDb, bool IsVoice);

/// <summary>
/// Riconosce il parlato in un flusso audio mono. Filtra la banda della voce (300–3400 Hz),
/// così il ronzio, i colpi bassi sul microfono e il fruscio pesano poco; un blocco vale come
/// voce se supera la soglia e se la maggior parte dell'energia sta in quella banda.
/// </summary>
public sealed class VoiceDetector
{
    public const double FrameSeconds = 0.02;

    /// <summary>Livello minimo della voce, in dBFS.</summary>
    public double ThresholdDb { get; set; } = -40;

    /// <summary>Quota minima di energia nella banda della voce.</summary>
    public double MinBandRatio { get; set; } = 0.15;

    readonly int _frameLength;
    readonly Biquad _highPass, _lowPass;
    int _count;
    double _band, _total;

    public VoiceDetector(int sampleRate)
    {
        SampleRate = sampleRate;
        _frameLength = Math.Max(1, (int)(sampleRate * FrameSeconds));
        _highPass = Biquad.HighPass(sampleRate, 300);
        _lowPass = Biquad.LowPass(sampleRate, 3400);
    }

    public int SampleRate { get; }

    public TimeSpan FrameDuration => TimeSpan.FromSeconds((double)_frameLength / SampleRate);

    /// <summary>Campioni ancora da analizzare (servono per datare i blocchi con precisione).</summary>
    public int PendingSamples => _count;

    /// <summary>Elabora campioni mono in [−1, 1]; <paramref name="onFrame"/> riceve ogni blocco completato e il suo indice nel buffer.</summary>
    public void Process(ReadOnlySpan<float> samples, Action<VoiceFrame, int> onFrame)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            double x = samples[i];
            double y = _lowPass.Process(_highPass.Process(x));
            _total += x * x;
            _band += y * y;
            if (++_count < _frameLength) continue;

            double rms = Math.Sqrt(_band / _count);
            double level = 20 * Math.Log10(rms + 1e-9) + 3; // +3: un'onda sinusoidale a fondo scala vale 0 dB
            double ratio = _total > 1e-12 ? _band / _total : 0;
            onFrame(new VoiceFrame(Math.Max(level, -90), level >= ThresholdDb && ratio >= MinBandRatio), i + 1);
            _count = 0;
            _band = _total = 0;
        }
    }

    /// <summary>Filtro biquadratico (RBJ Audio EQ Cookbook).</summary>
    sealed class Biquad
    {
        readonly double _b0, _b1, _b2, _a1, _a2;
        double _x1, _x2, _y1, _y2;

        Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            _b0 = b0 / a0; _b1 = b1 / a0; _b2 = b2 / a0; _a1 = a1 / a0; _a2 = a2 / a0;
        }

        public static Biquad HighPass(int rate, double freq)
        {
            var (cos, alpha) = Coefficients(rate, freq);
            return new((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }

        public static Biquad LowPass(int rate, double freq)
        {
            var (cos, alpha) = Coefficients(rate, Math.Min(freq, rate * 0.45));
            return new((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }

        static (double Cos, double Alpha) Coefficients(int rate, double freq)
        {
            double w = 2 * Math.PI * freq / rate;
            return (Math.Cos(w), Math.Sin(w) / (2 * Math.Sqrt(0.5)));
        }

        public double Process(double x)
        {
            double y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1; _x1 = x;
            _y2 = _y1; _y1 = y;
            return y;
        }
    }
}
