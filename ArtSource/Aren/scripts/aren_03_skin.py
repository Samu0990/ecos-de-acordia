"""Etapa 3 do rig: pesos do Aren (Aren_02_armature.blend -> Aren_03_skin.blend).

Auto-weights crus NÃO são o resultado: aqui o bone heat do Blender é só a primeira
passada, rodado POR GRUPO DE PEÇAS e só com os ossos permitidos para aquele grupo
(evita vazamento: calça não pega peso das abas, bota não pega do braço etc.). Depois:
- tabardo: cós travado no quadril (rampa em z);
- acessórios pequenos: herdam o peso do ponto de fixação no corpo (rígidos);
- fallback por distância a segmentos se o heat falhar numa peça;
- suavização leve, máx. 4 influências, normalização, limpeza de pesos < 0.02.

Rodar: blender -b Aren_02_armature.blend --python aren_03_skin.py
"""
import bpy, bmesh, json, os, sys, math, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aren_common as C

V3 = mathutils.Vector
body = bpy.data.objects["Aren_Body"]
rig = bpy.data.objects["Aren_Rig"]
isl = json.load(open(os.path.join(C.ROOT, "islands.json")))
NV = len(body.data.vertices)
co = [v.co.copy() for v in body.data.vertices]
bones = {b.name: b for b in rig.data.bones}

def side_bones(side):
    s = side
    arm = [s + "Shoulder", s + "Arm", s + "ArmTwist"]
    fore = [s + "ForeArm", s + "ForeArmTwist", s + "Hand"] + [n for n in bones if n.startswith(s + "Hand") and n != s + "Hand"]
    leg = [s + "UpLeg", s + "Leg"]
    foot = [s + "Foot", s + "ToeBase"]
    return arm, fore, leg, foot

armL, foreL, legL, footL = side_bones("Left")
armR, foreR, legR, footR = side_bones("Right")
spine = ["Hips", "Spine", "Spine1", "Spine2", "Neck", "Head"]
hood = ["Hood_01", "Hood_02", "Hood_03"]
tabard = [n for n in bones if n.startswith("Tabard")]
chain = ["Chain_01", "Chain_02", "Chain_03"]

# --- classificação das ilhas ---------------------------------------------------------
def bbox(i): return V3(isl[i]["min"]), V3(isl[i]["max"])
def is_chain(i):
    mn, mx = bbox(i)
    c = (mn + mx) / 2
    return 0.085 < c.x < 0.16 and c.y < -0.07 and 0.55 < c.z < 1.0 and (mx - mn).length < 0.12

GROUPS = [
    ("tronco_cabeca", [0], spine[1:] + armL[:2] + armR[:2] + ["LeftArmTwist", "RightArmTwist"] + hood + ["LeftForeArm", "RightForeArm"]),
    ("antebraco_D", [1], foreR + ["RightArm"]),
    ("antebraco_E", [2], foreL + ["LeftArm"]),
    # manga em volta do cotovelo (entre o braço da ilha 0 e o antebraço): só braço/antebraço.
    # Antes herdava por proximidade e pegava peso da coluna -> "asa" esticada na T-pose.
    ("cotovelo_D", [14], ["RightArm", "RightArmTwist", "RightForeArm", "RightForeArmTwist"]),
    ("cotovelo_E", [15], ["LeftArm", "LeftArmTwist", "LeftForeArm", "LeftForeArmTwist"]),
    ("bota_E", [3], footL + ["LeftLeg", "LeftUpLeg"]),
    ("bota_D", [4], footR + ["RightLeg", "RightUpLeg"]),
    ("calca", [6], ["Hips", "Spine"] + legL + legR),
    ("tabardo", [5], ["Hips"] + tabard),
    ("corrente", [i for i in range(len(isl)) if is_chain(i)], ["Hips"] + chain),
]
assigned = set()
for _, ids, _ in GROUPS:
    assigned.update(ids)
small = [i for i in range(len(isl)) if i not in assigned]
print("corrente: %d ilhas; acessórios herdados: %d ilhas" % (len(GROUPS[-1][1]), len(small)))

W = [dict() for _ in range(NV)]   # vi -> {bone: w}

# --- bone heat por grupo -------------------------------------------------------------
def heat_group(name, ids, allowed):
    verts = set()
    for i in ids:
        verts.update(isl[i]["verts"])
    # cópia só com essas ilhas, guardando o índice original
    me = body.data.copy()
    tmp = bpy.data.objects.new("tmp_" + name, me)
    bpy.context.scene.collection.objects.link(tmp)
    attr = me.attributes.new("orig", 'INT', 'POINT')
    for k in range(len(me.vertices)):
        attr.data[k].value = k
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in verts], context='VERTS')
    bm.to_mesh(me); bm.free()
    for b in rig.data.bones:
        b.use_deform = b.name in allowed
    bpy.ops.object.select_all(action='DESELECT')
    tmp.select_set(True); rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    orig = me.attributes["orig"]
    gname = {g.index: g.name for g in tmp.vertex_groups}
    zero = 0
    for v in me.vertices:
        vi = orig.data[v.index].value
        d = {gname[g.group]: g.weight for g in v.groups if g.weight > 1e-4 and gname[g.group] in allowed}
        if not d:
            zero += 1
        W[vi] = d
    bpy.data.objects.remove(tmp); bpy.data.meshes.remove(me)
    print("heat %-14s ilhas=%3d verts=%5d sem-peso=%d" % (name, len(ids), len(verts), zero))
    return [vi for vi in verts if not W[vi]]

def seg_dist(p, a, b):
    ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * t)).length

def distance_fallback(vis, allowed):
    segs = {n: (rig.matrix_world @ bones[n].head_local, rig.matrix_world @ bones[n].tail_local) for n in allowed}
    for vi in vis:
        ds = sorted(((seg_dist(co[vi], a, b), n) for n, (a, b) in segs.items()))[:3]
        ws = {n: 1.0 / max(d, 0.005) ** 4 for d, n in ds}
        s = sum(ws.values())
        W[vi] = {n: w / s for n, w in ws.items()}

for name, ids, allowed in GROUPS:
    if not ids:
        continue
    missing = heat_group(name, ids, allowed)
    if missing:
        distance_fallback(missing, allowed)
        print("   fallback por distância em %d vértices" % len(missing))

for b in rig.data.bones:
    b.use_deform = b.name not in ("Root", "FluteSocket")

# --- tronco: tira peso de BRAÇO da lateral do colete ---------------------------------------
# Medido (aren_torso_width.py): na ilha 0, a lateral do colete (x 0.13-0.16, z 1.16-1.36) saía
# do bone heat com 50-90% de peso de braço — em A-pose o braço pendurado fica a ~5 cm dessa
# superfície e a coluna a ~15 cm. Resultado: tira do colete esticando com o braço (T-pose).
# Regra: abaixo do ombro, dentro da largura do torso, braço -> Spine1/Spine2 (rampa em x).
def torso_fix():
    moved = 0
    for vi in isl[0]["verts"]:
        p = co[vi]
        ax = abs(p.x)
        if p.z > 1.40 or ax > 0.178:
            continue
        side = "Left" if p.x > 0 else "Right"
        # 1 = tira tudo (dentro do torso), 0 = mantém (já é braço); ombro (z>1.36) mais permissivo
        strip = 1.0 - smoothstep(0.158, 0.178, ax)
        if p.z > 1.36:
            strip *= 1.0 - smoothstep(1.36, 1.40, p.z)
        if strip <= 0:
            continue
        d = dict(W[vi])
        taken = 0.0
        for n in (side + "Arm", side + "ArmTwist"):
            if n in d:
                t = d[n] * strip; d[n] -= t; taken += t
        if taken <= 0:
            continue
        sp = {n: d.get(n, 0) for n in ("Spine1", "Spine2")}
        ssum = sum(sp.values())
        if ssum > 0:
            for n in sp:
                d[n] = d.get(n, 0) + taken * sp[n] / ssum
        else:
            d["Spine2" if p.z > 1.27 else "Spine1"] = d.get("Spine2" if p.z > 1.27 else "Spine1", 0) + taken
        W[vi] = {n: w for n, w in d.items() if w > 1e-4}
        moved += 1
    print("tronco: peso de braço retirado da lateral do colete em %d vértices" % moved)

def smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0))); return t * t * (3 - 2 * t)
torso_fix()

# --- tabardo: cós travado no quadril ---------------------------------------------------
def smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0))); return t * t * (3 - 2 * t)
for vi in isl[5]["verts"]:
    wh = smoothstep(0.97, 1.06, co[vi].z)
    d = {n: w * (1 - wh) for n, w in W[vi].items() if n != "Hips"}
    d["Hips"] = W[vi].get("Hips", 0) * (1 - wh) + wh
    W[vi] = d
# corrente: elo de cima preso no quadril
for i in GROUPS[-1][1]:
    for vi in isl[i]["verts"]:
        wh = smoothstep(0.95, 1.0, co[vi].z)
        d = {n: w * (1 - wh) for n, w in W[vi].items() if n != "Hips"}
        d["Hips"] = W[vi].get("Hips", 0) * (1 - wh) + wh
        W[vi] = d

# --- acessórios: peça-mãe por REGRA de região (não "o vizinho mais perto") -----------
# Medido (aren_debug_attach.py): bolsas traseiras do quadril ficam a 3.3 cm do antebraço
# pendurado e a 5 cm da calça -> por proximidade elas "voavam" com o braço na T-pose.
# O tabardo nunca é peça-mãe (bolsa/cinto não pode balançar com a aba).
def kd_for(ids):
    vis = [vi for i in ids for vi in isl[i]["verts"]]
    kd = mathutils.kdtree.KDTree(len(vis))
    for k, vi in enumerate(vis):
        kd.insert(co[vi], k)
    kd.balance()
    return kd, vis

KD = {name: kd_for(ids) for name, ids in (("anteD", [1, 14]), ("anteE", [2, 15]), ("tronco", [0]),
                                          ("calca", [6]), ("botaE", [3]), ("botaD", [4]))}

def median_dist(kdv, vis):
    kd, _ = kdv
    ds = sorted(kd.find(co[vi])[2] for vi in vis)
    return ds[len(ds) // 2]

def transfer(vis, kdv, allowed, rigid):
    kd, src = kdv
    def blend(p, k, pw):
        acc = {}
        for (_, idx, dist) in kd.find_n(p, k):
            wv = 1.0 / max(dist, 0.004) ** pw
            for n, w in W[src[idx]].items():
                if n in allowed:
                    acc[n] = acc.get(n, 0) + w * wv
        s = sum(acc.values())
        return {n: w / s for n, w in acc.items()} if s > 0 else {allowed[0]: 1.0}
    if rigid:
        c = sum((co[vi] for vi in vis), V3()) / len(vis)
        d = blend(c, 16, 1)
        for vi in vis:
            W[vi] = dict(d)
    else:
        for vi in vis:
            W[vi] = blend(co[vi], 6, 2)

def seg_d(p, n):
    return seg_dist(p, rig.matrix_world @ bones[n].head_local, rig.matrix_world @ bones[n].tail_local)

stats = {}
for i in small:
    vis = isl[i]["verts"]
    mn, mx = bbox(i)
    c = (mn + mx) / 2
    extent = (mx - mn).length
    # rígido só para peça pequena de verdade (fivela, rebite); tiras que abraçam o membro
    # seguem a superfície vértice a vértice (rígidas espetavam para fora com o cotovelo dobrado)
    rigid = extent < 0.06 or len(vis) < 60
    side = "Left" if c.x > 0 else "Right"
    ante = KD["anteE" if c.x > 0 else "anteD"]
    if median_dist(ante, vis) < 0.02:
        rule = "antebraco"
        transfer(vis, ante, [side + n for n in ("ForeArm", "ForeArmTwist", "Hand", "Arm", "ArmTwist")], rigid)
    elif abs(c.x) > 0.14 and 1.12 < c.z < 1.46 and seg_d(c, side + "Arm") < 0.085:
        rule = "braco"
        transfer(vis, KD["tronco"], [side + n for n in ("Arm", "ArmTwist", "Shoulder")], rigid)
    elif 0.88 <= c.z <= 1.17 and abs(c.x) < 0.25:
        rule = "cinto"
        d = {"Spine": 1.0} if c.z > 1.11 else {"Hips": 1.0}
        for vi in vis:
            W[vi] = dict(d)
    elif 0.52 <= c.z < 0.88:
        rule = "coxa"
        transfer(vis, KD["calca"], ["Hips", side + "UpLeg", side + "Leg"], rigid)
    elif c.z < 0.52:
        rule = "bota"
        transfer(vis, KD["botaE" if c.x > 0 else "botaD"], [side + n for n in ("Leg", "Foot", "ToeBase", "UpLeg")], rigid)
    else:
        # padrão: peça do corpo mais próxima (mediana), com os ossos que ela já usa
        best = min(KD, key=lambda k: median_dist(KD[k], vis))
        rule = "perto:" + best
        allowed = sorted({n for vi in KD[best][1] for n in W[vi]})
        if best == "tronco" and abs(c.x) < 0.14:
            allowed = [n for n in allowed if "Arm" not in n]
        transfer(vis, KD[best], allowed, rigid)
    stats[rule] = stats.get(rule, 0) + 1
print("acessórios por regra:", stats)

# --- escreve vertex groups ---------------------------------------------------------------
body.parent = None
body.vertex_groups.clear()
for m in list(body.modifiers):
    body.modifiers.remove(m)
groups = {}
for b in rig.data.bones:
    if b.use_deform:
        groups[b.name] = body.vertex_groups.new(name=b.name)

def finalize(d):
    d = {n: w for n, w in d.items() if n in groups and w > 0}
    items = sorted(d.items(), key=lambda kv: -kv[1])[:4]
    items = [(n, w) for n, w in items if w >= 0.02] or items[:1]
    s = sum(w for _, w in items)
    return [(n, w / s) for n, w in items]

for vi in range(NV):
    for n, w in finalize(W[vi]):
        groups[n].add([vi], w, 'REPLACE')

# suavização leve (só ao longo das arestas -> por peça), depois limita/normaliza de novo
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
bpy.ops.object.vertex_group_smooth(group_select_mode='ALL', factor=0.35, repeat=1)
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
bpy.ops.object.vertex_group_clean(group_select_mode='ALL', limit=0.02)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
bpy.ops.object.mode_set(mode='OBJECT')

# modificador de armadura + parent
mod = body.modifiers.new("Armature", 'ARMATURE')
mod.object = rig
mod.use_deform_preserve_volume = False   # Unity usa LBS; testar o que o jogo vai mostrar
body.parent = rig

# relatório
zero = sum(1 for v in body.data.vertices if not v.groups)
maxinf = max(len(v.groups) for v in body.data.vertices)
print("SKIN: vértices sem peso=%d  máx influências=%d  grupos=%d" % (zero, maxinf, len(body.vertex_groups)))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(C.ROOT, "Aren_03_skin.blend"))
print("salvo Aren_03_skin.blend")
