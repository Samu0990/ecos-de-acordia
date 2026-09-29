"""Renderiza ilhas isoladas (frente/lado/costas) para entender a estrutura da roupa.
Uso: blender -b Aren_01_prep.blend --python aren_render_islands.py -- nome 5,6 [cor]
"""
import bpy, bmesh, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aren_common as C

args = sys.argv[sys.argv.index("--") + 1:]
name, ids = args[0], [int(x) for x in args[1].split(",")]
isl = json.load(open(os.path.join(C.ROOT, "islands.json")))
keep = set()
for i in ids:
    keep.update(isl[i]["verts"])

body = bpy.data.objects["Aren_Body"]
bm = bmesh.new(); bm.from_mesh(body.data); bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context='VERTS')
bm.to_mesh(body.data); bm.free(); body.data.update()

C.setup_render(900)
mn, mx = C.world_bounds([body])
center = ((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, (mn.z + mx.z) / 2)
size = max(mx - mn) * 1.15
cam = C.ortho_camera()
for v, d in (("front", (0, -1, 0)), ("side", (1, 0, 0)), ("back", (0, 1, 0)), ("below", (0.0, -0.3, -1))):
    C.render_view(cam, center, d, size, os.path.join(C.RENDERS, "isl_%s_%s.png" % (name, v)))
