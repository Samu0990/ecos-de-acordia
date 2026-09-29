"""Etapa 2 do rig: cria o esqueleto do Aren (Aren_Rig) sobre Aren_01_prep.blend.

Juntas vêm de medições da malha (aren_measure*.py): centro real das seções dos membros,
não template. Lado esquerdo = +X; o direito é espelhado (malha simétrica).

Estrutura:
- DEFORM humanoide (nomes Mixamo → Unity Humanoid mapeia sozinho): Hips, Spine, Spine1,
  Spine2, Neck, Head, Shoulder/Arm/ForeArm/Hand, dedos 3 falanges, UpLeg/Leg/Foot/ToeBase.
- DEFORM extras (não-humanoides, dirigidos por script no Unity):
  ArmTwist/ForeArmTwist (torção distribuída), Hood_01-03 (capuz), TabardFront_01-03,
  TabardBack_01-03, TabardSide{L,R}_01-02 (abas), Chain_01-03 (corrente do quadril).
- NÃO-DEFORM: Root (origem, gameplay), FluteSocket (mão direita).

Rodar: blender -b Aren_01_prep.blend --python aren_02_armature.py
Saída: Aren_02_armature.blend + joints.json (para overlays).
"""
import bpy, json, os, sys, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aren_common as C

V = mathutils.Vector

# ---------------------------------------------------------------------------
# Juntas (lado esquerdo, +X). (head, tail, parent, deform)
# ---------------------------------------------------------------------------
L = {}
def b(name, head, tail, parent, deform=True, roll_axis=None):
    L[name] = dict(head=V(head), tail=V(tail), parent=parent, deform=deform, roll=roll_axis)

# tronco (centro das seções, um pouco para trás: a coluna é posterior)
b("Hips",   (0, 0.060, 0.990), (0, 0.070, 1.080), "Root")
b("Spine",  (0, 0.070, 1.080), (0, 0.080, 1.200), "Hips")
b("Spine1", (0, 0.080, 1.200), (0, 0.085, 1.320), "Spine")
b("Spine2", (0, 0.085, 1.320), (0, 0.080, 1.495), "Spine1")
b("Neck",   (0, 0.080, 1.495), (0, 0.072, 1.585), "Spine2")
b("Head",   (0, 0.072, 1.585), (0, 0.060, 1.770), "Neck")

# braço esquerdo
b("LeftShoulder", (0.045, 0.080, 1.455), (0.185, 0.085, 1.425), "Spine2")
b("LeftArm",      (0.185, 0.085, 1.425), (0.245, 0.080, 1.130), "LeftShoulder")
b("LeftForeArm",  (0.245, 0.080, 1.130), (0.302, 0.025, 0.925), "LeftArm")
b("LeftHand",     (0.302, 0.025, 0.925), (0.315, 0.000, 0.866), "LeftForeArm")
# twist: filhos, no meio do segmento, mesma direção do pai
def mid(a, c, t=0.5): return a.lerp(c, t)
b("LeftArmTwist",     mid(L["LeftArm"]["head"], L["LeftArm"]["tail"]), L["LeftArm"]["tail"], "LeftArm")
b("LeftForeArmTwist", mid(L["LeftForeArm"]["head"], L["LeftForeArm"]["tail"]), L["LeftForeArm"]["tail"], "LeftForeArm")

# dedos: MCP -> PIP -> DIP -> ponta (curvados para dentro/-X, como no modelo)
fingers = {
    "Index":  [(0.314, -0.044, 0.868), (0.316, -0.046, 0.838), (0.309, -0.047, 0.815), (0.300, -0.047, 0.795)],
    "Middle": [(0.318, -0.014, 0.864), (0.318, -0.014, 0.832), (0.309, -0.014, 0.807), (0.296, -0.014, 0.787)],
    "Ring":   [(0.316,  0.012, 0.864), (0.315,  0.012, 0.834), (0.305,  0.012, 0.810), (0.292,  0.012, 0.791)],
    "Pinky":  [(0.311,  0.036, 0.868), (0.309,  0.037, 0.843), (0.301,  0.038, 0.824), (0.290,  0.038, 0.808)],
    "Thumb":  [(0.297, -0.030, 0.905), (0.283, -0.045, 0.875), (0.272, -0.055, 0.848), (0.264, -0.062, 0.822)],
}
for fname, pts in fingers.items():
    parent = "LeftHand"
    for i in range(3):
        n = "LeftHand%s%d" % (fname, i + 1)
        b(n, pts[i], pts[i + 1], parent)
        parent = n

# perna esquerda (joelho 1 cm à frente da linha quadril-tornozelo: evita hiperextensão/IK invertido)
b("LeftUpLeg",   (0.092, 0.050, 0.955), (0.165, 0.052, 0.500), "Hips")
b("LeftLeg",     (0.165, 0.052, 0.500), (0.214, 0.075, 0.095), "LeftUpLeg")
b("LeftFoot",    (0.214, 0.075, 0.095), (0.235, -0.035, 0.030), "LeftLeg")
b("LeftToeBase", (0.235, -0.035, 0.030), (0.245, -0.120, 0.022), "LeftFoot")

# secundários
b("Hood_01", (0, 0.100, 1.755), (0, 0.165, 1.690), "Head")
b("Hood_02", (0, 0.165, 1.690), (0, 0.195, 1.600), "Hood_01")
b("Hood_03", (0, 0.195, 1.600), (0, 0.190, 1.520), "Hood_02")
b("TabardFront_01", (0, -0.085, 1.040), (0, -0.095, 0.860), "Hips")
b("TabardFront_02", (0, -0.095, 0.860), (0, -0.100, 0.690), "TabardFront_01")
b("TabardFront_03", (0, -0.100, 0.690), (0, -0.101, 0.530), "TabardFront_02")
b("TabardBack_01",  (0, 0.150, 1.040), (0, 0.172, 0.860), "Hips")
b("TabardBack_02",  (0, 0.172, 0.860), (0, 0.190, 0.690), "TabardBack_01")
b("TabardBack_03",  (0, 0.190, 0.690), (0, 0.210, 0.530), "TabardBack_02")
b("TabardSideL_01", (0.165, 0.040, 1.020), (0.190, 0.065, 0.850), "Hips")
b("TabardSideL_02", (0.190, 0.065, 0.850), (0.200, 0.080, 0.670), "TabardSideL_01")
b("Chain_01", (0.110, -0.095, 0.990), (0.120, -0.100, 0.840), "Hips")
b("Chain_02", (0.120, -0.100, 0.840), (0.125, -0.105, 0.700), "Chain_01")
b("Chain_03", (0.125, -0.105, 0.700), (0.128, -0.105, 0.575), "Chain_02")

# ---------------------------------------------------------------------------
# espelha "Left" -> "Right" e "SideL" -> "SideR"
# ---------------------------------------------------------------------------
def mirror_name(n):
    if n.startswith("Left"): return "Right" + n[4:]
    if "SideL" in n: return n.replace("SideL", "SideR")
    return None

J = dict(L)
for n, d in L.items():
    m = mirror_name(n)
    if m:
        mp = mirror_name(d["parent"]) or d["parent"]
        J[m] = dict(head=V((-d["head"].x, d["head"].y, d["head"].z)),
                    tail=V((-d["tail"].x, d["tail"].y, d["tail"].z)),
                    parent=mp, deform=d["deform"], roll=d["roll"])

# socket da flauta: palma da mão direita, eixo da empunhadura ao longo de Y (frente-trás)
J["FluteSocket"] = dict(head=V((-0.300, -0.010, 0.852)), tail=V((-0.300, -0.090, 0.852)),
                        parent="RightHand", deform=False, roll=None)
J["Root"] = dict(head=V((0, 0, 0)), tail=V((0, 0.25, 0)), parent=None, deform=False, roll=None)

# ---------------------------------------------------------------------------
# cria a armadura
# ---------------------------------------------------------------------------
arm_data = bpy.data.armatures.new("Aren_Rig")
arm = bpy.data.objects.new("Aren_Rig", arm_data)
bpy.context.scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
arm.select_set(True)
arm_data.display_type = 'OCTAHEDRAL'
arm.show_in_front = True
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones

order = ["Root"]
while len(order) < len(J):
    for n, d in J.items():
        if n not in order and (d["parent"] is None or d["parent"] in order):
            order.append(n)
for n in order:
    d = J[n]
    e = eb.new(n)
    e.head, e.tail = d["head"], d["tail"]
    e.use_deform = d["deform"]
    if d["parent"]:
        e.parent = eb[d["parent"]]
        # conecta quando a cabeça coincide com a cauda do pai (cadeias limpas)
        e.use_connect = (eb[d["parent"]].tail - e.head).length < 1e-5

# rolls consistentes: pernas/braços/dedos com eixo Z apontando para frente (-Y) no rest,
# assim o eixo X local é a dobradiça (joelho/cotovelo/dedos) nos dois lados.
bpy.ops.armature.select_all(action='DESELECT')
for e in eb:
    e.select = True
bpy.ops.armature.calculate_roll(type='GLOBAL_NEG_Y')
bpy.ops.object.mode_set(mode='OBJECT')

out = os.path.join(C.ROOT, "Aren_02_armature.blend")
bpy.ops.wm.save_as_mainfile(filepath=out)
json.dump({n: {"head": list(d["head"]), "tail": list(d["tail"]), "parent": d["parent"], "deform": d["deform"]} for n, d in J.items()},
          open(os.path.join(C.ROOT, "joints.json"), "w"), indent=1)
print("OSSOS", len(J), "deform", sum(1 for d in J.values() if d["deform"]), "salvo", out)
