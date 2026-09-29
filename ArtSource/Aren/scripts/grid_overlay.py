"""Desenha grade métrica (a cada 5 cm, rótulo a cada 10 cm) sobre um render ortográfico
usando o .json salvo por aren_common.render_view. Roda no Python do sistema (PIL).
Uso: python3 grid_overlay.py render.png [crop_x0 crop_y0 crop_x1 crop_y1 em metros]
"""
import json, sys
from PIL import Image, ImageDraw

png = sys.argv[1]
meta = json.load(open(png + ".json"))
img = Image.open(png).convert("RGB")
W, H = img.size
cx, cy, cz = meta["center"]
d = meta["dir"]
ortho = meta["ortho"]
ppm = W / ortho  # pixels por metro

# eixo horizontal da imagem: frente(dir=-Y) -> X mundo; lado(dir=+X) -> -Y mundo...
# (câmera olha para -dir; "direita" da imagem = up x forward)
import math
fwd = (-d[0], -d[1], -d[2])
up = (0, 0, 1)
right = (fwd[1] * up[2] - fwd[2] * up[1], fwd[2] * up[0] - fwd[0] * up[2], fwd[0] * up[1] - fwd[1] * up[0])
axis_name = "X" if abs(right[0]) > 0.5 else "Y"
sign = right[0] if axis_name == "X" else right[1]
c_h = cx if axis_name == "X" else cy

def to_px(h, v):
    return (W / 2 + (h - c_h) * sign * ppm, H / 2 - (v - cz) * ppm)

draw = ImageDraw.Draw(img)
step = 0.05
v = math.floor((cz - ortho / 2) / step) * step
while v < cz + ortho / 2:
    y = H / 2 - (v - cz) * ppm
    major = abs((v * 100) % 10) < 0.01 or abs((v * 100) % 10 - 10) < 0.01
    draw.line([(0, y), (W, y)], fill=(255, 0, 0) if major else (255, 170, 170), width=1)
    if major:
        draw.text((2, y - 11), "z=%.2f" % v, fill=(255, 0, 0))
    v += step
hh = math.floor((c_h - ortho / 2) / step) * step
while hh < c_h + ortho / 2:
    x = W / 2 + (hh - c_h) * sign * ppm
    major = abs((hh * 100) % 10) < 0.01 or abs((hh * 100) % 10 - 10) < 0.01
    draw.line([(x, 0), (x, H)], fill=(0, 0, 255) if major else (170, 170, 255), width=1)
    if major:
        draw.text((x + 2, 2), "%s=%.1f" % (axis_name, hh), fill=(0, 0, 255))
    hh += step

out = png.replace(".png", "_grid.png")
if len(sys.argv) >= 6:
    x0, z0, x1, z1 = map(float, sys.argv[2:6])
    p0 = to_px(x0, z1); p1 = to_px(x1, z0)
    box = (int(min(p0[0], p1[0])), int(p0[1]), int(max(p0[0], p1[0])), int(p1[1]))
    img = img.crop(box)
    out = png.replace(".png", "_grid_%s.png" % "_".join(a.replace(".", "") for a in sys.argv[2:6]))
img.save(out)
print(out)
