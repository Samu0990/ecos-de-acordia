"""Sons próprios do Sussurrante (síntese offline, numpy; nada de "glitch" digital — a regra da
lore é o som ficar ERRADO: desafinado, batendo, em uníssono torto).

  suss_whisper_loop.wav  laço de 8 s: sete vozes sussurrando sílabas sem sentido (ruído moldado
                         por formantes de vogais sussurradas e fricativas), quase em uníssono, com
                         um zumbido grave em trítono que bate devagar e um eco curto;
  suss_blade_ring.wav    o "canto" da lâmina de obsidiana no golpe: parciais inarmônicos de vidro
                         em duas cópias desafinadas (±18 cents) que batem, com um sopro no ataque.

Rodar com o Python do Blender (tem numpy):
  ~/.local/opt/blender-5.2/5.2/python/bin/python3.13 ArtSource/Sussurrante/scripts/suss_audio.py
"""
import os, wave
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
PROJ = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
OUT = os.path.join(PROJ, "Assets", "Aren", "Resources", "Sussurrante", "Audio")
SR = 44100
rng = np.random.default_rng(13)
os.makedirs(OUT, exist_ok=True)


def save(name, x, peak_db=-3.0):
    x = x - np.mean(x)
    pk = np.max(np.abs(x)) + 1e-9
    x = x / pk * (10 ** (peak_db / 20))
    pcm = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print("SALVO", name, "%.2fs" % (len(x) / SR), "rms %.1f dB" % (20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-9)))


# ---------------------------------------------------------------- sussurros (síntese espectral)
N, HOP = 1024, 256
freqs = np.fft.rfftfreq(N, 1 / SR)
win = np.hanning(N)

# vogais sussurradas (F1, F2, F3 em Hz) e fricativas (centro, largura)
VOWELS = {"a": (750, 1250, 2550), "o": (480, 880, 2450), "u": (360, 760, 2300), "e": (520, 1800, 2550), "i": (320, 2200, 2950)}
FRIC = {"s": (6800, 2200), "x": (3600, 1300), "f": (4200, 3500), "h": (1600, 1800)}


def formant_mag(fs, scale, bw=1.0):
    m = np.zeros_like(freqs)
    for k, f in enumerate(fs):
        f = f * scale
        b = (55 + 40 * k) * bw
        m += np.exp(-0.5 * ((freqs - f) / b) ** 2) * (1.0, 0.7, 0.35)[k]
    return m


def fric_mag(c, w):
    return np.exp(-0.5 * ((freqs - c) / w) ** 2)


def whisper_voice(seconds, scale, rate):
    frames = int(seconds * SR / HOP) + 4
    out = np.zeros(frames * HOP + N)
    # roteiro de sílabas: (início, duração, espectro)
    t = rng.uniform(0, 0.3)
    events = []
    while t < seconds:
        if rng.random() < 0.35:
            k = rng.choice(list(FRIC))
            mag = fric_mag(*FRIC[k]) * (0.16 if k != "s" else 0.09)
            d = rng.uniform(0.08, 0.2)
        else:
            v = rng.choice(list(VOWELS))
            mag = formant_mag(VOWELS[v], scale) + 0.05 * fric_mag(3000, 2500)
            d = rng.uniform(0.12, 0.34)
        events.append((t, d, mag))
        t += d + 0.12 + rng.exponential(1.0 / rate)
    env_cache = np.zeros((frames, len(freqs)))
    for (t0, d, mag) in events:
        f0, f1 = int(t0 * SR / HOP), int((t0 + d) * SR / HOP)
        for f in range(max(0, f0), min(frames, f1 + 3)):
            u = (f - f0) / max(1, f1 - f0)
            a = np.sin(np.pi * np.clip(u, 0, 1)) ** 0.8 if u <= 1 else max(0.0, 1 - (u - 1) * 2)
            env_cache[f] += mag * a
    for f in range(frames):
        if not env_cache[f].any():
            continue
        ph = rng.uniform(0, 2 * np.pi, len(freqs))
        spec = env_cache[f] * np.exp(1j * ph)
        seg = np.fft.irfft(spec, N) * win
        out[f * HOP:f * HOP + N] += seg
    return out[:int(seconds * SR)]


def reverb(x, seconds=1.1, mix=0.35, seed=3):
    r = np.random.default_rng(seed)
    n = int(seconds * SR)
    ir = r.standard_normal(n) * np.exp(-np.linspace(0, 7, n))
    ir[:int(0.012 * SR)] = 0
    ir /= np.sqrt(np.sum(ir ** 2))
    L = len(x) + n
    nf = 1 << int(np.ceil(np.log2(L)))
    wet = np.fft.irfft(np.fft.rfft(x, nf) * np.fft.rfft(ir, nf), nf)[:L]
    y = np.zeros(L); y[:len(x)] += x * (1 - mix); y += wet * mix
    return y


LOOP = 8.0
T = LOOP + 1.3
voices = []
for i in range(4):
    v = whisper_voice(T, scale=rng.uniform(0.86, 1.12), rate=rng.uniform(1.6, 2.4))
    # vozes quase juntas: atrasos pequenos ("uníssono" torto)
    d = int(rng.uniform(0, 0.09) * SR)
    v = np.concatenate([np.zeros(d), v])[:len(v)] * rng.uniform(0.6, 1.0)
    voices.append(v)
mix = np.sum(voices, axis=0)
# zumbido grave em trítono, batendo devagar (quase inaudível: só "pesa")
tt = np.arange(len(mix)) / SR
hum = (np.sin(2 * np.pi * 73.4 * tt) + 0.7 * np.sin(2 * np.pi * 103.8 * tt + 1.3) + 0.5 * np.sin(2 * np.pi * 104.6 * tt)) \
      * (0.55 + 0.45 * np.sin(2 * np.pi * 0.21 * tt))
mix = mix / (np.max(np.abs(mix)) + 1e-9) + hum * 0.035
mix = reverb(mix, 1.0, 0.24)[:len(mix)]
# menos agudo (sussurro perto, não chiado)
sp = np.fft.rfft(mix); fq = np.fft.rfftfreq(len(mix), 1 / SR)
sp *= 1 / np.sqrt(1 + (fq / 5200) ** 4)
mix = np.fft.irfft(sp, len(mix))
# laço sem emenda: os últimos 1,2 s entram cruzados no começo
n = int(LOOP * SR); x = int(1.2 * SR)
loop = mix[:n].copy()
fade = np.linspace(0, 1, x)
loop[:x] = loop[:x] * fade + mix[n:n + x] * (1 - fade)
save("suss_whisper_loop.wav", loop, -6.0)

# ---------------------------------------------------------------- canto da lâmina
D = 1.3
t = np.arange(int(D * SR)) / SR
ring = np.zeros_like(t)
base = 1180.0
for ratio, amp, dec in ((1.0, 1.0, 0.55), (2.76, 0.55, 0.38), (5.40, 0.32, 0.22), (8.93, 0.18, 0.12), (13.3, 0.08, 0.07)):
    for cents in (-18, 18):
        f = base * ratio * 2 ** (cents / 1200)
        vib = 1 + 0.0025 * np.sin(2 * np.pi * 5.2 * t)
        ring += amp * np.sin(2 * np.pi * f * vib * t + rng.uniform(0, 6.28)) * np.exp(-t / dec)
att = np.clip(t / 0.004, 0, 1)
ring *= att
# sopro de ar (o corte) no começo
air = rng.standard_normal(len(t)) * np.exp(-t / 0.09)
spec = np.fft.rfft(air)
f = np.fft.rfftfreq(len(air), 1 / SR)
spec *= np.exp(-0.5 * ((f - 3500) / 2200) ** 2)
air = np.fft.irfft(spec, len(air))
ring = ring / np.max(np.abs(ring)) + air / (np.max(np.abs(air)) + 1e-9) * 0.35
save("suss_blade_ring.wav", ring, -4.0)
print("OK")
