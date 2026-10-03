"""Sonoro procedurale per i video di TimerSala: tutto sintetizzato, sincronizzato agli eventi dell'animazione."""
import json, sys
import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 48000
rng = np.random.default_rng(7)

def env_exp(n, tau):
    return np.exp(-np.arange(n) / (tau * SR))

def butter(x, kind, f, order=4):
    sos = signal.butter(order, f, btype=kind, fs=SR, output='sos')
    return signal.sosfilt(sos, x)

def reverb_ir(seconds=0.9, damp=2500, seed=3):
    r = np.random.default_rng(seed)
    n = int(seconds * SR)
    ir = r.standard_normal(n) * np.exp(-np.arange(n) / (0.16 * SR))
    ir = butter(ir, 'low', damp, 2)
    ir[0] = 0
    return ir / np.sqrt((ir ** 2).sum())   # energia unitaria: il riverbero non gonfia il volume

IR_HALL = reverb_ir()

# ── voce ovattata: parlato sintetico (sorgente glottale + formanti), filtrato come da lontano ──
VOWELS = [(730, 1090, 2440), (530, 1840, 2480), (270, 2290, 3010), (570, 840, 2410), (300, 870, 2240), (660, 1700, 2400)]

def formant_filter(x, f1, f2, f3):
    y = np.zeros_like(x)
    for f, bw, g in ((f1, 90, 1.0), (f2, 120, 0.55), (f3, 160, 0.25)):
        b, a = signal.iirpeak(f, f / bw, fs=SR)
        y += g * signal.lfilter(b, a, x)
    return y

def babble(seconds, f0=112, seed=1):
    r = np.random.default_rng(seed)
    n = int(seconds * SR)
    out = np.zeros(n + SR)
    t = 0.0
    phrase_left = r.integers(5, 10)
    phrase_pos = 0
    while t < seconds:
        dur = r.uniform(0.11, 0.24)
        m = int(dur * SR)
        # intonazione: la frase scende, l'accento sale
        decl = 1 - 0.012 * phrase_pos
        accent = 1.12 if r.random() < 0.25 else 1.0
        f = f0 * decl * accent * (1 + 0.05 * np.sin(np.linspace(0, np.pi, m)) * r.uniform(-1, 1))
        phase = np.cumsum(f / SR)
        src = 2 * (phase % 1) - 1                              # dente di sega
        src += 0.15 * r.standard_normal(m)                     # respiro
        v = VOWELS[r.integers(len(VOWELS))]
        syl = formant_filter(src, *[x * r.uniform(0.93, 1.07) for x in v])
        w = np.sin(np.linspace(0, np.pi, m)) ** 0.6
        syl *= w * r.uniform(0.6, 1.0)
        if r.random() < 0.6:                                   # consonante all'attacco
            c = int(r.uniform(0.015, 0.04) * SR)
            burst = butter(r.standard_normal(c), 'band', [1800, 5000], 2) * np.hanning(c) * 0.5
            syl[:c] += burst
        i = int(t * SR)
        out[i:i + m] += syl
        t += dur + r.uniform(0.01, 0.05)
        phrase_pos += 1
        phrase_left -= 1
        if phrase_left == 0:
            t += r.uniform(0.18, 0.35)
            phrase_left = r.integers(5, 10)
            phrase_pos = 0
    out = out[:n]
    out = butter(out, 'low', 1000, 4)                          # ovattata: come attraverso l'impianto della sala
    out = butter(out, 'high', 120, 2)
    out /= np.abs(out).max() + 1e-9
    wet = signal.fftconvolve(out, IR_HALL)[:n]
    fade = int(0.04 * SR)
    y = out * 0.85 + wet * 0.5
    y[-fade:] *= np.linspace(1, 0, fade)
    return y * 0.45

# ── voce: sintesi MBROLA italiana, poi «sentita dalla sala»: banda stretta, un po' di sala ──
import subprocess, tempfile, os
VOICES = {'c': ('mb-it3', 165, 55), 's': ('mb-it3', 150, 40)}

IR_SMALL = None

def speech(text, who):
    """Parlato indistinto, come sentito da dentro un'auto: solo le frequenze basse, volume contenuto."""
    global IR_SMALL
    if IR_SMALL is None:
        IR_SMALL = reverb_ir(0.25, 900, 11)
    voice, speed, pitch = VOICES[who]
    if not text or text[0] == '#':                 # «#seme:parole» = frase in ostrogoto
        import random, gibberish
        seed, words = (int(v) for v in (text or '#1:7')[1:].split(':'))
        r = random.Random(seed)
        for _ in range(300):
            text = gibberish.sentence(r, words)
            if gibberish.ok(text, voice): break
    with tempfile.TemporaryDirectory() as d:
        f = os.path.join(d, 'v.wav')
        subprocess.run(['espeak-ng', '-v', voice, '-s', str(speed), '-p', str(pitch), text, '-w', f], check=True, capture_output=True)
        sr, x = wavfile.read(f)
    x = x.astype(float) / 32768
    x = signal.resample_poly(x, SR, sr)
    x = butter(x, 'high', 80, 2)
    x = butter(x, 'low', 380, 4)                   # ovattato: le consonanti spariscono, resta il ritmo
    b, a = signal.iirpeak(170, 1.2, fs=SR)         # rimbombo dell'abitacolo
    x = x + 0.6 * signal.lfilter(b, a, x)
    x /= np.abs(x).max() + 1e-9
    wet = signal.fftconvolve(x, IR_SMALL)[:len(x) + SR // 4]
    y = np.pad(x, (0, len(wet) - len(x))) * 0.85 + wet * 0.25
    fade = int(0.03 * SR); y[:fade] *= np.linspace(0, 1, fade); y[-fade:] *= np.linspace(1, 0, fade)
    return y * 0.22, len(x) / SR

# ── suoni d'interfaccia: attacco morbido, frequenze medie, niente transitori secchi ──
def soft(freq, decay=0.03, gain=0.5, attack=0.004, body=0.0):
    """Tocco morbido: seno con attacco graduale e un filo di corpo un'ottava sotto."""
    n = int((attack + decay * 6) * SR)
    tt = np.arange(n) / SR
    s = np.sin(2 * np.pi * freq * tt) + body * np.sin(2 * np.pi * freq / 2 * tt)
    e = np.minimum(1, tt / attack) * np.exp(-np.maximum(0, tt - attack) / decay)
    return butter(s * e, 'low', 3500, 2) * gain

def tick():  return soft(1250, 0.018, 0.07, 0.002)
def tock():  return soft(880, 0.04, 0.17, 0.003, 0.5)

def found():
    # due note che salgono: «pausa trovata»
    out = np.zeros(int(0.9 * SR))
    for k, f in enumerate((523.25, 783.99)):
        n = int(0.8 * SR); tt = np.arange(n) / SR
        bell = (np.sin(2 * np.pi * f * tt) + 0.15 * np.sin(2 * np.pi * f * 2 * tt)) * env_exp(n, 0.2)
        a = int(0.012 * SR); bell[:a] *= np.linspace(0, 1, a)
        i = int(k * 0.085 * SR)
        out[i:i + n] += bell[:len(out) - i] * 0.22
    return out

def whoosh(seconds=0.45):
    n = int(seconds * SR)
    x = rng.standard_normal(n)
    # banda che sale e scende, inviluppo a campana
    out = np.zeros(n)
    hop = 256
    centers = 220 * (5 ** np.sin(np.linspace(0, np.pi, n // hop + 1)))
    for j in range(0, n, hop):
        seg = x[max(0, j - 512):j + hop]
        f = centers[j // hop]
        sos = signal.butter(2, [f * 0.6, min(f * 1.6, 20000)], btype='band', fs=SR, output='sos')
        out[j:j + hop] = signal.sosfilt(sos, seg)[-min(hop, n - j):]
    e = np.sin(np.linspace(0, np.pi, n)) ** 1.6
    return butter(out * e, 'low', 2200, 2) * 0.28

def slam():
    n = int(0.6 * SR); tt = np.arange(n) / SR
    f = 46 + 70 * np.exp(-tt / 0.05)
    s = np.sin(2 * np.pi * np.cumsum(f) / SR) * env_exp(n, 0.16) * 0.9
    s += butter(rng.standard_normal(n), 'low', 500, 2) * env_exp(n, 0.02) * 0.4
    a = int(0.006 * SR); s[:a] *= np.linspace(0, 1, a)
    return s * 0.45

def step(g=1.0):
    # tacco e punta su un palco di legno
    n = int(0.16 * SR); tt = np.arange(n) / SR
    body = np.sin(2 * np.pi * (95 + rng.uniform(-8, 8)) * tt) * env_exp(n, 0.03)
    knock = butter(rng.standard_normal(n), 'band', [300, 2400], 2) * env_exp(n, 0.008)
    s = body * 0.5 + knock * 0.6
    toe = np.zeros(n); d = int(0.055 * SR)
    toe[d:] = (butter(rng.standard_normal(n - d), 'band', [400, 3000], 2) * env_exp(n - d, 0.006)) * 0.3
    return (s + toe) * 0.55 * g

def press():
    # pressione di un pulsante: tonfo morbido e un accenno di «clic» smussato
    a, b = soft(300, 0.05, 0.24, 0.003, 0.6), soft(1100, 0.012, 0.06, 0.002)
    return a + np.pad(b, (0, len(a) - len(b)))

def stamp():
    # timbro: colpo sordo con un po' di legno
    n = int(0.35 * SR); tt = np.arange(n) / SR
    s = np.sin(2 * np.pi * (70 + 60 * np.exp(-tt / 0.03)) * tt) * env_exp(n, 0.07)
    s += butter(rng.standard_normal(n), 'band', [200, 900], 2) * env_exp(n, 0.012) * 0.5
    a = int(0.004 * SR); s[:a] *= np.linspace(0, 1, a)
    return s * 0.5

def bump():
    # colpo sul microfono sentito dall'impianto: basso, gonfio, ovattato
    n = int(0.5 * SR); tt = np.arange(n) / SR
    s = np.sin(2 * np.pi * (55 + 40 * np.exp(-tt / 0.02)) * tt) * env_exp(n, 0.09)
    s += butter(rng.standard_normal(n), 'low', 300, 2) * env_exp(n, 0.03) * 0.8
    s = butter(s, 'low', 400, 2)
    a = int(0.002 * SR); s[:a] *= np.linspace(0, 1, a)
    return s * 0.5

def pop():
    # bandierina piantata: due note brevi e tonde
    a, b = soft(660, 0.05, 0.16, 0.004, 0.3), soft(990, 0.06, 0.12, 0.004, 0.3)
    out = np.zeros(len(b) + int(0.06 * SR)); out[:len(a)] += a; out[int(0.06 * SR):int(0.06 * SR) + len(b)] += b
    return out

def detent(v):  return soft(700 + v * 90, 0.016, 0.16, 0.002, 0.3)
def grab():     return soft(420, 0.03, 0.14, 0.004, 0.4)
def release():  return soft(360, 0.035, 0.12, 0.004, 0.4)

def room(seconds):
    n = int(seconds * SR)
    # rumore rosa a bassa frequenza: l'aria della sala
    w = rng.standard_normal(n)
    b, a = [0.049922035, -0.095993537, 0.050612699, -0.004408786], [1, -2.494956002, 2.017265875, -0.522189400]
    pink = signal.lfilter(b, a, w)
    pink = butter(pink, 'low', 700, 2)
    return pink / np.abs(pink).max() * 0.035

# ── missaggio ──
def pan_gains(p):
    a = (p + 1) * np.pi / 4
    return np.cos(a), np.sin(a)

def render(events, dur, out_path):
    n = int(dur * SR)
    mix = np.zeros((n + SR * 2, 2))
    def add(sig, t, p=0.0, p_to=None):
        i = int(round(t * SR))
        j0 = max(0, -i); i = max(0, i)
        sig = sig[j0:]
        m = min(len(sig), len(mix) - i)
        if m <= 0: return
        if p_to is None:
            l, r = pan_gains(p)
            mix[i:i + m, 0] += sig[:m] * l; mix[i:i + m, 1] += sig[:m] * r
        else:
            pp = np.linspace(p, p_to, m); l, r = pan_gains(pp)
            mix[i:i + m, 0] += sig[:m] * l; mix[i:i + m, 1] += sig[:m] * r
    for e in events:
        k, t = e['k'], e.get('t', 0)
        if k == 'room': add(room(e['d']), t)
        elif k == 'speech':
            y, d = speech(e['text'], e['who'])
            start = e['end'] - d if 'end' in e else t
            if 'cut' in e:                                    # taglio netto ma morbido (80 ms)
                m = max(0, int((e['cut'] - start) * SR)); f = min(int(0.08 * SR), m)
                y = y[:m].copy(); y[m - f:m] *= np.linspace(1, 0, f) if f else 1
            if 'fadeFrom' in e:                               # sotto i titoli finali la voce si allontana
                i0 = max(0, int((e['fadeFrom'] - start) * SR))
                if i0 < len(y): y[i0:] *= np.linspace(1, e['fadeTo'], len(y) - i0)
            add(y, start, e.get('pan', 0) * 0.6)
        elif k == 'voice': add(babble(e['d'], 112 if e['who'] == 'c' else 98, seed=1 if e['who'] == 'c' else 5), t, e.get('pan', 0) * 0.6)
        elif k == 'step': add(step(e.get('g', 1)), t, e.get('pan', 0) * 0.6)
        elif k == 'tick': add(tick(), t, e.get('pan', 0) * 0.5)
        elif k == 'tock': add(tock(), t, e.get('pan', 0) * 0.5)
        elif k == 'found': add(found(), t)
        elif k == 'whoosh': add(whoosh(), t, e['from'] * 0.8, e['to'] * 0.8)
        elif k == 'slam': add(slam(), t)
        elif k == 'press': add(press(), t)
        elif k == 'stamp': add(stamp(), t)
        elif k == 'bump': add(bump(), t, e.get('pan', 0) * 0.5)
        elif k == 'pop': add(pop(), t)
        elif k == 'detent': add(detent(e['v']), t, e.get('pan', 0) * 0.6)
        elif k == 'grab': add(grab(), t)
        elif k == 'release': add(release(), t)
    mix = mix[:n]
    # un filo di riverbero su tutto, poi limitatore morbido e picco a −1 dBFS
    for c in range(2):
        mix[:, c] += signal.fftconvolve(mix[:, c], IR_HALL)[:n] * 0.06
    mix *= 10 ** (-2 / 20) / (np.abs(mix).max() + 1e-9)   # picco a -2 dBFS: i rapporti tra i suoni restano quelli impostati
    fade = int(0.02 * SR); mix[:fade] *= np.linspace(0, 1, fade)[:, None]; mix[-fade:] *= np.linspace(1, 0, fade)[:, None]
    wavfile.write(out_path, SR, (mix * 32767).astype(np.int16))

if __name__ == '__main__':
    data = json.load(open(sys.argv[1]))
    render(data['events'], data['dur'], sys.argv[2])
