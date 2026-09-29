"""Etapa 5: flauta do Aren + bainha + export FBX para o Unity.

- Osso FluteHolster (não-deform, filho de Spine2): flauta atravessada nas costas quando
  não está em combate (mãos livres para parkour/escalada). Um script no Unity troca a
  flauta entre FluteHolster e FluteSocket (mão direita) ao sacar/guardar.
- Flauta modelada aqui (madeira escura, anéis dourados, 6 orifícios, bocal, pegada de
  couro vermelho), ~0.58 m, origem no ponto de pegada, eixo +Y para o bocal.
- Âncoras (empties, viram Transforms no Unity): Flute_Tip, Flute_Center, Flute_Mouth,
  Flute_Foot — VFX nascem daqui, não de posição aproximada (rig doc §26).
- Export: Aren.fbx (armadura + corpo + flauta presa ao osso FluteHolster na pose de descanso).

Rodar: blender -b Aren_03_skin.blend --python aren_05_flute_export.py
"""
import bpy, bmesh, math, os, sys, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aren_common as C

V = mathutils.Vector
rig = bpy.data.objects["Aren_Rig"]
body = bpy.data.objects["Aren_Body"]

# ---------------------------------------------------------------- osso da bainha
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = rig.data.edit_bones
if "FluteHolster" not in eb:
    h = eb.new("FluteHolster")
    # diagonal nas costas: do ombro esquerdo (alto) para o quadril direito (baixo), por fora do colete
    h.head = V((0.0, 0.215, 1.30))
    h.tail = h.head + V((0.18, 0.0, 0.18))
    h.parent = eb["Spine2"]
    h.use_deform = False
    h.roll = 0.0
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- materiais
def mat(name, rgb, metal=0.0, rough=0.5):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    m.diffuse_color = (*rgb, 1)
    return m
M_WOOD = mat("Flute_Wood", (0.24, 0.12, 0.065), 0.0, 0.4)   # clara o bastante para ler sobre a roupa preta
M_GOLD = mat("Flute_Gold", (0.83, 0.62, 0.26), 1.0, 0.3)
M_HOLE = mat("Flute_Hole", (0.01, 0.005, 0.005), 0.0, 0.9)
M_WRAP = mat("Flute_Wrap", (0.45, 0.04, 0.04), 0.0, 0.7)

# ---------------------------------------------------------------- malha da flauta
L_HEAD, L_FOOT = 0.47, -0.11          # extensão ao longo de +Y a partir da pegada
bm = bmesh.new()
SEG = 20

def ring(y0, y1, r0, r1, mi, cap0=False, cap1=False):
    """Tubo entre y0 e y1 (eixo Y), raio r0->r1, índice de material mi."""
    vs0, vs1 = [], []
    for i in range(SEG):
        a = 2 * math.pi * i / SEG
        vs0.append(bm.verts.new((r0 * math.cos(a), y0, r0 * math.sin(a))))
        vs1.append(bm.verts.new((r1 * math.cos(a), y1, r1 * math.sin(a))))
    for i in range(SEG):
        f = bm.faces.new((vs0[i], vs0[(i + 1) % SEG], vs1[(i + 1) % SEG], vs1[i]))
        f.material_index = mi
    if cap0:
        f = bm.faces.new(list(reversed(vs0))); f.material_index = mi
    if cap1:
        f = bm.faces.new(vs1); f.material_index = mi

R = 0.0115
def body_r(y):   # leve afunilamento para o pé
    t = (y - L_FOOT) / (L_HEAD - L_FOOT)
    return 0.0122 + 0.0022 * t   # ~1.2-1.45 cm: legível na distância de câmera do jogo

# segmentos: [pé]...[anel][corpo][anel][pegada][anel][cabeça][anel]
cuts = [L_FOOT, L_FOOT + 0.012, -0.045, -0.035, 0.035, 0.045, 0.175, 0.188, L_HEAD - 0.013, L_HEAD]
mats = [1, 0, 1, 3, 1, 0, 1, 0, 1]   # 0 madeira, 1 ouro, 2 furo, 3 couro
for k in range(len(cuts) - 1):
    y0, y1 = cuts[k], cuts[k + 1]
    mi = mats[k]
    r0, r1 = body_r(y0), body_r(y1)
    if mi == 1:
        r0 += 0.0020; r1 += 0.0020
    if mi == 3:
        r0 += 0.0010; r1 += 0.0010
    ring(y0, y1, r0, r1, mi, cap0=(k == 0), cap1=(k == len(cuts) - 2))

def disc(y, ang, rx, ry, mi, lift=0.0006):
    """Disco escuro na superfície (furo) no ângulo 'ang' em torno do eixo Y."""
    r = body_r(y) + lift
    n = V((math.cos(ang), 0, math.sin(ang)))
    t = V((0, 1, 0)); b = n.cross(t)
    c = V((0, y, 0)) + n * r
    vs = []
    for i in range(10):
        a = 2 * math.pi * i / 10
        vs.append(bm.verts.new(c + t * (ry * math.cos(a)) + b * (rx * math.sin(a))))
    f = bm.faces.new(vs); f.material_index = mi

TOP = math.pi / 2            # furos para "cima" (+Z local)
disc(0.415, TOP, 0.0045, 0.0060, 2)                    # bocal (embocadura oval)
for i, y in enumerate([0.07, 0.10, 0.13, 0.215, 0.245, 0.275]):
    disc(y, TOP, 0.0032, 0.0032, 2)                     # 6 orifícios

me = bpy.data.meshes.new("Aren_Flute_Mesh")
bm.to_mesh(me); bm.free()
for m in (M_WOOD, M_GOLD, M_HOLE, M_WRAP):
    me.materials.append(m)
for p in me.polygons:
    p.use_smooth = True
flute = bpy.data.objects.new("Aren_Flute", me)
bpy.context.scene.collection.objects.link(flute)

def empty(name, y):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = 'SPHERE'; e.empty_display_size = 0.01
    bpy.context.scene.collection.objects.link(e)
    e.parent = flute
    e.location = (0, y, 0)
    return e
empty("Flute_Tip", L_HEAD); empty("Flute_Center", 0.18); empty("Flute_Mouth", 0.415); empty("Flute_Foot", L_FOOT)

# ---------------------------------------------------------------- prende na bainha (costas)
bpy.context.view_layer.update()
flute.parent = rig
flute.parent_type = 'BONE'
flute.parent_bone = "FluteHolster"
bone = rig.data.bones["FluteHolster"]
# eixo Y do osso = eixo Y da flauta; pegada 0.23 m acima da cabeça do osso -> flauta centrada nas costas
M = rig.matrix_world @ bone.matrix_local
flute.matrix_world = M @ mathutils.Matrix.Translation((0, -0.17, 0))
bpy.context.view_layer.update()

# ---------------------------------------------------------------- export
out_blend = os.path.join(C.ROOT, "Aren_05_final.blend")
bpy.ops.wm.save_as_mainfile(filepath=out_blend)

bpy.ops.object.select_all(action='DESELECT')
for o in (rig, body, flute, *flute.children):
    o.select_set(True)
bpy.context.view_layer.objects.active = rig
fbx = os.path.join(os.path.dirname(os.path.dirname(C.ROOT)), "Assets", "Aren", "Character", "Aren.fbx")
os.makedirs(os.path.dirname(fbx), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=fbx, use_selection=True,
    object_types={'ARMATURE', 'MESH', 'EMPTY'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y',
    use_mesh_modifiers=False, mesh_smooth_type='FACE',
    add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
    use_armature_deform_only=False, bake_anim=False,
    path_mode='COPY', embed_textures=True)
print("EXPORT", fbx, os.path.getsize(fbx) // 1024, "KB")
print("FLAUTA tris", sum(len(p.vertices) - 2 for p in me.polygons))
