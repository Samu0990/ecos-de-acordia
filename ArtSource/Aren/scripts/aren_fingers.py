import bpy, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aren_common as C
body = bpy.data.objects["Aren_Body"]
V = [v.co.copy() for v in body.data.vertices]
isl = json.load(open(os.path.join(C.ROOT, "islands.json")))
H = [V[k] for k in isl[2]["verts"] if V[k].z < 0.97]
for z0, z1 in [(0.84, 0.87), (0.81, 0.84), (0.785, 0.81)]:
    for xcut in [(0.285, 0.36), (0.25, 0.285)]:
        s = [p for p in H if z0 <= p.z < z1 and xcut[0] <= p.x < xcut[1]]
        bins = {}
        for p in s:
            b = round(p.y / 0.004)
            bins[b] = bins.get(b, 0) + 1
        line = "".join(("#" if bins.get(b, 0) > 2 else ("+" if bins.get(b, 0) > 0 else ".")) for b in range(-20, 25))
        print("Z[%.3f,%.3f) X[%.3f,%.3f) y=-0.08..+0.10 | %s | n=%d" % (z0, z1, xcut[0], xcut[1], line, len(s)))
# por dedo: pontos com z<0.855 agrupados em faixas de y pela vista de fora
