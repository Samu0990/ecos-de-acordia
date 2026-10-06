"""Sussurrante — etapa 3: pesos finais (work/suss_rig_heat.blend -> work/suss_rig.blend).

O bone heat do Blender (etapa 2) é só a primeira passada. Aqui:
- BRAÇOS e PERNAS são separados pela malha (inundação a partir das pontas dos dedos / solas,
  só dentro de cápsulas em volta dos ossos): a mão não puxa a saia e a saia não pega peso da
  canela;
- SAIA (cinto -> barra): pelve no cós, mistura com as coxas descendo (lado pela posição x);
- CACHECOL/CAPUZ: spine_03 -> pescoço -> cabeça pela altura (+ clavícula nas pontas);
- CRÂNIO (inclui maxilar e dentes, que o heat não alcançou): cabeça rígida;
- tronco: heat filtrado (só pelve/coluna/clavículas/ombro);
- vértices sem peso: propagação pelos vizinhos; suavização leve, máx. 4 influências.

Rodar: blender -b --factory-startup --python ArtSource/Sussurrante/scripts/suss_03_weights.py
"""
import bpy, os, sys, math, mathutils
from collections import deque
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import suss_common as C

V = mathutils.Vector
bpy.ops.wm.open_mainfile(filepath=os.path.join(C.WORK, "suss_rig_heat.blend"))
body = bpy.data.objects["Sussurrante"]
rig = bpy.data.objects["Armature"]
me = body.data
NV = len(me.vertices)
P = [body.matrix_world @ v.co for v in me.vertices]
J = {k: (V(h), V(t)) for k, (h, t) in C.joints().items()}

nbr = [[] for _ in range(NV)]
for e in me.edges:
    a, b = e.vertices
    nbr[a].append(b); nbr[b].append(a)

gi = {g.index: g.name for g in body.vertex_groups}
W = [dict() for _ in range(NV)]
for v in me.vertices:
    for g in v.groups:
        if g.weight > 1e-4:
            W[v.index][gi[g.group]] = g.weight


def seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-12)))
    return (p - (a + ab * t)).length, t


def smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def flood(seeds, inside):
    seen = set(i for i in seeds if inside(i))
    q = deque(seen)
    while q:
        i = q.popleft()
        for j in nbr[i]:
            if j not in seen and inside(j):
                seen.add(j); q.append(j)
    return seen


def chain_inside(bones_radii):
    def f(i):
        p = P[i]
        for b, r in bones_radii:
            d, _ = seg_dist(p, *J[b])
            if d < r:
                return True
        return False
    return f


region = ["torso"] * NV
arm_bones = {}
leg_bones = {}
for s in ("l", "r"):
    fingers = [n for n in J if n.endswith("_" + s) and n.split("_")[0] in ("index", "middle", "ring", "pinky", "thumb")]
    # dedos curvados: a face de fora fica a ~7 cm do osso (que passa reto da base à ponta)
    arm = [("upperarm_" + s, 0.125), ("lowerarm_" + s, 0.11), ("hand_" + s, 0.125)] + [(f, 0.09) for f in fingers]
    sx = 1 if s == "r" else -1
    seeds = [i for i in range(NV) if P[i].z < 1.06 and P[i].x * sx > 0.50]
    A = flood(seeds, chain_inside(arm))
    for i in A:
        region[i] = "arm_" + s
    arm_bones[s] = set(b for b, _ in arm) | {"clavicle_" + s}
    leg = [("thigh_" + s, 0.16), ("calf_" + s, 0.165), ("foot_" + s, 0.15), ("ball_" + s, 0.12), ("ball_leaf_" + s, 0.10)]
    seeds = [i for i in range(NV) if P[i].z < 0.05 and P[i].x * sx > 0.2]
    L = flood(seeds, chain_inside(leg))
    for i in L:
        if region[i] == "torso":
            region[i] = "leg_" + s
    leg_bones[s] = set(b for b, _ in leg)
    print("REGIAO braço_%s %d  perna_%s %d" % (s, len(A), s, len(L)))

# rede de segurança 1: a mão cresce só por vértices CONECTADOS a ela e perto dos ossos da mão
# (um pedaço de saia ao lado da mão não está ligado a ela pela malha e não entra)
for s in ("l", "r"):
    hb = [(b, 0.15) for b in arm_bones[s] if not b.startswith(("upperarm", "lowerarm", "clavicle"))]
    near = chain_inside(hb)
    cur = [i for i in range(NV) if region[i] == "arm_" + s]
    grown = flood(cur, lambda i: region[i] in ("torso", "arm_" + s) and near(i))
    add = [i for i in grown if region[i] == "torso"]
    for i in add:
        region[i] = "arm_" + s
    print("MÃO_%s +%d" % (s, len(add)))
# rede de segurança 2: votação entre vizinhos (ilhotas que a inundação não alcançou)
for it in range(4):
    flips = 0
    for i in range(NV):
        if region[i].startswith(("arm_", "leg_")) or not nbr[i]:
            continue
        votes = {}
        for j in nbr[i]:
            votes[region[j]] = votes.get(region[j], 0) + 1
        r, n = max(votes.items(), key=lambda kv: kv[1])
        if r.startswith(("arm_", "leg_")) and n >= 0.6 * len(nbr[i]):
            region[i] = r; flips += 1
    if not flips:
        break

HEAD_C = V((0.0, 0.0, 2.64))
for i in range(NV):
    if region[i] != "torso":
        continue
    p = P[i]
    hood = p.y < -0.15 and p.z < 2.62
    if p.z > 2.40 and (p - HEAD_C).length < 0.27 and not hood:
        region[i] = "head"
    elif 2.04 < p.z < 2.62 and ((p - HEAD_C).length >= 0.27 or hood or p.z < 2.40):
        region[i] = "scarf"
    elif 0.40 < p.z < 1.84:
        region[i] = "skirt"
cnt = {}
for r in region:
    cnt[r] = cnt.get(r, 0) + 1
print("REGIOES", cnt)


def keep(w, allowed):
    return {k: v for k, v in w.items() if k in allowed}


for i in range(NV):
    p = P[i]; r = region[i]; w = W[i]
    if r.startswith("arm_"):
        s = r[-1]
        allowed = set(arm_bones[s])
        if p.z > 1.95:
            allowed |= {"spine_03"}
        w = keep(w, allowed)
    elif r.startswith("leg_"):
        s = r[-1]
        allowed = set(leg_bones[s])
        if p.z > 1.30:
            allowed |= {"pelvis"}
        w = keep(w, allowed)
    elif r == "head":
        k = smoothstep(2.40, 2.50, p.z)
        w = {"Head": 0.65 + 0.35 * k, "neck_01": 0.35 * (1 - k)}
    elif r == "scarf":
        s = smoothstep(2.10, 2.48, p.z)
        w = {"spine_03": 1 - s, "neck_01": s * 0.75, "Head": s * 0.25}
        side = "r" if p.x > 0 else "l"
        cl = smoothstep(0.16, 0.32, abs(p.x)) * (1 - smoothstep(2.25, 2.40, p.z)) * 0.45
        if cl > 0:
            w = {k: v * (1 - cl) for k, v in w.items()}
            w["clavicle_" + side] = cl
    elif r == "skirt":
        if p.z > 1.70:
            # cinto e barriga logo acima: coluna/pelve pelo heat filtrado
            k = smoothstep(1.70, 1.84, p.z)
            w = {"pelvis": 1 - k * 0.6, "spine_01": k * 0.6}
        else:
            t = smoothstep(1.74, 0.70, p.z)          # 0 no cós, 1 na barra
            # tiras do meio (frente/trás, entre as pernas) seguem a pelve: puxadas pelas duas
            # coxas numa passada larga elas esticariam; as de lado acompanham a coxa
            lateral = smoothstep(0.04, 0.20, abs(p.x))
            thigh = (0.10 + 0.55 * t) * lateral
            side = smoothstep(-0.10, 0.10, p.x)      # 0 esquerda, 1 direita
            w = {"pelvis": 1 - thigh, "thigh_r": thigh * side, "thigh_l": thigh * (1 - side)}
    else:  # tronco
        allowed = {"pelvis", "spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r"}
        if p.z > 1.90:
            allowed |= {"upperarm_l", "upperarm_r"}
        if p.z > 2.10:
            allowed |= {"neck_01"}
        if p.z < 1.65:
            allowed |= {"thigh_l", "thigh_r"}
        w = keep(w, allowed)
    W[i] = {k: v for k, v in w.items() if v > 1e-4}

# ---------------------------------------------------------------- sem peso: propaga pelos vizinhos
empty = [i for i in range(NV) if not W[i]]
print("SEM PESO antes da propagação", len(empty))
for it in range(60):
    changed = 0
    for i in empty:
        if W[i]:
            continue
        acc = {}; n = 0
        for j in nbr[i]:
            if W[j] and region[j] == region[i]:
                n += 1
                for k, v in W[j].items():
                    acc[k] = acc.get(k, 0) + v
        if n:
            W[i] = {k: v / n for k, v in acc.items()}
            changed += 1
    empty = [i for i in empty if not W[i]]
    if not changed:
        break
if empty:
    from mathutils.kdtree import KDTree
    kd = KDTree(NV)
    ok = [i for i in range(NV) if W[i]]
    for i in ok:
        kd.insert(P[i], i)
    kd.balance()
    for i in empty:
        _, j, _ = kd.find(P[i])
        W[i] = dict(W[j])
print("SEM PESO depois", sum(1 for w in W if not w))

# ---------------------------------------------------------------- suavização (dentro da mesma região)
for it in range(2):
    NW = []
    for i in range(NV):
        acc = dict((k, v * 2.0) for k, v in W[i].items()); tot = 2.0
        for j in nbr[i]:
            same = region[j] == region[i] or region[i] in ("torso", "skirt", "scarf") and region[j] in ("torso", "skirt", "scarf")
            if not same:
                continue
            tot += 1
            for k, v in W[j].items():
                acc[k] = acc.get(k, 0) + v
        NW.append({k: v / tot for k, v in acc.items()})
    W = NW

# ---------------------------------------------------------------- regiões como atributo (texturas usam)
CODES = {"head": 1, "arm_l": 2, "arm_r": 2, "leg_l": 3, "leg_r": 3, "torso": 4, "scarf": 5, "skirt": 6}
if "Region" in me.color_attributes:
    me.color_attributes.remove(me.color_attributes["Region"])
ra = me.color_attributes.new("Region", 'FLOAT_COLOR', 'POINT')
for i in range(NV):
    c = CODES[region[i]]
    side = 1.0 if P[i].x > 0 else 0.0
    ra.data[i].color = (c / 8.0, side, 0.0, 1.0)

# ---------------------------------------------------------------- grava: máx. 4 influências, normalizado
body.vertex_groups.clear()
groups = {b.name: body.vertex_groups.new(name=b.name) for b in rig.data.bones if b.use_deform}
maxinf = 0
for i in range(NV):
    items = sorted(W[i].items(), key=lambda kv: -kv[1])[:4]
    items = [(k, v) for k, v in items if v > 0.01 and k in groups]
    tot = sum(v for _, v in items) or 1.0
    maxinf = max(maxinf, len(items))
    for k, v in items:
        groups[k].add([i], v / tot, 'REPLACE')
for m in body.modifiers:
    if m.type == 'ARMATURE':
        m.object = rig
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(C.WORK, "suss_rig.blend"), compress=True)
print("OK máx influências", maxinf)
