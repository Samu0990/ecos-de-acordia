import bpy, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aren_common as C
body = bpy.data.objects["Aren_Body"]
V = [v.co.copy() for v in body.data.vertices]
isl = json.load(open(os.path.join(C.ROOT, "islands.json")))
def pts(i, cond=lambda p: True): return [V[k] for k in isl[i]["verts"] if cond(V[k])]
def slices(name, ps, z0, z1, step=0.03, dz=0.01):
    print("==", name); z = z1
    while z >= z0 - 1e-6:
        s = [p for p in ps if abs(p.z - z) < dz]
        if len(s) >= 3:
            xs=[p.x for p in s]; ys=[p.y for p in s]
            print("   z=%.3f cx=%+.3f cy=%+.3f w=%.3f d=%.3f n=%d" % (z,(min(xs)+max(xs))/2,(min(ys)+max(ys))/2,max(xs)-min(xs),max(ys)-min(ys),len(s)))
        z -= step
slices("CALÇA perna E (ilha 6, x>0.005)", pts(6, lambda p: p.x > 0.005), 0.47, 1.10)
tab = pts(5)
for nm, cond in (("frente", lambda p: p.y < -0.06 and abs(p.x) < 0.09), ("trás", lambda p: p.y > 0.13 and abs(p.x) < 0.12), ("lado E", lambda p: p.x > 0.15), ("lado D", lambda p: p.x < -0.15)):
    s = [p for p in tab if cond(p)]
    print("ABA %s: n=%d x[%+.3f,%+.3f] y[%+.3f,%+.3f] z[%.3f,%.3f]" % (nm, len(s), min(p.x for p in s), max(p.x for p in s), min(p.y for p in s), max(p.y for p in s), min(p.z for p in s), max(p.z for p in s)))
slices("TABARDO lado E (x>0.15)", [p for p in tab if p.x > 0.15], 0.5, 1.12, 0.06)
slices("TABARDO trás (y>0.13)", [p for p in tab if p.y > 0.13], 0.5, 1.12, 0.06)
slices("TABARDO frente (y<-0.06)", [p for p in tab if p.y < -0.06], 0.5, 1.12, 0.06)
