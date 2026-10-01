"""Gerador de texturas procedurais (numpy puro). Roda no Python do Blender:
   ~/.local/opt/blender-5.2/5.2/python/bin/python3.13 script.py
Sem PIL: PNG escrito à mão (zlib)."""
import numpy as np, zlib, struct, os

def save_png(path, img):
    """img: float [0,1] HxW (L), HxWx3 (RGB) ou HxWx4 (RGBA)."""
    a = np.clip(np.asarray(img, dtype=np.float64), 0, 1)
    if a.ndim == 2: a = a[..., None]
    h, w, c = a.shape
    ct = {1: 0, 2: 4, 3: 2, 4: 6}[c]
    raw = (a * 255 + 0.5).astype(np.uint8)
    rows = b''.join(b'\x00' + raw[y].tobytes() for y in range(h))
    def chunk(t, d): return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, ct, 0, 0, 0)) \
        + chunk(b'IDAT', zlib.compress(rows, 9)) + chunk(b'IEND', b'')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, 'wb').write(png)

def grid(n, m=None):
    m = m or n
    y, x = np.mgrid[0:m, 0:n].astype(np.float64)
    return (x + 0.5) / n, (y + 0.5) / m

def value_noise(n, cells, seed=0):
    """Ruído de valor tileável (interpolação suave), n px, 'cells' células por lado."""
    rng = np.random.default_rng(seed)
    g = rng.random((cells, cells))
    x, y = grid(n)
    fx, fy = x * cells, y * cells
    ix, iy = np.floor(fx).astype(int), np.floor(fy).astype(int)
    tx, ty = fx - ix, fy - iy
    tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty)
    x0, y0 = ix % cells, iy % cells
    x1, y1 = (ix + 1) % cells, (iy + 1) % cells
    a = g[y0, x0] * (1 - tx) + g[y0, x1] * tx
    b = g[y1, x0] * (1 - tx) + g[y1, x1] * tx
    return a * (1 - ty) + b * ty

def fbm(n, base=4, octaves=5, seed=0, gain=0.5):
    out = np.zeros((n, n)); amp = 1.0; tot = 0
    for o in range(octaves):
        out += value_noise(n, base * 2 ** o, seed + o * 17) * amp
        tot += amp; amp *= gain
    return out / tot

def worley(n, points, seed=0, second=False, cells=None, jitter=0.85):
    """Distância ao ponto mais próximo (tileável). Retorna (F1, F2, id).
    cells=k usa grade k×k com jitter (sem pontos quase coincidentes -> sem "raios")."""
    rng = np.random.default_rng(seed)
    if cells:
        gx, gy = np.meshgrid(np.arange(cells), np.arange(cells))
        p = (np.stack([gx.ravel(), gy.ravel()], 1) + 0.5 + (rng.random((cells * cells, 2)) - 0.5) * jitter) / cells
    else:
        p = rng.random((points, 2))
    x, y = grid(n)
    f1 = np.full((n, n), 9.0); f2 = np.full((n, n), 9.0); ids = np.zeros((n, n), int)
    for i, (px, py) in enumerate(p):
        dx = np.abs(x - px); dx = np.minimum(dx, 1 - dx)
        dy = np.abs(y - py); dy = np.minimum(dy, 1 - dy)
        d = np.sqrt(dx * dx + dy * dy)
        closer = d < f1
        f2 = np.where(closer, f1, np.minimum(f2, d))
        ids = np.where(closer, i, ids)
        f1 = np.where(closer, d, f1)
    return f1, f2, ids

def normal_from_height(h, strength=2.0):
    """Mapa normal (tangent space, +Y para cima no estilo Unity/OpenGL) de um height tileável."""
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5 * h.shape[1] / 256.0
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5 * h.shape[0] / 256.0
    nx, ny, nz = -dx * strength, dy * strength, np.ones_like(h)
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.dstack([nx / l * 0.5 + 0.5, ny / l * 0.5 + 0.5, nz / l * 0.5 + 0.5])

def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)
