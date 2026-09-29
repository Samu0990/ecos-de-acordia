import bpy, json, os, sys, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aren_common as C
body = bpy.data.objects["Aren_Body"]
isl = json.load(open(os.path.join(C.ROOT, "islands.json")))
co = [v.co.copy() for v in body.data.vertices]
cores = {0: "tronco", 1: "anteD", 2: "anteE", 3: "botaE", 4: "botaD", 5: "tabardo", 6: "calca"}
trees = {}
for i in cores:
    vs = isl[i]["verts"]; kd = mathutils.kdtree.KDTree(len(vs))
    for k, vi in enumerate(vs): kd.insert(co[vi], k)
    kd.balance(); trees[i] = kd
for i, it in enumerate(isl):
    if i in cores or len(it["verts"]) < 40: continue
    vs = it["verts"]
    res = []
    for c, kd in trees.items():
        ds = sorted(kd.find(co[vi])[2] for vi in vs)
        res.append((ds[len(ds)//2], ds[0], cores[c]))
    res.sort()
    mn, mx = it["min"], it["max"]
    print("ISL %3d n=%4d z[%.2f,%.2f] x[%+.2f,%+.2f] y[%+.2f,%+.2f] | %s" % (i, len(vs), mn[2], mx[2], mn[0], mx[0], mn[1], mx[1], "  ".join("%s med%.3f min%.3f" % (r[2], r[0], r[1]) for r in res[:3])))
