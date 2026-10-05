# Versões "desafinadas" (regra da lore: o som nunca some, ele ENTORTA) feitas a partir das gravações
# do ElevenLabs, sem nada eletrônico: reamostragem com afinação variável (a nota escorrega), cópias
# levemente fora do tom somadas (batimento), tremido lento. Roda no Python do Blender (tem numpy):
#   ~/.local/opt/blender-5.2/5.2/python/bin/python3.13 ArtSource/Audio/ElevenLabs/detune.py
import os, subprocess, numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "..", "Assets/Aren/Resources/Audio/Eleven"))
SR = 44100

def load(name):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", os.path.join(HERE, "raw", name + ".ogg"), "-f", "f32le", "-ac", "1", "-ar", str(SR), "-"], capture_output=True, check=True).stdout
    return np.frombuffer(raw, dtype=np.float32).astype(np.float64)

def save(name, x, peak=0.89):
    x = x / max(1e-6, np.max(np.abs(x))) * peak
    p = subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-", "-c:a", "pcm_s16le", os.path.join(OUT, name + ".wav")], input=x.astype(np.float32).tobytes(), check=True)
    print(name, round(len(x) / SR, 2), "s")

def bend(x, cents):
    """Lê x com velocidade 2^(cents/1200) por amostra (cents: array do tamanho da saída)."""
    rate = 2.0 ** (cents / 1200.0)
    pos = np.cumsum(rate)
    pos = pos[pos < len(x) - 2]
    i = pos.astype(int); f = pos - i
    return x[i] * (1 - f) + x[i + 1] * f

def shift(x, cents): return bend(x, np.full(len(x), cents))

def fade(x, a=0.01, b=0.3):
    n = len(x); e = np.ones(n)
    na, nb = int(a * SR), int(b * SR)
    if na > 0: e[:na] = np.linspace(0, 1, na)
    if nb > 0: e[-nb:] = np.linspace(1, 0, nb)
    return x * e

def mix(*parts):
    n = max(len(p) for p, _, _ in parts)
    y = np.zeros(n + SR)
    for p, g, off in parts:
        o = int(off * SR); y[o:o + len(p)] += p * g
    return y[:np.max(np.nonzero(np.abs(y) > 1e-5)) + 1]

# ---- flauta: a melodia (até a abertura cortar) e a nota que entorta
fl = load("flute_melody_1")
# a melodia dura 12 s; a abertura precisa de ~15 s antes da nota errada: repete o começo por baixo do fim
tail = fl[: int(5 * SR)]
xf = int(1.2 * SR)
a = fl.copy(); a[-xf:] *= np.linspace(1, 0, xf)
b = tail.copy(); b[:xf] *= np.linspace(0, 1, xf)
flute_a = np.concatenate([a[:-xf], a[-xf:] + b[:xf], b[xf:]])
save("flute_a", fade(flute_a, 0.02, 1.5), 0.8)
# a nota errada: um trecho sustentado da melodia (o de maior energia estável) escorrega ~1 semitom para
# baixo com um tremido que se abre; uma cópia +30 cents bate contra ela
env = np.convolve(np.abs(fl), np.ones(4410) / 4410, "same")
st = np.argmax([np.mean(env[i:i + int(1.6 * SR)]) - np.std(env[i:i + int(1.6 * SR)]) for i in range(int(2 * SR), len(fl) - int(2 * SR), 2205)]) * 2205 + int(2 * SR)
note = fl[st: st + int(2.6 * SR)]
n = int(3.2 * SR); t = np.arange(n) / SR
k = np.clip((t - 0.35) / 1.6, 0, 1)
cents = -110 * (k * k * (3 - 2 * k)) + 22 * np.sin(2 * np.pi * 5.3 * t) * k
w1 = bend(np.tile(note, 2), cents)
w2 = bend(np.tile(note, 2), cents + 31)
wrong = mix((w1, 1.0, 0), (w2, 0.55 * 1.0, 0.012))
wrong = wrong[: int(3.0 * SR)]
wrong *= np.clip(1.2 - t[: len(wrong)] / 2.6, 0, 1)
save("flute_wrong", fade(wrong, 0.02, 0.6), 0.8)

# ---- sinos da torre: afinados (a gravação) e desafinados (cópias fora do tom, batendo; uma atrasada)
bells = shift(load("bells_harmony_3"), -12)   # fá exato (a flauta está em lá menor: fá e ré cabem)
save("tower_tuned", fade(bells, 0.005, 0.8), 0.85)
det = mix((bells, 1.0, 0), (shift(bells, -63), 0.75, 0.33), (shift(bells, 41), 0.6, 0.71))
save("tower_detuned", fade(det, 0.005, 0.9), 0.85)

# ---- o sino do caminho: "cantando sozinho" (ataque apagado) e tremendo/desafinado
bn = shift(load("bell_near_4"), -133)          # a parcial forte (fá +33 c) vira mi, a nota mais forte da flauta
sung = bn.copy(); na = int(1.0 * SR); sung[:na] *= np.linspace(0, 1, na) ** 2
save("bell_sung", fade(sung, 0.0, 0.8), 0.8)
n = len(bn); t = np.arange(n) / SR
wob = bend(bn, 34 * np.sin(2 * np.pi * 5.7 * t) * np.clip(t / 0.8, 0, 1))
wob2 = shift(bn, -48)
save("bell_wobble", fade(mix((wob, 1.0, 0), (wob2, 0.6, 0.05)), 0.003, 0.8), 0.85)
