"""Cervo Corrompido: reduz a malha (Tripo, 176k tris) para o notebook alvo, cria esqueleto
Humanoid (nomes Mixamo) a partir da pose T e pesa por distância aos ossos com regras de lado.
Rodar: blender -b --python deer_rig.py
"""
import bpy, bmesh, math, os
from mathutils import Vector
from mathutils.kdtree import KDTree

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, '..', '..', 'Assets', 'Aren', 'Enemies', 'Deer')
os.makedirs(OUT, exist_ok=True)
H = 2.55   # altura final (chefe imponente; o Aren tem 1.80)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath="/home/samuel/Downloads/corrupted+deer+3d+model.glb")
body = [o for o in bpy.data.objects if o.type == 'MESH'][0]
body.name = 'Deer_Body'
for o in list(bpy.data.objects):
    if o != body and o.type == 'EMPTY': bpy.data.objects.remove(o)
bpy.context.view_layer.objects.active = body
body.select_set(True)
body.parent = None
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# ---------------------------------------------------------------- redução
mod = body.modifiers.new('dec', 'DECIMATE')
mod.ratio = 11000 / 176000
bpy.ops.object.modifier_apply(modifier='dec')
# escala para H e pés no chão, centrado
bpy.context.view_layer.update()
zs = [v.co.z for v in body.data.vertices]
s = H / (max(zs) - min(zs))
for v in body.data.vertices:
    v.co *= s
zmin = min(v.co.z for v in body.data.vertices)
for v in body.data.vertices:
    v.co.z -= zmin
print('TRIS', sum(len(p.vertices) - 2 for p in body.data.polygons))

# textura 4K -> 1024 (memória do notebook)
for img in bpy.data.images:
    if img.size[0] > 1024:
        img.scale(1024, 1024)
    img.filepath_raw = os.path.join(OUT, img.name.replace('+', '_').split('.')[0] + '.png')
    img.file_format = 'PNG'
    img.save()
    print('IMG', img.name, img.size[:])

# ---------------------------------------------------------------- esqueleto (medido na vista lateral da pose T)
def P(x, z, y=0.0):
    return Vector((x * s, y * s, z * s - 0))   # coordenadas normalizadas da malha original (altura 0.956)

J = {
    'Hips': P(0, 0.40), 'Spine': P(0, 0.47), 'Spine1': P(0, 0.54), 'Spine2': P(0, 0.61),
    'Neck': P(0, 0.685), 'Head': P(0, 0.735), 'HeadTop_End': P(0, 0.90),
    'LeftShoulder': P(0.035, 0.655), 'LeftArm': P(0.105, 0.652), 'LeftForeArm': P(0.25, 0.655), 'LeftHand': P(0.40, 0.66), 'LeftHandEnd': P(0.49, 0.645),
    'LeftUpLeg': P(0.06, 0.39), 'LeftLeg': P(0.085, 0.225), 'LeftFoot': P(0.095, 0.06), 'LeftToeBase': P(0.10, 0.015, -0.05), 'LeftToeEnd': P(0.10, 0.01, -0.09),
}
for k in list(J.keys()):
    if k.startswith('Left'):
        v = J[k].copy(); v.x = -v.x
        J['Right' + k[4:]] = v
# correção do eixo frente/trás: a vista lateral olha de −Y; joelho e cotovelo levemente para fora do plano
arm = bpy.data.armatures.new('Deer_Armature')
rig = bpy.data.objects.new('Deer_Rig', arm)
bpy.context.scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.edit_bones
chain = [
    ('Hips', None, 'Spine'), ('Spine', 'Hips', 'Spine1'), ('Spine1', 'Spine', 'Spine2'), ('Spine2', 'Spine1', 'Neck'),
    ('Neck', 'Spine2', 'Head'), ('Head', 'Neck', 'HeadTop_End'),
]
for side in ('Left', 'Right'):
    chain += [
        (side + 'Shoulder', 'Spine2', side + 'Arm'), (side + 'Arm', side + 'Shoulder', side + 'ForeArm'),
        (side + 'ForeArm', side + 'Arm', side + 'Hand'), (side + 'Hand', side + 'ForeArm', side + 'HandEnd'),
        (side + 'UpLeg', 'Hips', side + 'Leg'), (side + 'Leg', side + 'UpLeg', side + 'Foot'),
        (side + 'Foot', side + 'Leg', side + 'ToeBase'), (side + 'ToeBase', side + 'Foot', side + 'ToeEnd'),
    ]
for name, parent, tail in chain:
    b = eb.new(name)
    b.head = J[name]; b.tail = J[tail]
    if (b.tail - b.head).length < 0.01: b.tail = b.head + Vector((0, 0, 0.05))
    if parent: b.parent = eb[parent]; b.use_connect = False
# dobra mínima nos joelhos/cotovelos (ajuda o IK/humanoid a saber a direção)
for side in ('Left', 'Right'):
    eb[side + 'Leg'].head.y -= 0.02 * s
    eb[side + 'UpLeg'].tail = eb[side + 'Leg'].head
    eb[side + 'ForeArm'].head.y += 0.01 * s
    eb[side + 'Arm'].tail = eb[side + 'ForeArm'].head
bpy.ops.armature.calculate_roll(type='GLOBAL_NEG_Y')
bpy.ops.object.mode_set(mode='OBJECT')

# ---------------------------------------------------------------- pesos por distância (com regras de lado)
deform = [b for b in arm.bones]
segs = {b.name: (rig.matrix_world @ b.head_local, rig.matrix_world @ b.tail_local) for b in deform}

def seg_dist(p, a, b):
    ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * t)).length

body.vertex_groups.clear()
groups = {b.name: body.vertex_groups.new(name=b.name) for b in deform}
sigma = 0.06 * s * 1.0
for v in body.data.vertices:
    p = body.matrix_world @ v.co
    side = 'Left' if p.x > 0.02 * s else ('Right' if p.x < -0.02 * s else None)
    cands = []
    for name, (a, b) in segs.items():
        if name.endswith('End'):
            continue
        # um lado não pega osso do outro (braço esquerdo nunca puxa vértice da direita)
        if side == 'Left' and name.startswith('Right'): continue
        if side == 'Right' and name.startswith('Left'): continue
        # pernas só abaixo do quadril; braços só acima do peito
        if ('UpLeg' in name or name.endswith('Leg') or 'Foot' in name or 'Toe' in name) and p.z > 0.43 * s: continue
        if ('Arm' in name or 'Hand' in name) and p.z < 0.52 * s: continue
        d = seg_dist(p, a, b)
        cands.append((d, name))
    cands.sort()
    best = cands[:3]
    d0 = best[0][0]
    ws = [(math.exp(-((d - d0) ** 2) / (2 * sigma ** 2)) , n) for d, n in best]
    tot = sum(w for w, _ in ws)
    for w, n in ws:
        if w / tot > 0.02:
            groups[n].add([v.index], w / tot, 'REPLACE')

mod = body.modifiers.new('arm', 'ARMATURE')
mod.object = rig
body.parent = rig

# raiz Humanoid do Unity espera o personagem olhando para +Z (Blender −Y)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, 'Deer_rigged.blend'))
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); body.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(
    filepath=os.path.join(OUT, 'Deer.fbx'), use_selection=True, object_types={'ARMATURE', 'MESH'},
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
    use_mesh_modifiers=False, add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
    bake_anim=False, path_mode='STRIP')
print('EXPORT ok')
