"""Árvores e arbustos de Campanula, v2: galhos de verdade e cartões de folhas (substitui as copas de
"bolinhas" — o visual de pirulito low poly).

- Esqueleto recursivo: tronco com raiz alargada → galhos mestres → galhos → raminhos, cada nível
  mais fino, curvando para cima (carvalho) ou caindo e subindo na ponta (pinheiro); tubos afunilados
  com casca (CMP_bark).
- Folhagem: cartões quadrados com o atlas renderizado por leaf_atlas.py (metade esquerda = ramo de
  carvalho, direita = ramo de pinheiro), presos nas pontas e ao longo dos raminhos, virados para fora
  (CMP_leafcards → shader Campanula/LeafCards, recorte pelo alfa).
- Normais dos cartões = direção a partir do centro da copa (misturada com a do cartão): a copa inteira
  é iluminada como um volume, sem facetas de cartão.
- Cor de vértice: R = oclusão (miolo e base da copa mais escuros), A = peso do vento.
- <nome>_LOD1: mesmo esqueleto só até os galhos, cartões maiores e em menor número (o builder monta o
  LODGroup sozinho quando encontra o _LOD1).

Rodar: blender -b --factory-startup --python ArtSource/Campanula/scripts/kit_trees.py
"""
import bpy, bmesh, math, random, os, sys
from mathutils import Vector, Matrix, noise
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import get_materials, clear_scene, export

V = Vector
MATS = None


class TreeMesh:
    """Acumula tubos (casca) e cartões (folhas) num bmesh, com cor de vértice e normais próprias."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new('UVMap')
        self.col = self.bm.loops.layers.color.new('Col')
        self.normals = {}          # loop → normal (para normals_split_custom_set)
        self.cards = []            # faces de folha (para as normais esféricas)

    def tube(self, pts, radii, segs=6, sway0=0.0, sway1=0.3):
        bm = self.bm
        rings = []
        for i, p in enumerate(pts):
            d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            up = V((0, 0, 1)) if abs(d.z) < 0.95 else V((1, 0, 0))
            x = d.cross(up).normalized(); y = x.cross(d).normalized()
            rings.append([bm.verts.new(p + (x * math.cos(k / segs * math.tau) + y * math.sin(k / segs * math.tau)) * radii[i]) for k in range(segs)])
        L = 0.0
        n = len(pts)
        for i in range(n - 1):
            L1 = L + (pts[i + 1] - pts[i]).length
            for k in range(segs):
                j = (k + 1) % segs
                f = bm.faces.new((rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k]))
                f.material_index = 0
                for m, loop in enumerate(f.loops):
                    kk = [k, k + 1, k + 1, k][m]; ll = [L, L, L1, L1][m]; ii = [i, i, i + 1, i + 1][m]
                    loop[self.uv].uv = (kk / segs * (1.0 + radii[0] * 4), ll / 1.2)
                    t = ii / (n - 1)
                    sw = sway0 + (sway1 - sway0) * t
                    ao = 0.6 + 0.4 * min(1.0, loop.vert.co.z / 1.5)
                    loop[self.col] = (ao, ao, ao, sw)
            L = L1

    def card(self, center, normal, up, size, half, sway, ao):
        """Cartão quadrado centrado em 'center', face para 'normal'; 'half' 0 = carvalho, 1 = pinheiro."""
        n = normal.normalized()
        u = (up - n * up.dot(n)).normalized()
        r = n.cross(u).normalized()
        hs = size * 0.5
        corners = [center - r * hs - u * hs * 0.15, center + r * hs - u * hs * 0.15, center + r * hs + u * hs * 1.85, center - r * hs + u * hs * 1.85]
        vs = [self.bm.verts.new(c) for c in corners]
        f = self.bm.faces.new(vs)
        f.material_index = 1
        u0 = 0.0 if half == 0 else 0.5
        uvs = [(u0 + 0.0, 0.0), (u0 + 0.5, 0.0), (u0 + 0.5, 1.0), (u0 + 0.0, 1.0)]
        for loop, uv in zip(f.loops, uvs):
            loop[self.uv].uv = uv
            loop[self.col] = (ao, ao, ao, sway)
        self.cards.append(f)
        return f

    def finish(self, crown_c, crown_r, normal_blend=0.72):
        me = bpy.data.meshes.new(self.name)
        self.bm.normal_update()
        # normais: casca suave; cartões = direção do centro da copa (volume), um pouco da do cartão
        loop_n = []
        card_set = set(self.cards)
        for f in self.bm.faces:
            for loop in f.loops:
                if f in card_set:
                    p = loop.vert.co
                    d = V(((p.x - crown_c.x) / crown_r.x, (p.y - crown_c.y) / crown_r.y, (p.z - crown_c.z) / crown_r.z + 0.25))
                    d = d.normalized() if d.length > 1e-5 else V((0, 0, 1))
                    nn = f.normal if f.normal.dot(d) >= 0 else -f.normal
                    loop_n.append((d * normal_blend + nn * (1 - normal_blend)).normalized())
                else:
                    loop_n.append(loop.vert.normal.copy())
        self.bm.to_mesh(me)
        self.bm.free()
        me.materials.append(MATS['bark']); me.materials.append(MATS['leafcards'])
        for p in me.polygons: p.use_smooth = True
        me.normals_split_custom_set([tuple(n) for n in loop_n])
        obj = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(obj)
        return obj


def bezier(a, b, c, n):
    return [((1 - t) ** 2) * a + 2 * (1 - t) * t * b + (t ** 2) * c for t in [i / (n - 1) for i in range(n)]]


def rand_perp(d):
    a = V((random.uniform(-1, 1), random.uniform(-1, 1), random.uniform(-1, 1)))
    p = a - d * a.dot(d)
    return p.normalized() if p.length > 1e-4 else V((1, 0, 0))


def oak(name, seed, height, crown, lod=0):
    random.seed(seed)
    tm = TreeMesh(name)
    trunk_h = height * random.uniform(0.36, 0.44)
    lean = V((random.uniform(-0.25, 0.25), random.uniform(-0.25, 0.25), 0))
    # tronco com raiz alargada
    pts, radii = [], []
    for i in range(7):
        t = i / 6
        pts.append(V((0, 0, trunk_h * t * 1.05)) + lean * t * t)
        radii.append(0.24 * (1 - t) ** 1.2 + 0.14 + (0.12 if i == 0 else 0.0))
    tm.tube(pts, radii, 9 if lod == 0 else 6, 0.0, 0.05)
    cc = V((lean.x, lean.y, height * 0.66))
    rad = V((crown, crown * random.uniform(0.85, 1.0), crown * 0.72))
    anchors = []
    limbs = random.randint(4, 6)
    for k in range(limbs):
        a = k / limbs * math.tau + random.uniform(-0.35, 0.35)
        z0 = trunk_h * random.uniform(0.8, 1.02)
        start = V((0, 0, z0)) + lean * (z0 / trunk_h) ** 2
        out = V((math.cos(a), math.sin(a), 0))
        end = cc + out * crown * random.uniform(0.55, 0.85) + V((0, 0, random.uniform(-0.4, 0.9)))
        mid = start.lerp(end, 0.45) + V((0, 0, random.uniform(0.2, 0.6))) + out * 0.25
        limb = bezier(start, mid, end, 5)
        tm.tube(limb, [0.16, 0.12, 0.09, 0.065, 0.04], 6 if lod == 0 else 4, 0.05, 0.25)
        # galhos secundários ao longo do galho mestre
        nsub = 4 if lod == 0 else 2
        for s in range(nsub):
            t = 0.35 + s * (0.6 / max(1, nsub - 1)) if nsub > 1 else 0.6
            idx = t * (len(limb) - 1)
            i0 = int(idx); f = idx - i0
            p = limb[min(i0, len(limb) - 1)].lerp(limb[min(i0 + 1, len(limb) - 1)], f)
            d = (limb[-1] - limb[0]).normalized()
            sd = (d + rand_perp(d) * random.uniform(0.6, 1.1) + V((0, 0, random.uniform(0.1, 0.5)))).normalized()
            ln = crown * random.uniform(0.38, 0.6)
            e = p + sd * ln
            m = p.lerp(e, 0.5) + V((0, 0, 0.12))
            if lod == 0:
                tm.tube(bezier(p, m, e, 3), [0.05, 0.034, 0.016], 4, 0.25, 0.6)
            anchors.append((e, sd))
            anchors.append((p.lerp(e, 0.55), sd))
        anchors.append((end, (end - start).normalized()))
        anchors.append((limb[3], (end - start).normalized()))
    # cartões: 2–3 por ponto (carvalho), alguns extras no volume para fechar a copa
    per = 3 if lod == 0 else 2
    size = crown * (0.62 if lod == 0 else 0.95)
    for (p, d) in anchors:
        for c in range(per):
            off = rand_perp(d) * random.uniform(0.1, 0.45) * crown * 0.4 + d * random.uniform(-0.1, 0.3)
            c0 = p + off
            out = (c0 - cc); out.z *= 0.6
            nrm = (out.normalized() + rand_perp(d) * 0.8 + V((0, 0, 0.35))).normalized()
            rel = V(((c0.x - cc.x) / rad.x, (c0.y - cc.y) / rad.y, (c0.z - cc.z) / rad.z))
            ao = 0.42 + 0.58 * min(1.0, 0.5 * rel.length + 0.5 * max(0.0, min(1.0, rel.z * 0.5 + 0.5)))
            tm.card(c0 - V((0, 0, size * 0.25)), nrm, (d + V((0, 0, 1.2))).normalized(), size * random.uniform(0.85, 1.15), 0, 0.5 + 0.5 * min(1.0, rel.length), ao)
    extra = 26 if lod == 0 else 8
    for i in range(extra):
        u = random.uniform(-0.5, 1); th = random.uniform(0, math.tau); rr = random.uniform(0.35, 0.9)
        c0 = cc + V((math.sqrt(1 - u * u) * math.cos(th) * rad.x * rr, math.sqrt(1 - u * u) * math.sin(th) * rad.y * rr, u * rad.z * rr))
        nrm = ((c0 - cc).normalized() + rand_perp(V((0, 0, 1))) * 0.6).normalized()
        tm.card(c0 - V((0, 0, size * 0.3)), nrm, V((0, 0, 1)), size, 0, 0.7, 0.55 + 0.35 * rr)
    return tm.finish(cc, rad)


def pine(name, seed, height, crown, lod=0):
    random.seed(seed)
    tm = TreeMesh(name)
    pts = [V((random.uniform(-0.05, 0.05), random.uniform(-0.05, 0.05), height * t)) for t in (0, 0.2, 0.45, 0.7, 0.9, 1.0)]
    tm.tube(pts, [0.3, 0.22, 0.16, 0.1, 0.05, 0.02], 8 if lod == 0 else 5, 0.0, 0.15)
    z_start = height * 0.16
    whorls = 15 if lod == 0 else 8
    cc = V((0, 0, height * 0.5))
    for w in range(whorls):
        t = w / (whorls - 1)
        z = z_start + t * (height * 0.94 - z_start)
        r = crown * (1.0 - t * 0.88) * random.uniform(0.88, 1.08) + 0.2
        nb = 7 if lod == 0 else 5
        a0 = random.uniform(0, math.tau)
        for b in range(nb):
            a = a0 + b / nb * math.tau + random.uniform(-0.25, 0.25)
            out = V((math.cos(a), math.sin(a), 0))
            s = V((0, 0, z))
            # galho cai e a ponta sobe um pouco (abeto)
            e = s + out * r + V((0, 0, -r * 0.32 + r * 0.18 * t))
            m = s + out * r * 0.55 + V((0, 0, -r * 0.3))
            br = bezier(s, m, e, 3)
            if lod == 0:
                tm.tube(br, [0.05 * (1 - t * 0.6), 0.028, 0.012], 4, 0.1, 0.55)
            ncard = (4 if lod == 0 else 3) if r > crown * 0.35 else 2
            side = out.cross(V((0, 0, 1))).normalized()
            for c in range(ncard):
                tt = 0.3 + 0.7 * (c + 0.5) / ncard
                p = br[0].lerp(br[1], min(1.0, tt * 2)) if tt < 0.5 else br[1].lerp(br[2], (tt - 0.5) * 2)
                dirb = (e - s).normalized()
                # ramos em ângulos variados (deitado, inclinado para cada lado): volume em qualquer vista
                tilt = [0.0, 0.7, -0.7, 0.35][c % 4] + random.uniform(-0.2, 0.2)
                nrm = (V((0, 0, 1)) * math.cos(tilt) + side * math.sin(tilt) + out * 0.2).normalized()
                size = (0.95 + 0.7 * r / crown) * (1.0 if lod == 0 else 1.45)
                ao = 0.38 + 0.62 * (0.35 + 0.65 * tt) * (0.45 + 0.55 * t)
                tm.card(p - dirb * size * 0.2, nrm, dirb, size, 1, 0.4 + 0.6 * tt, ao)
        # enchimento junto ao tronco (sem "buracos" vendo através da copa)
        if lod == 0 and r > 0.6:
            for k in range(2):
                a = random.uniform(0, math.tau)
                tm.card(V((0, 0, z - 0.3)), V((math.cos(a), math.sin(a), 0.3)).normalized(), V((0, 0, 1)), r * 1.1, 1, 0.5, 0.35 + 0.3 * t)
    # topo
    for k in range(3 if lod == 0 else 2):
        a = k / 3 * math.pi
        tm.card(V((0, 0, height * 0.84)), V((math.cos(a), math.sin(a), 0)), V((0, 0, 1)), crown * 0.5, 1, 0.8, 0.9)
    return tm.finish(cc, V((crown, crown, height * 0.45)), 0.6)


def bush(name, seed, size=0.9, lod=0):
    random.seed(seed)
    tm = TreeMesh(name)
    for k in range(4 if lod == 0 else 2):
        a = k / 4 * math.tau + random.uniform(-0.3, 0.3)
        out = V((math.cos(a), math.sin(a), 0))
        tm.tube(bezier(V((0, 0, 0)), out * size * 0.2 + V((0, 0, size * 0.35)), out * size * 0.45 + V((0, 0, size * 0.7)), 3), [0.035, 0.022, 0.01], 4, 0.0, 0.5)
    cc = V((0, 0, size * 0.5))
    rad = V((size, size * 0.85, size * 0.65))
    n = 22 if lod == 0 else 9
    for i in range(n):
        u = random.uniform(-0.2, 1); th = random.uniform(0, math.tau); rr = random.uniform(0.3, 0.85)
        c0 = cc + V((math.sqrt(1 - u * u) * math.cos(th) * rad.x * rr, math.sqrt(1 - u * u) * math.sin(th) * rad.y * rr, u * rad.z * rr))
        nrm = ((c0 - cc).normalized() + rand_perp(V((0, 0, 1))) * 0.7).normalized()
        sz = size * (0.75 if lod == 0 else 1.1) * random.uniform(0.85, 1.15)
        tm.card(c0 - V((0, 0, sz * 0.35)), nrm, V((0, 0, 1)), sz, 0, 0.4 + 0.6 * rr, 0.45 + 0.5 * rr)
    return tm.finish(cc, rad)


def main():
    global MATS
    jobs = [
        ('Tree_Oak', lambda l: oak('Tree_Oak', 3, 7.2, 2.7, l)),
        ('Tree_Oak2', lambda l: oak('Tree_Oak2', 8, 5.8, 2.3, l)),
        ('Tree_Pine', lambda l: pine('Tree_Pine', 5, 10.0, 2.7, l)),
        ('Bush', lambda l: bush('Bush', 4, 1.3, l)),
    ]
    only = sys.argv[sys.argv.index('--') + 1].split(',') if '--' in sys.argv and len(sys.argv) > sys.argv.index('--') + 1 else None
    for name, fn in jobs:
        if only and name not in only: continue
        for lod in (0, 1):
            clear_scene(); MATS = get_materials()
            if 'leafcards' not in MATS:
                m = bpy.data.materials.get('CMP_leafcards') or bpy.data.materials.new('CMP_leafcards')
                m.diffuse_color = (0.12, 0.2, 0.07, 1)
                MATS['leafcards'] = m
            o = fn(lod)
            nm = name if lod == 0 else name + '_LOD1'
            o.name = nm
            export([o], nm)


if __name__ == '__main__':
    main()
