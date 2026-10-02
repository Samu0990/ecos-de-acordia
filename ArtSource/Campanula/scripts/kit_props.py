"""Objetos de cena de Campanula (mercado, ruas, campo). blender -b --python kit_props.py"""
import bpy, math, os, sys, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import *
from mathutils import Vector, Matrix, noise

MATS = None


def stall(name, cloth):
    mb = MeshBuilder(name)
    w, d = 3.2, 2.2
    for x in (-w / 2, w / 2):
        for y in (-d / 2, d / 2):
            mb.beam((x, y, 0), (x, y, 2.6 if y > 0 else 2.1), 0.12)
    mb.box((-w / 2, -d / 2 + 0.1, 0.85), (w / 2, d / 2 - 0.3, 0.95), 'planks')            # balcão
    mb.box((-w / 2 + 0.1, -d / 2 + 0.15, 0), (w / 2 - 0.1, -d / 2 + 0.25, 0.85), 'planks')
    # toldo inclinado (topo plano o bastante para pousar: vira plataforma de parkour)
    pts = [(-w / 2 - 0.25, -d / 2 - 0.5, 2.05), (w / 2 + 0.25, -d / 2 - 0.5, 2.05), (w / 2 + 0.25, d / 2 + 0.1, 2.65), (-w / 2 - 0.25, d / 2 + 0.1, 2.65)]
    mb.poly(pts, cloth)
    mb.poly(list(reversed(pts)), cloth)
    # franja recortada
    for i in range(8):
        x0 = -w / 2 - 0.25 + (w + 0.5) * i / 8
        x1 = x0 + (w + 0.5) / 8
        mb.poly([(x0, -d / 2 - 0.5, 2.05), (x1, -d / 2 - 0.5, 2.05), ((x0 + x1) / 2, -d / 2 - 0.52, 1.8)], cloth)
    # mercadorias: caixotes pequenos e potes
    for i, x in enumerate((-1.0, -0.2, 0.7)):
        mb.box((x - 0.25, -0.5, 0.95), (x + 0.25, -0.05, 1.25), 'planks')
    mb.cylinder((1.2, -0.3, 0.95), 0.14, 0.3, 'bronze', segs=8)
    return mb.finish(MATS)


def crate(size=1.0, name='Crate'):
    mb = MeshBuilder(name)
    s = size / 2
    mb.box((-s, -s, 0), (s, s, size), 'planks')
    for (a, b) in (((-s - 0.02, -s - 0.02), (s + 0.02, -s + 0.1)), ((-s - 0.02, s - 0.1), (s + 0.02, s + 0.02))):
        mb.box((a[0], a[1], 0), (b[0], b[1], 0.08), 'timber')
        mb.box((a[0], a[1], size - 0.08), (b[0], b[1], size), 'timber')
    mb.beam((-s + 0.05, -s - 0.03, 0.08), (s - 0.05, -s - 0.03, size - 0.08), 0.07)
    mb.beam((-s + 0.05, s + 0.03, size - 0.08), (s - 0.05, s + 0.03, 0.08), 0.07)
    return mb.finish(MATS)


def barrel(name='Barrel'):
    mb = MeshBuilder(name)
    prof = [(0.3, 0.0), (0.36, 0.25), (0.38, 0.5), (0.36, 0.75), (0.3, 1.0)]
    mb.lathe(prof, 'planks', segs=12)
    for z in (0.12, 0.88):
        mb.lathe([(prof[0][0] + 0.06 + 0.02, z - 0.03), (prof[0][0] + 0.06 + 0.02, z + 0.03)] if z < 0.5 else [(0.35, z - 0.03), (0.35, z + 0.03)], 'iron', segs=12)
    mb.cylinder((0, 0, 0.97), 0.3, 0.02, 'planks', segs=12)
    return mb.finish(MATS)


def hay_bale():
    mb = MeshBuilder('HayBale')
    mb.box((-0.7, -0.45, 0), (0.7, 0.45, 0.9), 'straw')
    for x in (-0.35, 0.35):
        mb.box((x - 0.03, -0.47, 0), (x + 0.03, 0.47, 0.92), 'planks', sides='yYZ')
    return mb.finish(MATS)


def cart():
    mb = MeshBuilder('Cart')
    mb.box((-1.3, -0.75, 0.7), (1.3, 0.75, 0.82), 'planks')
    for y in (-0.75, 0.75):
        mb.box((-1.3, y - 0.05, 0.82), (1.3, y + 0.05, 1.3), 'planks')
    mb.box((1.25, -0.75, 0.82), (1.35, 0.75, 1.3), 'planks')
    mb.beam((-1.3, -0.3, 0.75), (-2.8, -0.3, 0.55), 0.1)
    mb.beam((-1.3, 0.3, 0.75), (-2.8, 0.3, 0.55), 0.1)
    for y in (-0.9, 0.9):
        # roda
        m = MeshBuilder('w')
        segs = 12; r = 0.6
        for i in range(segs):
            a0, a1 = i / segs * math.tau, (i + 1) / segs * math.tau
            mb.beam((0.2 + math.cos(a0) * r, y, 0.6 + math.sin(a0) * r), (0.2 + math.cos(a1) * r, y, 0.6 + math.sin(a1) * r), 0.09, 'timber', 0.12)
        for i in range(6):
            a = i / 6 * math.pi
            mb.beam((0.2 + math.cos(a) * r, y, 0.6 + math.sin(a) * r), (0.2 - math.cos(a) * r, y, 0.6 - math.sin(a) * r), 0.05, 'timber')
        m.bm.free()
    # carga: sacos e um barril tombado
    mb.box((-1.0, -0.5, 0.82), (-0.2, 0.3, 1.25), 'straw')
    mb.box((0.0, -0.4, 0.82), (0.9, 0.5, 1.15), 'cloth_blue')
    return mb.finish(MATS)


def fence(length=3.0):
    mb = MeshBuilder('Fence')
    L = length / 2
    for x in (-L, 0, L):
        mb.box((x - 0.07, -0.07, 0), (x + 0.07, 0.07, 1.05), 'timber')
    for z in (0.4, 0.85):
        mb.box((-L, -0.04, z), (L, 0.04, z + 0.12), 'planks')
    return mb.finish(MATS)


def low_wall(length=4.0):
    """Muro baixo de pedra (0.9 m): obstáculo de vault."""
    mb = MeshBuilder('LowWall')
    L = length / 2
    mb.box((-L, -0.17, 0), (L, 0.17, 0.74), 'stone_wall')
    mb.box((-L - 0.05, -0.2, 0.74), (L + 0.05, 0.2, 0.84), 'stone_dark')
    return mb.finish(MATS)


def well():
    mb = MeshBuilder('Well')
    mb.cylinder((0, 0, 0), 1.1, 0.95, 'stone_wall', segs=16, cap_top=False)
    mb.cylinder((0, 0, 0.0), 0.9, 0.96, 'stone_dark', segs=16, cap_top=False)
    mb.cylinder((0, 0, 0.95), 1.15, 0.12, 'stone_dark', segs=16)
    mb.cylinder((0, 0, 0.2), 0.88, 0.02, 'dark', segs=16)
    for x in (-1.0, 1.0):
        mb.beam((x, 0, 1.0), (x, 0, 2.6), 0.14)
    mb.beam((-1.1, 0, 2.5), (1.1, 0, 2.5), 0.12)
    # telhadinho
    for s in (-1, 1):
        pts = [(-1.4, 0, 3.2), (1.4, 0, 3.2), (1.4, s * 1.2, 2.55), (-1.4, s * 1.2, 2.55)]
        if s > 0: pts = [pts[1], pts[0], pts[3], pts[2]]
        mb.poly(pts, 'roof_tiles'); mb.poly(list(reversed(pts)), 'planks')
    mb.beam((-0.95, 0, 2.5), (0.95, 0, 2.5), 0.1)
    mb.lathe([(0.0, 1.5), (0.18, 1.5), (0.2, 1.8), (0.0, 1.8)], 'planks', segs=8)   # balde
    return mb.finish(MATS)


def lamp_post():
    """Poste de lanterna: haste vertical alta = poste de salto (Pole) do DPS."""
    mb = MeshBuilder('LampPost')
    mb.box((-0.25, -0.25, 0), (0.25, 0.25, 0.4), 'stone_dark')
    mb.cylinder((0, 0, 0.4), 0.09, 3.6, 'timber', segs=8)
    mb.beam((0, 0, 3.6), (0, -0.7, 3.7), 0.07, 'iron')
    mb.beam((0, 0, 3.2), (0, -0.55, 3.65), 0.05, 'iron')
    mb.box((-0.15, -0.85, 3.05), (0.15, -0.55, 3.45), 'glass')
    mb.box((-0.18, -0.88, 3.45), (0.18, -0.52, 3.52), 'iron')
    mb.cone((0, -0.7, 3.52), 0.2, 0.18, 'iron', segs=6)
    return mb.finish(MATS)


def bench():
    mb = MeshBuilder('Bench')
    mb.box((-0.9, -0.22, 0.42), (0.9, 0.22, 0.5), 'planks')
    for x in (-0.75, 0.75):
        mb.box((x - 0.06, -0.2, 0), (x + 0.06, 0.2, 0.42), 'timber')
    return mb.finish(MATS)


def banner_pole():
    mb = MeshBuilder('BannerPole')
    mb.cylinder((0, 0, 0), 0.08, 6.0, 'timber', segs=8)
    mb.beam((0, 0, 5.8), (0, -1.0, 5.8), 0.05, 'iron')
    # estandarte com leve ondulação
    segs = 6
    for i in range(segs):
        z0 = 5.8 - 2.6 * i / segs; z1 = 5.8 - 2.6 * (i + 1) / segs
        o0 = math.sin(i * 0.9) * 0.08; o1 = math.sin((i + 1) * 0.9) * 0.08
        mb.poly([(o0, -0.05, z0), (o0, -0.95, z0), (o1, -0.95, z1), (o1, -0.05, z1)], 'cloth_red')
        mb.poly([(o1, -0.05, z1), (o1, -0.95, z1), (o0, -0.95, z0), (o0, -0.05, z0)], 'cloth_red')
    return mb.finish(MATS)


def bridge(length=12.0, width=3.2):
    mb = MeshBuilder('Bridge')
    L = length / 2; W = width / 2
    segs = 10
    def zat(x):  # arco suave
        return 0.9 + 0.6 * math.cos(x / L * math.pi / 2)
    for i in range(segs):
        x0 = -L + length * i / segs; x1 = x0 + length / segs
        mb.poly([(x0, -W, zat(x0)), (x1, -W, zat(x1)), (x1, W, zat(x1)), (x0, W, zat(x0))], 'planks')
        mb.poly([(x0, W, zat(x0) - 0.15), (x1, W, zat(x1) - 0.15), (x1, -W, zat(x1) - 0.15), (x0, -W, zat(x0) - 0.15)], 'timber')
        for y in (-W, W):
            mb.poly([(x0, y, zat(x0) - 0.15), (x1, y, zat(x1) - 0.15), (x1, y, zat(x1)), (x0, y, zat(x0))] if y < 0 else
                    [(x1, y, zat(x1) - 0.15), (x0, y, zat(x0) - 0.15), (x0, y, zat(x0)), (x1, y, zat(x1))], 'timber')
    for k in range(segs + 1):
        x = -L + length * k / segs
        for y in (-W + 0.05, W - 0.05):
            mb.beam((x, y, zat(x)), (x, y, zat(x) + 1.0), 0.1)
            if k % 2 == 0:
                mb.beam((x, y, zat(x) - 0.15), (x, y, -0.8), 0.18, 'timber')
    for y in (-W + 0.05, W - 0.05):
        for i in range(segs):
            x0 = -L + length * i / segs; x1 = x0 + length / segs
            mb.beam((x0, y, zat(x0) + 1.0), (x1, y, zat(x1) + 1.0), 0.09)
    return mb.finish(MATS)


def windmill():
    mb = MeshBuilder('Windmill')
    mb.cylinder((0, 0, 0), 3.2, 1.0, 'stone_dark', segs=12)
    mb.cylinder((0, 0, 1.0), 3.0, 9.0, 'plaster', segs=12, r_top=2.2)
    mb.cone((0, 0, 10.0), 2.7, 3.5, 'roof_tiles', segs=12)
    mb.box((-0.6, -3.05, 1.0), (0.6, -2.9, 3.2), 'planks', sides='y')
    # pás (giram no Unity: objeto separado)
    obj = mb.finish(MATS)
    sb = MeshBuilder('Windmill_Sails')
    for k in range(4):
        a = k * math.pi / 2
        d = Vector((math.cos(a), 0, math.sin(a)))
        sb.beam((0, 0, 0), tuple(d * 7.0), 0.18)
        p = Vector((-d.z, 0, d.x)) * 0.12
        q = Vector((-d.z, 0, d.x)) * 1.3
        pts = [d * 1.2 + p, d * 7.0 + p, d * 7.0 + q, d * 1.2 + q]
        sb.poly([tuple(v + Vector((0, -0.05, 0))) for v in pts], 'cloth_blue')
        sb.poly([tuple(v + Vector((0, -0.05, 0))) for v in reversed(pts)], 'cloth_blue')
        for t in range(5):
            u = 1.2 + 5.8 * t / 4
            sb.beam(tuple(d * u + p), tuple(d * u + q), 0.05)
    sails = sb.finish(MATS)
    sails.location = (0, -2.6, 9.6)
    sails.parent = obj
    return obj


def tree(name, seed, height=6.0, crown=2.6, kind='round'):
    random.seed(seed)
    mb = MeshBuilder(name)
    mb.cylinder((0, 0, 0), 0.28, height * 0.55, 'bark', segs=7, r_top=0.18)
    # galhos
    for k in range(3):
        a = random.uniform(0, math.tau)
        z = height * random.uniform(0.35, 0.5)
        mb.beam((0, 0, z), (math.cos(a) * 1.2, math.sin(a) * 1.2, z + 1.0), 0.12, 'bark')
    obj = mb.finish(MATS)
    # copa: icosferas deslocadas por ruído (volumes estilizados, sem alpha = barato)
    blobs = 5 if kind == 'round' else 4
    for b in range(blobs):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=crown * random.uniform(0.6, 0.85))
        s = bpy.context.active_object
        if kind == 'round':
            s.location = (random.uniform(-1, 1) * crown * 0.5, random.uniform(-1, 1) * crown * 0.5, height * 0.62 + random.uniform(-0.3, 0.9) * crown * 0.5)
        else:   # pinheiro: cones empilhados
            bpy.data.objects.remove(s)
            bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=crown * (1.0 - b * 0.2), depth=crown * 1.2, location=(0, 0, height * 0.35 + b * crown * 0.65))
            s = bpy.context.active_object
        for v in s.data.vertices:
            n = noise.noise(v.co * 0.9 + Vector((seed, b, 0)))
            v.co += v.co.normalized() * n * crown * 0.18
        s.data.materials.append(MATS['foliage'])
        s.parent = obj
        s.name = '%s_leaf%d' % (name, b)
    # junta tudo num objeto só (1 draw call por árvore)
    bpy.ops.object.select_all(action='DESELECT')
    for c in obj.children: c.select_set(True)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.join()
    # UV caixa simples nas folhas (o join manteve a UV do tronco)
    return obj


def bush(seed):
    random.seed(seed)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=0.9)
    s = bpy.context.active_object
    s.name = 'Bush'
    s.scale = (1.3, 1.0, 0.75)
    bpy.ops.object.transform_apply(scale=True)
    for v in s.data.vertices:
        v.co += v.co.normalized() * noise.noise(v.co * 1.4 + Vector((seed, 0, 0))) * 0.25
        v.co.z = max(v.co.z, -0.1)
    s.location.z = 0.45
    bpy.ops.object.transform_apply(location=True)
    s.data.materials.append(MATS['foliage'])
    return s


def rock(seed, size=1.2, name='Rock'):
    random.seed(seed)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=size)
    s = bpy.context.active_object
    s.name = name
    s.scale = (1.0, random.uniform(0.7, 1.0), random.uniform(0.5, 0.8))
    bpy.ops.object.transform_apply(scale=True)
    for v in s.data.vertices:
        v.co += v.co.normalized() * noise.noise(v.co * 1.1 + Vector((seed, 3, 0))) * size * 0.3
    s.data.materials.append(MATS['stone_dark'])
    return s


def slide_beam():
    """Viga baixa atravessada (passar deslizando): dois postes + viga a 1.1 m."""
    mb = MeshBuilder('SlideBeam')
    for x in (-1.8, 1.8):
        mb.box((x - 0.15, -0.15, 0), (x + 0.15, 0.15, 1.9), 'timber')
    mb.box((-1.9, -0.2, 0.8), (1.9, 0.2, 1.25), 'timber')       # viga baixa: só passa deslizando
    mb.box((-1.9, -0.12, 1.6), (1.9, 0.12, 1.85), 'timber')
    mb.box((-1.6, -0.22, 0.85), (1.6, -0.2, 1.2), 'cloth_red', sides='y')
    return mb.finish(MATS)


def uv_box_all(obj, tile=2.0):
    """UV caixa em escala de mundo para objetos feitos com primitivas do Blender."""
    import bmesh
    me = obj.data
    bm = bmesh.new(); bm.from_mesh(me)
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal; ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            p = l.vert.co
            l[uv].uv = (p.x / tile, p.y / tile) if ax == 2 else ((p.y / tile, p.z / tile) if ax == 0 else (p.x / tile, p.z / tile))
    bm.to_mesh(me); bm.free()


def main():
    global MATS
    jobs = [
        ('Stall_Red', lambda: stall('Stall_Red', 'cloth_red')),
        ('Stall_Blue', lambda: stall('Stall_Blue', 'cloth_blue')),
        ('Crate', lambda: crate(1.0)),
        ('Barrel', barrel),
        ('HayBale', hay_bale),
        ('Cart', cart),
        ('Fence', fence),
        ('LowWall', low_wall),
        ('Well', well),
        ('LampPost', lamp_post),
        ('Bench', bench),
        ('BannerPole', banner_pole),
        ('Bridge', bridge),
        ('Windmill', windmill),
        ('SlideBeam', slide_beam),
        ('Tree_Oak', lambda: tree('Tree_Oak', 3, 6.5, 2.4)),
        ('Tree_Oak2', lambda: tree('Tree_Oak2', 8, 5.2, 2.0)),
        ('Tree_Pine', lambda: tree('Tree_Pine', 5, 9.0, 2.2, kind='pine')),
        ('Bush', lambda: bush(4)),
        ('Rock_A', lambda: rock(2, 1.2, 'Rock_A')),
        ('Rock_B', lambda: rock(9, 2.4, 'Rock_B')),
    ]
    total = 0
    for name, fn in jobs:
        clear_scene(); MATS = get_materials()
        o = fn()
        if name.startswith(('Tree', 'Bush', 'Rock')):
            uv_box_all(o, 2.0)
        total += export([o] + list(o.children), name)
    print('TOTAL TRIS', total)


main()
