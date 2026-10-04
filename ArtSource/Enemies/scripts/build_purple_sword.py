"""Cria a espada do Eco da Lamina como asset independente do corpo.

Uso:
  blender -b --factory-startup --python build_purple_sword.py -- <saida.fbx>
"""

import bpy
import math
import os
import sys
from mathutils import Vector


def output_path():
    if "--" not in sys.argv or len(sys.argv[sys.argv.index("--") + 1:]) != 1:
        raise SystemExit("uso: -- <saida.fbx>")
    return os.path.abspath(sys.argv[sys.argv.index("--") + 1])


def material(name, color, metallic, roughness, emission=None):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1.0)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 3.2
    return m


def extruded_outline(name, outline, depth, mat):
    half = depth * 0.5
    verts = [(x, y, -half) for x, y in outline] + [(x, y, half) for x, y in outline]
    n = len(outline)
    faces = [tuple(range(n)), tuple(range(n, n * 2))[::-1]]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bevel = obj.modifiers.new("EdgeBevel", "BEVEL")
    bevel.width = 0.008
    bevel.segments = 2
    return obj


def cube(name, location, scale, mat, bevel=0.02, rotation=0.0):
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=(0, 0, rotation))
    o = bpy.context.object
    o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(mat)
    if bevel > 0:
        mod = o.modifiers.new("EdgeBevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
    return o


def cylinder(name, location, radius, depth, mat, vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                       location=location, rotation=(math.pi * 0.5, 0, 0))
    o = bpy.context.object
    o.name = name
    o.data.materials.append(mat)
    return o


out = output_path()
os.makedirs(os.path.dirname(out), exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

dark = material("Sword_DarkMetal", (0.055, 0.035, 0.075), 0.9, 0.22)
bone = material("Sword_BoneSilver", (0.34, 0.31, 0.38), 0.82, 0.28)
violet = material("Sword_RiftGlow", (0.16, 0.015, 0.32), 0.35, 0.18, (0.55, 0.03, 1.0))

# Empunhadura no zero; lamina sobe no +Y. O desenho assimetrico lembra a referencia,
# mas continua barato e legivel no notebook alvo.
blade_outline = [
    (-0.055, 0.23), (-0.095, 0.39), (-0.065, 0.47), (-0.115, 0.60),
    (-0.072, 0.69), (-0.105, 0.84), (-0.055, 0.94), (0.0, 1.20),
    (0.050, 0.94), (0.082, 0.82), (0.055, 0.70), (0.094, 0.58),
    (0.052, 0.45), (0.078, 0.35), (0.042, 0.23),
]
blade = extruded_outline("Blade", blade_outline, 0.055, dark)

# Veio emissivo separado: permanece parte do item, nunca do corpo do inimigo.
core_outline = [(-0.014, 0.28), (-0.025, 0.52), (-0.012, 0.66), (-0.022, 0.84),
                (0.0, 1.10), (0.018, 0.82), (0.010, 0.64), (0.020, 0.50), (0.012, 0.28)]
core = extruded_outline("RiftCore", core_outline, 0.062, violet)

guard_l = cube("Guard_L", (-0.105, 0.20, 0), (0.105, 0.027, 0.035), bone, 0.018, math.radians(18))
guard_r = cube("Guard_R", (0.105, 0.20, 0), (0.105, 0.027, 0.035), bone, 0.018, math.radians(-18))
handle = cylinder("Grip", (0, -0.02, 0), 0.035, 0.40, dark, 12)
wrap1 = cube("GripWrap_A", (0, -0.02, 0), (0.045, 0.012, 0.043), violet, 0.008, math.radians(32))
wrap2 = cube("GripWrap_B", (0, -0.11, 0), (0.045, 0.012, 0.043), violet, 0.008, math.radians(-32))
pommel = cube("Pommel", (0, -0.25, 0), (0.07, 0.07, 0.05), bone, 0.025, math.radians(45))

objects = [blade, core, guard_l, guard_r, handle, wrap1, wrap2, pommel]
for o in objects:
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
bpy.context.view_layer.objects.active = blade
bpy.ops.object.convert(target="MESH")
bpy.ops.object.join()
blade = bpy.context.object
blade.name = "PurpleRiftSword"
blade.data.name = "PurpleRiftSword_Mesh"

# Centro da empunhadura no pivot e escala real de ~1,45 m.
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

bpy.ops.export_scene.fbx(
    filepath=out,
    use_selection=True,
    object_types={"MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    use_mesh_modifiers=True,
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="STRIP",
    embed_textures=False,
)

tris = sum(len(p.vertices) - 2 for p in blade.data.polygons)
print("PurpleRiftSword", tris, "triangles", out)
