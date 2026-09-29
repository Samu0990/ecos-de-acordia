import bpy, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aren_common as C
sys.argv += ["--", "tpose"]
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "aren_04_stress.py")).read().split("args = sys.argv")[0])
reset(); tpose()
body = bpy.data.objects["Aren_Body"]
isl = json.load(open(os.path.join(C.ROOT, "islands.json")))
dg = bpy.context.evaluated_depsgraph_get(); ev = body.evaluated_get(dg); me = ev.to_mesh()
rest = [v.co.copy() for v in body.data.vertices]; posed = [v.co.copy() for v in me.vertices]
gn = {g.index: g.name for g in body.vertex_groups}
rows = []
for i, it in enumerate(isl):
    vs = it["verts"]
    disp = [(posed[k] - rest[k]).length for k in vs]
    # espalhamento: variação do deslocamento dentro da ilha (web = uns vértices ficam, outros vão)
    rows.append((max(disp) - min(disp), max(disp), i, len(vs)))
rows.sort(reverse=True)
for spread, mx, i, n in rows[:14]:
    it = isl[i]; vs = it["verts"]
    acc = {}
    for k in vs:
        for g in body.data.vertices[k].groups:
            acc[gn[g.group]] = acc.get(gn[g.group], 0) + g.weight
    top = sorted(acc.items(), key=lambda kv: -kv[1])[:4]
    print("ILHA %3d n=%4d spread=%.3f max=%.3f bbox z[%.2f,%.2f] x[%+.2f,%+.2f] | %s" % (i, n, spread, mx, it["min"][2], it["max"][2], it["min"][0], it["max"][0], ", ".join("%s:%.0f" % t for t in top)))
