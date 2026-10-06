"""Sprites da UI gótica (menu de pausa, configurações e HUD), desenhados por código como a referência do
autor: moldura de bronze escuro com relevo, faixa de título vermelha, caveiras de osso no alto e embaixo,
flâmulas vermelhas rasgadas, botões com pontas de lança, interruptor, sliders com botão de caveira e o
brasão espinhoso do HUD com as barras de Vida e Ressonância.

Tudo é campo de distância (SDF) em numpy, renderizado com superamostragem 3x: o relevo do metal sai do
gradiente da altura (chanfro) iluminado de cima à esquerda, com sujeira de ruído. Saída em PNG (RGBA) via
ImageMagick em Assets/Aren/Resources/UI/Gothic/.

Rodar: ~/.local/opt/blender-5.2/5.2/python/bin/python3.13 Tools/texgen/gen_gothic_ui.py
"""
import math, os, subprocess
from contextlib import contextmanager
import numpy as np

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Aren', 'Resources', 'UI', 'Gothic')
SS = 3
rng = np.random.default_rng(5)

# ------------------------------------------------------------------ cores
BRONZE = dict(dark=(0.09, 0.065, 0.045), mid=(0.30, 0.22, 0.14), light=(0.62, 0.49, 0.32), hi=(0.95, 0.82, 0.58))
BRONZE_HI = dict(dark=(0.14, 0.09, 0.06), mid=(0.46, 0.33, 0.2), light=(0.82, 0.64, 0.4), hi=(1.0, 0.9, 0.66))
BONE = dict(dark=(0.16, 0.14, 0.12), mid=(0.5, 0.46, 0.4), light=(0.78, 0.74, 0.66), hi=(0.96, 0.93, 0.86))
RED = dict(dark=(0.16, 0.02, 0.025), mid=(0.48, 0.07, 0.07), light=(0.78, 0.16, 0.13), hi=(1.0, 0.45, 0.35))
INK = (0.055, 0.042, 0.038)


# ------------------------------------------------------------------ tela
class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        H, W = h * SS, w * SS
        ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
        self.X = (xs + 0.5) / SS
        self.Y = (ys + 0.5) / SS
        self.x, self.y = self.X, self.Y
        self.sl = (slice(None), slice(None))
        self.rgb = np.zeros((H, W, 3), np.float32)
        self.a = np.zeros((H, W), np.float32)

    @contextmanager
    def win(self, x0, y0, x1, y1):
        """Desenha só dentro do retângulo (em px): as formas são calculadas só ali (muito mais rápido)."""
        old = (self.x, self.y, self.sl)
        i0, i1 = max(0, int(y0 * SS)), min(self.h * SS, int(math.ceil(y1 * SS)))
        j0, j1 = max(0, int(x0 * SS)), min(self.w * SS, int(math.ceil(x1 * SS)))
        self.sl = (slice(i0, i1), slice(j0, j1))
        self.x, self.y = self.X[self.sl], self.Y[self.sl]
        try:
            yield
        finally:
            self.x, self.y, self.sl = old

    def over(self, rgb, alpha):
        alpha = np.nan_to_num(np.clip(alpha, 0, 1))
        RGB, A = self.rgb[self.sl], self.a[self.sl]
        if np.ndim(rgb) == 1 or (isinstance(rgb, tuple)):
            rgb = np.broadcast_to(np.array(rgb, np.float32), RGB.shape)
        rgb = np.nan_to_num(rgb)
        oa = alpha + A * (1 - alpha)
        num = rgb * alpha[..., None] + RGB * (A * (1 - alpha))[..., None]
        self.rgb[self.sl] = np.where(oa[..., None] > 1e-6, num / np.maximum(oa, 1e-6)[..., None], 0)
        self.a[self.sl] = oa

    def save(self, name):
        H, W = self.h, self.w
        pre = self.rgb * self.a[..., None]
        pre = pre.reshape(H, SS, W, SS, 3).mean(axis=(1, 3))
        a = self.a.reshape(H, SS, W, SS).mean(axis=(1, 3))
        rgb = np.where(a[..., None] > 1e-5, pre / np.maximum(a, 1e-5)[..., None], 0)
        img = np.concatenate([np.clip(rgb, 0, 1), np.clip(a, 0, 1)[..., None]], -1)
        img = (img * 255 + 0.5).astype(np.uint8)
        os.makedirs(OUT, exist_ok=True)
        p = os.path.join(OUT, name + '.png')
        subprocess.run(['convert', '-size', f'{W}x{H}', '-depth', '8', 'rgba:-', '-strip', p], input=img.tobytes(), check=True)
        print('ok', name, W, H)


# ------------------------------------------------------------------ SDF
def cov(d):
    return np.clip(0.5 - d * SS, 0, 1)


def box(c, cx, cy, hw, hh, r=0.0):
    qx = np.abs(c.x - cx) - (hw - r)
    qy = np.abs(c.y - cy) - (hh - r)
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r


def circle(c, cx, cy, r):
    return np.hypot(c.x - cx, c.y - cy) - r


def ellipse(c, cx, cy, rx, ry):
    # aproximação boa o bastante para ornamentos
    k = np.hypot((c.x - cx) / rx, (c.y - cy) / ry)
    return (k - 1) * min(rx, ry)


def segment(c, ax, ay, bx, by, r):
    px, py = c.x - ax, c.y - ay
    dx, dy = bx - ax, by - ay
    t = np.clip((px * dx + py * dy) / (dx * dx + dy * dy + 1e-9), 0, 1)
    return np.hypot(px - dx * t, py - dy * t) - r


def poly(c, pts):
    pts = [tuple(p) for p in pts]
    n = len(pts)
    d = np.full(c.x.shape, 1e9, np.float32)
    inside = np.zeros(c.x.shape, bool)
    for i in range(n):
        ax, ay = pts[i]; bx, by = pts[(i + 1) % n]
        d = np.minimum(d, segment(c, ax, ay, bx, by, 0))
        cond = ((ay > c.y) != (by > c.y)) & (c.x < (bx - ax) * (c.y - ay) / (by - ay + 1e-9) + ax)
        inside ^= cond
    return np.where(inside, -d, d)


def stroke(c, pts, r0, r1=None):
    """Traço afunilado ao longo de uma polilinha (raio r0 → r1)."""
    r1 = r0 if r1 is None else r1
    d = np.full(c.x.shape, 1e9, np.float32)
    n = len(pts)
    for i in range(n - 1):
        t = i / max(1, n - 2)
        r = r0 + (r1 - r0) * t
        d = np.minimum(d, segment(c, pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], r))
    return d


def bezier(p0, p1, p2, p3, n=24):
    out = []
    for i in range(n + 1):
        t = i / n
        u = 1 - t
        out.append((u ** 3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t ** 3 * p3[0],
                    u ** 3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t ** 3 * p3[1]))
    return out


def curl(cx, cy, r0, turns=1.3, sign=1, start=0.0, n=40):
    """Espiral (voluta) em volta de (cx, cy)."""
    pts = []
    for i in range(n + 1):
        t = i / n
        a = start + sign * t * turns * 2 * math.pi
        r = r0 * (1 - 0.75 * t)
        pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return pts


def union(*ds):
    out = ds[0]
    for d in ds[1:]: out = np.minimum(out, d)
    return out


def sub(a, b):
    return np.maximum(a, -b)


# ------------------------------------------------------------------ ruído e materiais
def noise(c, scale, seed=0):
    r = np.random.default_rng(seed)
    gh, gw = int(c.h / scale) + 3, int(c.w / scale) + 3
    g = r.random((gh, gw)).astype(np.float32)
    fx = c.x / scale; fy = c.y / scale
    x0 = np.floor(fx).astype(int); y0 = np.floor(fy).astype(int)
    tx = fx - x0; ty = fy - y0
    tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty)
    a = g[y0, x0]; b = g[y0, x0 + 1]; cc = g[y0 + 1, x0]; d = g[y0 + 1, x0 + 1]
    return (a * (1 - tx) + b * tx) * (1 - ty) + (cc * (1 - tx) + d * tx) * ty


def fbm(c, scale, seed=0, oct=3):
    s, a, tot = 0, 1.0, 0
    for o in range(oct):
        s = s + noise(c, scale / (2 ** o), seed + o * 7) * a
        tot += a; a *= 0.5
    return s / tot


def ramp(t, pal):
    t = np.clip(t, 0, 1)[..., None]
    d, m, l = (np.array(pal[k], np.float32) for k in ('dark', 'mid', 'light'))
    return np.where(t < 0.5, d + (m - d) * (t / 0.5), m + (l - m) * ((t - 0.5) / 0.5))


def metal(c, d, pal=BRONZE, bevel=2.5, grime=0.25, seed=1, alpha=1.0, gloss=1.0):
    """Pinta a forma d com relevo metálico (chanfro iluminado de cima à esquerda)."""
    h = np.clip(-d / bevel, 0, 1)
    h = 1 - (1 - h) ** 2
    gy, gx = np.gradient(h)
    k = SS * bevel * 0.9
    nx, ny, nz = -gx * k, -gy * k, np.ones_like(h)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    nx, ny, nz = nx / ln, ny / ln, nz / ln
    L = np.array([-0.55, -0.65, 0.52]); L = L / np.linalg.norm(L)
    diff = np.clip(nx * L[0] + ny * L[1] + nz * L[2], 0, 1)
    Hh = L + np.array([0, 0, 1.0]); Hh = Hh / np.linalg.norm(Hh)
    spec = np.clip(nx * Hh[0] + ny * Hh[1] + nz * Hh[2], 0, 1) ** 28 * gloss
    g = fbm(c, 9, seed)
    t = 0.12 + diff * 0.78 - (g - 0.5) * grime
    col = ramp(t, pal) + np.array(pal['hi'], np.float32) * spec[..., None] * 0.55
    # cavidades (borda externa) mais escuras
    col *= (0.65 + 0.35 * np.clip(-d / (bevel * 0.6), 0, 1))[..., None]
    c.over(col, cov(d) * alpha)


def flat(c, d, color, alpha=1.0):
    c.over(color, cov(d) * alpha)


# ------------------------------------------------------------------ peças
def skull(c, cx, cy, s, pal=BONE, jaw=True):
    """Caveira de osso (s = raio do crânio)."""
    with c.win(cx - s * 1.3, cy - s * 1.3, cx + s * 1.3, cy + s * 1.35):
        cran = ellipse(c, cx, cy - s * 0.15, s, s * 0.95)
        cheek = box(c, cx, cy + s * 0.55, s * 0.62, s * 0.38, s * 0.25)
        head = union(cran, cheek)
        if jaw:
            head = union(head, box(c, cx, cy + s * 0.98, s * 0.45, s * 0.24, s * 0.12))
        metal(c, head, pal, bevel=s * 0.35, grime=0.35, seed=11)
        # órbitas, nariz e dentes (buracos escuros)
        eyes = union(ellipse(c, cx - s * 0.38, cy + s * 0.18, s * 0.27, s * 0.24), ellipse(c, cx + s * 0.38, cy + s * 0.18, s * 0.27, s * 0.24))
        nose = poly(c, [(cx, cy + s * 0.42), (cx - s * 0.12, cy + s * 0.68), (cx + s * 0.12, cy + s * 0.68)])
        holes = union(eyes, nose)
        flat(c, holes + 0.4, (0.02, 0.015, 0.012))
        flat(c, holes, (0.04, 0.03, 0.025))
        if jaw:
            for i in range(-2, 3):
                flat(c, segment(c, cx + i * s * 0.15, cy + s * 0.84, cx + i * s * 0.15, cy + s * 1.12, s * 0.025), (0.05, 0.04, 0.03))
        # brilho no topo do crânio
        flat(c, ellipse(c, cx - s * 0.3, cy - s * 0.55, s * 0.32, s * 0.14), (1, 0.97, 0.9), 0.12)


def spike(c, x, y, ang, length, w):
    a = math.radians(ang)
    dx, dy = math.cos(a), math.sin(a)
    px, py = -dy, dx
    return poly(c, [(x + px * w, y + py * w), (x + dx * length, y + dy * length), (x - px * w, y - py * w)])


def filigree(c, cx, cy, side, scale=1.0, down=False):
    """Volutas que saem do centro para um lado (side = ±1)."""
    s = scale
    sy = 1 if down else -1
    d = stroke(c, bezier((cx, cy), (cx + side * 40 * s, cy + sy * 2 * s), (cx + side * 70 * s, cy - sy * 18 * s), (cx + side * 108 * s, cy - sy * 4 * s)), 5.5 * s, 2.0 * s)
    d = union(d, stroke(c, curl(cx + side * 108 * s, cy - sy * 14 * s, 10 * s, 1.2, -side * sy, math.pi / 2 * sy), 2.2 * s, 1.2 * s))
    d = union(d, stroke(c, bezier((cx + side * 30 * s, cy), (cx + side * 46 * s, cy + sy * 20 * s), (cx + side * 66 * s, cy + sy * 22 * s), (cx + side * 78 * s, cy + sy * 10 * s)), 3.2 * s, 1.2 * s))
    d = union(d, stroke(c, curl(cx + side * 72 * s, cy + sy * 4 * s, 7 * s, 1.1, side * sy, -math.pi / 2 * sy), 1.6 * s, 0.9 * s))
    d = union(d, spike(c, cx + side * 52 * s, cy - sy * 6 * s, -90 * (1 if not down else -1) + side * 25, 16 * s, 3 * s))
    return d


def frame(c, x0, y0, x1, y1, pal=BRONZE, outer=6.0, inner_inset=11.0, corner=34.0, seed=3):
    """Moldura de dois filetes com cantoneiras ornamentadas e chanfro."""
    ii = inner_inset
    m = ii + 4
    for wx0, wy0, wx1, wy1 in ((x0 - 2, y0 - 2, x1 + 2, y0 + m), (x0 - 2, y1 - m, x1 + 2, y1 + 2),
                               (x0 - 2, y0 + m, x0 + m, y1 - m), (x1 - m, y0 + m, x1 + 2, y1 - m)):
        with c.win(wx0, wy0, wx1, wy1):
            o = box(c, (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2, 4)
            metal(c, np.maximum(o, -(o + outer)), pal, bevel=2.6, seed=seed)
            i = box(c, (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2 - ii, (y1 - y0) / 2 - ii, 2)
            metal(c, np.maximum(i, -(i + 2.0)), pal, bevel=1.2, seed=seed + 1, alpha=0.95)
    # cantoneiras: L grosso + botão + espinho para fora + voluta
    for sx, sy, px, py in ((1, 1, x0, y0), (-1, 1, x1, y0), (1, -1, x0, y1), (-1, -1, x1, y1)):
      R = corner + 22
      with c.win(min(px, px + sx * R) - 16, min(py, py + sy * R) - 16, max(px, px + sx * R) + 16, max(py, py + sy * R) + 16):
        L = union(box(c, px + sx * corner / 2, py + sy * 5.5, corner / 2, 5.5, 2), box(c, px + sx * 5.5, py + sy * corner / 2, 5.5, corner / 2, 2))
        boss = circle(c, px + sx * 8, py + sy * 8, 6.5)
        sp = spike(c, px + sx * 3, py + sy * 3, math.degrees(math.atan2(-sy, -sx)), 13, 4)
        cr = stroke(c, curl(px + sx * (corner + 6), py + sy * 12, 6.5, 1.1, sx * sy, math.atan2(-sy, 0)), 2.0, 1.0)
        cr2 = stroke(c, curl(px + sx * 12, py + sy * (corner + 6), 6.5, 1.1, -sx * sy, math.atan2(0, -sx)), 2.0, 1.0)
        metal(c, union(L, sp, cr, cr2), pal, bevel=2.4, seed=seed + 2)
        metal(c, boss, BRONZE_HI, bevel=4, seed=seed + 3)


def panel_fill(c, x0, y0, x1, y1, seed=4, alpha=0.97):
    d = box(c, (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2, 4)
    g = fbm(c, 40, seed, 4)
    g2 = fbm(c, 6, seed + 9, 2)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    vig = np.clip(1 - (np.hypot((c.x - cx) / ((x1 - x0) / 2), (c.y - cy) / ((y1 - y0) / 2)) ** 2) * 0.35, 0.4, 1)
    base = np.array(INK, np.float32) * (0.75 + 0.5 * g[..., None]) * (0.92 + 0.16 * g2[..., None]) * vig[..., None]
    base += np.array((0.02, 0.006, 0.004), np.float32) * (1 - g[..., None])   # um pouco de vermelho no escuro
    c.over(base, cov(d) * alpha)


def header_band(c, x0, y0, x1, y1, seed=6):
    with c.win(x0 - 2, y0 - 3, x1 + 2, y1 + 3):
        d = box(c, (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2, (y1 - y0) / 2, 1)
        cx = (x0 + x1) / 2
        t = np.clip(1 - np.abs(c.x - cx) / ((x1 - x0) / 2), 0, 1)
        v = (c.y - y0) / (y1 - y0)
        g = fbm(c, 18, seed, 3)
        k = (0.35 + 0.65 * t ** 0.8) * (1.0 - 0.35 * v) * (0.82 + 0.3 * g)
        col = np.array((0.08, 0.012, 0.015), np.float32) + np.array((0.30, 0.045, 0.045), np.float32) * k[..., None]
        c.over(col, cov(d))
        # filetes de bronze em cima e embaixo
        metal(c, box(c, cx, y0, (x1 - x0) / 2, 1.6), BRONZE, bevel=1.2, seed=seed + 1)
        metal(c, box(c, cx, y1, (x1 - x0) / 2, 1.6), BRONZE, bevel=1.2, seed=seed + 2)


def divider(c, cx, cy, half, gem=True, pal=BRONZE):
    with c.win(cx - half - 3, cy - 10, cx + half + 3, cy + 16):
        d = union(stroke(c, [(cx - half, cy), (cx - 12, cy)], 0.5, 1.4), stroke(c, [(cx + half, cy), (cx + 12, cy)], 0.5, 1.4))
        if gem:
            d = union(d, poly(c, [(cx - 10, cy), (cx, cy - 6), (cx + 10, cy), (cx, cy + 6)]))
            d = union(d, spike(c, cx, cy + 4, 90, 9, 2.4))
        metal(c, d, pal, bevel=1.5, seed=21)


def banner(c, x0, y0, w, h, seed):
    with c.win(x0 - 6, y0 - 2, x0 + w + 6, y0 + h + 2):
        r = np.random.default_rng(seed)
        pts = [(x0, y0), (x0 + w, y0)]
        # borda de baixo rasgada (pontas irregulares)
        n = 6
        for i in range(n, -1, -1):
            x = x0 + w * i / n
            yy = y0 + h * (0.7 + 0.3 * r.random()) if i % 2 == 0 else y0 + h * (0.45 + 0.25 * r.random())
            if i in (0, n): yy = y0 + h * (0.8 + 0.2 * r.random())
            pts.append((x + r.uniform(-3, 3), yy))
        d = poly(c, pts)
        # dobras verticais + escurece embaixo + furos de desgaste
        fold = 0.55 + 0.45 * np.cos((c.x - x0) / w * math.pi * 5.0 + 0.6)
        v = np.clip((c.y - y0) / h, 0, 1)
        g = fbm(c, 7, seed, 3)
        k = fold * (1 - 0.45 * v) * (0.75 + 0.4 * g)
        col = np.array((0.12, 0.012, 0.015), np.float32) + np.array((0.5, 0.06, 0.05), np.float32) * k[..., None]
        holes = (fbm(c, 11, seed + 3, 2) > 0.76) & (v > 0.6)   # poucos rasgos grandes perto da ponta
        a = cov(d) * np.where(holes, 0.0, 1.0)
        c.over(col, a)
        # fio dourado na barra de cima
        metal(c, box(c, x0 + w / 2, y0 + 3, w / 2 + 3, 3.2, 1.5), BRONZE, bevel=1.5, seed=seed + 5)


# ================================================================== sprites

def pause_panel():
    W, H = 600, 820
    c = Canvas(W, H)
    x0, y0, x1, y1 = 22, 92, 578, 712
    # flâmulas atrás da moldura (penduradas embaixo)
    banner(c, 66, 690, 92, 104, 31)
    banner(c, 442, 690, 92, 104, 32)
    panel_fill(c, x0 + 3, y0 + 3, x1 - 3, y1 - 3)
    header_band(c, x0 + 13, y0 + 26, x1 - 13, y0 + 112)
    divider(c, (x0 + x1) / 2, y0 + 126, 230)
    frame(c, x0, y0, x1, y1)
    # topo: filigrana + espinhos + caveira com coroa de espinhos
    cx = W / 2
    with c.win(cx - 130, y0 - 40, cx + 130, y0 + 40):
        fil = union(filigree(c, cx, y0 + 2, -1, 1.05), filigree(c, cx, y0 + 2, 1, 1.05))
        metal(c, fil, BRONZE, bevel=2.2, seed=41)
    with c.win(cx - 60, y0 - 85, cx + 60, y0 + 6):
      crown = union(spike(c, cx, y0 - 48, -90, 30, 6), spike(c, cx - 17, y0 - 40, -112, 22, 5), spike(c, cx + 17, y0 - 40, -68, 22, 5),
                  spike(c, cx - 30, y0 - 26, -140, 18, 4), spike(c, cx + 30, y0 - 26, -40, 18, 4))
      crown = union(crown, ellipse(c, cx, y0 - 26, 36, 22))
      metal(c, crown, BRONZE, bevel=3, seed=42)
    skull(c, cx, y0 - 20, 23)
    # embaixo: filigrana descendo + caveira menor + espinhos para baixo
    with c.win(cx - 125, y1 - 40, cx + 125, y1 + 40):
        fil = union(filigree(c, cx, y1 - 2, -1, 0.95, down=True), filigree(c, cx, y1 - 2, 1, 0.95, down=True))
        metal(c, fil, BRONZE, bevel=2.2, seed=43)
    with c.win(cx - 45, y1 - 6, cx + 45, y1 + 70):
        sp = union(spike(c, cx, y1 + 40, 90, 24, 5), spike(c, cx - 14, y1 + 34, 110, 16, 4), spike(c, cx + 14, y1 + 34, 70, 16, 4), ellipse(c, cx, y1 + 18, 30, 20))
        metal(c, sp, BRONZE, bevel=3, seed=44)
    skull(c, cx, y1 + 10, 18)
    # pontas nas laterais, na altura da faixa do título
    for sx, px in ((-1, x0), (1, x1)):
        with c.win(px - 22, y0 + 55, px + 22, y0 + 83):
            metal(c, union(spike(c, px, y0 + 69, 180 if sx < 0 else 0, 16, 5), circle(c, px, y0 + 69, 5)), BRONZE, bevel=2, seed=45)
    c.save('pause_panel')


def settings_panel():
    W, H = 560, 560
    c = Canvas(W, H)
    x0, y0, x1, y1 = 20, 60, 540, 540
    panel_fill(c, x0 + 3, y0 + 3, x1 - 3, y1 - 3, seed=7)
    header_band(c, x0 + 13, y0 + 22, x1 - 13, y0 + 92, seed=8)
    divider(c, (x0 + x1) / 2, y0 + 104, 200)
    frame(c, x0, y0, x1, y1, seed=9)
    cx = W / 2
    with c.win(cx - 115, y0 - 36, cx + 115, y0 + 36):
        fil = union(filigree(c, cx, y0 + 2, -1, 0.9), filigree(c, cx, y0 + 2, 1, 0.9))
        metal(c, fil, BRONZE, bevel=2.2, seed=46)
    # brasão em losango (o da referência)
    crest = poly(c, [(cx, y0 - 46), (cx + 24, y0 - 6), (cx, y0 + 30), (cx - 24, y0 - 6)])
    metal(c, crest, BRONZE, bevel=4, seed=47)
    inner = poly(c, [(cx, y0 - 32), (cx + 13, y0 - 6), (cx, y0 + 16), (cx - 13, y0 - 6)])
    flat(c, inner, (0.05, 0.035, 0.03))
    metal(c, poly(c, [(cx, y0 - 24), (cx + 7, y0 - 6), (cx, y0 + 8), (cx - 7, y0 - 6)]), BRONZE_HI, bevel=3, seed=48)
    # embaixo: pequeno losango com espinho
    bot = union(poly(c, [(cx, y1 - 12), (cx + 14, y1 + 2), (cx, y1 + 16), (cx - 14, y1 + 2)]), spike(c, cx, y1 + 12, 90, 14, 3))
    metal(c, bot, BRONZE, bevel=2.5, seed=49)
    c.save('settings_panel')


def button(name, selected):
    W, H = 440, 84
    c = Canvas(W, H)
    x0, x1, y0, y1 = 56, 384, 14, 70
    cx, cy = W / 2, H / 2
    pal = BRONZE_HI if selected else BRONZE
    # pontas de lança laterais
    if selected:
        for sx in (-1, 1):
            px = x0 if sx < 0 else x1
            d = union(poly(c, [(px + sx * 2, cy - 7), (px + sx * 34, cy), (px + sx * 2, cy + 7)]),
                      poly(c, [(px + sx * 14, cy), (px + sx * 24, cy - 10), (px + sx * 22, cy), (px + sx * 24, cy + 10)]),
                      segment(c, px + sx * 34, cy, px + sx * 50, cy, 1.4), circle(c, px + sx * 36, cy, 3.2))
            metal(c, d, pal, bevel=2, seed=51)
    else:
        for sx in (-1, 1):
            px = x0 if sx < 0 else x1
            d = union(poly(c, [(px + sx * 1, cy - 4), (px + sx * 16, cy), (px + sx * 1, cy + 4)]), circle(c, px + sx * 4, cy, 2.6))
            metal(c, d, pal, bevel=1.5, seed=52, alpha=0.85)
    # corpo com cantos chanfrados
    ch = 7
    body = poly(c, [(x0 + ch, y0), (x1 - ch, y0), (x1, y0 + ch), (x1, y1 - ch), (x1 - ch, y1), (x0 + ch, y1), (x0, y1 - ch), (x0, y0 + ch)])
    if selected:
        t = np.clip(1 - np.abs(c.x - cx) / ((x1 - x0) / 2), 0, 1)
        v = (c.y - y0) / (y1 - y0)
        g = fbm(c, 14, 53, 3)
        k = (0.45 + 0.55 * t ** 0.7) * (1.1 - 0.5 * v) * (0.85 + 0.25 * g)
        col = np.array((0.14, 0.015, 0.02), np.float32) + np.array((0.5, 0.07, 0.065), np.float32) * k[..., None]
        c.over(col, cov(body))
        flat(c, box(c, cx, y0 + 6, (x1 - x0) / 2 - 12, 2.5, 2), (1, 0.55, 0.45), 0.10)
    else:
        g = fbm(c, 14, 54, 3)
        col = np.array(INK, np.float32) * (0.8 + 0.45 * g[..., None])
        c.over(col, cov(body) * 0.94)
    ring = np.maximum(body, -(body + 3.2))
    metal(c, ring, pal, bevel=1.6, seed=55)
    inner = poly(c, [(x0 + ch + 5, y0 + 5), (x1 - ch - 5, y0 + 5), (x1 - 5, y0 + ch + 5), (x1 - 5, y1 - ch - 5), (x1 - ch - 5, y1 - 5), (x0 + ch + 5, y1 - 5), (x0 + 5, y1 - ch - 5), (x0 + 5, y0 + ch + 5)])
    metal(c, np.maximum(inner, -(inner + 1.1)), pal, bevel=0.8, seed=56, alpha=0.6 if not selected else 0.8)
    c.save(name)


def toggle():
    c = Canvas(100, 44)
    track = box(c, 50, 22, 44, 16, 9)
    flat(c, track, (0.03, 0.022, 0.02))
    metal(c, np.maximum(track, -(track + 3)), BRONZE, bevel=1.8, seed=61)
    c.save('toggle_track')
    c = Canvas(40, 36)
    k = box(c, 20, 18, 15, 12, 3)
    metal(c, k, RED, bevel=4, seed=62, grime=0.15)
    metal(c, np.maximum(k, -(k + 1.5)), BRONZE_HI, bevel=1, seed=63, alpha=0.7)
    c.save('toggle_knob')


def slider():
    W = 460
    c = Canvas(W, 34)
    cy = 17
    groove = box(c, W / 2, cy, W / 2 - 22, 5.5, 2.5)
    flat(c, groove, (0.025, 0.018, 0.016))
    metal(c, np.maximum(groove, -(groove + 1.6)), BRONZE, bevel=1.2, seed=71)
    for sx in (-1, 1):
        px = W / 2 + sx * (W / 2 - 18)
        d = union(poly(c, [(px - sx * 4, cy - 10), (px + sx * 12, cy), (px - sx * 4, cy + 10), (px + sx * 2, cy)]), segment(c, px - sx * 8, cy - 9, px - sx * 8, cy + 9, 1.8))
        metal(c, d, BRONZE, bevel=1.8, seed=72)
    c.save('slider_track')
    c = Canvas(W - 48, 12)
    d = box(c, (W - 48) / 2, 6, (W - 48) / 2, 4.2, 2)
    v = (c.y - 2) / 8
    col = np.array((0.36, 0.04, 0.045), np.float32) + np.array((0.42, 0.08, 0.06), np.float32) * (1 - np.abs(v - 0.35) * 1.6).clip(0, 1)[..., None]
    c.over(col, cov(d))
    c.save('slider_fill')
    c = Canvas(44, 44)
    disc = circle(c, 22, 22, 17)
    metal(c, disc, BRONZE, bevel=5, seed=73)
    metal(c, np.maximum(disc, -(disc + 2.4)), BRONZE_HI, bevel=1.5, seed=74)
    skull(c, 22, 20, 8.5)
    c.save('slider_knob')


def close_btn():
    c = Canvas(48, 48)
    b = box(c, 24, 24, 19, 19, 2)
    flat(c, b, (0.04, 0.03, 0.028))
    metal(c, np.maximum(b, -(b + 2.6)), BRONZE, bevel=1.6, seed=81)
    x = union(segment(c, 15, 15, 33, 33, 1.7), segment(c, 33, 15, 15, 33, 1.7))
    metal(c, x, BONE, bevel=1.4, seed=82)
    c.save('close_button')


def hud():
    # brasão espinhoso
    S = 180
    c = Canvas(S, S)
    cx = cy = S / 2
    pts = []
    for i in range(48):
        a = i / 48 * 2 * math.pi - math.pi / 2
        r = 84 if i % 3 == 0 else (72 if i % 3 == 1 else 74)
        if i % 12 == 0: r = 89
        pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    thorns = poly(c, pts)
    metal(c, thorns, BRONZE, bevel=3.5, seed=91)
    ring = circle(c, cx, cy, 64)
    metal(c, np.maximum(ring, -(ring + 9)), BRONZE_HI, bevel=3, seed=92)
    flat(c, circle(c, cx, cy, 55), (0.035, 0.026, 0.024))
    g = fbm(c, 8, 93, 3)
    c.over(np.array((0.07, 0.02, 0.022), np.float32) * (0.6 + 0.6 * g[..., None]), cov(circle(c, cx, cy, 54)) * 0.9)
    # sigilo: chama/losango com braços e anel (a Ressonância)
    sig = union(poly(c, [(cx, cy - 38), (cx + 12, cy - 6), (cx, cy + 34), (cx - 12, cy - 6)]),
                stroke(c, bezier((cx - 6, cy + 4), (cx - 26, cy - 2), (cx - 32, cy - 22), (cx - 22, cy - 34)), 3.2, 1.2),
                stroke(c, bezier((cx + 6, cy + 4), (cx + 26, cy - 2), (cx + 32, cy - 22), (cx + 22, cy - 34)), 3.2, 1.2),
                stroke(c, bezier((cx - 5, cy + 18), (cx - 20, cy + 22), (cx - 24, cy + 34), (cx - 14, cy + 40)), 2.4, 1.0),
                stroke(c, bezier((cx + 5, cy + 18), (cx + 20, cy + 22), (cx + 24, cy + 34), (cx + 14, cy + 40)), 2.4, 1.0))
    sig = sub(sig, poly(c, [(cx, cy - 22), (cx + 5, cy - 6), (cx, cy + 14), (cx - 5, cy - 6)]))
    metal(c, sig, BONE, bevel=2.5, seed=94)
    c.save('hud_emblem')

    def bar_frame(name, L, th, tip):
        c = Canvas(L, th + 18)
        cy = (th + 18) / 2
        body = box(c, (L - tip) / 2, cy, (L - tip) / 2 - 2, th / 2, 2)
        flat(c, body, (0.025, 0.018, 0.016), 0.92)
        metal(c, np.maximum(body, -(body + 2.2)), BRONZE, bevel=1.5, seed=95)
        x1 = L - tip - 2
        d = union(poly(c, [(x1 - 4, cy - th / 2 - 4), (L - 2, cy), (x1 - 4, cy + th / 2 + 4), (x1 + 4, cy)]), circle(c, x1, cy, th / 2 + 1))
        metal(c, d, BRONZE, bevel=2, seed=96)
        c.save(name)

    def bar_fill(name, L, th, pal):
        c = Canvas(L, th)
        d = box(c, L / 2, th / 2, L / 2, th / 2 - 0.5, 1.5)
        v = c.y / th
        g = fbm(c, 10, 97, 2)
        k = (1 - np.abs(v - 0.38) * 1.7).clip(0, 1) * (0.85 + 0.3 * g)
        col = np.array(pal[0], np.float32) + np.array(pal[1], np.float32) * k[..., None]
        c.over(col, cov(d))
        flat(c, box(c, L / 2, th * 0.28, L / 2 - 2, 0.9, 0.8), (1, 1, 1), 0.18)
        c.save(name)

    bar_frame('hud_life_frame', 500, 18, 26)
    bar_fill('hud_life_fill', 464, 14, ((0.3, 0.02, 0.03), (0.62, 0.09, 0.08)))
    bar_frame('hud_res_frame', 290, 10, 18)
    bar_fill('hud_res_fill', 264, 7, ((0.02, 0.18, 0.2), (0.12, 0.62, 0.66)))

    # ícone das Notas (diapasão com asas, de osso e bronze)
    c = Canvas(64, 64)
    fork = union(stroke(c, [(24, 8), (24, 32), (32, 40)], 2.6, 3.2), stroke(c, [(40, 8), (40, 32), (32, 40)], 2.6, 3.2), segment(c, 32, 38, 32, 58, 3.2),
                 poly(c, [(26, 50), (32, 62), (38, 50)]))
    wing = union(stroke(c, bezier((28, 30), (16, 34), (8, 24), (10, 12)), 2.2, 0.8), stroke(c, bezier((36, 30), (48, 34), (56, 24), (54, 12)), 2.2, 0.8))
    metal(c, wing, BRONZE, bevel=1.6, seed=98)
    metal(c, fork, BONE, bevel=2, seed=99)
    c.save('hud_notes_icon')

    # filete sob o contador (some para a esquerda) com voluta pendurada
    c = Canvas(300, 36)
    d = union(stroke(c, [(4, 10), (296, 10)], 0.4, 1.4), stroke(c, curl(250, 20, 8, 1.2, 1, -math.pi / 2), 1.8, 0.8), spike(c, 250, 12, 90, 16, 3))
    metal(c, d, BRONZE, bevel=1.4, seed=100)
    fade = np.clip((c.x - 4) / 120, 0, 1)
    c.a *= fade
    c.save('hud_notes_line')

    # selo de tecla/botão das dicas
    c = Canvas(52, 44)
    b = box(c, 26, 22, 22, 18, 5)
    flat(c, b, (0.04, 0.035, 0.033), 0.85)
    metal(c, np.maximum(b, -(b + 1.8)), BONE, bevel=1, seed=101, alpha=0.8)
    c.save('key_badge')


def backdrop():
    """Vinheta escura atrás dos painéis (a cena ao fundo fica escurecida nas bordas)."""
    c = Canvas(256, 144)
    v = np.hypot((c.x - 128) / 128, (c.y - 72) / 72)
    c.over((0.01, 0.006, 0.006), np.clip(0.25 + 0.55 * v ** 1.6, 0, 0.92))
    c.save('pause_backdrop')


if __name__ == '__main__':
    import sys
    jobs = {'pause': pause_panel, 'settings': settings_panel, 'buttons': lambda: (button('button_normal', False), button('button_selected', True)),
            'toggle': toggle, 'slider': slider, 'close': close_btn, 'hud': hud, 'backdrop': backdrop}
    for k in (sys.argv[1:] or jobs):   # ex.: python3 gen_gothic_ui.py pause hud
        jobs[k]()
