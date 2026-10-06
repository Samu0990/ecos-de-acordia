"""Sussurrante — etapa 6: a lâmina dentada (prancha: lâmina longa, escura, de bordas
serrilhadas como obsidiana quebrada, com brilho violeta).

Modelo procedural (~1,6 k triângulos), facetado:
- lâmina de 1,15 m, seção em losango (aresta viva + nervura central), fio irregular com
  dentes que apontam para a ponta (lascas), alguns cristais saindo da base;
- guarda de cristais irradiando, empunhadura hexagonal enfaixada (0,30 m: a mão do
  Sussurrante é enorme), pomo de cristal.
Eixos: empunhadura na origem, lâmina ao longo de +Y, largura em X, espessura em Z.
Cores de vértice (o shader Aren/SussurranteBlade usa): R = parte (1 lâmina, 0.6 cristal,
0.25 faixa da empunhadura, 0.1 madeira/couro), G = borda (0 nervura -> 1 fio),
B = posição ao longo da lâmina (0 base -> 1 ponta).

Rodar: blender -b --factory-startup --python ArtSource/Sussurrante/scripts/suss_06_blade.py
"""
import bpy, bmesh, os, sys, math, random
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import suss_common as C

rnd = random.Random(7)
L = 1.15          # comprimento da lâmina
W0 = 0.060        # meia-largura perto da guarda
T0 = 0.014        # meia-espessura na nervura
GRIP = 0.30
STATIONS = 64

bpy.ops.wm.read_factory_settings(use_empty=True)
bm = bmesh.new()
col = bm.loops.layers.color.new("Col")
vcol = {}


def V(p, c):
    v = bm.verts.new(p)
    vcol[v] = c
    return v


def face(vs):
    try:
        return bm.faces.new(vs)
    except ValueError:
        return None


# ---------------------------------------------------------------- perfil do fio (dentes)
def width(y):
    t = y / L
    belly = 1.0 + 0.14 * math.sin(math.pi * t)
    return W0 * belly * (1.0 - t ** 3.0)       # larga até ~2/3 e afina na ponta


def make_edge(seed):
    r = random.Random(seed)
    cuts = []
    y = 0.06
    k = 0
    while y < L * 0.93:
        step = r.uniform(0.045, 0.15)
        depth = r.uniform(0.007, 0.024) * (1.0 - 0.5 * y / L)
        if k % 4 == 3 or r.random() < 0.12:     # lasca grande de vez em quando
            depth *= 1.7
            step *= 1.3
        cuts.append((y, step, depth))
        y += step
        k += 1
    def notch(yy):
        for (y0, st, d) in cuts:
            if y0 <= yy < y0 + st:
                u = (yy - y0) / st
                # dente de serra: corta fundo no começo e volta devagar (lasca apontando p/ a ponta)
                return d * max(0.0, 1.0 - u * 1.25) ** 1.6
        return 0.0
    return notch


notch_l, notch_r = make_edge(11), make_edge(23)
rows = []
for i in range(STATIONS + 1):
    y = L * (i / STATIONS) ** 1.08
    w = width(y)
    t = T0 * (1.0 - 0.65 * y / L)
    rx = 0.004 * math.sin(y * 9.0)          # nervura levemente ondulada (pedra lascada)
    t_along = y / L
    el = V((-max(0.003, w - notch_l(y)), y, 0.0), (1.0, 1.0, t_along))
    er = V((max(0.003, w - notch_r(y)), y, 0.0), (1.0, 1.0, t_along))
    rt = V((rx, y, t), (1.0, 0.0, t_along))
    rb = V((rx, y, -t), (1.0, 0.0, t_along))
    rows.append((el, rt, er, rb))
tip = V((0.0, L + 0.06, 0.0), (1.0, 1.0, 1.0))
for i in range(STATIONS):
    a, b = rows[i], rows[i + 1]
    face([a[0], b[0], b[1], a[1]])
    face([a[1], b[1], b[2], a[2]])
    face([a[2], b[2], b[3], a[3]])
    face([a[3], b[3], b[0], a[0]])
last = rows[-1]
for k in range(4):
    face([last[k], tip, last[(k + 1) % 4]])
base = rows[0]
face([base[3], base[2], base[1], base[0]])


# ---------------------------------------------------------------- cristais (pirâmides alongadas)
def shard(origin, direction, length, radius, sides, c):
    d = Vector(direction).normalized()
    up = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))
    a = d.cross(up).normalized(); b = d.cross(a).normalized()
    o = Vector(origin)
    ring = []
    for k in range(sides):
        ang = 2 * math.pi * k / sides + rnd.uniform(-0.25, 0.25)
        rr = radius * rnd.uniform(0.75, 1.15)
        ring.append(V(o + (a * math.cos(ang) + b * math.sin(ang)) * rr + d * length * 0.18, c))
    top = V(o + d * length, (c[0], 1.0, c[2]))
    bot = V(o - d * length * 0.05, (c[0], 0.3, c[2]))
    for k in range(sides):
        face([ring[k], ring[(k + 1) % sides], top])
        face([ring[(k + 1) % sides], ring[k], bot])


CRYSTAL = (0.6, 0.6, 0.0)
# guarda: leque de cristais
for k, (dx, dy, dz, ln) in enumerate([(1, 0.25, 0.1, 0.15), (-1, 0.3, -0.1, 0.13), (1, -0.15, 0.25, 0.09),
                                       (-1, -0.1, 0.3, 0.10), (0.6, 0.9, 0.0, 0.11), (-0.5, 0.95, 0.1, 0.10),
                                       (0.2, 0.2, 1.0, 0.07), (-0.2, 0.15, -1.0, 0.07)]):
    shard((dx * 0.02, 0.0, dz * 0.01), (dx, dy, dz), ln * 0.8, 0.015, 4, CRYSTAL)
# cristais crescendo da base da lâmina (prancha: lâmina "rachada" com lascas)
for k in range(5):
    y = rnd.uniform(0.05, 0.32)
    side = -1 if k % 2 else 1
    w = width(y)
    shard((side * w * 0.55, y, side * 0.006), (side * 0.9, 0.55, rnd.uniform(-0.3, 0.3)), rnd.uniform(0.05, 0.09), 0.012, 4, CRYSTAL)


# ---------------------------------------------------------------- empunhadura e pomo
def ring_col(y0, y1, r0, r1, sides, c, cap0=False, cap1=False):
    r_a = [V((r0 * math.cos(2 * math.pi * k / sides), y0, r0 * math.sin(2 * math.pi * k / sides)), c) for k in range(sides)]
    r_b = [V((r1 * math.cos(2 * math.pi * k / sides), y1, r1 * math.sin(2 * math.pi * k / sides)), c) for k in range(sides)]
    for k in range(sides):
        face([r_a[k], r_a[(k + 1) % sides], r_b[(k + 1) % sides], r_b[k]])
    if cap0:
        face(list(reversed(r_a)))
    if cap1:
        face(r_b)


GRIPC = (0.1, 0.0, 0.0)
WRAP = (0.25, 0.0, 0.0)
ring_col(-GRIP, 0.0, 0.021, 0.024, 6, GRIPC, cap0=True, cap1=True)
n_wraps = 7
for k in range(n_wraps):
    y0 = -GRIP + (k + 0.15) * GRIP / n_wraps
    ring_col(y0, y0 + GRIP / n_wraps * 0.55, 0.027, 0.0265, 6, WRAP, cap0=True, cap1=True)
shard((0, -GRIP - 0.005, 0), (0, -1, 0), 0.07, 0.030, 5, CRYSTAL)

mesh = bpy.data.meshes.new("SussurranteBlade")
for f in bm.faces:
    for l in f.loops:
        c = vcol[l.vert]
        l[col] = (c[0], c[1], c[2], 1.0)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.to_mesh(mesh)
bm.free()
for p in mesh.polygons:
    p.use_smooth = False
obj = bpy.data.objects.new("SussurranteBlade", mesh)
bpy.context.scene.collection.objects.link(obj)
mat = bpy.data.materials.new("M_SussurranteBlade")
mesh.materials.append(mat)
# UV simples (o shader usa as cores de vértice; a UV só existe para tangentes)
uv = mesh.uv_layers.new(name="UVMap")
for poly in mesh.polygons:
    for li in poly.loop_indices:
        co = mesh.vertices[mesh.loops[li].vertex_index].co
        uv.data[li].uv = (co.x * 2 + 0.5, (co.y + GRIP) / (L + GRIP + 0.1))
mesh.calc_loop_triangles()
print("LAMINA tris", len(mesh.loop_triangles))

os.makedirs(C.OUT, exist_ok=True)
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.fbx(
    filepath=os.path.join(C.OUT, "SussurranteBlade.fbx"), use_selection=True,
    object_types={'MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z', axis_up='Y', use_mesh_modifiers=False, mesh_smooth_type='FACE',
    colors_type='LINEAR', path_mode='STRIP', embed_textures=False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(C.WORK, "suss_blade.blend"), compress=True)
print("OK")
