"""Construções de Campanula: casas enxaimel, taverna, Torre dos Doze Sinos, muralha e portão.
Rodar: blender -b --python kit_buildings.py
Marcadores de parkour exportados como empties: LEDGE_<comprimento>, REACH, etc. O local −Y
do empty aponta para fora da parede (vira +Z no Unity)."""
import bpy, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import *

MATS = None
_LEDGE_N = [0]


def ledge_name(length):
    """Nome sem ponto (o Blender trata ".7" como sufixo numérico em nomes repetidos)."""
    _LEDGE_N[0] += 1
    return 'LEDGE_%03d_%d' % (int(round(length * 10)), _LEDGE_N[0])


def marker(name, pos, outward=(0, -1), parent=None):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = 'ARROWS'; e.empty_display_size = 0.3
    bpy.context.scene.collection.objects.link(e)
    e.location = pos
    nx, ny = outward
    e.rotation_euler = (0, 0, math.atan2(nx, -ny))
    if parent:
        e.parent = parent
    return e


def timber_facade(mb, x0, x1, y, z0, z1, side, spacing=1.15, braces=True, windows=None, proud=0.05):
    """Moldura de enxaimel numa fachada plana entre x0..x1 (em coordenada local da fachada).
    side: 'front' (−Y), 'back' (+Y), 'left' (−X), 'right' (+X). y = coordenada da parede."""
    w = 0.16
    def P(u, z):
        if side == 'front': return (u, y - proud, z)
        if side == 'back': return (u, y + proud, z)
        if side == 'left': return (y - proud, u, z)
        return (y + proud, u, z)
    L = x1 - x0
    n = max(2, int(round(L / spacing)))
    xs = [x0 + L * i / n for i in range(n + 1)]
    for i, u in enumerate(xs):
        uu = min(max(u, x0 + w / 2), x1 - w / 2)
        mb.beam(P(uu, z0), P(uu, z1), w)
    mb.beam(P(x0, z0 + w / 2), P(x1, z0 + w / 2), w)
    mb.beam(P(x0, z1 - w / 2), P(x1, z1 - w / 2), w)
    sill = z0 + (z1 - z0) * 0.38
    mb.beam(P(x0, sill), P(x1, sill), w * 0.8)
    if braces and n >= 3:
        # mãos-francesas nos vãos das pontas (o "X"/"K" clássico)
        mb.beam(P(xs[0], sill), P(xs[1], z1 - w), w * 0.75)
        mb.beam(P(xs[-1], sill), P(xs[-2], z1 - w), w * 0.75)
        mb.beam(P(xs[0], z0 + w), P(xs[1], sill), w * 0.75)
        mb.beam(P(xs[-1], z0 + w), P(xs[-2], sill), w * 0.75)
    return xs


def window(mb, side, u, y, z, ww=0.75, wh=0.95, shutters=True, lit=True):
    """Janela: vidro quente (emissivo no Unity) recuado, moldura e venezianas abertas."""
    d = 0.06
    if side in ('front', 'back'):
        s = -1 if side == 'front' else 1
        mb.box((u - ww / 2, y - 0.02 if s < 0 else y - d, z), (u + ww / 2, y + d if s < 0 else y + 0.02, z + wh), 'glass' if lit else 'dark', sides='y' if s < 0 else 'Y')
        for a, b in (((u - ww / 2 - 0.08, z - 0.08), (u + ww / 2 + 0.08, z)), ((u - ww / 2 - 0.08, z + wh), (u + ww / 2 + 0.08, z + wh + 0.08)),
                     ((u - ww / 2 - 0.08, z), (u - ww / 2, z + wh)), ((u + ww / 2, z), (u + ww / 2 + 0.08, z + wh))):
            mb.box((a[0], y + s * 0.0 - 0.06 if s < 0 else y, a[1]), (b[0], y if s < 0 else y + 0.06, b[1]), 'timber')
        mb.beam((u, y + s * 0.07, z + 0.02), (u, y + s * 0.07, z + wh - 0.02), 0.04)   # mainel
        if shutters:
            for k in (-1, 1):
                ux = u + k * (ww / 2 + 0.08 + ww * 0.25)
                mb.box((ux - ww * 0.24, y + s * 0.09 - 0.025, z), (ux + ww * 0.24, y + s * 0.09 + 0.025, z + wh), 'planks')
    else:
        s = -1 if side == 'left' else 1
        mb.box((y - 0.02 if s < 0 else y - d, u - ww / 2, z), (y + d if s < 0 else y + 0.02, u + ww / 2, z + wh), 'glass' if lit else 'dark', sides='x' if s < 0 else 'X')
        for (a0, a1, b0, b1) in ((u - ww / 2 - 0.08, z - 0.08, u + ww / 2 + 0.08, z), (u - ww / 2 - 0.08, z + wh, u + ww / 2 + 0.08, z + wh + 0.08)):
            mb.box((y - 0.06 if s < 0 else y, a0, a1), (y if s < 0 else y + 0.06, b0, b1), 'timber')


def door(mb, u, y, w=1.1, h=2.1, z=0.5):
    mb.box((u - w / 2, y - 0.02, z), (u + w / 2, y + 0.08, z + h), 'planks', sides='y')
    mb.box((u - w / 2 - 0.12, y - 0.1, z), (u - w / 2, y, z + h + 0.12), 'timber')
    mb.box((u + w / 2, y - 0.1, z), (u + w / 2 + 0.12, y, z + h + 0.12), 'timber')
    mb.box((u - w / 2 - 0.12, y - 0.1, z + h), (u + w / 2 + 0.12, y, z + h + 0.14), 'timber')
    mb.box((u - w / 2 - 0.2, y - 0.5, z - 0.02), (u + w / 2 + 0.2, y, z + 0.0), 'stone_dark')  # soleira
    for k in (0.3, 0.7):   # ferragens
        mb.box((u - w / 2 + 0.05, y - 0.04, z + h * k), (u + w / 2 - 0.05, y - 0.02, z + h * k + 0.05), 'iron')


def gable_roof(mb, x0, x1, y0, y1, z, rise, m, over_x=0.45, over_y=0.5, thick=0.16):
    """Telhado de duas águas com cumeeira paralela a X."""
    yc = (y0 + y1) / 2
    ya, yb = y0 - over_y, y1 + over_y
    xa, xb = x0 - over_x, x1 + over_x
    run = (y1 - y0) / 2 + over_y
    zr = z + rise
    drop = over_y * rise / ((y1 - y0) / 2)
    ze = z - drop
    for (ye, sgn) in ((ya, -1), (yb, 1)):
        top = [(xa, ye, ze), (xb, ye, ze), (xb, yc, zr), (xa, yc, zr)]
        if sgn > 0: top = [top[1], top[0], top[3], top[2]]
        mb.poly([(p[0], p[1], p[2] + thick) for p in top], m)
        mb.poly(list(reversed(top)), 'planks')
        # bordas da laje
        e0, e1 = top[0], top[1]
        mb.poly([e0, e1, (e1[0], e1[1], e1[2] + thick), (e0[0], e0[1], e0[2] + thick)], 'timber')
        for a, b in ((top[1], top[2]), (top[3], top[0])):
            mb.poly([a, b, (b[0], b[1], b[2] + thick), (a[0], a[1], a[2] + thick)], 'timber')
    mb.beam((xa, yc, zr + thick * 0.5), (xb, yc, zr + thick * 0.5), 0.22, 'timber')   # cumeeira
    return ze, zr


def gable_wall(mb, x, y0, y1, z, rise, side, m='plaster', frame=True):
    yc = (y0 + y1) / 2
    pts = [(x, y0, z), (x, y1, z), (x, yc, z + rise)]
    if side == 'left': pts = [pts[1], pts[0], pts[2]]
    mb.poly(pts, m)
    if frame:
        s = -0.05 if side == 'left' else 0.05
        mb.beam((x + s, y0, z + 0.08), (x + s, y1, z + 0.08), 0.16)
        mb.beam((x + s, yc, z), (x + s, yc, z + rise - 0.1), 0.16)
        mb.beam((x + s, y0 + 0.1, z + 0.1), (x + s, yc, z + rise - 0.15), 0.14)
        mb.beam((x + s, y1 - 0.1, z + 0.1), (x + s, yc, z + rise - 0.15), 0.14)


def house(name, w, d, floors, ground='stone_wall', roof='roof_tiles', jetty=0.35, rise=None,
          chimney=True, shed=None, balcony=False, sign=False, lit=(True, True, False)):
    """Casa enxaimel. floors = [alturas]. Térreo de pedra/reboco; andares de cima com
    moldura de madeira e balanço (jetty) na frente e atrás."""
    mb = MeshBuilder(name)
    x0, x1 = -w / 2, w / 2
    y0, y1 = -d / 2, d / 2
    mb.box((x0 - 0.06, y0 - 0.06, 0), (x1 + 0.06, y1 + 0.06, 0.5), 'stone_dark', sides='xXyYZ')
    z = 0.5
    markers = []
    for fi, fh in enumerate(floors):
        j = jetty * fi
        ya, yb = y0 - j, y1 + j
        mat = ground if fi == 0 else 'plaster'
        mb.box((x0, ya, z), (x1, yb, z + fh), mat, sides='xXyY')
        if fi > 0 and j > 0:
            mb.box((x0, ya, z - 0.01), (x1, ya + jetty, z + 0.01), 'planks', sides='z')
            mb.box((x0, yb - jetty, z - 0.01), (x1, yb, z + 0.01), 'planks', sides='z')
            # mísulas sob o balanço
            for u in [x0 + 0.3 + (w - 0.6) * k / 4 for k in range(5)]:
                mb.beam((u, ya + jetty + 0.05, z - 0.45), (u, ya + 0.05, z - 0.02), 0.12)
                mb.beam((u, yb - jetty - 0.05, z - 0.45), (u, yb - 0.05, z - 0.02), 0.12)
        if fi > 0 or ground == 'plaster':
            xs = timber_facade(mb, x0, x1, ya, z, z + fh, 'front')
            timber_facade(mb, x0, x1, yb, z, z + fh, 'back')
            timber_facade(mb, ya, yb, x0, z, z + fh, 'left', braces=False)
            timber_facade(mb, ya, yb, x1, z, z + fh, 'right', braces=False)
        # janelas
        nwin = max(1, int(w / 2.0))
        for k in range(nwin):
            u = x0 + w * (k + 0.5) / nwin
            if fi == 0 and abs(u) < 0.9:
                continue
            window(mb, 'front', u, ya, z + fh * 0.32, lit=lit[min(fi, 2)])
            window(mb, 'back', u, yb, z + fh * 0.32, lit=False)
        if d > 5:
            window(mb, 'left', 0, x0, z + fh * 0.32, lit=False)
            window(mb, 'right', 0, x1, z + fh * 0.32, lit=lit[min(fi, 2)])
        if fi == 0:
            door(mb, 0, ya, z=0.5)
        if balcony and fi == 1:
            # sacada de tábuas na frente (borda = LEDGE para escalar)
            bz = z + 0.02
            mb.box((x0 + 0.6, ya - 1.0, bz - 0.12), (x1 - 0.6, ya, bz), 'planks')
            for u in [x0 + 0.65 + (w - 1.3) * k / 6 for k in range(7)]:
                mb.beam((u, ya - 0.95, bz), (u, ya - 0.95, bz + 0.9), 0.07)
                mb.beam((u, ya - 0.95, bz - 0.1), (u, ya - 0.2, bz - 0.7), 0.1)
            mb.beam((x0 + 0.6, ya - 0.95, bz + 0.9), (x1 - 0.6, ya - 0.95, bz + 0.9), 0.09)
            markers.append((ledge_name(w - 1.4), (0, ya - 1.0, bz - 0.06), (0, -1)))
        z += fh
    jt = jetty * (len(floors) - 1)
    rise = rise or d * 0.55
    ze, zr = gable_roof(mb, x0, x1, y0 - jt, y1 + jt, z, rise, roof)
    gable_wall(mb, x0, y0 - jt, y1 + jt, z, rise, 'left')
    gable_wall(mb, x1, y0 - jt, y1 + jt, z, rise, 'right')
    if chimney:
        cx = x1 - 0.9
        mb.box((cx - 0.4, 0.6, z - 0.5), (cx + 0.4, 1.4, zr + 0.9), 'stone_dark')
        mb.box((cx - 0.5, 0.5, zr + 0.9), (cx + 0.5, 1.5, zr + 1.05), 'stone_dark')
    if shed:
        # anexo de telhado plano no lado (+X ou −X): degrau de parkour (2.5 m)
        s = 1 if shed == 'right' else -1
        sx0, sx1 = (x1, x1 + 3.0) if s > 0 else (x0 - 3.0, x0)
        sy0, sy1 = y0 + 0.4, y1 - 0.4
        mb.box((sx0, sy0, 0), (sx1, sy1, 2.1), 'planks' if ground == 'plaster' else 'stone_wall', sides='xXyY')
        mb.box((sx0 - 0.1 * (s < 0), sy0 - 0.1, 2.1), (sx1 + 0.1 * (s > 0), sy1 + 0.1, 2.25), 'planks')
        mb.beam((sx0, sy0 - 0.12, 2.17), (sx1, sy0 - 0.12, 2.17), 0.14)
        door(mb, (sx0 + sx1) / 2, sy0, w=1.4, h=1.7, z=0.0)
        markers.append((ledge_name(2.6), ((sx0 + sx1) / 2, sy0 - 0.1, 2.17), (0, -1)))
        markers.append(('TOP_%.1f' % 2.25, ((sx0 + sx1) / 2, (sy0 + sy1) / 2, 2.25), (0, -1)))
    if sign:
        mb.beam((x1 - 0.8, y0 - 0.2, 3.3), (x1 - 0.8, y0 - 1.4, 3.3), 0.1, 'iron')
        mb.box((x1 - 1.25, y0 - 1.35, 2.3), (x1 - 0.35, y0 - 1.3, 3.2), 'planks')
    obj = mb.finish(MATS)
    for n, p, o in markers:
        marker(n, p, o, obj)
    return obj


def bell_tower():
    """Torre dos Doze Sinos: fuste de pedra com cornijas (bordas de escalada), sineira
    com 12 arcos (3 por face) e agulha de ardósia; andaime de madeira na face sul."""
    mb = MeshBuilder('BellTower')
    s = 3.6       # meia largura
    H = 19.0      # base da sineira
    mb.box((-s - 0.3, -s - 0.3, 0), (s + 0.3, s + 0.3, 1.2), 'stone_dark', sides='xXyYZ')
    mb.box((-s, -s, 1.2), (s, s, H), 'stone_dark', sides='xXyY')
    markers = []
    for zc in (5.0, 9.5, 14.0):
        mb.box((-s - 0.1, -s - 0.1, zc), (s + 0.1, s + 0.1, zc + 0.3), 'stone_wall')
    # rota de escalada: coluna de pedras salientes na face sul, do chão ao parapeito,
    # a cada 1.3 m (o espaçamento que o DPS salta de borda em borda), em zigue-zague
    for i in range(14):
        zc = 1.85 + 1.3 * i
        u = -1.9 + (0.3 if i % 2 == 0 else -0.3)
        mb.box((u - 0.4, -s - 0.15, zc - 0.25), (u + 0.4, -s, zc), 'stone_wall')
        markers.append((ledge_name(0.8), (u, -s - 0.15, zc - 0.05), (0, -1)))
    # porta e frestas
    mb.box((-0.8, -s - 0.02, 1.2), (0.8, -s + 0.1, 3.8), 'planks', sides='y')
    for zc in (6.5, 11.0, 15.5):
        for (a, b) in (((-0.15, -s - 0.02), (0.15, -s + 0.05)),):
            mb.box((a[0], a[1], zc), (b[0], b[1], zc + 1.2), 'dark', sides='y')
    # sineira: pilares nos cantos + arcos (3 vãos por face)
    zb, zt = H, H + 5.0
    mb.box((-s - 0.2, -s - 0.2, zb - 0.3), (s + 0.2, s + 0.2, zb), 'stone_wall')
    for (cx, cy) in ((-s, -s), (s, -s), (s, s), (-s, s)):
        mb.box((cx - 0.55, cy - 0.55, zb), (cx + 0.55, cy + 0.55, zt), 'stone_dark')
    for face in range(4):
        for k in range(3):
            u0 = -s + 0.55 + (2 * s - 1.1) * k / 3
            u1 = -s + 0.55 + (2 * s - 1.1) * (k + 1) / 3
            pillar = 0.18
            def P(u, v, z):
                return [(u, -s + v, z), (s - v, u, z), (-u, s - v, z), (-s + v, -u, z)][face]
            # pilaretes entre vãos
            if k > 0:
                a = P(u0 - pillar, 0, zb); b = P(u0 + pillar, 0.5, zt - 1.0)
                mb.box((min(a[0], b[0]), min(a[1], b[1]), zb), (max(a[0], b[0]), max(a[1], b[1]), zt - 1.0), 'stone_dark')
            # arco: segmentos de pedra na parte de cima do vão
            segs = 6
            for i in range(segs):
                t0, t1 = i / segs * math.pi, (i + 1) / segs * math.pi
                r = (u1 - u0) / 2; uc = (u0 + u1) / 2; zc = zt - 1.6
                ua, za = uc - math.cos(t0) * r, zc + math.sin(t0) * r * 0.9
                ub, zb2 = uc - math.cos(t1) * r, zc + math.sin(t1) * r * 0.9
                pa, pb = P(ua, -0.05, za), P(ub, -0.05, zb2)
                mb.beam(pa, pb, 0.35, 'stone_wall', 0.5)
        # parapeito
        a = P(-s, 0, zb); b = P(s, 0.45, zb + 0.9)
        mb.box((min(a[0], b[0]), min(a[1], b[1]), zb), (max(a[0], b[0]), max(a[1], b[1]), zb + 0.9), 'stone_wall')
    mb.box((-s - 0.3, -s - 0.3, zt), (s + 0.3, s + 0.3, zt + 0.45), 'stone_wall')
    mb.box((-s + 0.4, -s + 0.4, zb - 0.05), (s - 0.4, s - 0.4, zb), 'planks', sides='Z')   # piso da sineira
    mb.pyramid(0, 0, zt + 0.45, s + 0.3, 9.0, 'roof_slate', overhang=0.25)
    mb.beam((0, 0, zt + 9.3), (0, 0, zt + 11.2), 0.08, 'iron')
    mb.beam((-0.5, 0, zt + 10.5), (0.5, 0, zt + 10.5), 0.06, 'iron')
    tower = mb.finish(MATS)

    # sinos (12, em 3 por face, malha separada para poder balançar no Unity)
    bells = []
    profile = [(0.0, 0.62), (0.16, 0.6), (0.24, 0.5), (0.27, 0.3), (0.34, 0.1), (0.42, 0.0), (0.38, 0.03), (0.0, 0.05)]
    for face in range(4):
        for k in range(3):
            u = -s + 0.55 + (2 * s - 1.1) * (k + 0.5) / 3
            p = [(u, -s + 1.0, 0), (s - 1.0, u, 0), (-u, s - 1.0, 0), (-s + 1.0, -u, 0)][face]
            bm = MeshBuilder('Bell_%d' % (face * 3 + k))
            scale = 1.0 + 0.25 * ((face * 3 + k) % 4) / 3
            bm.lathe([(r * scale, z * scale) for r, z in profile], 'bronze', segs=14)
            bm.beam((0, 0, 0.62 * scale), (0, 0, 0.95), 0.08, 'iron')
            bm.beam((-0.4, 0, 0.95), (0.4, 0, 0.95), 0.12, 'timber')
            b = bm.finish(MATS)
            b.location = (p[0], p[1], zt - 2.2)
            b.parent = tower
            bells.append(b)

    # andaime na face sul: postes + plataformas a cada 3 m (rota de escalada até a 1ª cornija)
    sc = MeshBuilder('Scaffold')
    ys0, ys1 = -s - 1.8, -s - 0.3
    xs = (-2.6, 0.0, 2.6)
    for x in xs:
        for y in (ys0, ys1):
            sc.cylinder((x, y, 0), 0.07, 14.3, 'bark', segs=6)
    levels = (2.1, 4.3, 6.5, 8.7, 10.9, 13.1)
    for lv_i, z in enumerate(levels):
        # plataformas alternam lado: zigue-zague obriga a subir pulando de borda em borda
        xa, xb = (-2.75, 0.4) if lv_i % 2 == 0 else (-0.4, 2.75)
        if z > 12: xa, xb = -2.75, 2.75   # última plataforma inteira, encostada na torre
        sc.box((xa, ys0 - 0.05, z - 0.08), (xb, ys1 + 0.3, z), 'planks')
        sc.beam((xa, ys0, z + 0.95), (xb, ys0, z + 0.95), 0.06, 'bark')
        sc.beam((xa, ys0, z - 0.2), (xb, ys1, z - 0.2), 0.08, 'bark')
    for x in xs:   # travamentos em X
        sc.beam((x, ys0, 0.3), (x, ys0, 14.5), 0.05, 'bark')
    sc.beam((-2.6, ys0, 0.5), (2.6, ys0, 6.0), 0.06, 'bark')
    sc.beam((2.6, ys0, 6.5), (-2.6, ys0, 12.0), 0.06, 'bark')
    sc.transform(Matrix.Rotation(math.radians(90), 4, 'Z'))   # face sul -> face leste
    scaffold = sc.finish(MATS)
    scaffold.parent = tower
    for n, p, o in markers:
        marker(n, p, o, tower)
    # remate saliente no topo do parapeito (mesma saliência das pedras: o DPS segue
    # firme com os pés na parede e a subida final cai em cima do parapeito)
    mb2 = MeshBuilder('BellTower_Lip')
    mb2.box((-3.0, -s - 0.15, zb + 0.62), (-0.8, -s, zb + 0.9), 'stone_wall')
    lip = mb2.finish(MATS); lip.parent = tower
    marker(ledge_name(2.2), (-1.9, -s - 0.15, zb + 0.85), (0, -1), tower)
    marker('TOP_BELFRY', (0, 0, zb), (0, -1), tower)
    return [tower, scaffold] + bells


def wall_segment(length=10.0, height=6.0, thick=2.4, skip_u=None):
    mb = MeshBuilder('Wall_Segment')
    L = length / 2; t = thick / 2
    mb.box((-L, -t - 0.15, 0), (L, t + 0.15, 0.8), 'stone_dark', sides='yYZ')
    mb.box((-L, -t, 0.8), (L, t, height), 'stone_wall', sides='yY')
    mb.box((-L, -t, height - 0.01), (L, t, height), 'cobble', sides='Z')
    # ameias dos dois lados
    n = int(length / 1.25)
    for side in (-1, 1):
        y0 = side * t - (0.5 if side > 0 else 0); y1 = y0 + 0.5
        mb.box((-L, y0, height), (L, y1, height + 0.6), 'stone_wall', sides='yYZ')
        if side < 0:
            for k in range(n):
                u = -L + length * (k + 0.25) / n
                mc = u + length * 0.25 / n
                if skip_u is not None and abs(mc - skip_u) < 1.0:
                    continue   # passagem larga acima da rota de escalada
                mb.box((u, y0, height + 0.6), (u + length * 0.5 / n, y1, height + 1.4), 'stone_wall', sides='xXyYZ')
    # contrafortes externos
    for u in (-L + 1.2, L - 1.2):
        mb.box((u - 0.5, -t - 0.9, 0), (u + 0.5, -t, height * 0.55), 'stone_dark', sides='xXyZ')
    obj = mb.finish(MATS)
    return obj


def wall_climb(length=10.0, height=6.0, thick=2.4):
    n0 = int(length / 1.25)
    k0 = n0 // 2
    u0 = -length / 2 + length * (k0 + 0.25) / n0 + length * 0.5 / n0 + (length * 0.5 / n0) / 2
    o = wall_segment(length, height, thick, skip_u=u0)
    o.name = 'Wall_Climb'
    import bmesh
    mb = MeshBuilder('Wall_Climb_Stones')
    t = thick / 2
    n = int(length / 1.25)
    # coluna de pedras centrada num vão entre ameias
    k = n // 2
    u = -length / 2 + length * (k + 0.25) / n + length * 0.5 / n + (length * 0.5 / n) / 2
    for i, zc in enumerate((1.9, 3.2, 4.4, 5.6)):
        x = u + (-0.25 if i % 2 == 0 else 0.25)
        mb.box((x - 0.35, -t - 0.15, zc - 0.25), (x + 0.35, -t, zc), 'stone_dark')
        marker(ledge_name(0.7), (x, -t - 0.15, zc - 0.05), (0, -1), o)
    mb.box((u - 0.9, -t - 0.15, height + 0.32), (u + 0.9, -t, height + 0.6), 'stone_dark')   # remate saliente (largo: IK das mãos)
    marker(ledge_name(1.8), (u, -t - 0.15, height + 0.55), (0, -1), o)
    st = mb.finish(MATS)
    st.parent = o
    return o


def wall_tower(radius=2.8, height=9.0):
    mb = MeshBuilder('Wall_Tower')
    mb.cylinder((0, 0, 0), radius + 0.25, 1.0, 'stone_dark', segs=16, cap_top=True)
    mb.cylinder((0, 0, 1.0), radius, height - 1.0, 'stone_wall', segs=16, cap_top=True)
    mb.cylinder((0, 0, height), radius + 0.35, 0.5, 'stone_wall', segs=16)
    mb.cone((0, 0, height + 0.5), radius + 0.6, 5.0, 'roof_slate', segs=16)
    for a in range(4):
        ang = a * math.pi / 2 + 0.4
        x, y = math.cos(ang) * (radius - 0.02), math.sin(ang) * (radius - 0.02)
        mb.box((x - 0.12, y - 0.12, height * 0.55), (x + 0.12, y + 0.12, height * 0.55 + 1.0), 'dark')
    return mb.finish(MATS)


def gatehouse(width=9.0, height=9.5, depth=5.0, arch_w=4.0, arch_h=5.0):
    mb = MeshBuilder('Gatehouse')
    W = width / 2; D = depth / 2; a = arch_w / 2
    # dois blocos laterais + lintel acima do arco
    mb.box((-W, -D, 0), (-a, D, height), 'stone_wall', sides='xXyY')
    mb.box((a, -D, 0), (W, D, height), 'stone_wall', sides='xXyY')
    mb.box((-a, -D, arch_h), (a, D, height), 'stone_wall', sides='yYz')
    mb.box((-W - 0.2, -D - 0.2, height), (W + 0.2, D + 0.2, height + 0.4), 'stone_dark')
    # aduelas do arco
    for side in (-1, 1):
        y = side * (D + 0.02)
        for i in range(8):
            t0, t1 = i / 8 * math.pi, (i + 1) / 8 * math.pi
            p0 = (-math.cos(t0) * a, y, arch_h - a * 0.5 + math.sin(t0) * a * 0.5)
            p1 = (-math.cos(t1) * a, y, arch_h - a * 0.5 + math.sin(t1) * a * 0.5)
            mb.beam(p0, p1, 0.6, 'stone_dark', 0.15)
    # ameias
    n = 7
    for k in range(n):
        u = -W + width * (k + 0.2) / n
        mb.box((u, -D - 0.2, height + 0.4), (u + width * 0.55 / n, -D + 0.4, height + 1.3), 'stone_wall')
    # portões de madeira abertos para dentro
    for side in (-1, 1):
        x0 = side * a
        mb.box((x0 - side * 0.1 if side > 0 else x0, D - 0.2, 0), (x0 + 0.1 if side < 0 else x0 + 0.1, D + 1.9, arch_h - 0.6), 'planks')
    # estandartes
    for side in (-1, 1):
        mb.box((side * (a + 1.4) - 0.6, -D - 0.08, height - 4.2), (side * (a + 1.4) + 0.6, -D - 0.03, height - 0.8), 'cloth_red', sides='y')
    obj = mb.finish(MATS)
    return obj


def main():
    global MATS
    clear_scene()
    MATS = get_materials()
    out = []
    total = 0
    builds = [
        ('House_A', lambda: house('House_A', 6.0, 7.0, [3.0, 2.8], ground='stone_wall', shed='right')),
        ('House_B', lambda: house('House_B', 5.0, 6.0, [2.8, 2.7, 2.6], ground='plaster', balcony=True, lit=(True, True, True))),
        ('House_C', lambda: house('House_C', 7.0, 5.5, [3.2], ground='stone_wall', roof='roof_slate', rise=2.8, jetty=0, shed='left')),
        ('House_D', lambda: house('House_D', 6.5, 7.5, [2.8, 2.9], ground='plaster', balcony=True, roof='roof_slate')),
        ('Tavern', lambda: house('Tavern', 10.0, 8.0, [2.8, 3.0], ground='stone_wall', balcony=True, sign=True, shed='left', lit=(True, True, True))),
    ]
    for name, fn in builds:
        clear_scene(); MATS = get_materials()
        o = fn()
        objs = [o] + list(o.children)
        total += export(objs, name)
    clear_scene(); MATS = get_materials()
    objs = bell_tower()
    allobjs = []
    for o in objs:
        allobjs.append(o)
        allobjs += [c for c in o.children if c not in allobjs]
    total += export(list(dict.fromkeys(allobjs)), 'BellTower')
    for name, fn in (('Wall_Segment', wall_segment), ('Wall_Climb', wall_climb), ('Wall_Tower', wall_tower), ('Gatehouse', gatehouse)):
        clear_scene(); MATS = get_materials()
        o = fn()
        total += export([o] + list(o.children), name)
    print('TOTAL TRIS', total)


main()
