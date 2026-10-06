"""Render de apresentação do Sussurrante (Cycles): pose de espreita com a lâmina baixa (como na
prancha), olhos acesos no fundo das órbitas, fenda do peito, luz de lua + contraluz violeta e
lascas flutuando. Saída: ArtSource/Sussurrante/renders/sussurrante_<vista>.png

Rodar: blender -b --factory-startup --python ArtSource/Sussurrante/scripts/suss_render.py [-- res samples]
"""
import bpy, os, sys, math, random, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import suss_common as C

V = mathutils.Vector
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RES = int(args[0]) if len(args) > 0 else 1100
SAMPLES = int(args[1]) if len(args) > 1 else 48
OUTD = os.path.join(C.ART, "renders")
os.makedirs(OUTD, exist_ok=True)

bpy.ops.wm.open_mainfile(filepath=os.path.join(C.WORK, "suss_final.blend"))
body = bpy.data.objects["Sussurrante"]
rig = bpy.data.objects["Armature"]
lod1 = bpy.data.objects.get("Sussurrante_LOD1")
if lod1:
    lod1.hide_render = True; lod1.hide_viewport = True
rig.hide_render = True
with bpy.data.libraries.load(os.path.join(C.WORK, "suss_blade.blend"), link=False) as (src, dst):
    dst.objects = ["SussurranteBlade"]
blade = dst.objects[0]
bpy.context.scene.collection.objects.link(blade)

# ---------------------------------------------------------------- materiais
TEX = C.OUT_TEX


def img(path, nc):
    im = bpy.data.images.load(path)
    im.colorspace_settings.name = 'Non-Color' if nc else 'sRGB'
    return im


m = bpy.data.materials.new("Body"); m.use_nodes = True
nt = m.node_tree; N = nt.nodes; L = nt.links
for n in list(N): N.remove(n)
out = N.new("ShaderNodeOutputMaterial"); bs = N.new("ShaderNodeBsdfPrincipled"); L.new(bs.outputs[0], out.inputs[0])
ta = N.new("ShaderNodeTexImage"); ta.image = img(os.path.join(TEX, "Sussurrante_Albedo.png"), False)
tn = N.new("ShaderNodeTexImage"); tn.image = img(os.path.join(TEX, "Sussurrante_Normal.png"), True)
tm = N.new("ShaderNodeTexImage"); tm.image = img(os.path.join(TEX, "Sussurrante_Mask.png"), True)
sep = N.new("ShaderNodeSeparateColor"); L.new(tm.outputs[0], sep.inputs[0])
mix = N.new("ShaderNodeMix"); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'; mix.inputs[0].default_value = 0.85
L.new(ta.outputs[0], mix.inputs[6]); L.new(sep.outputs[0], mix.inputs[7]); L.new(mix.outputs[2], bs.inputs["Base Color"])
nm = N.new("ShaderNodeNormalMap"); L.new(tn.outputs[0], nm.inputs["Color"]); L.new(nm.outputs[0], bs.inputs["Normal"])
inv = N.new("ShaderNodeMath"); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0; L.new(sep.outputs[1], inv.inputs[1])
L.new(inv.outputs[0], bs.inputs["Roughness"])
bs.inputs["Emission Color"].default_value = (0.62, 0.3, 1.0, 1)
em = N.new("ShaderNodeMath"); em.operation = 'MULTIPLY'; em.inputs[1].default_value = 3.2; L.new(sep.outputs[2], em.inputs[0])
L.new(em.outputs[0], bs.inputs["Emission Strength"])
body.data.materials.clear(); body.data.materials.append(m)

bm = blade.data.materials[0]; bm.use_nodes = True
bnt = bm.node_tree; bbs = bnt.nodes.get("Principled BSDF")
vc = bnt.nodes.new("ShaderNodeVertexColor"); vc.layer_name = "Col"
vs = bnt.nodes.new("ShaderNodeSeparateColor"); bnt.links.new(vc.outputs[0], vs.inputs[0])
bbs.inputs["Base Color"].default_value = (0.03, 0.02, 0.045, 1); bbs.inputs["Roughness"].default_value = 0.12; bbs.inputs["Metallic"].default_value = 0.3
pw = bnt.nodes.new("ShaderNodeMath"); pw.operation = 'POWER'; pw.inputs[1].default_value = 4.0; bnt.links.new(vs.outputs[1], pw.inputs[0])
cr = bnt.nodes.new("ShaderNodeMath"); cr.operation = 'COMPARE'; cr.inputs[1].default_value = 0.6; cr.inputs[2].default_value = 0.05; bnt.links.new(vs.outputs[0], cr.inputs[0])
crm = bnt.nodes.new("ShaderNodeMath"); crm.operation = 'MULTIPLY'; crm.inputs[1].default_value = 0.8; bnt.links.new(cr.outputs[0], crm.inputs[0])
mx = bnt.nodes.new("ShaderNodeMath"); mx.operation = 'MAXIMUM'; bnt.links.new(pw.outputs[0], mx.inputs[0]); bnt.links.new(crm.outputs[0], mx.inputs[1])
st = bnt.nodes.new("ShaderNodeMath"); st.operation = 'MULTIPLY'; st.inputs[1].default_value = 3.5; bnt.links.new(mx.outputs[0], st.inputs[0])
bbs.inputs["Emission Color"].default_value = (0.6, 0.28, 1.0, 1); bnt.links.new(st.outputs[0], bbs.inputs["Emission Strength"])

# ---------------------------------------------------------------- lâmina na mão (como no Unity)
pb = {b.name: b for b in rig.data.bones}
MW = rig.matrix_world
wr_h = MW @ pb["weapon_r"].head_local; wt_h = MW @ pb["weapon_tip_r"].head_local
bdir = (wt_h - wr_h).normalized()
knuck = ((MW @ pb["middle_01_r"].head_local) - (MW @ pb["hand_r"].head_local)).normalized()
wdir = (knuck - bdir * knuck.dot(bdir)).normalized()
zdir = wdir.cross(bdir)
R = mathutils.Matrix((wdir, bdir, zdir)).transposed().to_4x4()   # colunas: X=largura, Y=lâmina, Z=espessura
blade.matrix_world = mathutils.Matrix.Translation(wr_h + bdir * 0.15) @ R
bpy.context.view_layer.update()


def parent_to_bone(obj, bone):
    mw = obj.matrix_world.copy()
    obj.parent = rig; obj.parent_type = 'BONE'; obj.parent_bone = bone
    bpy.context.view_layer.update()
    obj.matrix_world = mw


parent_to_bone(blade, "weapon_r")

# olhos: brilho no fundo das órbitas
eye_mat = bpy.data.materials.new("Eye"); eye_mat.use_nodes = True
en = eye_mat.node_tree.nodes; [en.remove(n) for n in list(en)]
eo = en.new("ShaderNodeOutputMaterial"); ee = en.new("ShaderNodeEmission"); ee.inputs[0].default_value = (0.75, 0.45, 1.0, 1); ee.inputs[1].default_value = 40.0
eye_mat.node_tree.links.new(ee.outputs[0], eo.inputs[0])
for name in ("eye_l", "eye_r"):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.016, location=MW @ pb[name].head_local)
    s = bpy.context.object; s.data.materials.append(eye_mat)
    parent_to_bone(s, name)

# ---------------------------------------------------------------- pose de espreita (prancha)
Minv = MW.to_3x3().inverted()
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='POSE')


def rot(name, axis, deg):
    p = rig.pose.bones[name]
    a = (Minv @ V(axis)).normalized()
    h = p.head.copy()
    Rm = mathutils.Matrix.Rotation(math.radians(deg), 4, a)
    p.matrix = mathutils.Matrix.Translation(h) @ Rm @ mathutils.Matrix.Translation(-h) @ p.matrix
    bpy.context.view_layer.update()


X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)
for b, ax, d in [("spine_01", X, 7), ("spine_02", X, 7), ("spine_03", X, 5), ("neck_01", X, 12), ("Head", X, -16), ("Head", Z, -10),
                 ("thigh_l", X, 14), ("calf_l", X, -24), ("foot_l", X, 10), ("thigh_r", X, 6), ("calf_r", X, -14), ("foot_r", X, 8),
                 ("upperarm_r", X, 14), ("upperarm_r", Y, -12), ("lowerarm_r", X, 22),
                 ("upperarm_l", X, 18), ("upperarm_l", Y, 16), ("lowerarm_l", X, 32), ("lowerarm_l", Y, 10)]:
    rot(b, ax, d)
# a lâmina aponta para baixo e para a frente (prancha): gira a mão até o eixo da lâmina chegar lá
def wpos(b):
    return MW @ rig.pose.bones[b].head


cur = (wpos("weapon_tip_r") - wpos("weapon_r")).normalized()
want = V((0.22, 0.62, -0.75)).normalized()
ax = cur.cross(want)
if ax.length > 1e-5:
    rot("hand_r", tuple(ax.normalized()), math.degrees(cur.angle(want)))
for f in ("index", "middle", "ring", "pinky"):
    for k, d in ((1, 62), (2, 70), (3, 45)):
        rot("%s_0%d_r" % (f, k), Y, d)
    for k, d in ((1, -16), (2, -20), (3, -12)):
        rot("%s_0%d_l" % (f, k), Y, d)
for k, d in ((1, 25), (2, 30), (3, 20)):
    rot("thumb_0%d_r" % k, Z, -d)
rig.pose.bones["root"].location = (0, 0, 0)
bpy.ops.object.mode_set(mode='OBJECT')
# pés no chão depois da flexão
bpy.context.view_layer.update()
dg = bpy.context.evaluated_depsgraph_get()
ev = body.evaluated_get(dg); me = ev.to_mesh()
zmin = min((ev.matrix_world @ v.co).z for v in me.vertices)
ev.to_mesh_clear()
rig.location.z -= zmin

# lascas flutuando (pixels da prancha)
rnd = random.Random(4)
shard_dark = bpy.data.materials.new("ShardDark"); shard_dark.use_nodes = True
sd = shard_dark.node_tree.nodes["Principled BSDF"]; sd.inputs["Base Color"].default_value = (0.05, 0.025, 0.09, 1)
sd.inputs["Emission Color"].default_value = (0.5, 0.25, 1, 1); sd.inputs["Emission Strength"].default_value = 0.35
shard_glow = bpy.data.materials.new("ShardGlow"); shard_glow.use_nodes = True
sg = shard_glow.node_tree.nodes["Principled BSDF"]; sg.inputs["Base Color"].default_value = (0.4, 0.2, 0.8, 1)
sg.inputs["Emission Color"].default_value = (0.8, 0.55, 1, 1); sg.inputs["Emission Strength"].default_value = 6
for i in range(46):
    side = rnd.choice((-1, 1))
    c = V((rnd.uniform(-0.05, 0.55) * (1 if rnd.random() < 0.7 else -1), rnd.uniform(-0.35, 0.15), rnd.uniform(2.0, 3.3)))
    if rnd.random() < 0.35:
        c = V((-0.55 + rnd.uniform(-0.15, 0.15), rnd.uniform(-0.1, 0.3), rnd.uniform(1.1, 1.9)))   # braço esquerdo
    s = rnd.uniform(0.008, 0.022)
    bpy.ops.mesh.primitive_cube_add(size=s, location=c, rotation=(rnd.random() * 6, rnd.random() * 6, rnd.random() * 6))
    o = bpy.context.object
    o.data.materials.append(shard_glow if rnd.random() < 0.22 else shard_dark)

# chão e mundo
bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, 0))
ground = bpy.context.object
gm = bpy.data.materials.new("Ground"); gm.use_nodes = True
gb = gm.node_tree.nodes["Principled BSDF"]; gb.inputs["Base Color"].default_value = (0.012, 0.014, 0.016, 1); gb.inputs["Roughness"].default_value = 0.9
ground.data.materials.append(gm)
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Medium High Contrast'
w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.008, 0.01, 0.018, 1)


def area(name, loc, energy, color, size):
    l = bpy.data.lights.new(name, 'AREA'); l.energy = energy; l.color = color; l.size = size
    o = bpy.data.objects.new(name, l); sc.collection.objects.link(o); o.location = loc
    o.rotation_euler = (V((0, 0, 1.6)) - V(loc)).to_track_quat('-Z', 'Y').to_euler()


area("lua", (3.0, 4.5, 5.5), 520, (0.72, 0.8, 1.0), 2.0)
area("contraluz", (-2.6, -3.2, 3.6), 1100, (0.58, 0.36, 1.0), 1.2)
area("contraluz2", (2.8, -2.6, 2.4), 700, (0.45, 0.3, 1.0), 1.2)
area("preench", (-3.5, 3.0, 1.2), 90, (0.4, 0.42, 0.6), 4.0)
pl = bpy.data.lights.new("brilho_chao", 'POINT'); pl.energy = 25; pl.color = (0.6, 0.3, 1.0); pl.shadow_soft_size = 0.3
po = bpy.data.objects.new("brilho_chao", pl); sc.collection.objects.link(po); po.location = (0.35, 0.6, 0.35)

cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
sc.render.resolution_x = int(RES * 0.8); sc.render.resolution_y = RES
for name, d, tgt, dist, lens in (("frente", (0.32, 1, 0.02), (0, 0.1, 1.45), 6.4, 42), ("rosto", (0.22, 1, 0.05), (0, 0.05, 2.55), 2.1, 50),
                                  ("baixo", (0.55, 1, -0.12), (0, 0.2, 1.7), 4.6, 28)):
    dd = V(d).normalized()
    cam.data.lens = lens
    cam.location = V(tgt) + dd * dist
    cam.rotation_euler = (-dd).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(OUTD, "sussurrante_%s.png" % name)
    bpy.ops.render.render(write_still=True)
    print("RENDER", sc.render.filepath)
print("OK")
