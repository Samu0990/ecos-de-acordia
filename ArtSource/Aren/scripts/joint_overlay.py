"""Desenha os ossos (joints.json) sobre um render ortográfico (usa o .json do render).
Python do sistema (PIL). Uso: python3 joint_overlay.py render.png [filtro_de_nome]
"""
import json, os, sys
from PIL import Image, ImageDraw

png = sys.argv[1]
flt = sys.argv[2] if len(sys.argv) > 2 else ""
meta = json.load(open(png + ".json"))
joints = json.load(open(os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "joints.json")))
img = Image.open(png).convert("RGB")
W, H = img.size
cx, cy, cz = meta["center"]; d = meta["dir"]; ppm = W / meta["ortho"]
fwd = (-d[0], -d[1], -d[2]); up = (0, 0, 1)
right = (fwd[1] * up[2] - fwd[2] * up[1], fwd[2] * up[0] - fwd[0] * up[2], fwd[0] * up[1] - fwd[1] * up[0])
# "up" da imagem = up projetado (câmera com up Z, exceto vista de baixo)
if abs(fwd[2]) > 0.9:
    up = (0, 1 if fwd[2] < 0 else -1, 0)
    right = (fwd[1] * up[2] - fwd[2] * up[1], fwd[2] * up[0] - fwd[0] * up[2], fwd[0] * up[1] - fwd[1] * up[0])

def px(p):
    rel = (p[0] - cx, p[1] - cy, p[2] - cz)
    h = sum(rel[i] * right[i] for i in range(3)); v = sum(rel[i] * up[i] for i in range(3))
    return (W / 2 + h * ppm, H / 2 - v * ppm)

draw = ImageDraw.Draw(img)
for n, j in joints.items():
    if flt and flt not in n:
        continue
    col = (0, 255, 0) if j["deform"] else (255, 255, 0)
    if any(k in n for k in ("Hood", "Tabard", "Chain")): col = (0, 200, 255)
    if "Twist" in n: col = (255, 0, 255)
    a, bb = px(j["head"]), px(j["tail"])
    draw.line([a, bb], fill=col, width=2)
    r = 3
    draw.ellipse([a[0] - r, a[1] - r, a[0] + r, a[1] + r], outline=(255, 255, 255))
out = png.replace(".png", "_bones.png")
img.save(out); print(out)
