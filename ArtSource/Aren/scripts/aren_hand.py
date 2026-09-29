"""Close da mão esquerda (ilha 2) + fatias para contar dedos separados.
Rodar: blender -b Aren_01_prep.blend --python aren_hand.py
"""
import bpy, bmesh, json, os, sys, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aren_common as C

isl = json.load(open(os.path.join(C.ROOT, "islands.json")))
body = bpy.data.objects["Aren_Body"]
keep = set(isl[2]["verts"])
bm = bmesh.new(); bm.from_mesh(body.data); bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep or v.co.z > 0.97], context='VERTS')
bm.to_mesh(body.data); body.data.update()

# fatias horizontais: agrupa pontos por proximidade para contar "dedos" separados
V = [v.co.copy() for v in body.data.vertices]
for z in [0.90, 0.88, 0.86, 0.84, 0.82, 0.80, 0.79]:
    s = [p for p in V if abs(p.z - z) < 0.004]
    # clusters simples por distância 2D
    clusters = []
    for p in s:
        for c in clusters:
            if any((p.xy - q.xy).length < 0.009 for q in c):
                c.append(p); break
        else:
            clusters.append([p])
    # funde clusters que se tocam
    merged = True
    while merged:
        merged = False
        for i in range(len(clusters)):
            for j in range(i + 1, len(clusters)):
                if any((a.xy - b.xy).length < 0.009 for a in clusters[i] for b in clusters[j]):
                    clusters[i] += clusters.pop(j); merged = True; break
            if merged: break
    desc = ["(%+.3f,%+.3f)n%d" % (sum(p.x for p in c)/len(c), sum(p.y for p in c)/len(c), len(c)) for c in clusters if len(c) >= 2]
    print("FATIA z=%.2f  %d grupos: %s" % (z, len(desc), " ".join(desc)))

C.setup_render(900)
mn, mx = C.world_bounds([body])
center = ((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, (mn.z + mx.z) / 2)
size = max(mx - mn) * 1.2
cam = C.ortho_camera()
for v, d in (("front", (0, -1, 0)), ("out", (1, 0, 0)), ("in", (-1, 0, 0)), ("back", (0, 1, 0)), ("below", (0, 0, -1))):
    C.render_view(cam, center, d, size, os.path.join(C.RENDERS, "hand_%s.png" % v))
print("BOUNDS mão", tuple(round(x, 3) for x in mn), tuple(round(x, 3) for x in mx))
