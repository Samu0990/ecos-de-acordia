import bpy, math
from mathutils import Vector, Quaternion
bpy.ops.wm.open_mainfile(filepath='/home/samuel/Unity/ParkourLab/ArtSource/Deer/Deer_rigged.blend')
rig = bpy.data.objects['Deer_Rig']
pb = rig.pose.bones
def rot(n, axis, deg):
    b = pb[n]; b.rotation_mode = 'XYZ'; setattr(b.rotation_euler, axis, math.radians(deg))
# braços abaixados, perna dando passo, tronco inclinado
rot('LeftArm', 'z', 0); rot('LeftArm', 'x', 0)
pb['LeftArm'].rotation_mode = 'QUATERNION'; pb['LeftArm'].rotation_quaternion = Quaternion(Vector((0, 1, 0)), math.radians(-70))
pb['RightArm'].rotation_mode = 'QUATERNION'; pb['RightArm'].rotation_quaternion = Quaternion(Vector((0, 1, 0)), math.radians(70))
pb['LeftForeArm'].rotation_mode = 'QUATERNION'; pb['LeftForeArm'].rotation_quaternion = Quaternion(Vector((1, 0, 0)), math.radians(50))
pb['RightUpLeg'].rotation_mode = 'QUATERNION'; pb['RightUpLeg'].rotation_quaternion = Quaternion(Vector((1, 0, 0)), math.radians(40))
pb['RightLeg'].rotation_mode = 'QUATERNION'; pb['RightLeg'].rotation_quaternion = Quaternion(Vector((1, 0, 0)), math.radians(-50))
pb['Spine1'].rotation_mode = 'QUATERNION'; pb['Spine1'].rotation_quaternion = Quaternion(Vector((0, 1, 0)), math.radians(15))
bpy.context.view_layer.update()
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.color_type = 'TEXTURE'; sc.display.shading.light = 'STUDIO'
sc.render.resolution_x = 900; sc.render.resolution_y = 600
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 3.4
cam.location = Vector((1.6, -4, 1.3)); cam.rotation_euler = (math.radians(88), 0, math.radians(22))
sc.render.filepath = '/tmp/claude-1000/deer_pose.png'; bpy.ops.render.render(write_still=True)
