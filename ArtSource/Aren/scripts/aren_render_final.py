import bpy, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aren_common as C
C.setup_render(900)
cam = C.ortho_camera()
for v, d in (("back", (0, 1, 0)), ("q", (0.8, 0.6, 0.2)), ("front", (0, -1, 0))):
    C.render_view(cam, (0, 0.05, 0.95), d, 2.0, os.path.join(C.RENDERS, "final_%s.png" % v))
fl = bpy.data.objects["Aren_Flute"]
import mathutils
c = fl.matrix_world @ mathutils.Vector((0, 0.18, 0))
C.render_view(cam, tuple(c), (0.3, 1, 0.4), 0.75, os.path.join(C.RENDERS, "final_flute.png"))
