"""Etapa 1 do rig: prepara a malha do Aren.

- importa personagem.fbx (Tripo), aplica transformações;
- escala para 1.80 m, pés em z=0, centralizado em x/y pelos pés, olhando para -Y;
- merge by distance CONSERVADOR (1e-5 m): só funde vértices duplicados dentro das
  peças — NÃO pode fundir peças soltas (169 ilhas: fivelas, alças, antebraços...);
- normais: mantém as do modelo (recalcular por ilha inverteria peças de roupa internas);
- salva Aren_01_prep.blend e renders de referência (frente/lado/costas).

Rodar: blender -b --python aren_01_prep.py
"""
import os, sys, bmesh, bpy, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aren_common as C

C.reset_scene()
bpy.ops.import_scene.fbx(filepath=C.SOURCE_FBX)
objs = C.mesh_objects()
assert len(objs) == 1, "esperava 1 malha no FBX do Tripo"
body = objs[0]
body.name = "Aren_Body"
body.data.name = "Aren_Body_Mesh"
if body.data.materials:
    body.data.materials[0].name = "Aren_MAT"

# aplica transformações do import (rotação/escala do FBX)
bpy.context.view_layer.objects.active = body
body.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

mn, mx = C.world_bounds([body])
h = mx.z - mn.z
s = C.TARGET_HEIGHT / h
print("altura original %.4f m -> escala %.4f" % (h, s))

bm = bmesh.new()
bm.from_mesh(body.data)
# centro dos pés: média x/y dos vértices abaixo de 3% da altura
feet = [v.co for v in bm.verts if v.co.z < mn.z + 0.03 * h]
cx = sum(p.x for p in feet) / len(feet)
cy = sum(p.y for p in feet) / len(feet)
for v in bm.verts:
    v.co = mathutils.Vector(((v.co.x - cx) * s, (v.co.y - cy) * s, (v.co.z - mn.z) * s))
before = len(bm.verts)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
after = len(bm.verts)
bm.to_mesh(body.data)
bm.free()
body.data.update()
print("merge by distance: %d -> %d vértices (%d fundidos)" % (before, after, before - after))

mn, mx = C.world_bounds([body])
print("bounds finais", tuple(round(x, 3) for x in mn), tuple(round(x, 3) for x in mx))

out = os.path.join(C.ROOT, "Aren_01_prep.blend")
bpy.ops.wm.save_as_mainfile(filepath=out)
print("salvo", out)

# renders de referência
os.makedirs(C.RENDERS, exist_ok=True)
C.setup_render(1400)
cam = C.ortho_camera()
center = ((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, (mn.z + mx.z) / 2)
size = max(mx - mn) * 1.08
C.render_view(cam, center, (0, -1, 0), size, os.path.join(C.RENDERS, "prep_front.png"))
C.render_view(cam, center, (1, 0, 0), size, os.path.join(C.RENDERS, "prep_side.png"))
C.render_view(cam, center, (0, 1, 0), size, os.path.join(C.RENDERS, "prep_back.png"))
