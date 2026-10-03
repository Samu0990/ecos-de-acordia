"""Texturas tileáveis da vila de Campanula (geradas, licença limpa) -> Assets/Campanula/Textures.
Cada material: _albedo.png (+ _normal.png). 1024 px (2026-10-02: 512 ficava borrado de perto)."""
import os, sys, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from texlib import *
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Campanula', 'Textures')
N = 1024
rng = np.random.default_rng(7)

def col(*rgb): return np.array(rgb, dtype=np.float64) / 255.0

def tint(mask, c0, c1):
    return c0[None, None, :] * (1 - mask[..., None]) + c1[None, None, :] * mask[..., None]

def save(name, albedo, height=None, strength=2.0):
    save_png(f'{OUT}/{name}_albedo.png', np.clip(albedo, 0, 1))
    if height is not None:
        save_png(f'{OUT}/{name}_normal.png', normal_from_height(height, strength))

x, y = grid(N)

# ---------------------------------------------------------------- pedra de muralha (blocos irregulares)
def stone_blocks(rows, cols_, mortar, seed, base0, base1, mortar_col):
    r = np.random.default_rng(seed)
    yy = y * rows; row = np.floor(yy).astype(int); fy = yy - row
    offs = r.random(rows + 1)
    xx = x * cols_ + offs[row % rows] * 1.0
    colI = np.floor(xx).astype(int); fx = xx - colI
    # variação por bloco
    ids = (row * 131 + colI * 71) % 997
    per = r.random(1000)
    bev = np.minimum(np.minimum(fx, 1 - fx) * cols_ / rows * 1.2, np.minimum(fy, 1 - fy))
    block = smoothstep(mortar * 0.5, mortar * 1.6, bev)
    n = fbm(N, 8, 5, seed=seed)
    n2 = fbm(N, 32, 3, seed=seed + 1)
    h = block * (0.65 + 0.25 * n + 0.1 * per[ids]) + (1 - block) * 0.05
    shade = 0.75 + 0.35 * per[ids] + (n - 0.5) * 0.5 + (n2 - 0.5) * 0.25
    c = tint(np.clip(per[ids] * 0.8 + n * 0.3, 0, 1), base0, base1) * shade[..., None]
    c = c * block[..., None] + mortar_col[None, None, :] * (1 - block[..., None]) * (0.8 + n[..., None] * 0.4)
    return c, h

c, h = stone_blocks(8, 4, 0.06, 11, col(120, 112, 100), col(158, 148, 128), col(80, 74, 66))
save('stone_wall', c, h, 3.0)

# pedra mais escura (base das casas, torre)
c, h = stone_blocks(6, 3, 0.07, 21, col(92, 88, 84), col(128, 120, 108), col(60, 56, 52))
save('stone_dark', c, h, 3.0)

# ---------------------------------------------------------------- calçamento (Voronoi)
f1, f2, ids = worley(N, 0, seed=31, cells=14, jitter=0.9)
edge = smoothstep(0.0, 0.018, f2 - f1)
per = np.random.default_rng(32).random(14 * 14 + 1)
n = fbm(N, 8, 5, seed=33)
dome = np.clip(1 - f1 * 14 * 0.9, 0, 1) ** 0.5
h = edge * (0.5 + 0.5 * dome) + n * 0.08
cc = tint(np.clip(per[ids] * 0.9 + (n - 0.5) * 0.4, 0, 1), col(104, 98, 90), col(150, 140, 122))
cc = cc * (0.8 + 0.3 * n[..., None]) * edge[..., None] + col(64, 58, 50)[None, None, :] * (1 - edge[..., None])
save('cobble', cc, h, 3.5)

# ---------------------------------------------------------------- reboco (paredes claras com sujeira)
n = fbm(N, 4, 6, seed=41); n2 = fbm(N, 24, 3, seed=42)
h = n * 0.6 + n2 * 0.4
cc = tint(np.clip(n * 1.2 - 0.1, 0, 1), col(214, 200, 172), col(232, 222, 198)) * (0.9 + 0.15 * n2[..., None])
stains = smoothstep(0.55, 0.8, fbm(N, 3, 4, seed=43))
cc = cc * (1 - stains[..., None] * 0.18)
cf1, cf2, _ = worley(N, 0, seed=44, cells=6, jitter=0.9)
crack = (1 - smoothstep(0.0, 0.006, cf2 - cf1)) * smoothstep(0.45, 0.7, fbm(N, 5, 3, seed=45))
cc = cc * (1 - crack[..., None] * 0.35)
save('plaster', cc, h - crack * 0.3, 1.4)

# ---------------------------------------------------------------- madeira escura (enxaimel, vigas)
def wood(seed, c0, c1, rings=22, stretch=0.08):
    n = fbm(N, 4, 5, seed=seed)
    wobble = fbm(N, 2, 3, seed=seed + 1)
    g = np.sin((x * rings + wobble * 3 + n * 0.6) * np.pi * 2) * 0.5 + 0.5
    fine = value_noise(N, 128, seed + 2)
    h = g * 0.5 + fine * 0.3 + n * 0.2
    cc = tint(np.clip(g * 0.7 + n * 0.4 - 0.1, 0, 1), c0, c1) * (0.85 + 0.25 * fine[..., None])
    return cc, h
cc, h = wood(51, col(44, 30, 22), col(78, 54, 36))
save('timber', cc, h, 1.6)

# tábuas (chão de barraca, portas, ponte): tábuas verticais com frestas
cc, h = wood(61, col(92, 66, 42), col(134, 98, 62), rings=40)
planks = (x * 6) % 1.0
gap = smoothstep(0.0, 0.04, planks) * smoothstep(1.0, 0.96, planks)
per = np.random.default_rng(62).random(6)
pi = np.floor(x * 6).astype(int) % 6
cc = cc * (0.8 + 0.35 * per[pi][..., None]) * (0.35 + 0.65 * gap[..., None])
save('planks', cc, h * gap, 2.0)

# ---------------------------------------------------------------- telhas cerâmicas (fileiras sobrepostas)
rows = 12; colsT = 10
yy = y * rows; row = np.floor(yy).astype(int); fy = yy - row
xx = x * colsT + (row % 2) * 0.5; ci = np.floor(xx).astype(int); fx = xx - ci
arc = np.sqrt(np.clip(1 - ((fx - 0.5) * 2) ** 2, 0, 1))         # telha curva
lap = smoothstep(0.0, 0.25, fy)                                    # sombra da fileira de cima
h = arc * 0.6 * (0.4 + 0.6 * fy) + lap * 0.3
per = np.random.default_rng(71).random(2000)
idx = (row * 37 + ci * 11) % 2000
n = fbm(N, 6, 4, seed=72)
cc = tint(np.clip(per[idx] * 0.8 + n * 0.3, 0, 1), col(120, 52, 34), col(168, 84, 52))
cc = cc * (0.55 + 0.45 * lap[..., None]) * (0.8 + 0.3 * arc[..., None])
moss = smoothstep(0.62, 0.8, fbm(N, 4, 4, seed=73))
cc = cc * (1 - moss[..., None]) + col(70, 82, 48)[None, None, :] * moss[..., None] * 0.9
save('roof_tiles', cc, h, 2.5)

# ardósia (torre, muralha)
rows = 14
yy = y * rows; row = np.floor(yy).astype(int); fy = yy - row
xx = x * 8 + (row % 2) * 0.5; ci = np.floor(xx).astype(int); fx = xx - ci
lap = smoothstep(0.0, 0.2, fy); gap = smoothstep(0.0, 0.05, fx) * smoothstep(1.0, 0.95, fx)
per = np.random.default_rng(81).random(2000); idx = (row * 37 + ci * 11) % 2000
n = fbm(N, 8, 4, seed=82)
cc = tint(np.clip(per[idx] * 0.7 + n * 0.3, 0, 1), col(48, 52, 62), col(78, 82, 94)) * (0.6 + 0.4 * lap[..., None]) * (0.5 + 0.5 * gap[..., None])
save('roof_slate', cc, lap * gap * 0.7 + n * 0.2, 2.5)

# ---------------------------------------------------------------- chão: grama, terra batida, campo
n = fbm(N, 8, 6, seed=91); n2 = value_noise(N, 320, 92)
streak = np.sin((x * 260 + fbm(N, 16, 3, seed=94) * 9) * np.pi * 2) * 0.5 + 0.5   # fios de capim
cc = tint(np.clip(n * 1.3 - 0.15, 0, 1), col(58, 80, 34), col(112, 132, 60)) * (0.74 + 0.22 * n2[..., None] + 0.14 * streak[..., None])
dry = smoothstep(0.6, 0.8, fbm(N, 3, 4, seed=93))
cc = cc * (1 - dry[..., None] * 0.45) + col(150, 132, 76)[None, None, :] * dry[..., None] * 0.45
flowers = smoothstep(0.9, 0.93, value_noise(N, 420, 95)) * smoothstep(0.4, 0.7, fbm(N, 4, 3, seed=96))
fc = np.where((value_noise(N, 420, 97) > 0.5)[..., None], col(232, 214, 120)[None, None, :], col(236, 236, 226)[None, None, :])
cc = cc * (1 - flowers[..., None]) + fc * flowers[..., None]
save('grass', cc, n2 * 0.5 + n * 0.3 + streak * 0.2, 1.4)

n = fbm(N, 6, 6, seed=101)
pf1, pf2, pid = worley(N, 0, seed=102, cells=22, jitter=0.95)
pper = np.random.default_rng(104).random(22 * 22 + 1)
pebbles = smoothstep(0.035, 0.012, pf1) * (pper[pid] > 0.55)
cc = tint(np.clip(n * 1.2, 0, 1), col(100, 80, 58), col(146, 120, 88)) * (0.82 + 0.24 * value_noise(N, 400, 103)[..., None])
cc = cc * (1 - pebbles[..., None] * 0.6) + tint(pper[pid], col(120, 112, 100), col(178, 168, 150)) * pebbles[..., None] * 0.6
wet = smoothstep(0.62, 0.78, fbm(N, 3, 4, seed=105))
cc = cc * (1 - wet[..., None] * 0.22)
save('dirt', cc, n * 0.4 + pebbles * 0.6, 2.0)

# campo arado / trigo dourado ao pôr do sol
n = fbm(N, 6, 5, seed=111)
furrow = np.sin(y * 40 * np.pi * 2 + n * 2) * 0.5 + 0.5
cc = tint(np.clip(furrow * 0.6 + n * 0.5, 0, 1), col(150, 116, 52), col(214, 176, 92)) * (0.85 + 0.2 * value_noise(N, 220, 112)[..., None])
save('field', cc, furrow * 0.7 + n * 0.3, 1.5)

# ---------------------------------------------------------------- tecido listrado (toldos), metal
stripes = (np.floor(x * 8) % 2)
n = fbm(N, 6, 4, seed=121)
cc = tint(stripes, col(170, 40, 36), col(226, 210, 176)) * (0.85 + 0.2 * n[..., None])
save('cloth_red', cc, n * 0.3, 0.8)
cc = tint(stripes, col(40, 70, 120), col(214, 198, 160)) * (0.85 + 0.2 * n[..., None])
save('cloth_blue', cc, n * 0.3, 0.8)

n = fbm(N, 8, 5, seed=131)
cc = tint(np.clip(n, 0, 1), col(110, 78, 34), col(180, 136, 60))   # bronze dos sinos
patina = smoothstep(0.6, 0.75, fbm(N, 5, 4, seed=132))
cc = cc * (1 - patina[..., None]) + col(70, 130, 110)[None, None, :] * patina[..., None]
save('bronze', cc, n * 0.4, 1.0)

n = fbm(N, 6, 5, seed=141)
lf1, lf2, lid = worley(N, 0, seed=143, cells=34, jitter=0.95)
lper = np.random.default_rng(144).random(34 * 34 + 1)
leaf = smoothstep(0.0, 0.22 / 34 * 6, lf2 - lf1)               # miolo da folha claro, frestas escuras
lit = np.clip(1 - lf1 * 34 * 0.9, 0, 1)                        # folha abaulada
cc = tint(np.clip(lper[lid] * 0.75 + n * 0.35, 0, 1), col(46, 62, 30), col(104, 124, 56))
cc = cc * (0.45 + 0.4 * leaf[..., None] + 0.25 * lit[..., None])
autumn = smoothstep(0.7, 0.85, fbm(N, 3, 3, seed=145)) * (lper[lid] > 0.8)
cc = cc * (1 - autumn[..., None] * 0.6) + col(150, 112, 44)[None, None, :] * autumn[..., None] * 0.6
save('foliage', cc, leaf * 0.6 + lit * 0.4, 2.2)
n = fbm(N, 6, 5, seed=151)
cc = tint(np.clip(np.sin(x * 30 + n * 4) * 0.5 + 0.5, 0, 1), col(58, 44, 32), col(96, 76, 54))
save('bark', cc, n, 2.0)
n = fbm(N, 6, 5, seed=161)
cc = tint(np.clip(np.sin(y * 50 + n * 6) * 0.5 + 0.5, 0, 1), col(176, 146, 70), col(224, 196, 110)) * (0.85 + 0.2 * n[..., None])
save('straw', cc, n, 1.5)
print('world tex ok')
