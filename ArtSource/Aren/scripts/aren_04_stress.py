"""RIG_STRESS_TEST: aplica poses extremas no Aren e renderiza (frente + 3/4).
Rodar: blender -b Aren_03_skin.blend --python aren_04_stress.py [-- pose1,pose2]
Saída: renders/stress_<pose>.png e renders/stress_sheet_<n>.png
"""
import bpy, os, sys, math, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aren_common as C

V = mathutils.Vector
rig = bpy.data.objects["Aren_Rig"]
body = bpy.data.objects["Aren_Body"]
PB = rig.pose.bones

def reset():
    for pb in PB:
        pb.location = (0, 0, 0); pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (1, 0, 0, 0); pb.scale = (1, 1, 1)
    bpy.context.view_layer.update()

def world_mat(pb):
    return rig.matrix_world @ pb.matrix

def rot_world(name, axis, deg):
    """Gira o osso em torno de um eixo do MUNDO passando pela cabeça dele."""
    pb = PB[name]
    m = world_mat(pb)
    head = m.to_translation()
    R = mathutils.Matrix.Translation(head) @ mathutils.Matrix.Rotation(math.radians(deg), 4, V(axis)) @ mathutils.Matrix.Translation(-head)
    pb.matrix = rig.matrix_world.inverted() @ (R @ m)
    bpy.context.view_layer.update()

def aim(name, target_dir):
    """Aponta o osso (eixo Y local) para uma direção do mundo."""
    pb = PB[name]
    m = world_mat(pb)
    cur = (m.to_3x3() @ V((0, 1, 0))).normalized()
    q = cur.rotation_difference(V(target_dir).normalized())
    head = m.to_translation()
    R = mathutils.Matrix.Translation(head) @ q.to_matrix().to_4x4() @ mathutils.Matrix.Translation(-head)
    pb.matrix = rig.matrix_world.inverted() @ (R @ m)
    bpy.context.view_layer.update()

def move_hips(dz, dy=0.0):
    pb = PB["Hips"]
    m = world_mat(pb)
    m.translation += V((0, dy, dz))
    pb.matrix = rig.matrix_world.inverted() @ m
    bpy.context.view_layer.update()

def both(fn, name, *a):
    fn("Left" + name, *a)
    # espelha: direções/eixos com x invertido
    b = []
    for x in a:
        if isinstance(x, (tuple, list)) and len(x) == 3:
            b.append((-x[0], x[1], x[2]) if fn is aim else (x[0], -x[1], -x[2]))
        elif isinstance(x, (int, float)) and fn is rot_world:
            b.append(x)
        else:
            b.append(x)
    fn("Right" + name, *b)

def curl_fingers(side, deg):
    for f in ("Index", "Middle", "Ring", "Pinky"):
        for i in (1, 2, 3):
            pb = PB["%sHand%s%d" % (side, f, i)]
            pb.rotation_quaternion = mathutils.Quaternion(V((1, 0, 0)), math.radians(deg))
    bpy.context.view_layer.update()

POSES = {}
def pose(fn):
    POSES[fn.__name__] = fn; return fn

@pose
def tpose():
    both(aim, "Arm", (1, 0, 0)); both(aim, "ForeArm", (1, 0, 0)); both(aim, "Hand", (1, 0, 0))

@pose
def crouch_deep():
    move_hips(-0.42, -0.05)
    both(rot_world, "UpLeg", (1, 0, 0), 105)
    both(rot_world, "Leg", (1, 0, 0), -125)
    both(rot_world, "Foot", (1, 0, 0), 20)
    rot_world("Spine", (1, 0, 0), 25); rot_world("Spine1", (1, 0, 0), 10)
    both(aim, "Arm", (0.2, -0.8, -0.4)); both(aim, "ForeArm", (0.0, -1, 0.1))

@pose
def arms_overhead():
    both(aim, "Arm", (0.15, 0.0, 1)); both(aim, "ForeArm", (0.05, 0.05, 1))

@pose
def arm_across_chest():
    aim("LeftArm", (-0.8, -0.55, 0.05)); aim("LeftForeArm", (-0.9, 0.3, 0.1))
    aim("RightArm", (0.8, -0.55, 0.05)); aim("RightForeArm", (0.9, 0.3, 0.1))

@pose
def torso_twist():
    for n in ("Spine", "Spine1", "Spine2"):
        rot_world(n, (0, 0, 1), 22)
    rot_world("Neck", (0, 0, 1), -20)

@pose
def run_stride():
    rot_world("LeftUpLeg", (1, 0, 0), 60); rot_world("LeftLeg", (1, 0, 0), -80)
    rot_world("RightUpLeg", (1, 0, 0), -30); rot_world("RightLeg", (1, 0, 0), -60)
    aim("LeftArm", (0.2, 0.5, -0.8)); aim("LeftForeArm", (0.1, -0.2, -1))
    aim("RightArm", (-0.2, -0.6, -0.7)); aim("RightForeArm", (0.0, -1, 0.3))
    rot_world("Spine1", (0, 0, 1), -12)

@pose
def legs_wide():
    rot_world("LeftUpLeg", (0, 1, 0), 45); rot_world("RightUpLeg", (0, 1, 0), -45)
    move_hips(-0.18)

@pose
def high_kick():
    rot_world("LeftUpLeg", (1, 0, 0), 95); rot_world("LeftLeg", (1, 0, 0), -15)
    both(aim, "Arm", (0.9, 0, -0.3))

@pose
def ledge_grab():
    both(aim, "Arm", (0.15, -0.5, 1)); both(aim, "ForeArm", (0.05, -0.3, 1)); both(aim, "Hand", (0, -0.6, 1))
    curl_fingers("Left", 55); curl_fingers("Right", 55)
    both(rot_world, "UpLeg", (1, 0, 0), 25); both(rot_world, "Leg", (1, 0, 0), -40)

@pose
def flute_mouth():
    # flauta transversal: mão direita perto da boca à direita, esquerda à frente/esquerda
    aim("RightArm", (-0.45, -0.75, -0.25)); aim("RightForeArm", (0.35, -0.3, 0.9)); aim("RightHand", (0.5, -0.6, 0.6))
    aim("LeftArm", (0.3, -0.85, -0.3)); aim("LeftForeArm", (-0.55, -0.6, 0.55))
    rot_world("Head", (0, 0, 1), 12); rot_world("Head", (1, 0, 0), 8)
    curl_fingers("Left", 35); curl_fingers("Right", 35)

@pose
def contracanto():
    rot_world("Spine1", (1, 0, 0), -12); rot_world("Spine2", (1, 0, 0), -10)
    aim("LeftArm", (0.9, -0.3, 0.35)); aim("LeftForeArm", (0.8, -0.5, 0.3))
    aim("RightArm", (-0.5, -0.8, 0.1)); aim("RightForeArm", (0.2, -0.7, 0.7))
    rot_world("LeftUpLeg", (0, 1, 0), 22); rot_world("RightUpLeg", (1, 0, 0), -20); rot_world("RightLeg", (1, 0, 0), -35)
    move_hips(-0.08)
    rot_world("Head", (1, 0, 0), -15)

@pose
def head_turn():
    rot_world("Neck", (0, 0, 1), 30); rot_world("Head", (0, 0, 1), 35); rot_world("Head", (1, 0, 0), 20)

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
names = args[0].split(",") if args else list(POSES.keys())

C.setup_render(int(os.environ.get("AREN_RES", "640")))
cam = C.ortho_camera()
os.makedirs(C.RENDERS, exist_ok=True)
outs = []
for n in names:
    reset(); POSES[n]()
    dg = bpy.context.evaluated_depsgraph_get()
    ev = body.evaluated_get(dg)
    me = ev.to_mesh()
    ws = [ev.matrix_world @ v.co for v in me.vertices]
    ev.to_mesh_clear()
    mn = V([min(p[i] for p in ws) for i in range(3)]); mx = V([max(p[i] for p in ws) for i in range(3)])
    center = (mn + mx) / 2; size = max(mx - mn) * 1.1
    for view, d in (("f", (0, -1, 0)), ("q", (0.75, -0.9, 0.25))):
        p = os.path.join(C.RENDERS, "stress_%s_%s.png" % (n, view))
        C.render_view(cam, center, d, size, p)
        outs.append(p)
print("RENDERS", len(outs))
