# O nascimento de cada um dos sete (S5): em vez do "ping" sintetizado, o SINO GRAVADO (bell_near_4,
# parcial forte em ~1424 Hz) reafinado para a nota de cada brilho (RuptureSynth.SignatureHz, as notas
# desafinadas da lore), na oitava mais perto de 1 kHz; por baixo um baque grave na nota/4 (o "peso" do
# brilho saindo), um brilho uma oitava acima e uma cauda longa de ar. Saída: Audio/Eleven/birth0..6.wav
# (o OpeningSound prefere x_birthN ao sintetizado).
#   ~/.local/opt/blender-5.2/5.2/python/bin/python3.13 ArtSource/Audio/ElevenLabs/births.py
import os, subprocess, numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "..", "Assets/Aren/Resources/Audio/Eleven"))
SR = 48000
SIG = [242.6, 117.2, 168.9, 309.1, 381.2, 449.9, 534.1]
BELL_F = 1424.0
rng = np.random.default_rng(5)

def load(name):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", os.path.join(HERE, "raw", name + ".ogg"), "-f", "f32le", "-ac", "1", "-ar", str(SR), "-"], capture_output=True, check=True).stdout
    x = np.frombuffer(raw, dtype=np.float32).astype(np.float64)
    i = int(np.argmax(np.abs(x) > 0.02))
    return x[max(0, i - 24):]

def shift(x, cents):
    r = 2.0 ** (cents / 1200.0)
    pos = np.arange(0, len(x) - 2, r)
    i = pos.astype(int); f = pos - i
    return x[i] * (1 - f) + x[i + 1] * f

def conv(x, ir):
    n = len(x) + len(ir) - 1; N = 1 << int(np.ceil(np.log2(n)))
    return np.fft.irfft(np.fft.rfft(x, N) * np.fft.rfft(ir, N), N)[:n]

bell = load("bell_near_4")
n_ir = int(3.5 * SR); t = np.arange(n_ir) / SR
ir = rng.standard_normal(n_ir) * np.exp(-t / 0.9) * (1 - np.exp(-t / 0.02)) * 0.04
ir[0] = 1.0
for i, f in enumerate(SIG):
    k = 2 ** round(np.log2(1000.0 / f)); target = f * k
    cents = 1200 * np.log2(target / BELL_F)
    b = shift(bell, cents)[: int(3.2 * SR)]
    b *= np.exp(-np.arange(len(b)) / SR / 1.4)
    hi = shift(bell, cents + 1200)[: len(b)] * 0.18
    hi = np.concatenate([np.zeros(int(0.03 * SR)), hi, np.zeros(len(b))])[: len(b)]
    n = len(b); tt = np.arange(n) / SR
    fb = f * 0.25 + 30 * np.exp(-tt / 0.05)
    thump = np.sin(2 * np.pi * np.cumsum(fb) / SR) * np.exp(-tt / 0.16)
    thump = np.tanh(thump * 2.0) * 0.5
    dry = b / np.abs(b).max() + hi + thump
    y = conv(dry, ir)[: int(4.0 * SR)]
    y[-int(0.3 * SR):] *= np.linspace(1, 0, int(0.3 * SR))
    y = y / np.abs(y).max() * 0.89
    p = os.path.join(OUT, f"birth{i}.wav")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "-", "-ar", "44100", "-c:a", "pcm_s16le", p], input=y.astype(np.float32).tobytes(), check=True)
    print(f"birth{i}: {f:6.1f} Hz -> sino em {target:6.1f} Hz ({cents:+.0f} c)")
