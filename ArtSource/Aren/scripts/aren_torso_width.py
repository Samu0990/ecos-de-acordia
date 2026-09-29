import bpy, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aren_common as C
body = bpy.data.objects["Aren_Body"]; V=[v.co for v in body.data.vertices]
isl = json.load(open(os.path.join(C.ROOT, "islands.json")))
gn = {g.index: g.name for g in body.vertex_groups}
vs = isl[0]["verts"]
for z in [1.12,1.16,1.20,1.24,1.28,1.32,1.36,1.40]:
    row=[]
    for x in [0.13,0.14,0.15,0.16,0.17,0.18,0.19,0.20,0.21,0.22]:
        s=[k for k in vs if abs(V[k].z-z)<0.012 and abs(V[k].x-x)<0.005]
        if not s: row.append("  .  "); continue
        arm=sum(g.weight for k in s for g in body.data.vertices[k].groups if "Arm" in gn[g.group] or "Shoulder" in gn[g.group])/len(s)
        row.append(" %.2f"%arm)
    print("z=%.2f |"%z+"".join(row))
print("x:       0.13 0.14 0.15 0.16 0.17 0.18 0.19 0.20 0.21 0.22   (peso médio de braço/ombro, lado +X, ilha 0)")
