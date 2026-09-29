"""Utilidades compartilhadas pelos scripts do rig do Aren (Blender headless).

Convenções (iguais às do Mixamo, que o Unity Humanoid entende bem):
- 1 unidade = 1 m, personagem com ~1.80 m, pés em z = 0, olhando para -Y.
- Lado ESQUERDO do personagem = +X (olhando para -Y, a esquerda dele fica em +X).
"""
import os
import bpy
import mathutils

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)                      # ArtSource/Aren
SOURCE_FBX = os.path.join(ROOT, "source", "personagem.fbx")
RENDERS = os.path.join(ROOT, "renders")
TARGET_HEIGHT = 1.80


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def mesh_objects():
    return [o for o in bpy.context.scene.objects if o.type == 'MESH']


def world_bounds(objs):
    """Bounds pelos vértices (o bound_box fica desatualizado depois de editar a malha)."""
    mn = mathutils.Vector((1e9, 1e9, 1e9))
    mx = mathutils.Vector((-1e9, -1e9, -1e9))
    for o in objs:
        if o.type != 'MESH':
            continue
        mw = o.matrix_world
        for v in o.data.vertices:
            w = mw @ v.co
            mn = mathutils.Vector(map(min, mn, w))
            mx = mathutils.Vector(map(max, mx, w))
    return mn, mx


def setup_render(res=1024, engine='BLENDER_WORKBENCH', color='TEXTURE'):
    scn = bpy.context.scene
    scn.render.engine = engine
    if engine == 'BLENDER_WORKBENCH':
        scn.display.shading.color_type = color
        scn.display.shading.light = 'STUDIO'
        scn.display.shading.show_backface_culling = False
    scn.render.resolution_x = res
    scn.render.resolution_y = res
    scn.render.film_transparent = False
    return scn


def ortho_camera(name="RefCam"):
    cam = bpy.data.objects.get(name)
    if cam is None:
        data = bpy.data.cameras.new(name)
        data.type = 'ORTHO'
        cam = bpy.data.objects.new(name, data)
        bpy.context.scene.collection.objects.link(cam)
    bpy.context.scene.camera = cam
    return cam


def render_view(cam, center, direction, ortho_scale, path, up=(0, 0, 1)):
    """Renderiza ortográfico olhando de 'direction' (vetor do centro para a câmera)."""
    d = mathutils.Vector(direction).normalized()
    cam.data.ortho_scale = ortho_scale
    cam.data.clip_end = 100
    cam.location = mathutils.Vector(center) + d * 10
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    # metadados para o overlay de grade (pixel <-> mundo)
    with open(path + ".json", "w") as f:
        import json
        json.dump({"center": list(center), "dir": list(d), "ortho": ortho_scale,
                   "res": bpy.context.scene.render.resolution_x}, f)
