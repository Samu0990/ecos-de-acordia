"""Após posar, mede se cada acessório continua encostado no corpo (peça flutuando = peso errado)."""
import bpy, json, os, sys, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aren_common as C
POSE = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "tpose"
src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "aren_04_stress.py")).read().split("args = sys.argv")[0]
exec(src)
body = bpy.data.objects["Aren_Body"]
isl = json.load(open(os.path.join(C.ROOT, "islands.json")))
rest = [v.co.copy() for v in body.data.vertices]
def posed_coords():
    dg = bpy.context.evaluated_depsgraph_get(); ev = body.evaluated_get(dg); me = ev.to_mesh()
    p = [v.co.copy() for v in me.vertices]; ev.to_mesh_clear(); return p
cores = [0, 1, 2, 3, 4, 5, 6, 14, 15]
def gap_table(P):
    core_v = [vi for i in cores for vi in isl[i]["verts"]]
    kd = mathutils.kdtree.KDTree(len(core_v))
    for k, vi in enumerate(core_v): kd.insert(P[vi], k)
    kd.balance()
    res = {}
    for i, it in enumerate(isl):
        if i in cores: continue
        ds = sorted(kd.find(P[vi])[2] for vi in it["verts"])
        res[i] = ds[len(ds) // 2]
    return res
reset(); g0 = gap_table(rest)
reset(); POSES[POSE](); g1 = gap_table(posed_coords())
gn = {g.index: g.name for g in body.vertex_groups}
bad = sorted(((g1[i] - g0[i], i) for i in g0), reverse=True)[:12]
for dg_, i in bad:
    it = isl[i]; acc = {}
    for k in it["verts"]:
        for g in body.data.vertices[k].groups: acc[gn[g.group]] = acc.get(gn[g.group], 0) + g.weight
    top = sorted(acc.items(), key=lambda kv: -kv[1])[:3]
    print("%s ILHA %3d n=%3d gap_rest=%.3f gap_pose=%.3f z[%.2f,%.2f] x[%+.2f,%+.2f] | %s" % (POSE, i, len(it["verts"]), g0[i], g1[i], it["min"][2], it["max"][2], it["min"][0], it["max"][0], ", ".join("%s:%.0f" % t for t in top)))
