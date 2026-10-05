# Ruído para a noite da Ruptura (céu, paisagem distante, pó do impacto): 512x512 RGBA, tileável.
# R: fbm suave (nuvens grandes), G: fbm detalhado, B: células (Worley F1), A: cristas.
# Rodar com o python do Blender (tem numpy): ~/.local/opt/blender-5.2/5.2/python/bin/python3.13 gen_night.py
import numpy as np, os, sys, subprocess
N = 512
rng = np.random.default_rng(1234)

def spectral(beta, lo=1.0):
    fx = np.fft.fftfreq(N)[:, None] * N
    fy = np.fft.fftfreq(N)[None, :] * N
    f = np.sqrt(fx * fx + fy * fy)
    f[0, 0] = 1
    amp = 1.0 / (f ** (beta / 1.0))
    amp[f < lo] = 0
    ph = rng.uniform(0, 2 * np.pi, (N, N))
    spec = amp * np.exp(1j * ph)
    img = np.real(np.fft.ifft2(spec))
    img = (img - img.mean()) / (img.std() + 1e-9)
    return np.clip(img * 0.18 + 0.5, 0, 1)

def worley(npts):
    pts = rng.uniform(0, N, (npts, 2))
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
    d = np.full((N, N), 1e9, np.float32)
    for ox in (-N, 0, N):
        for oy in (-N, 0, N):
            for p in pts:
                dx = xx - (p[0] + ox); dy = yy - (p[1] + oy)
                d = np.minimum(d, dx * dx + dy * dy)
    d = np.sqrt(d)
    return np.clip(d / d.max() * 1.4, 0, 1)

r = spectral(2.2, 1.5)
g = spectral(1.55, 2.0)
b = worley(90)
a0 = spectral(1.9, 2.0)
a = 1 - np.abs(2 * a0 - 1)
a = a * a
img = np.stack([r, g, b, a], -1)
out = (img * 255 + 0.5).astype(np.uint8)
dst = os.path.expanduser('~/Unity/ParkourLab/Assets/Aren/Resources/VFX/noise_night.png')
raw = dst + '.rgba'
out.tofile(raw)
subprocess.run(['convert', '-size', f'{N}x{N}', '-depth', '8', 'rgba:' + raw, '-strip', dst], check=True)
os.remove(raw)
print('ok', dst)
