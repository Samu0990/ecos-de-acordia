"""Linha central dos membros do lado ESQUERDO (+X) por ilha (o modelo é espelhado).
Rodar: blender -b Aren_01_prep.blend --python aren_measure3.py
"""
import bpy, json, os, sys, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aren_common as C

body = bpy.data.objects["Aren_Body"]
V = [v.co.copy() for v in body.data.vertices]
isl = json.load(open(os.path.join(C.ROOT, "islands.json")))

def pts(i, cond=lambda p: True):
    return [V[k] for k in isl[i]["verts"] if cond(V[k])]

def slices(name, ps, z0, z1, step=0.02, dz=0.008):
    print("==", name)
    z = z1
    while z >= z0 - 1e-6:
        s = [p for p in ps if abs(p.z - z) < dz]
        if len(s) >= 3:
            xs = [p.x for p in s]; ys = [p.y for p in s]
            print("   z=%.3f cx=%+.3f cy=%+.3f w=%.3f d=%.3f n=%d" % (z, (min(xs)+max(xs))/2, (min(ys)+max(ys))/2, max(xs)-min(xs), max(ys)-min(ys), len(s)))
        z -= step

slices("BRAÇO SUPERIOR E (ilha 0, x>0.15)", pts(0, lambda p: p.x > 0.15), 1.10, 1.50)
slices("ANTEBRAÇO+MÃO E (ilha 2)", pts(2), 0.78, 1.16)
slices("CALÇA lado E (ilha 5, x>0.005)", pts(5, lambda p: p.x > 0.005), 0.52, 1.14, 0.03)
slices("BOTA E (ilha 3)", pts(3), 0.0, 0.52, 0.03)
slices("CABEÇA/PESCOÇO (ilha 0, |x|<0.13)", pts(0, lambda p: abs(p.x) < 0.13), 1.30, 1.80, 0.03)
slices("TABARDO (ilha 6)", pts(6), 0.47, 1.10, 0.04)
foot = pts(3)
toe = min(foot, key=lambda p: p.y)
heel = max(foot, key=lambda p: p.y)
print("PÉ E: ponta", tuple(round(c, 3) for c in toe), "calcanhar", tuple(round(c, 3) for c in heel))
low = [p for p in foot if p.z < 0.03]
print("   sola: x[%+.3f,%+.3f] y[%+.3f,%+.3f]" % (min(p.x for p in low), max(p.x for p in low), min(p.y for p in low), max(p.y for p in low)))
hand = pts(2, lambda p: p.z < 0.93)
tip = min(hand, key=lambda p: p.z)
print("MÃO E: ponta mais baixa", tuple(round(c, 3) for c in tip))
