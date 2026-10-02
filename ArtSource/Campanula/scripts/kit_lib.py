"""Biblioteca de modelagem procedural da vila de Campanula (Blender, bmesh).

Convenções:
- metros; Z para cima; frente das construções olha para −Y (vira +Z no Unity com
  axis_forward='-Z', igual ao export do Aren).
- UV "caixa" em escala de mundo: cada material repete a textura a cada TILE[mat] m,
  então paredes de tamanhos diferentes têm a mesma densidade de textura.
"""
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

V = Vector
TILE = {
    'stone_wall': 2.4, 'stone_dark': 2.0, 'cobble': 3.0, 'plaster': 2.5, 'timber': 1.2,
    'planks': 1.6, 'roof_tiles': 2.2, 'roof_slate': 2.0, 'cloth_red': 2.0, 'cloth_blue': 2.0,
    'bronze': 1.0, 'foliage': 2.5, 'bark': 1.5, 'straw': 1.5, 'dark': 1.0, 'glass': 1.0,
    'iron': 1.0, 'dirt': 3.0, 'grass': 3.0,
}
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UNITY_MODELS = os.path.join(ROOT, '..', '..', 'Assets', 'Campanula', 'Models')


class MeshBuilder:
    """Acumula geometria (bmesh) com índice de material por face."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new('UVMap')
        self.mats = []

    def mat(self, m):
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def _uv_face(self, f, m, mode='box', u_axis=None):
        tile = TILE.get(m, 2.0)
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for loop in f.loops:
            p = loop.vert.co
            if mode == 'beam' and u_axis is not None:
                # viga: veio da madeira ao longo do comprimento
                u = p.dot(u_axis) / tile
                other = [i for i in range(3) if abs(u_axis[i]) < 0.9]
                v = (p[other[0]] + p[other[-1]]) / tile
            elif ax == 2:
                u, v = p.x / tile, p.y / tile
            elif ax == 0:
                u, v = p.y / tile, p.z / tile
            else:
                u, v = p.x / tile, p.z / tile
            loop[self.uv].uv = (u, v)

    def poly(self, pts, m, mode='box', u_axis=None):
        vs = [self.bm.verts.new(V(p)) for p in pts]
        f = self.bm.faces.new(vs)
        f.material_index = self.mat(m)
        f.normal_update()
        self._uv_face(f, m, mode, u_axis)
        return f

    def box(self, mn, mx, m, sides='all', mode='box'):
        """Caixa alinhada. sides: 'all' ou string com letras de faces: x X y Y z Z."""
        x0, y0, z0 = mn; x1, y1, z1 = mx
        faces = {
            'z': [(x0, y0, z0), (x0, y1, z0), (x1, y1, z0), (x1, y0, z0)],
            'Z': [(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)],
            'y': [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)],
            'Y': [(x1, y1, z0), (x0, y1, z0), (x0, y1, z1), (x1, y1, z1)],
            'x': [(x0, y1, z0), (x0, y0, z0), (x0, y0, z1), (x0, y1, z1)],
            'X': [(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)],
        }
        keys = 'zZyYxX' if sides == 'all' else sides
        ua = None
        if mode == 'beam':
            d = [x1 - x0, y1 - y0, z1 - z0]
            ua = V([1 if i == d.index(max(d)) else 0 for i in range(3)])
        for k in keys:
            self.poly(faces[k], m, mode, ua)

    def beam(self, a, b, w, m='timber', h=None):
        """Viga de seção w×h de a até b (qualquer direção)."""
        a, b = V(a), V(b)
        h = h or w
        d = b - a
        L = d.length
        if L < 1e-5:
            return
        z = d.normalized()
        up = V((0, 0, 1)) if abs(z.z) < 0.95 else V((1, 0, 0))
        x = z.cross(up).normalized(); y = x.cross(z).normalized()
        c = [a + x * sx * w / 2 + y * sy * h / 2 for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        e = [p + d for p in c]
        for i in range(4):
            j = (i + 1) % 4
            self.poly([c[i], c[j], e[j], e[i]], m, 'beam', z)
        self.poly([c[3], c[2], c[1], c[0]], m)
        self.poly([e[0], e[1], e[2], e[3]], m)

    def cylinder(self, base, radius, height, m, segs=12, cap_top=True, cap_bot=False, r_top=None):
        base = V(base)
        r_top = radius if r_top is None else r_top
        bot = [base + V((math.cos(a) * radius, math.sin(a) * radius, 0)) for a in [i / segs * math.tau for i in range(segs)]]
        top = [base + V((math.cos(a) * r_top, math.sin(a) * r_top, height)) for a in [i / segs * math.tau for i in range(segs)]]
        tile = TILE.get(m, 2.0)
        circ = 2 * math.pi * radius
        for i in range(segs):
            j = (i + 1) % segs
            f = self.poly([bot[i], bot[j], top[j], top[i]], m)
            # UV cilíndrico (não caixa) para fuste/troncos
            for k, loop in enumerate(f.loops):
                idx = [i, j, j, i][k]
                u = (idx if not (i == segs - 1 and k in (1, 2)) else segs) / segs * circ / tile
                loop[self.uv].uv = (u, [0, 0, height, height][k] / tile)
        if cap_top and r_top > 0.001:
            self.poly(top, m)
        if cap_bot:
            self.poly(list(reversed(bot)), m)

    def cone(self, base, radius, height, m, segs=12):
        base = V(base)
        apex = base + V((0, 0, height))
        ring = [base + V((math.cos(a) * radius, math.sin(a) * radius, 0)) for a in [i / segs * math.tau for i in range(segs)]]
        for i in range(segs):
            self.poly([ring[i], ring[(i + 1) % segs], apex], m)

    def pyramid(self, cx, cy, z, half, height, m, overhang=0.0):
        h = half + overhang
        c = [V((cx - h, cy - h, z)), V((cx + h, cy - h, z)), V((cx + h, cy + h, z)), V((cx - h, cy + h, z))]
        apex = V((cx, cy, z + height))
        for i in range(4):
            self.poly([c[i], c[(i + 1) % 4], apex], m)
        self.poly(list(reversed(c)), m)

    def lathe(self, profile, m, segs=16, center=(0, 0, 0)):
        """Sólido de revolução: profile = [(raio, z), ...] de baixo para cima."""
        c = V(center)
        rings = [[c + V((math.cos(a) * r, math.sin(a) * r, z)) for a in [i / segs * math.tau for i in range(segs)]] for r, z in profile]
        for k in range(len(rings) - 1):
            for i in range(segs):
                j = (i + 1) % segs
                self.poly([rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]], m)

    def transform(self, mat):
        bmesh.ops.transform(self.bm, matrix=mat, verts=self.bm.verts)

    def finish(self, materials):
        me = bpy.data.meshes.new(self.name)
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        self.bm.normal_update()
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(materials[m])
        obj = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(obj)
        return obj


def get_materials():
    mats = {}
    colors = {
        'stone_wall': (0.55, 0.52, 0.47), 'stone_dark': (0.42, 0.40, 0.38), 'cobble': (0.5, 0.47, 0.42),
        'plaster': (0.86, 0.8, 0.68), 'timber': (0.22, 0.15, 0.1), 'planks': (0.45, 0.33, 0.21),
        'roof_tiles': (0.55, 0.26, 0.17), 'roof_slate': (0.25, 0.27, 0.32), 'cloth_red': (0.7, 0.2, 0.18),
        'cloth_blue': (0.25, 0.32, 0.5), 'bronze': (0.6, 0.45, 0.2), 'foliage': (0.28, 0.36, 0.16),
        'bark': (0.3, 0.23, 0.16), 'straw': (0.8, 0.68, 0.36), 'dark': (0.03, 0.025, 0.02),
        'glass': (0.95, 0.7, 0.35), 'iron': (0.12, 0.12, 0.13), 'dirt': (0.5, 0.4, 0.28), 'grass': (0.35, 0.45, 0.2),
    }
    for k, c in colors.items():
        m = bpy.data.materials.get('CMP_' + k) or bpy.data.materials.new('CMP_' + k)
        m.diffuse_color = (*c, 1)
        mats[k] = m
    return mats


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def export(objs, name):
    os.makedirs(UNITY_MODELS, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    path = os.path.join(UNITY_MODELS, name + '.fbx')
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True, mesh_smooth_type='FACE',
        bake_anim=False, path_mode='STRIP')
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs if o.type == 'MESH')
    print('EXPORT', name, tris, 'tris')
    return tris
