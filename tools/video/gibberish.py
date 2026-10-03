"""Frasi in «ostrogoto»: sillabe dal suono italiano, nessun significato; si scartano quelle con difoni MBROLA mancanti."""
import random, subprocess, sys
CONS = ['b', 'd', 'f', 'l', 'm', 'n', 'p', 'r', 's', 't', 'v', 'g', 'c', 'l', 'm', 'n', 'r', 't'] * 3 + ['br', 'tr', 'st']
VOW = ['a', 'e', 'o', 'u', 'a', 'o', 'e']

def word(r):
    n = r.choice([2, 2, 3, 3, 4])
    w = ''.join(r.choice(CONS) + r.choice(VOW) for _ in range(n))
    return w if r.random() < .7 else w + r.choice(['n', 'l', 'r'])

def sentence(r, words):
    ws = [word(r) for _ in range(words)]
    out, i = [], 0
    while i < len(ws):
        k = r.choice([2, 3, 4])
        out.append(' '.join(ws[i:i + k])); i += k
    return (', '.join(out)).capitalize() + r.choice(['.', '.', '?'])

def ok(text, voice='mb-it3'):
    p = subprocess.run(['espeak-ng', '-v', voice, text, '-w', '/dev/null'], capture_output=True, text=True)
    return 'Warning' not in p.stderr

if __name__ == '__main__':
    seed, words = int(sys.argv[1]), int(sys.argv[2])
    r = random.Random(seed)
    for _ in range(200):
        s = sentence(r, words)
        if ok(s):
            print(s); break
