"""Prepara a flauta fornecida pelo autor para uso em tempo real no Unity.

Uso:
  blender -b --python magic_flute_export.py -- <origem.glb> <diretorio_saida>

Somente o maior mesh do GLB e exportado. Camera, luz e cubo auxiliares nao entram.
"""

import bpy
import hashlib
import json
import math
import os
import sys
from mathutils import Matrix, Vector


def args_after_separator():
    if "--" not in sys.argv:
        raise SystemExit("informe origem.glb e diretorio de saida depois de --")
    args = sys.argv[sys.argv.index("--") + 1:]
    if len(args) != 2:
        raise SystemExit("uso: -- <origem.glb> <diretorio_saida>")
    return os.path.abspath(args[0]), os.path.abspath(args[1])


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as src:
        for block in iter(lambda: src.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def mesh_stats(obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    result = {
        "vertices": len(mesh.vertices),
        "triangles": sum(max(0, len(poly.vertices) - 2) for poly in mesh.polygons),
    }
    evaluated.to_mesh_clear()
    return result


def local_bounds(obj):
    coords = [vertex.co.copy() for vertex in obj.data.vertices]
    mins = Vector(tuple(min(c[i] for c in coords) for i in range(3)))
    maxs = Vector(tuple(max(c[i] for c in coords) for i in range(3)))
    return mins, maxs


def save_image(image, path, size=1024):
    # Cria uma copia para nao alterar o datablock usado pelo material de origem.
    copy = image.copy()
    copy.scale(size, size)
    copy.filepath_raw = path
    copy.file_format = "PNG"
    copy.save()
    bpy.data.images.remove(copy)


def save_metallic_smoothness(image, path, size=1024):
    """Converte o RM do glTF (G=rugosidade, B=metal) para Standard (R=metal, A=smooth)."""
    source = image.copy()
    source.scale(size, size)
    pixels = list(source.pixels)
    converted = [0.0] * len(pixels)
    for i in range(0, len(pixels), 4):
        converted[i] = pixels[i + 2]
        converted[i + 1] = 0.0
        converted[i + 2] = 0.0
        converted[i + 3] = 1.0 - pixels[i + 1]
    output = bpy.data.images.new("MagicFlute_MetallicSmoothness", width=size, height=size, alpha=True)
    output.pixels = converted
    output.filepath_raw = path
    output.file_format = "PNG"
    output.save()
    bpy.data.images.remove(source)
    bpy.data.images.remove(output)


source, output_dir = args_after_separator()
os.makedirs(output_dir, exist_ok=True)
texture_dir = os.path.join(output_dir, "Textures")
os.makedirs(texture_dir, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=source)

meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
if not meshes:
    raise RuntimeError("nenhum mesh encontrado no GLB")
weapon = max(meshes, key=lambda o: len(o.data.polygons))
for obj in list(bpy.context.scene.objects):
    if obj != weapon:
        bpy.data.objects.remove(obj, do_unlink=True)

weapon.name = "MagicFlute"
weapon.data.name = "MagicFlute_Mesh"

# Aplica a transformacao do GLB e centraliza o mesh.
bpy.context.view_layer.objects.active = weapon
weapon.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mins, maxs = local_bounds(weapon)
center = (mins + maxs) * 0.5
for vertex in weapon.data.vertices:
    vertex.co -= center

# A arma fica alinhada no eixo Z como a flauta original do Aren.
dims = maxs - mins
long_axis = max(range(3), key=lambda i: dims[i])
if long_axis == 0:  # X -> Z
    weapon.data.transform(Matrix.Rotation(-math.pi * 0.5, 4, "Y"))
elif long_axis == 1:  # Y -> Z
    weapon.data.transform(Matrix.Rotation(math.pi * 0.5, 4, "X"))
weapon.data.update()

# Comprimento de 1,08 m: legivel em jogo sem atravessar o corpo nos golpes.
mins, maxs = local_bounds(weapon)
height = maxs.z - mins.z
uniform = 1.08 / max(height, 0.001)
weapon.scale = Vector((uniform, uniform, uniform))
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

before = mesh_stats(weapon)
target_triangles = 18000
ratio = min(1.0, target_triangles / max(1, before["triangles"]))
if ratio < 0.999:
    modifier = weapon.modifiers.new("GameReady_18k", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = ratio
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = weapon
    bpy.ops.object.modifier_apply(modifier=modifier.name)

# Copia as tres texturas PBR em 1024. O mapa RM permanece como fonte para o
# conversor do editor Unity (R=metalico, A=suavidade a partir da rugosidade).
material = next((m for m in weapon.data.materials if m and m.use_nodes), None)
images = {}
if material:
    for node in material.node_tree.nodes:
        if node.type != "TEX_IMAGE" or node.image is None:
            continue
        low = node.image.name.lower()
        if "basecolor" in low:
            images["basecolor"] = node.image
        elif "normal" in low:
            images["normal"] = node.image
        elif "_rm" in low or low.endswith("rm.jpg") or low.endswith("rm.png"):
            images["rm"] = node.image

names = {
    "basecolor": "MagicFlute_BaseColor.png",
    "normal": "MagicFlute_Normal.png",
}
for kind, filename in names.items():
    if kind not in images:
        raise RuntimeError("textura ausente no GLB: " + kind)
    target = os.path.join(texture_dir, filename)
    save_image(images[kind], target)
    images[kind].filepath = target
save_metallic_smoothness(images["rm"], os.path.join(texture_dir, "MagicFlute_MetallicSmoothness.png"))

fbx_path = os.path.join(output_dir, "MagicFlute.fbx")
bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=True,
    object_types={"MESH"},
    apply_scale_options="FBX_SCALE_ALL",
    use_mesh_modifiers=True,
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="RELATIVE",
    embed_textures=False,
)

after = mesh_stats(weapon)
mins, maxs = local_bounds(weapon)
report = {
    "source": source,
    "source_sha256": sha256(source),
    "output": fbx_path,
    "before": before,
    "after": after,
    "bounds_m": {
        "x": maxs.x - mins.x,
        "y": maxs.y - mins.y,
        "z": maxs.z - mins.z,
    },
    "texture_size": 1024,
    "license_status": "fornecido pelo autor; comprovante de licenca pendente",
}
with open(os.path.join(output_dir, "magic_flute_export_report.json"), "w", encoding="utf-8") as dst:
    json.dump(report, dst, ensure_ascii=False, indent=2)
print(json.dumps(report, ensure_ascii=False, indent=2))
