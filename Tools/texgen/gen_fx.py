"""Texturas de VFX do Aren -> Assets/Aren/Resources/VFX (todas geradas, licença limpa)."""
import os, sys, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from texlib import *
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Aren', 'Resources', 'VFX')

# brilho suave (flash, partículas)
x, y = grid(64); r = np.hypot(x - .5, y - .5) * 2
soft = np.exp(-r * r * 4.5) * (1 - smoothstep(0.85, 1.0, r))
save_png(f'{OUT}/fx_soft.png', np.dstack([np.ones_like(soft)] * 3 + [soft]))

# faísca esticada (partícula stretched): núcleo fino e brilhante
x, y = grid(64, 16)
streak = np.exp(-((y - .5) * 2) ** 2 * 18) * np.clip(1 - np.abs(x - .5) * 2, 0, 1) ** 1.5
save_png(f'{OUT}/fx_streak.png', np.dstack([np.ones_like(streak)] * 3 + [streak]))

# fumaça/poeira: blob com ruído
n = 64; x, y = grid(n); r = np.hypot(x - .5, y - .5) * 2
f = fbm(n, 3, 4, seed=3)
smoke = np.clip((1 - r) * 1.4 - 0.25 + (f - 0.5) * 0.9, 0, 1) ** 1.3
save_png(f'{OUT}/fx_smoke.png', np.dstack([np.ones_like(smoke)] * 3 + [smoke]))

# glifos de ressonância (4 quadros 64x64 em 256x64): não são notas literais,
# são "fragmentos de frequência" — abstratos, legíveis pequenos
W, H = 256, 64
img = np.zeros((H, W))
def stamp(cx, frame, fn):
    xs, ys = np.mgrid[0:H, 0:64].astype(float)
    u = (ys + 0.5) / 64 * 2 - 1; v = (xs + 0.5) / 64 * 2 - 1   # u horiz, v vert (para baixo)
    img[:, frame * 64:(frame + 1) * 64] = np.maximum(img[:, frame * 64:(frame + 1) * 64], fn(u, -v))
def ring(u, v, rad, w): return np.exp(-((np.hypot(u, v) - rad) / w) ** 2)
def line(u, v, x0, y0, x1, y1, w):
    px, py = u - x0, v - y0; dx, dy = x1 - x0, y1 - y0
    t = np.clip((px * dx + py * dy) / (dx * dx + dy * dy), 0, 1)
    return np.exp(-(np.hypot(px - t * dx, py - t * dy) / w) ** 2)
# 0: semínima estilizada (cabeça oval inclinada + haste)
stamp(0, 0, lambda u, v: np.maximum(np.exp(-(((u + .18) * 1.6 + (v + .38) * .5) ** 2 + ((v + .38) * 2.2 - (u + .18) * .4) ** 2) * 6),
                                      line(u, v, .1, -.3, .1, .62, .07)))
# 1: duas ondas senoidais cruzadas (frequência)
stamp(0, 1, lambda u, v: np.maximum(np.exp(-((v - .28 * np.sin(u * 4.2)) / .07) ** 2), np.exp(-((v + .28 * np.sin(u * 4.2)) / .07) ** 2)) * (np.abs(u) < .85))
# 2: anel com 4 raios (ressonância)
stamp(0, 2, lambda u, v: np.maximum(ring(u, v, .42, .07), np.maximum.reduce([line(u, v, np.cos(a) * .55, np.sin(a) * .55, np.cos(a) * .85, np.sin(a) * .85, .06) for a in np.arange(4) * np.pi / 2 + np.pi / 4])))
# 3: losango/estrela de 4 pontas (brilho)
stamp(0, 3, lambda u, v: np.clip(1 - (np.abs(u) ** .55 + np.abs(v) ** .55) / .95, 0, 1) ** 1.2 * 1.6)
img = np.clip(img, 0, 1)
save_png(f'{OUT}/fx_glyphs.png', np.dstack([np.ones_like(img)] * 3 + [img]))

# ruído perlin tileável (distorção, dissolve)
p = fbm(128, 4, 5, seed=11)
p = (p - p.min()) / (p.max() - p.min())
save_png(f'{OUT}/noise_perlin.png', p)

# rachaduras (Worley F2-F1) — corrupção nos inimigos
f1, f2, _ = worley(256, 0, seed=5, cells=7)
edge = 1 - smoothstep(0.0, 0.035, f2 - f1)
warp = fbm(256, 6, 3, seed=8)
cr = np.clip(edge * (0.6 + warp * 0.8), 0, 1)
save_png(f'{OUT}/noise_cracks.png', cr)
print('fx ok')
