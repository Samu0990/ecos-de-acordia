# Aldeões de Campanula (para a cena da Corrupção na vila e para a pele dos Ecos).
# Fonte: Quaternius — Modular Character Outfits: Fantasy [Standard] + Universal Base Characters
# [Standard] (CC0, ~/Downloads/QuaterniusChars). Mesmo esqueleto da UAL2 (as animações servem direto).
# Para cada variante: roupa de camponês (corpo, braços, pernas, pés) + a CABEÇA do personagem base
# (o resto do corpo base é apagado: a roupa já traz braços/mãos) + olhos + sobrancelhas + cabelo.
# Tudo vira UMA malha (menos draw calls) num único Armature, exportada em FBX para o Unity.
#   ~/.local/opt/blender-5.2/blender -b --factory-startup --python ArtSource/Villagers/scripts/villager_export.py
import bpy, os, shutil, subprocess
Q = os.path.expanduser("~/Downloads/QuaterniusChars/")
OUT = os.path.expanduser("~/Unity/ParkourLab/Assets/Aren/Character/Villagers/")
OUTFIT = Q + "Outfits/Modular Character Outfits - Fantasy[Standard]/Exports/FBX (Unity)/Outfits/"
BASE = Q + "Base/Universal Base Characters[Standard]/Base Characters/Unity/"
HAIR = Q + "Base/Universal Base Characters[Standard]/Hairstyles/Rigged to Head Bone/FBX (Unity)/"
VARIANTS = [
    ("Villager_M1", "Male_Peasant", "Superhero_Male_FullBody", ["Hair_SimpleParted", "Hair_Beard"]),
    ("Villager_M2", "Male_Peasant", "Superhero_Male_FullBody", ["Hair_Buzzed"]),
    ("Villager_F1", "Female_Peasant", "Superhero_Female_FullBody", ["Hair_Long"]),
    ("Villager_F2", "Female_Peasant", "Superhero_Female_FullBody", ["Hair_Buns"]),
]
os.makedirs(OUT, exist_ok=True)

def imp(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o not in before]

def rebind(obj, arm):
    mw = obj.matrix_world.copy()
    obj.parent = arm
    obj.matrix_world = mw
    for m in obj.modifiers:
        if m.type == 'ARMATURE': m.object = arm
    if not any(m.type == 'ARMATURE' for m in obj.modifiers):
        m = obj.modifiers.new("Armature", 'ARMATURE'); m.object = arm

def keep_head(obj, neck_z):
    me = obj.data
    gi = {g.index: g.name for g in obj.vertex_groups}
    kill = []
    for v in me.vertices:
        w = sum(g.weight for g in v.groups if gi.get(g.group) in ("Head", "neck_01"))
        p = obj.matrix_world @ v.co
        head = w >= 0.5 and p.z >= neck_z
        chest = p.z >= neck_z - 0.17 and abs(p.x) < 0.13 and p.y < 0.0   # pele no decote da camisa (frente = -Y)
        if not (head or chest): kill.append(v.index)
    import bmesh
    bm = bmesh.new(); bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in kill], context='VERTS')
    bm.to_mesh(me); bm.free()

for name, outfit, base, hairs in VARIANTS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = imp(OUTFIT + outfit + ".fbx")
    arm = next(o for o in objs if o.type == 'ARMATURE')
    arm.name = "Armature"
    neck_z = (arm.matrix_world @ arm.data.bones["neck_01"].head_local).z
    meshes = [o for o in objs if o.type == 'MESH']
    bobjs = imp(BASE + base + ".fbx")
    for o in bobjs:
        if o.type != 'MESH': continue
        if o.name.lower().startswith("superhero"):
            keep_head(o, neck_z - 0.07)
        rebind(o, arm); meshes.append(o)
    for h in hairs:
        for o in imp(HAIR + h + ".fbx"):
            if o.type == 'MESH': rebind(o, arm); meshes.append(o)
    for o in list(bpy.data.objects):
        if o.type == 'ARMATURE' and o != arm: bpy.data.objects.remove(o, do_unlink=True)
    # materiais repetidos (cada FBX importado traz a sua cópia: MI_Hair_1, MI_Hair_1.001...) viram um só
    unique = {}
    for o in meshes:
        for slot in o.material_slots:
            if slot.material is None: continue
            base = slot.material.name.split(".")[0]
            unique.setdefault(base, slot.material)
            slot.material = unique[base]
    for m in unique.values(): m.name = m.name.split(".")[0]
    # uma malha só
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes: o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    body.name = name
    print(name, "verts", len(body.data.vertices), "mats", [m.name for m in body.data.materials])
    bpy.ops.object.select_all(action='DESELECT')
    body.select_set(True); arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=OUT + name + ".fbx", use_selection=True,
        object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y',
        use_mesh_modifiers=False, mesh_smooth_type='FACE',
        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        use_armature_deform_only=False, bake_anim=False,
        path_mode='STRIP', embed_textures=False)

# texturas (1024 px; o personagem ocupa pouco da tela)
TEX = OUT + "Textures/"
os.makedirs(TEX, exist_ok=True)
src = {
    "T_Peasant_BaseColor": Q + "Outfits/Modular Character Outfits - Fantasy[Standard]/Textures/Peasant/T_Peasant_BaseColor.png",
    "T_Peasant_2_BaseColor": Q + "Outfits/Modular Character Outfits - Fantasy[Standard]/Textures/Peasant/T_Peasant_2_BaseColor.png",
    "T_Peasant_Normal": Q + "Outfits/Modular Character Outfits - Fantasy[Standard]/Textures/Peasant/T_Peasant_Normal.png",
    "T_Peasant_ORM": Q + "Outfits/Modular Character Outfits - Fantasy[Standard]/Textures/Peasant/T_Peasant_ORM.png",
    "T_Regular_Male_Dark_BaseColor": Q + "Outfits/Modular Character Outfits - Fantasy[Standard]/Textures/Base/T_Regular_Male_Dark_BaseColor.png",
    "T_Regular_Male_Normal": Q + "Outfits/Modular Character Outfits - Fantasy[Standard]/Textures/Base/T_Regular_Male_Normal.png",
    "T_Superhero_Male_Dark": Q + "Base/Universal Base Characters[Standard]/Base Characters/Textures/T_Superhero_Male_Dark.png",
    "T_Superhero_Male_Light": Q + "Base/Universal Base Characters[Standard]/Base Characters/Textures/T_Superhero_Male_Ligh.png",
    "T_Superhero_Male_Normal": Q + "Base/Universal Base Characters[Standard]/Base Characters/Textures/Normals Unity - Godot/T_Superhero_Male_Normal.png",
    "T_Superhero_Female_Dark": Q + "Base/Universal Base Characters[Standard]/Base Characters/Textures/T_Superhero_Female_Dark_BaseColor.png",
    "T_Superhero_Female_Light": Q + "Base/Universal Base Characters[Standard]/Base Characters/Textures/T_Superhero_Female_Light_BaseColor.png",
    "T_Superhero_Female_Normal": Q + "Base/Universal Base Characters[Standard]/Base Characters/Textures/Normals Unity - Godot/T_Superhero_Female_Normal.png",
    "T_Eye_Brown": Q + "Base/Universal Base Characters[Standard]/Base Characters/Textures/T_Eye_Brown.png",
    "T_Hair_1_BaseColor": Q + "Base/Universal Base Characters[Standard]/Hairstyles/Textures/T_Hair_1_BaseColor.png",
    "T_Hair_1_Normal": Q + "Base/Universal Base Characters[Standard]/Base Characters/Textures/Normals Unity - Godot/T_Hair_1_Normal.png",
    "T_Hair_2_BaseColor": Q + "Base/Universal Base Characters[Standard]/Hairstyles/Textures/T_Hair_2_BaseColor.png",
    "T_Hair_2_Normal": Q + "Base/Universal Base Characters[Standard]/Base Characters/Textures/Normals Unity - Godot/T_Hair_2_Normal.png",
}
for k, p in src.items():
    if not os.path.exists(p): print("FALTA", p); continue
    subprocess.run(["convert", p, "-resize", "1024x1024>", TEX + k + ".png"], check=True)
print("ok")
