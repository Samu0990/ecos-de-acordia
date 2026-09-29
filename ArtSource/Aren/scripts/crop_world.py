"""Recorta região (centro em metros, meia-largura) de um render ortográfico. Uso: crop_world.py png x y z half out"""
import json, sys
from PIL import Image
png, x, y, z, half, out = sys.argv[1], *map(float, sys.argv[2:6]), sys.argv[6]
m = json.load(open(png + ".json")); img = Image.open(png)
W = img.width; ppm = W / m["ortho"]; cx, cy, cz = m["center"]; d = m["dir"]
fwd = (-d[0], -d[1], -d[2]); up = (0, 0, 1)
right = (fwd[1]*up[2]-fwd[2]*up[1], fwd[2]*up[0]-fwd[0]*up[2], fwd[0]*up[1]-fwd[1]*up[0])
rel = (x-cx, y-cy, z-cz); h = sum(rel[i]*right[i] for i in range(3)); v = sum(rel[i]*up[i] for i in range(3))
px, py = W/2 + h*ppm, W/2 - v*ppm; r = half*ppm
img.crop((int(px-r), int(py-r), int(px+r), int(py+r))).resize((400, 400)).save(out)
