"""Sussurrante — etapa 1: prepara a malha gerada pelo Tripo H3.1.

- Importa source/sussurrante_tripo_h31.glb (1,48 M faces, 1 m de altura, virado para +X).
- Gira para olhar para +Y (mesma convenção do esqueleto Quaternius/UAL2 no Blender),
  escala para ALTURA m (1,6x o Aren, como na prancha "Escala no jogo") e põe os pés em z = 0
  com o eixo vertical passando pelo quadril.
- Salva work/suss_hi.blend (fonte do bake, ~400 k faces) e work/suss_low.blend (~TRIS faces,
  a malha do jogo antes do UV novo/rig).

Rodar: blender -b --factory-startup --python ArtSource/Sussurrante/scripts/suss_01_prep.py
"""
import bpy, os, math, mathutils
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.dirname(HERE)
SRC = os.path.join(ART, "source", "sussurrante_tripo_h31.glb")
WORK = os.path.join(ART, "work")
ALTURA = 2.85
HI_FACES = 400000
TRIS = 26000
os.makedirs(WORK, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
body = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
for o in list(bpy.context.scene.objects):
    if o is not body:
        bpy.data.objects.remove(o, do_unlink=True)
body.name = "Sussurrante_Hi"
body.parent = None

me = body.data
n = len(me.vertices)
co = np.empty(n * 3, dtype=np.float64)
me.vertices.foreach_get("co", co)
co = co.reshape(-1, 3)
co = (np.array(body.matrix_world)[:3, :3] @ co.T).T + np.array(body.matrix_world.translation)
# +X -> +Y (gira 90 graus em Z)
rot = np.array([[0, -1, 0], [1, 0, 0], [0, 0, 1]], dtype=np.float64)
co = co @ rot.T
zmin, zmax = co[:, 2].min(), co[:, 2].max()
s = ALTURA / (zmax - zmin)
co *= s
co[:, 2] -= co[:, 2].min()
# eixo vertical pelo quadril: centro da fatia na altura da cintura (corda/cinto)
band = co[(co[:, 2] > ALTURA * 0.50) & (co[:, 2] < ALTURA * 0.56)]
cx, cy = np.median(band[:, 0]), np.median(band[:, 1])
co[:, 0] -= cx
co[:, 1] -= cy
body.matrix_world = mathutils.Matrix.Identity(4)
me.vertices.foreach_set("co", co.reshape(-1).astype(np.float32))
me.update()
print("ESCALA", s, "centro", cx, cy, "bbox", co.min(0), co.max(0))

# o glTF duplica os vértices nas costuras de UV: solda (o UV fica por canto de face) para a
# malha virar uma superfície só — senão o decimate abre frestas nas costuras e o bone heat /
# a separação de regiões param em cada ilha
import bmesh
bm = bmesh.new(); bm.from_mesh(me)
nv0 = len(bm.verts)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bm.to_mesh(me); bm.free(); me.update()
print("SOLDA", nv0, "->", len(me.vertices))

def decimate(obj, faces):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    d = obj.modifiers.new("Decimate", 'DECIMATE')
    d.ratio = min(1.0, faces / max(1, len(obj.data.polygons)))
    d.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier="Decimate")
    print("DECIMATE", obj.name, len(obj.data.polygons))

decimate(body, HI_FACES)
for p in body.data.polygons:
    p.use_smooth = True
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(WORK, "suss_hi.blend"), compress=True)

low = body.copy()
low.data = body.data.copy()
low.name = "Sussurrante"
low.data.name = "Sussurrante"
bpy.context.scene.collection.objects.link(low)
bpy.data.objects.remove(body, do_unlink=True)
decimate(low, TRIS)
for p in low.data.polygons:
    p.use_smooth = True
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(WORK, "suss_low.blend"), compress=True)
print("OK")
