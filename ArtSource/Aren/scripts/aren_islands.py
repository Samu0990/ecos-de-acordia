"""Lista as peças soltas (ilhas) do Aren com bbox e salva a classificação em JSON.
Também mede a linha central de ilhas grandes (fatias em z).
Rodar: blender -b Aren_01_prep.blend --python aren_islands.py
"""
import bpy, bmesh, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aren_common as C

body = bpy.data.objects["Aren_Body"]
bm = bmesh.new(); bm.from_mesh(body.data); bm.verts.ensure_lookup_table()
seen = [False] * len(bm.verts); islands = []
for v in bm.verts:
    if seen[v.index]:
        continue
    stack = [v]; seen[v.index] = True; comp = []
    while stack:
        x = stack.pop(); comp.append(x.index)
        for e in x.link_edges:
            y = e.other_vert(x)
            if not seen[y.index]:
                seen[y.index] = True; stack.append(y)
    islands.append(comp)
islands.sort(key=len, reverse=True)

info = []
for i, comp in enumerate(islands):
    ps = [bm.verts[k].co for k in comp]
    mn = [min(p[a] for p in ps) for a in range(3)]
    mx = [max(p[a] for p in ps) for a in range(3)]
    info.append({"id": i, "n": len(comp), "min": mn, "max": mx, "verts": comp})
    if len(comp) >= 120:
        print("ISL %3d n=%5d x[%+.3f,%+.3f] y[%+.3f,%+.3f] z[%.3f,%.3f]" % (i, len(comp), mn[0], mx[0], mn[1], mx[1], mn[2], mx[2]))
print("TOTAL", len(islands), "ilhas;", sum(1 for c in islands if len(c) < 120), "pequenas (<120 v)")
json.dump(info, open(os.path.join(C.ROOT, "islands.json"), "w"))

def centerline(idx, z0, z1, step=0.03, dz=0.01):
    comp = islands[idx]; ps = [bm.verts[k].co for k in comp]
    print("CENTERLINE ilha", idx)
    z = z1
    while z >= z0:
        s = [p for p in ps if abs(p.z - z) < dz]
        if len(s) >= 3:
            xs = [p.x for p in s]; ys = [p.y for p in s]
            print("   z=%.2f cx=%+.3f cy=%+.3f w=%.3f d=%.3f" % (z, (min(xs)+max(xs))/2, (min(ys)+max(ys))/2, max(xs)-min(xs), max(ys)-min(ys)))
        z -= step

for a in sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []:
    idx, z0, z1 = a.split(":")
    centerline(int(idx), float(z0), float(z1))
