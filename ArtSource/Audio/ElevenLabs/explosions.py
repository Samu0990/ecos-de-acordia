# Explosões cinematográficas da abertura, montadas em CAMADAS (receita clássica de sound design:
# estalo → corpo de pressão → peso grave → detritos → espaço → rescaldo). A matéria-prima é do
# ElevenLabs (raw/*.ogg); por cima entram camadas sintetizadas aqui:
#   · peso grave: seno que desce (50 → 27 Hz) saturado — a saturação cria harmônicos de 80–200 Hz que
#     fazem o grave "aparecer" até em alto-falante de notebook (que não toca 40 Hz);
#   · estalo: ruído de 1–3 ms acima de 1,5 kHz, saturado (é o estalo que o ouvido lê como força);
#   · espaço: resposta de impulso de VALE (ecos discretos das serras + cauda difusa, agudos morrem antes);
#   · distância: tira o estalo, abafa e atrasa (o som de longe chega sem ataque).
# Roda no Python do Blender (tem numpy):
#   ~/.local/opt/blender-5.2/5.2/python/bin/python3.13 ArtSource/Audio/ElevenLabs/explosions.py
import os, subprocess, numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "..", "Assets/Aren/Resources/Audio/Eleven"))
SR = 48000
rng = np.random.default_rng(31)

def load(name):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", os.path.join(HERE, "raw", name + ".ogg"), "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"], capture_output=True, check=True).stdout
    x = np.frombuffer(raw, dtype=np.float32).astype(np.float64).reshape(-1, 2)
    # corta o silêncio do começo (o ataque fica no tempo zero)
    e = np.abs(x).max(axis=1)
    i = int(np.argmax(e > 10 ** (-42 / 20)))
    return x[max(0, i - 48):]

def save(name, x, peak_db=-1.0, lufs=None):
    x = np.asarray(x, dtype=np.float64)
    if x.ndim == 1: x = np.stack([x, x], 1)
    # abaixo de ~30 Hz é só pressão que ninguém ouve (e alto-falante de notebook não toca): tira, sobra margem
    x = fft_filter(x, lo=32, slope=2.0)
    # limitador suave: tanh acima de ~-6 dB, depois normaliza o pico
    k = 10 ** (-6 / 20)
    a = np.abs(x)
    x = np.where(a > k, np.sign(x) * (k + (1 - k) * np.tanh((a - k) / (1 - k))), x)
    x = x / max(1e-9, np.abs(x).max()) * 10 ** (peak_db / 20)
    # cauda: corta onde o sinal some e faz fade
    e = np.abs(x).max(axis=1)
    nz = np.nonzero(e > 10 ** (-60 / 20))[0]
    end = min(len(x), (nz[-1] if len(nz) else len(x)) + int(0.05 * SR))
    x = x[:end]
    f = int(min(0.25, end / SR * 0.1) * SR)
    x[-f:] *= np.linspace(1, 0, f)[:, None]
    p = os.path.join(OUT, name + ".wav")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "2", "-i", "-", "-ar", "44100", "-c:a", "pcm_s16le", p], input=x.astype(np.float32).tobytes(), check=True)
    print(f"{name:20s} {len(x) / SR:5.2f} s")

def fft_filter(x, lo=None, hi=None, slope=2.0):
    """Filtro de fase zero no domínio da frequência (passa-alta 'lo', passa-baixa 'hi', rampa suave)."""
    n = len(x); N = 1 << int(np.ceil(np.log2(n + 1)))
    X = np.fft.rfft(x, N, axis=0)
    f = np.fft.rfftfreq(N, 1 / SR)
    g = np.ones_like(f)
    if lo: g *= 1 / np.sqrt(1 + (lo / np.maximum(f, 1e-3)) ** (2 * slope))
    if hi: g *= 1 / np.sqrt(1 + (f / hi) ** (2 * slope))
    if x.ndim == 2: g = g[:, None]
    return np.fft.irfft(X * g, N, axis=0)[:n]

def conv(x, ir):
    n = len(x) + len(ir) - 1; N = 1 << int(np.ceil(np.log2(n)))
    return np.fft.irfft(np.fft.rfft(x, N, axis=0) * np.fft.rfft(ir, N, axis=0), N, axis=0)[:n]

def env(n, att, dec, hold=0.0):
    t = np.arange(n) / SR
    e = np.where(t < att, t / max(att, 1e-6), np.exp(-np.maximum(0, t - att - hold) / dec))
    return e

def sub(dur=3.2, f0=52, f1=27, dec=1.1, drive=2.2):
    n = int(dur * SR); t = np.arange(n) / SR
    f = f1 + (f0 - f1) * np.exp(-t / 0.35)
    ph = 2 * np.pi * np.cumsum(f) / SR
    y = np.sin(ph) * env(n, 0.004, dec)
    y = np.tanh(y * drive) / np.tanh(drive)          # harmônicos (grave audível em alto-falante pequeno)
    return np.stack([y, y], 1)

def crack(dur=0.25, lo=1500, dec=0.045, drive=3.0):
    n = int(dur * SR)
    y = rng.standard_normal((n, 2)) * env(n, 0.0008, dec)[:, None]
    y = fft_filter(y, lo=lo, slope=1.5)
    return np.tanh(y / np.abs(y).max() * drive)

def body(dur=1.6, lo=70, hi=650, dec=0.42, drive=2.5):
    n = int(dur * SR)
    y = rng.standard_normal((n, 2))
    y = fft_filter(y, lo=lo, hi=hi, slope=2) * env(n, 0.006, dec)[:, None]
    return np.tanh(y / np.abs(y).max() * drive)

def valley_ir(dur=6.0, echoes=((0.42, 0.5), (0.95, 0.38), (1.6, 0.3), (2.5, 0.2), (3.6, 0.12)), tail=2.2):
    """Vale entre serras: ecos discretos (cada um mais abafado) + cauda difusa; agudos morrem antes."""
    n = int(dur * SR); t = np.arange(n) / SR
    ir = np.zeros((n, 2))
    ir[0] = 1.0
    for d, g in echoes:
        i = int(d * SR)
        w = int(0.03 * SR)
        burst = rng.standard_normal((w, 2)) * np.hanning(w)[:, None] * g * 0.35
        ir[i:i + w] += burst
    diff = rng.standard_normal((n, 2)) * (1 - np.exp(-t / 0.05))[:, None]
    lowp = fft_filter(diff, hi=900) * np.exp(-t / tail)[:, None]
    midp = fft_filter(diff, lo=900, hi=4000) * np.exp(-t / (tail * 0.45))[:, None]
    highp = fft_filter(diff, lo=4000) * np.exp(-t / (tail * 0.18))[:, None]
    ir[1:] += (lowp * 0.05 + midp * 0.03 + highp * 0.015)[1:]
    # ecos ficam mais escuros
    return ir

def mix(n_sec, *parts):
    n = int(n_sec * SR)
    y = np.zeros((n, 2))
    for p, g, off in parts:
        o = int(off * SR)
        m = min(len(p), n - o)
        if m > 0: y[o:o + m] += p[:m] * g
    return y

def rms(x): return np.sqrt(np.mean(x ** 2) + 1e-12)
def norm(x, r=0.1): return x * (r / rms(x[: int(1.0 * SR)]))

IR = valley_ir()

# 1) O CÉU ESTILHAÇA (S4): estalo cristalino + o estilhaço do ElevenLabs + peso grave + espaço de vale
rs = norm(load("reality_shatter_3"), 0.12)
rs_alt = norm(load("reality_shatter_1"), 0.12)
x = mix(9.0, (crack(0.3, 2200, 0.05), 0.55, 0.0), (rs, 1.0, 0.0), (rs_alt, 0.35, 0.06),
        (sub(4.0, 60, 32, 1.2), 0.5, 0.0), (body(1.4, 80, 900, 0.35), 0.3, 0.0))
wet = conv(fft_filter(x, lo=120), IR)[: len(x)]
save("reality_shatter", x + wet * 0.35)

# 2) OS CACOS SE PARTINDO (logo depois): pedra rachando/rolando + um baque grave
rb = norm(load("rock_burst_3"), 0.12)
x = mix(6.0, (rb, 1.0, 0.0), (norm(load("rock_burst_4"), 0.12), 0.4, 0.12), (sub(1.5, 70, 40, 0.35, 1.6), 0.35, 0.0))
save("rock_burst", x + conv(x, IR)[: len(x)] * 0.25)

# 3) A ONDA DE PRESSÃO CHEGA (S7, o golpe): explosão cheia, perto — estalo, corpo, grave enorme, cauda de vale
ce = norm(load("cine_explosion_4"), 0.12)
ce2 = norm(load("cine_explosion_1"), 0.12)
x = mix(11.0, (crack(0.35, 1500, 0.06, 3.5), 0.7, 0.0), (ce, 1.0, 0.0), (ce2, 0.45, 0.03),
        (sub(5.0, 52, 30, 1.5, 2.8), 0.6, 0.0), (body(2.0, 60, 520, 0.6, 3.0), 0.45, 0.0))
wet = conv(fft_filter(x, lo=90), IR)[: len(x)]
save("cine_explosion", x + wet * 0.4)

# 4) O IMPACTO LONGE (no clarão): o tremor do chão chega antes do ar (ondas no solo são mais rápidas) —
#    só grave abafado, sem ataque, crescendo; depois os ecos do vale rolando
db = norm(load("distant_boom_3"), 0.12)
far = fft_filter(db, hi=420, slope=2.5)
n = len(far); far *= (1 - np.exp(-np.arange(n) / SR / 0.25))[:, None]   # sem ataque
x = mix(10.0, (far, 1.0, 0.0), (sub(6.0, 34, 24, 2.6, 1.4), 0.5, 0.15))
save("distant_rumble", x + conv(x, IR)[: len(x)] * 0.3)

# 5) PESO GRAVE de reforço (título, golpes de câmera): o sub do ElevenLabs + seno saturado
sb = norm(load("sub_boom_1"), 0.12)
x = mix(6.0, (sb, 1.0, 0.0), (sub(4.5, 55, 28, 1.5, 2.4), 0.6, 0.0))
save("sub_boom", x)

# 6) SUCÇÃO antes do golpe (termina EXATAMENTE no fim do arquivo: tocar 'duração' antes do impacto)
ps = load("presuck_1")
e = np.abs(ps).max(axis=1)
sm = np.convolve(e, np.ones(480) / 480, "same")
cut = int(np.argmax(sm)) + int(0.012 * SR)            # o pico do crescendo = onde o golpe entra
x = ps[:cut].copy()
f = int(0.006 * SR); x[-f:] *= np.linspace(1, 0, f)[:, None]
x[: int(0.15 * SR)] *= np.linspace(0, 1, int(0.15 * SR))[:, None]
x = x / max(1e-9, np.abs(x).max()) * 10 ** (-1 / 20)
p = os.path.join(OUT, "presuck.wav")
subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "2", "-i", "-", "-ar", "44100", "-c:a", "pcm_s16le", p], input=x.astype(np.float32).tobytes(), check=True)
print(f"{'presuck':20s} {len(x) / SR:5.2f} s (o golpe entra no fim)")
