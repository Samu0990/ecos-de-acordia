"""Prévia texturizada dos FBX da vila (Workbench, rápido). blender -b --python preview.py -- A,B,C out.png"""
import bpy, os, sys, math
from mathutils import Vector
args = sys.argv[sys.argv.index('--') + 1:]
names = args[0].split(','); out = args[1]
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODELS = os.path.join(ROOT, '..', '..', 'Assets', 'Campanula', 'Models')
TEX = os.path.join(ROOT, '..', '..', 'Assets', 'Campanula', 'Textures')
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'TEXTURE'
sc.display.shading.show_shadows = True
sc.display.shading.show_cavity = True
sc.render.resolution_x = 520; sc.render.resolution_y = 520
sc.world = bpy.data.worlds.new('w'); sc.world.color = (0.35, 0.38, 0.45)
x = 0.0
for n in names:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, n + '.fbx'))
    new = [o for o in bpy.data.objects if o not in before]
    roots = [o for o in new if o.parent is None]
    for o in new:
        if o.type == 'MESH':
            for slot in o.material_slots:
                m = slot.material
                if m is None or m.get('_done'): continue
                key = m.name.replace('CMP_', '').split('.')[0]
                m.use_nodes = True
                nt = m.node_tree
                bsdf = nt.nodes.get('Principled BSDF')
                p = os.path.join(TEX, key + '_albedo.png')
                if os.path.exists(p) and bsdf:
                    t = nt.nodes.new('ShaderNodeTexImage'); t.image = bpy.data.images.load(p)
                    nt.links.new(t.outputs['Color'], bsdf.inputs['Base Color'])
                m['_done'] = 1
    # agrupa lado a lado
    mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
    bpy.context.view_layer.update()
    for o in new:
        if o.type == 'MESH':
            for v in o.bound_box:
                w = o.matrix_world @ Vector(v)
                mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
    for r in roots:
        r.location.x += x - mn.x
    x += (mx.x - mn.x) + 3.0
bpy.context.view_layer.update()
mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
for o in bpy.data.objects:
    if o.type == 'MESH':
        for v in o.bound_box:
            w = o.matrix_world @ Vector(v)
            mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
c = (mn + mx) / 2; size = (mx - mn).length
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 40
d = Vector((0.55, -1.0, 0.45)).normalized()
cam.location = c + d * size * 1.05
cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
sc.render.resolution_x = 1000 if len(names) > 1 else 640; sc.render.resolution_y = 560 if len(names) > 1 else 640
sc.render.filepath = out
bpy.ops.render.render(write_still=True)
print('PREVIEW', out)
