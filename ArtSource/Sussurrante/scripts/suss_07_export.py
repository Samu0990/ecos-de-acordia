"""Sussurrante — etapa 7: exporta o FBX do jogo.

- Abre work/suss_uv.blend (malha 26 k tris com o atlas do Tripo, pesos e o esqueleto UAL2).
- Ossos-marcadores (sem deformação) que o jogo usa:
    weapon_r / weapon_tip_r  pegada da lâmina (centro do punho fechado) e direção da lâmina;
    eye_l / eye_r            fundo das órbitas (brilho violeta dos olhos);
    mouth                    origem do grito;
    chest_fx                 a fenda acesa do peito (luz/partículas).
- LOD1 (~8,5 k tris, mesmos pesos e UV) para longe da câmera.
- FBX com os mesmos eixos/opções dos aldeões (Assets/Aren/Character/Villagers).

Rodar: blender -b --factory-startup --python ArtSource/Sussurrante/scripts/suss_07_export.py
"""
import bpy, os, sys, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import suss_common as C

V = mathutils.Vector
LOD1_TRIS = 8500
bpy.ops.wm.open_mainfile(filepath=os.path.join(C.WORK, "suss_uv.blend"))
body = bpy.data.objects["Sussurrante"]
rig = bpy.data.objects["Armature"]
for m in body.modifiers:
    m.show_render = m.show_viewport = True
if "Region" in body.data.color_attributes:
    body.data.color_attributes.remove(body.data.color_attributes["Region"])

MARKERS = {
    # nome: (pai, cabeça, cauda) no mundo
    "weapon_r":     ("hand_r",   (0.565, -0.020, 1.130), (0.565, 0.100, 1.130)),
    "weapon_tip_r": ("weapon_r", (0.565, 1.190, 1.130), (0.565, 1.240, 1.130)),
    "eye_r":        ("Head",     (0.060, 0.120, 2.625), (0.060, 0.150, 2.625)),
    "eye_l":        ("Head",     (-0.064, 0.120, 2.625), (-0.064, 0.150, 2.625)),
    "mouth":        ("Head",     (0.000, 0.210, 2.500), (0.000, 0.250, 2.500)),
    "chest_fx":     ("spine_03", (0.000, 0.140, 2.050), (0.000, 0.180, 2.050)),
}
inv = rig.matrix_world.inverted()
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = rig.data.edit_bones
for name, (parent, h, t) in MARKERS.items():
    b = eb.get(name) or eb.new(name)
    b.head = inv @ V(h)
    b.tail = inv @ V(t)
    b.parent = eb[parent]
    b.use_connect = False
    b.use_deform = False
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- LOD1
lod1 = body.copy()
lod1.data = body.data.copy()
lod1.name = "Sussurrante_LOD1"
lod1.data.name = "Sussurrante_LOD1"
bpy.context.scene.collection.objects.link(lod1)
bpy.ops.object.select_all(action='DESELECT')
lod1.select_set(True)
bpy.context.view_layer.objects.active = lod1
arm_mods = [m for m in lod1.modifiers if m.type == 'ARMATURE']
for m in arm_mods:
    m.show_viewport = False
d = lod1.modifiers.new("Decimate", 'DECIMATE')
d.ratio = LOD1_TRIS / len(body.data.polygons)
d.use_collapse_triangulate = True
bpy.ops.object.modifier_move_to_index(modifier="Decimate", index=0)
bpy.ops.object.modifier_apply(modifier="Decimate")
for m in arm_mods:
    m.show_viewport = True
# o decimate interpola pesos: limita/normaliza de novo (máx. 4 influências)
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
for mo in (body, lod1):
    mo.data.calc_loop_triangles()
    print("MALHA", mo.name, "tris", len(mo.data.loop_triangles), "verts", len(mo.data.vertices))

# o personagem olha para +Y no Blender (como a armadura Quaternius); no FBX ele precisa olhar para
# -Y (o "para a frente" do Blender) para chegar à Unity olhando +Z — com a T-pose explícita do
# SussurranteSetup, um corpo de costas inverte o espaço de músculos (braços subiam no repouso)
import math
rig.rotation_euler.z += math.pi
body.name = "Sussurrante_LOD0"      # a raiz do FBX já se chama "Sussurrante": nome repetido confunde o avatar
bpy.context.view_layer.update()
os.makedirs(C.OUT, exist_ok=True)
bpy.ops.object.select_all(action='DESELECT')
for o in (rig, body, lod1):
    o.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(
    filepath=os.path.join(C.OUT, "Sussurrante.fbx"), use_selection=True,
    object_types={'ARMATURE', 'MESH'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y',
    use_mesh_modifiers=False, mesh_smooth_type='FACE',
    add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
    use_armature_deform_only=False, bake_anim=False,
    path_mode='STRIP', embed_textures=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(C.WORK, "suss_final.blend"), compress=True)
print("OK", os.path.join(C.OUT, "Sussurrante.fbx"))
