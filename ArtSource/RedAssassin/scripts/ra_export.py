"""Novo modelo do jogador: "red assassin" (Tripo, já riggado com 41 ossos).

- Remove o objeto auxiliar "Icosphere" do GLB.
- Escala para 1.78 m (o GLB vem com 1.0 m) aplicada na armadura e na malha.
- Decimate para ~40k triângulos (o original tem 229k: pesado demais para o Intel UHD 620).
- Texturas 4096 → 2048; o mapa "rm" do glTF (G = rugosidade, B = metal) vira o
  MetallicGloss do Standard da Unity (R = metal, A = suavidade).
- Ossos não-deform FluteSocket (mão direita, pegada) e FluteHolster (costas, diagonal) e
  a flauta do Aren (de Aren_05_final.blend) presa na bainha — o ArenFlute procura esses nomes.
- Exporta Assets/Aren/Character/RedAssassin/RedAssassin.fbx com os mesmos eixos do Aren.fbx.

Rodar: blender -b --factory-startup --python ArtSource/RedAssassin/scripts/ra_export.py
"""
import bpy, os, math, mathutils
import numpy as np

V = mathutils.Vector
HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.dirname(HERE)
PROJ = os.path.dirname(os.path.dirname(ART))
SRC = os.path.expanduser("~/Downloads/red+assassin+3d+model.glb")
OUT_DIR = os.path.join(PROJ, "Assets", "Aren", "Character", "RedAssassin")
TEX_DIR = os.path.join(OUT_DIR, "Textures")
FLUTE_BLEND = os.path.join(PROJ, "ArtSource", "Aren", "Aren_05_final.blend")
SCALE = 1.78
TARGET_TRIS = 40000
os.makedirs(TEX_DIR, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
for o in list(bpy.context.scene.objects):
    if o.name.startswith("Icosphere"):
        bpy.data.objects.remove(o, do_unlink=True)
rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
body = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
rig.name = "RA_Rig"; body.name = "RA_Body"

# ---------------------------------------------------------------- escala aplicada
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); body.select_set(True)
bpy.context.view_layer.objects.active = rig
rig.scale = (SCALE, SCALE, SCALE)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

# ---------------------------------------------------------------- decimate (antes da armadura)
me = body.data
me.calc_loop_triangles()
tris0 = len(me.loop_triangles)
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True); bpy.context.view_layer.objects.active = body
dec = body.modifiers.new("Decimate", 'DECIMATE')
dec.ratio = min(1.0, TARGET_TRIS / tris0)
dec.use_collapse_triangulate = True
bpy.ops.object.modifier_move_to_index(modifier="Decimate", index=0)
bpy.ops.object.modifier_apply(modifier="Decimate")
body.data.calc_loop_triangles()
print("TRIS", tris0, "->", len(body.data.loop_triangles))

# ---------------------------------------------------------------- material e texturas
mat = body.data.materials[0]
mat.name = "RedAssassin_MAT"
imgs = {}
for n in mat.node_tree.nodes:
    if n.type == 'TEX_IMAGE' and n.image:
        nm = n.image.name.lower()
        if "basecolor" in nm: imgs["base"] = n.image
        elif "normal" in nm: imgs["normal"] = n.image
        elif "_rm" in nm or nm.endswith("rm"): imgs["rm"] = n.image

def save_png(img, path, size=2048, convert=None):
    img.scale(size, size)
    px = np.empty(size * size * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(size, size, 4)
    if convert is not None: px = convert(px)
    out = bpy.data.images.new(os.path.basename(path), size, size, alpha=True)
    out.pixels.foreach_set(px.ravel())
    out.filepath_raw = path; out.file_format = 'PNG'
    out.save()
    print("TEX", path)

save_png(imgs["base"], os.path.join(TEX_DIR, "RedAssassin_BaseColor.png"))
save_png(imgs["normal"], os.path.join(TEX_DIR, "RedAssassin_Normal.png"))
def rm_to_metallic_gloss(px):
    out = np.zeros_like(px)
    metal = px[..., 2]; rough = px[..., 1]
    out[..., 0] = metal; out[..., 1] = metal; out[..., 2] = metal
    out[..., 3] = 1.0 - rough
    return out
save_png(imgs["rm"], os.path.join(TEX_DIR, "RedAssassin_MetallicGloss.png"), convert=rm_to_metallic_gloss)

# ---------------------------------------------------------------- ossos da flauta
verts = np.array([body.matrix_world @ v.co for v in body.data.vertices])
band = verts[(verts[:, 2] > 1.18) & (verts[:, 2] < 1.36) & (np.abs(verts[:, 0]) < 0.12)]
back_y = float(band[:, 1].max())     # o personagem olha para −Y: as costas são +Y
print("COSTAS y", back_y)

bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = rig.data.edit_bones
rh = eb["R_Hand"]
d = (rh.tail - rh.head)
palm = rh.head + d * 0.42 + V((0.012, -0.004, 0.0))    # mão direita (−X) com a palma para dentro (+X)
s = eb.new("FluteSocket")
s.head = palm
s.tail = palm + V((0.0, -0.1, 0.0))   # flauta saindo do punho para a frente, como uma lâmina
s.roll = 0.0
s.parent = rh; s.use_deform = False
h = eb.new("FluteHolster")
h.head = V((0.0, back_y + 0.035, 1.27))
h.tail = h.head + V((0.18, 0.0, 0.18))     # diagonal nas costas, como no Aren
h.roll = 0.0
h.parent = eb["Spine02"]; h.use_deform = False
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- flauta do Aren
with bpy.data.libraries.load(FLUTE_BLEND, link=False) as (src, dst):
    dst.objects = [n for n in src.objects if n == "Aren_Flute" or n.startswith("Flute_")]
flute = None
for o in dst.objects:
    if o is None: continue
    bpy.context.scene.collection.objects.link(o)
    if o.name == "Aren_Flute": flute = o
for o in dst.objects:
    if o is not None and o is not flute and o.parent is None:
        o.parent = flute
flute.parent = None
bpy.context.view_layer.update()
flute.parent = rig
flute.parent_type = 'BONE'
flute.parent_bone = "FluteHolster"
bone = rig.data.bones["FluteHolster"]
flute.matrix_world = rig.matrix_world @ bone.matrix_local @ mathutils.Matrix.Translation((0, -0.17, 0))
bpy.context.view_layer.update()

# ---------------------------------------------------------------- salvar e exportar
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ART, "RedAssassin_final.blend"))
bpy.ops.object.select_all(action='DESELECT')
for o in (rig, body, flute, *flute.children):
    o.select_set(True)
bpy.context.view_layer.objects.active = rig
fbx = os.path.join(OUT_DIR, "RedAssassin.fbx")
bpy.ops.export_scene.fbx(
    filepath=fbx, use_selection=True,
    object_types={'ARMATURE', 'MESH', 'EMPTY'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y',
    use_mesh_modifiers=False, mesh_smooth_type='FACE',
    add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
    use_armature_deform_only=False, bake_anim=False,
    path_mode='STRIP', embed_textures=False)
print("EXPORT", fbx, os.path.getsize(fbx) // 1024, "KB")
