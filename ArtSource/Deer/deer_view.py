import bpy, sys, math
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath="/home/samuel/Downloads/corrupted+deer+3d+model.glb")
o = [x for x in bpy.data.objects if x.type == 'MESH'][0]
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.color_type = 'TEXTURE'; sc.display.shading.light = 'STUDIO'
sc.render.resolution_x = 900; sc.render.resolution_y = 450
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 1.3
bpy.context.view_layer.update()
mn = Vector([min((o.matrix_world @ Vector(b))[i] for b in o.bound_box) for i in range(3)])
mx = Vector([max((o.matrix_world @ Vector(b))[i] for b in o.bound_box) for i in range(3)])
c = (mn + mx) / 2
print('BOUNDS', mn, mx)
cam.location = c + Vector((0, -3, 0)); cam.rotation_euler = (math.pi / 2, 0, 0)   # vista do -Y (lateral)
sc.render.filepath = '/tmp/claude-1000/deer_side.png'; bpy.ops.render.render(write_still=True)
