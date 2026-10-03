using NAudio.CoreAudioApi;
using NAudio.Wave;
using TimerSala.Core.Audio;

namespace TimerSala.App.Audio;

public sealed record AudioDevice(string Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// Ascolta un ingresso audio di Windows (WASAPI, in condivisione: Zoom può usarlo insieme)
/// e lo passa al rilevatore di voce. Gli eventi arrivano sul thread di cattura.
/// </summary>
public sealed class AudioInput : IDisposable
{
    static readonly Guid IeeeFloat = new("00000003-0000-0010-8000-00aa00389b71"); // KSDATAFORMAT_SUBTYPE_IEEE_FLOAT

    WasapiCapture? _capture;
    VoiceDetector? _detector;
    float[] _mono = [];

    /// <summary>Se impostato, decide quando avviare la parte.</summary>
    public VoiceTrigger? Trigger { get; init; }

    public double ThresholdDb
    {
        get;
        set { field = value; if (_detector is { } d) d.ThresholdDb = value; }
    } = -40;

    /// <summary>Ogni blocco di 20 ms analizzato.</summary>
    public event Action<VoiceFrame>? Frame;

    /// <summary>La voce ha avviato la parte: istante in cui è iniziata.</summary>
    public event Action<DateTimeOffset>? VoiceStarted;

    /// <summary>La cattura si è interrotta (dispositivo scollegato o in errore).</summary>
    public event Action<string>? Failed;

    public string? DeviceId { get; private set; }
    public string? DeviceName { get; private set; }
    public bool IsRunning => _capture is not null;

    public static IReadOnlyList<AudioDevice> Devices()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .Select(d => new AudioDevice(d.ID, d.FriendlyName)).OrderBy(d => d.Name).ToList();
        }
        catch { return []; }
    }

    /// <summary>Avvia la cattura; <paramref name="deviceId"/> vuoto = ingresso predefinito di Windows.</summary>
    public void Start(string? deviceId)
    {
        Stop();
        using var enumerator = new MMDeviceEnumerator();
        var device = string.IsNullOrEmpty(deviceId)
            ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console)
            : enumerator.GetDevice(deviceId);
        if (device.State != DeviceState.Active) throw new InvalidOperationException("L'ingresso audio non è attivo.");

        var capture = new WasapiCapture(device, true, 20);
        _detector = new VoiceDetector(capture.WaveFormat.SampleRate) { ThresholdDb = ThresholdDb };
        capture.DataAvailable += OnData;
        capture.RecordingStopped += (_, e) =>
        {
            if (e.Exception is not null) Failed?.Invoke(e.Exception.Message);
        };
        capture.StartRecording();
        _capture = capture;
        DeviceId = deviceId;
        DeviceName = device.FriendlyName;
    }

    public void Stop()
    {
        var capture = _capture;
        _capture = null;
        if (capture is null) return;
        capture.DataAvailable -= OnData;
        try { capture.StopRecording(); } catch { }
        capture.Dispose();
    }

    public void Dispose() => Stop();

    void OnData(object? sender, WaveInEventArgs e)
    {
        if (sender is not WasapiCapture capture || _detector is not { } detector) return;
        var now = DateTimeOffset.UtcNow;
        int count = ToMono(capture.WaveFormat, e.Buffer, e.BytesRecorded);
        double rate = detector.SampleRate;
        var frameLength = detector.FrameDuration;
        detector.Process(_mono.AsSpan(0, count), (frame, end) =>
        {
            Frame?.Invoke(frame);
            if (Trigger is null) return;
            // il blocco finisce (count - end) campioni prima dell'ultimo arrivato
            var frameStart = now - TimeSpan.FromSeconds((count - end) / rate) - frameLength;
            if (Trigger.Feed(frame.IsVoice, frameStart, frameLength) is { } at) VoiceStarted?.Invoke(at);
        });
    }

    /// <summary>Converte il buffer in campioni mono float in [−1, 1].</summary>
    int ToMono(WaveFormat format, byte[] buffer, int bytes)
    {
        int channels = Math.Max(1, format.Channels);
        int bytesPerSample = format.BitsPerSample / 8;
        int frames = bytes / (bytesPerSample * channels);
        if (_mono.Length < frames) _mono = new float[frames];
        bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat
                       || (format is WaveFormatExtensible ext && ext.SubFormat == IeeeFloat);

        for (int f = 0; f < frames; f++)
        {
            double sum = 0;
            for (int c = 0; c < channels; c++)
            {
                int i = (f * channels + c) * bytesPerSample;
                sum += bytesPerSample switch
                {
                    4 when isFloat => BitConverter.ToSingle(buffer, i),
                    4 => BitConverter.ToInt32(buffer, i) / 2147483648.0,
                    3 => ((buffer[i] << 8 | buffer[i + 1] << 16 | buffer[i + 2] << 24) >> 8) / 8388608.0,
                    2 => BitConverter.ToInt16(buffer, i) / 32768.0,
                    _ => 0,
                };
            }
            _mono[f] = (float)(sum / channels);
        }
        return frames;
    }
}
