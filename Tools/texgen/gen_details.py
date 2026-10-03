"""Texturas dos detalhes do terreno (capim e trigo), RGBA com alfa, geradas com numpy.
Rodar: blender -b --factory-startup --python Tools/texgen/gen_details.py
"""
import bpy, os, math, random
import numpy as np
OUT = os.path.expanduser("~/Unity/ParkourLab/Assets/Campanula/Textures/")

def save(img, name):
    h, w, _ = img.shape
    im = bpy.data.images.new(name, w, h, alpha=True)
    im.pixels.foreach_set(np.ascontiguousarray(img[::-1]).ravel())   # bpy guarda de baixo para cima
    im.filepath_raw = OUT + name; im.file_format = 'PNG'; im.save()
    print("TEX", name)

def blade(img, x0, h, w0, lean, col, rng):
    H, W, _ = img.shape
    steps = int(h * 1.2)
    for s in range(steps):
        t = s / steps
        y = H - 1 - int(t * h)
        x = x0 + lean * t * t * h
        w = max(0.6, w0 * (1 - t) ** 0.8)
        shade = 0.65 + 0.45 * t
        for dx in range(int(-w - 1), int(w + 2)):
            xi = int(x + dx)
            if 0 <= xi < W and 0 <= y < H:
                a = max(0.0, min(1.0, w + 0.5 - abs(x + dx - xi - 0.5 + 0.5 - (x - int(x)))))
                cov = max(0.0, min(1.0, (w - abs(dx)) + 0.5))
                if cov <= 0: continue
                c = np.array(col) * shade * (0.9 + 0.2 * rng.random())
                img[y, xi, :3] = img[y, xi, :3] * (1 - cov) + c * cov
                img[y, xi, 3] = max(img[y, xi, 3], cov)

def grass():
    rng = random.Random(7)
    W, H = 128, 128
    img = np.zeros((H, W, 4), dtype=np.float32)
    img[..., :3] = (0.22, 0.3, 0.12)
    for i in range(22):
        g = rng.uniform(0.75, 1.0)
        col = (0.28 * g + rng.uniform(-0.03, 0.05), 0.42 * g, 0.14 * g)
        if rng.random() < 0.2: col = (0.55, 0.5, 0.25)     # folhas secas
        blade(img, rng.uniform(10, W - 10), rng.uniform(H * 0.45, H * 0.98), rng.uniform(1.6, 3.2), rng.uniform(-0.25, 0.25), col, rng)
    save(img, "detail_grass.png")

def wheat():
    rng = random.Random(11)
    W, H = 128, 256
    img = np.zeros((H, W, 4), dtype=np.float32)
    img[..., :3] = (0.55, 0.45, 0.22)
    for i in range(14):
        x0 = rng.uniform(10, W - 10); h = rng.uniform(H * 0.7, H * 0.97); lean = rng.uniform(-0.12, 0.12)
        blade(img, x0, h, 1.4, lean, (0.78, 0.66, 0.36), rng)
        # espiga: grãos em zigue-zague no topo
        top_x = x0 + lean * h; top_y = H - h
        for k in range(16):
            t = k / 16
            yy = int(top_y + t * 38)
            for side in (-1, 1):
                cx = top_x + side * 2.2 + lean * (h - t * 38) * 0.0
                for dy in range(-3, 4):
                    for dx in range(-2, 3):
                        xi, yi = int(cx + dx), yy + dy
                        if 0 <= xi < W and 0 <= yi < H and dx * dx / 4.0 + dy * dy / 9.0 <= 1.0:
                            c = np.array((0.86, 0.72, 0.38)) * (0.8 + 0.25 * rng.random())
                            img[yi, xi, :3] = c; img[yi, xi, 3] = 1.0
    save(img, "detail_wheat.png")

grass(); wheat()
