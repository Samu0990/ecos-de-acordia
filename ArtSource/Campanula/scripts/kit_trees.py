"""Árvores e arbustos de Campanula com volume (substitui as copas de icosfera lisas).

- Tronco cônico com raiz alargada, leve curvatura e galhos que entram na copa.
- Copa de muitos aglomerados (icosferas subdiv 2 (80 faces) deslocadas por ruído) distribuídos num
  elipsoide; normais ESFÉRICAS (apontam para fora do centro da copa) — a luz rasante do
  pôr do sol desenha o volume da copa inteira, não as facetas de cada bolinha.
- Cor de vértice: R = oclusão (miolo e base da copa mais escuros), A = peso do vento
  (0 no tronco, 1 nas pontas) — lidos pelo shader Campanula/Foliage.
- ~1.3k triângulos por carvalho (110 árvores na cena, alvo Intel UHD 620).

Rodar: blender -b --factory-startup --python ArtSource/Campanula/scripts/kit_trees.py
"""
import bpy, bmesh, math, random, os, sys
from mathutils import Vector, noise
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import MeshBuilder, get_materials, clear_scene, export

V = Vector
MATS = None


def tube(mb, pts, radii, m, segs=8):
    """Tubo ao longo de uma polilinha (tronco/galho), UV cilíndrico."""
    bm = mb.bm
    rings = []
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        up = V((0, 0, 1)) if abs(d.z) < 0.95 else V((1, 0, 0))
        x = d.cross(up).normalized(); y = x.cross(d).normalized()
        ring = []
        for k in range(segs):
            a = k / segs * math.tau
            ring.append(bm.verts.new(p + (x * math.cos(a) + y * math.sin(a)) * radii[i]))
        rings.append(ring)
    mi = mb.mat(m)
    L = 0.0
    for i in range(len(rings) - 1):
        L1 = L + (pts[i + 1] - pts[i]).length
        for k in range(segs):
            j = (k + 1) % segs
            f = bm.faces.new((rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k]))
            f.material_index = mi
            for n, loop in enumerate(f.loops):
                kk = [k, k + 1, k + 1, k][n]
                ll = [L, L, L1, L1][n]
                loop[mb.uv].uv = (kk / segs * 2.0, ll / 1.5)
        L = L1


def blob(center, radius, seed, subdiv=1, squash=1.0):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=radius, location=center)
    s = bpy.context.active_object
    for v in s.data.vertices:
        n = noise.noise(v.co * 1.3 + V((seed, seed * 0.7, 0)))
        v.co = v.co + v.co.normalized() * n * radius * 0.35
        v.co.z *= squash
    return s


def finalize(trunk, foliage, crown_center, crown_radii, pine=False):
    """Junta tronco + copa num objeto, normais esféricas na copa e cor de vértice."""
    for f in foliage:
        f.data.materials.clear(); f.data.materials.append(MATS['foliage'])
    bpy.ops.object.select_all(action='DESELECT')
    for f in foliage: f.select_set(True)
    trunk.select_set(True)
    bpy.context.view_layer.objects.active = trunk
    bpy.ops.object.join()
    obj = trunk
    me = obj.data
    for p in me.polygons: p.use_smooth = True
    fol_idx = [i for i, m in enumerate(me.materials) if m and m.name == 'CMP_foliage']
    # UV caixa nas folhas (densidade de textura constante)
    bm = bmesh.new(); bm.from_mesh(me)
    uvl = bm.loops.layers.uv.verify()
    for f in bm.faces:
        if f.material_index not in fol_idx: continue
        n = f.normal; ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            p = l.vert.co
            l[uvl].uv = (p.x / 2.5, p.y / 2.5) if ax == 2 else ((p.y / 2.5, p.z / 2.5) if ax == 0 else (p.x / 2.5, p.z / 2.5))
    bm.to_mesh(me); bm.free()

    col = me.color_attributes.new("Col", 'BYTE_COLOR', 'CORNER')
    normals = []
    top_z = max(v.co.z for v in me.vertices)
    cx, cy, cz = crown_center
    rx, ry, rz = crown_radii
    for poly in me.polygons:
        is_leaf = poly.material_index in fol_idx
        for li in poly.loop_indices:
            v = me.vertices[me.loops[li].vertex_index].co
            if is_leaf:
                if pine:
                    d = V((v.x - cx, v.y - cy, 0)).normalized() + V((0, 0, 0.55))
                else:
                    d = V(((v.x - cx) / rx, (v.y - cy) / ry, (v.z - cz + rz * 0.25) / rz))
                d = d.normalized() if d.length > 1e-5 else V((0, 0, 1))
                normals.append(tuple(d))
                # oclusão: base e miolo da copa mais escuros
                rel = V(((v.x - cx) / rx, (v.y - cy) / ry, (v.z - cz) / rz))
                outer = min(1.0, rel.length)
                height = max(0.0, min(1.0, (v.z - (cz - rz)) / (2 * rz)))
                ao = 0.38 + 0.62 * (0.55 * outer + 0.45 * height)
                sway = 0.35 + 0.65 * outer
                col.data[li].color = (ao, ao, ao, sway)
            else:
                normals.append(tuple(me.vertices[me.loops[li].vertex_index].normal))
                sway = max(0.0, min(0.3, v.z / top_z * 0.35))
                ao = 0.55 + 0.45 * min(1.0, v.z / 1.2)   # base do tronco escurece no chão
                col.data[li].color = (ao, ao, ao, sway)
    me.normals_split_custom_set(normals)
    return obj


def oak(name, seed, height, crown, blobs=13):
    random.seed(seed)
    mb = MeshBuilder(name)
    trunk_h = height * 0.62
    pts, radii = [], []
    bend = V((random.uniform(-0.3, 0.3), random.uniform(-0.3, 0.3), 0))
    for i in range(7):
        t = i / 6
        pts.append(V((0, 0, trunk_h * t)) + bend * (t * t))
        radii.append(0.42 * (1 - t) ** 1.6 + 0.16 + (0.18 if i == 0 else 0))
    tube(mb, pts, radii, 'bark', 9)
    top = pts[-1]
    cc = V((bend.x, bend.y, height * 0.7))
    branch_dirs = []
    for k in range(5):
        a = k / 5 * math.tau + random.uniform(-0.4, 0.4)
        z0 = trunk_h * random.uniform(0.5, 0.85)
        start = V((0, 0, z0)) + bend * ((z0 / trunk_h) ** 2)
        d = V((math.cos(a), math.sin(a), 0))
        end = cc + d * crown * random.uniform(0.45, 0.7) + V((0, 0, random.uniform(-0.3, 0.6)))
        mid = start.lerp(end, 0.5) + V((0, 0, 0.35))
        tube(mb, [start, mid, end], [0.13, 0.09, 0.05], 'bark', 6)
        branch_dirs.append(end)
    trunk = mb.finish(MATS)
    rx, ry, rz = crown, crown * random.uniform(0.85, 1.0), crown * 0.78
    leaves = []
    # aglomerados nas pontas dos galhos + preenchimento no elipsoide
    for i in range(blobs):
        if i < len(branch_dirs):
            c = branch_dirs[i] + V((0, 0, 0.25))
        else:
            u = random.uniform(-1, 1); th = random.uniform(0, math.tau)
            r = random.uniform(0.25, 0.8)
            c = cc + V((math.sqrt(1 - u * u) * math.cos(th) * rx * r, math.sqrt(1 - u * u) * math.sin(th) * ry * r, u * rz * r + rz * 0.15))
        leaves.append(blob(c, crown * random.uniform(0.42, 0.62), seed * 10 + i, 2, 0.85))
    return finalize(trunk, leaves, (cc.x, cc.y, cc.z), (rx, ry, rz))


def pine(name, seed, height, crown):
    random.seed(seed)
    mb = MeshBuilder(name)
    pts = [V((0, 0, height * t)) for t in (0, 0.25, 0.5, 0.75, 0.95)]
    tube(mb, pts, [0.34, 0.24, 0.18, 0.12, 0.05], 'bark', 8)
    trunk = mb.finish(MATS)
    leaves = []
    layers = 6
    for b in range(layers):
        t = b / (layers - 1)
        r = crown * (1.05 - t * 0.8)
        z = height * 0.22 + t * height * 0.68
        bpy.ops.mesh.primitive_cone_add(vertices=10, radius1=r, depth=crown * 0.95, location=(0, 0, z))
        s = bpy.context.active_object
        for v in s.data.vertices:
            n = noise.noise(v.co * 1.6 + V((seed, b, 0)))
            radial = V((v.co.x, v.co.y, 0))
            v.co += radial.normalized() * n * r * 0.22 if radial.length > 1e-4 else V((0, 0, 0))
            if radial.length > r * 0.7:
                v.co.z -= r * 0.18    # saia caída nas pontas
        leaves.append(s)
    return finalize(trunk, leaves, (0, 0, height * 0.55), (crown, crown, height * 0.4), pine=True)


def bush(name, seed, size=0.9):
    random.seed(seed)
    mb = MeshBuilder(name)
    mb.cylinder((0, 0, 0), 0.06, 0.3, 'bark', segs=5)
    trunk = mb.finish(MATS)
    leaves = []
    for i in range(5):
        a = i / 5 * math.tau
        c = V((math.cos(a) * size * 0.45, math.sin(a) * size * 0.35, size * random.uniform(0.35, 0.6)))
        leaves.append(blob(c, size * random.uniform(0.45, 0.6), seed * 7 + i, 2, 0.8))
    leaves.append(blob(V((0, 0, size * 0.75)), size * 0.55, seed * 7 + 9, 2, 0.85))
    return finalize(trunk, leaves, (0, 0, size * 0.5), (size, size * 0.8, size * 0.6))


def main():
    global MATS
    jobs = [
        ('Tree_Oak', lambda: oak('Tree_Oak', 3, 6.8, 2.6)),
        ('Tree_Oak2', lambda: oak('Tree_Oak2', 8, 5.4, 2.2, blobs=11)),
        ('Tree_Pine', lambda: pine('Tree_Pine', 5, 9.0, 2.3)),
        ('Bush', lambda: bush('Bush', 4, 0.9)),
    ]
    for name, fn in jobs:
        clear_scene(); MATS = get_materials()
        o = fn()
        o.name = name
        export([o], name)


main()
