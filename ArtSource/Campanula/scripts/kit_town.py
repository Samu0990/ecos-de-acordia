"""Campânula v3: casas variadas para a cidade maior (ruas sem a mesma casa repetida).

Cada casa sai de uma semente: largura, fundo, número e altura dos andares, estilo da fachada
(pedra lavrada gótica, pedra escura, reboco com cunhais de pedra ou enxaimel com andares em
balanço sobre o térreo de pedra), telhado (empena para a rua, cumeeira paralela à rua com
trapeiras, ou empena escalonada), loja no térreo (vitrine em arco, toldo e placa), janela
saliente (balcão fechado), sacada, venezianas, floreiras, chaminés e torreão.

Mesmas convenções do kit_lib/kit_gothic (metros, Z para cima, frente = −Y, materiais CMP_*,
marcadores WIN_/LAMP_ para a luz da noite). Oclusão de ambiente assada no atlas próprio da cidade
(ao_atlas_town*.png): UV2 com u + 6 (perto) ou u + 8 (longe) — o shader Campanula/CityLit lê.
Rodar: blender -b --factory-startup --python ArtSource/Campanula/scripts/kit_town.py [-- THouse_00,THouse_05] [--no-ao]
"""
import bpy, math, os, sys, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import *
import kit_gothic as G
from mathutils import Matrix

N_HOUSES = 28
TOWN_GRID = 6                      # atlas 6×6 (36 ladrilhos)
TOWN_NAMES = ['THouse_%02d' % i for i in range(N_HOUSES)] + ['TGuild', 'TCornerTower', 'TCatedral',
              'TArch_5', 'TArch_8', 'TPassage_5', 'TPassage_6', 'TCage', 'TBanners_5', 'TBanners_8', 'TBanners_6']
AO_DIR = os.path.join(ROOT, 'ao')
BAKE_AO = '--no-ao' not in sys.argv


def fbox(mb, F, ua, ub, za, zb, oa, ob, m, sides='all'):
    """Caixa alinhada a uma fachada (as fachadas do kit são alinhadas aos eixos)."""
    a, b = F.P(ua, za, oa), F.P(ub, zb, ob)
    mb.box(tuple(map(min, a, b)), tuple(map(max, a, b)), m, sides=sides)


def rect_window(mb, F, u, z0, w, h, lit, mk, shutters=False, flowers=False, frame='timber', cross=True):
    """Janela retangular (reboco/enxaimel): vidro rente, moldura, peitoril de pedra, cruzeta, venezianas abertas."""
    u0, u1 = u - w / 2, u + w / 2
    mb.poly([F.P(u1, z0, 0.012), F.P(u1, z0 + h, 0.012), F.P(u0, z0 + h, 0.012), F.P(u0, z0, 0.012)], 'glass' if lit else 'dark')
    t = 0.09
    fbox(mb, F, u0 - t, u1 + t, z0 + h, z0 + h + t, 0.0, 0.09, frame)
    fbox(mb, F, u0 - t, u0, z0, z0 + h, 0.0, 0.09, frame)
    fbox(mb, F, u1, u1 + t, z0, z0 + h, 0.0, 0.09, frame)
    fbox(mb, F, u0 - 0.15, u1 + 0.15, z0 - 0.1, z0, 0.0, 0.17, 'trim')
    if cross and G.DETAIL:
        mb.beam(F.P(u, z0, 0.04), F.P(u, z0 + h, 0.04), 0.05, frame, 0.05)
        mb.beam(F.P(u0, z0 + h * 0.62, 0.04), F.P(u1, z0 + h * 0.62, 0.04), 0.05, frame, 0.05)
    if shutters and G.DETAIL:
        sw = w / 2 + 0.02
        fbox(mb, F, u0 - t - sw, u0 - t - 0.02, z0, z0 + h, 0.03, 0.07, 'planks')
        fbox(mb, F, u1 + t + 0.02, u1 + t + sw, z0, z0 + h, 0.03, 0.07, 'planks')
    if flowers and G.DETAIL:
        fbox(mb, F, u0 - 0.05, u1 + 0.05, z0 - 0.42, z0 - 0.1, 0.06, 0.36, 'planks')
        fbox(mb, F, u0, u1, z0 - 0.1, z0 + 0.14, 0.09, 0.33, 'foliage')
    p = F.P(u, z0 + h / 2, 0.02)
    mk.append(('WIN_%s_%03d_%03d_00_100' % ('L' if lit else 'D', int(w * 100), int(h * 100)), tuple(p), (F.N.x, F.N.y)))


def gwin(mb, F, u, z0, w, hrect, lit, mk, k=1.15):
    """Janela gótica: completa de perto; na versão de longe (LOD1) só o vidro recortado no arco."""
    if G.DETAIL:
        return G.window(mb, F, u, z0, w, hrect, lit, mk, k=k)
    zs = z0 + hrect
    u0, u1 = u - w / 2, u + w / 2
    inner = [(u1, z0), (u1, zs)] + list(reversed(G.arch_pts(u0, u1, zs, k, segs=3)))[1:-1] + [(u0, zs), (u0, z0)]
    mb.poly([F.P(a, b, 0.03) for a, b in inner], 'glass' if lit else 'dark')
    h = hrect + G.arch_rise(w, k)
    mk.append(('WIN_%s_%03d_%03d_%02d_%03d' % ('L' if lit else 'D', int(w * 100), int(h * 100), int(hrect / h * 100), int(k * 100)), tuple(F.P(u, z0 + h / 2, 0.02)), (F.N.x, F.N.y)))
    return h


def timber_frame(mb, F, L, za, zb, posts, braces=True):
    """Enxaimel: frechal e soleira, esteios nos cantos e entre as janelas, mãos-francesas nos painéis cegos."""
    off = 0.025
    mb.beam(F.P(-L / 2, za + 0.08, off), F.P(L / 2, za + 0.08, off), 0.17, 'timber', 0.1)
    mb.beam(F.P(-L / 2, zb - 0.08, off), F.P(L / 2, zb - 0.08, off), 0.17, 'timber', 0.1)
    for u in posts:
        mb.beam(F.P(u, za + 0.16, off), F.P(u, zb - 0.16, off), 0.16, 'timber', 0.1)
    if braces and G.DETAIL:
        for u, s in ((-L / 2 + 0.1, 1), (L / 2 - 0.1, -1)):
            mb.beam(F.P(u, za + 0.2, off), F.P(u + s * min(1.1, L * 0.18), zb - 0.25, off), 0.12, 'timber', 0.08)


def bays(L, n):
    return [-L / 2 + L * (k + 0.5) / n for k in range(n)]


def side_roof(mb_main, x0, x1, y0, y1, z, rise, m, lit, mk, dormers, wall_m, stone_gable):
    """Cumeeira paralela à rua (águas para a frente e para os fundos), empenas nas laterais e trapeiras na água
    da frente. Montado no referencial do steep_roof (cumeeira em Y) e girado −90° em Z."""
    rb = MeshBuilder('roof_tmp')
    rmk = []
    w, d = x1 - x0, y1 - y0
    yc = (y0 + y1) / 2
    lx0, lx1, ly0, ly1 = -d / 2, d / 2, -w / 2, w / 2      # local: X' = profundidade (frente em −X'), Y' = largura
    ze, zr = G.steep_roof(rb, lx0, lx1, ly0, ly1, z, rise, m)
    for side_y, outward in ((ly0, -1), (ly1, 1)):
        if stone_gable:
            G.gable(rb, lx0, lx1, side_y, z, rise, outward, lit, rmk, win=G.DETAIL)
        else:
            tri = [(lx0, side_y, z), (lx1, side_y, z), (0, side_y, z + rise)]
            if outward > 0: tri = [tri[1], tri[0], tri[2]]
            rb.poly(tri, wall_m)
            if G.DETAIL:
                rb.beam((lx0 + 0.2, side_y + outward * 0.03, z + 0.1), (0, side_y + outward * 0.03, z + rise - 0.2), 0.15, 'timber', 0.08)
                rb.beam((lx1 - 0.2, side_y + outward * 0.03, z + 0.1), (0, side_y + outward * 0.03, z + rise - 0.2), 0.15, 'timber', 0.08)
                rb.beam((0, side_y + outward * 0.03, z + 0.1), (0, side_y + outward * 0.03, z + rise - 0.3), 0.15, 'timber', 0.08)
    if G.DETAIL:
        for ux in dormers:
            G.dormer(rb, lx0, lx1, ze, zr, 0.3, ux, -1, lit, rmk)
    R = Matrix.Rotation(-math.pi / 2, 4, 'Z')
    T = Matrix.Translation((0, yc, 0))
    rb.transform(T @ R)
    for n, p, o in rmk:
        pv = T @ R @ V(p)
        ov = R.to_3x3() @ V((o[0], o[1], 0))
        mk.append((n, tuple(pv), (ov.x, ov.y)))
    return rb, ze, zr


def thouse(name, seed, lit_p=0.8):
    rng = random.Random(seed * 7919 + 13)
    R = rng.random
    w = rng.choice([4.8, 5.2, 5.6, 6.0, 6.4, 7.0, 7.6, 8.2])
    d = rng.choice([6.5, 7.0, 7.5, 8.0, 8.5])
    nf = rng.choices([2, 3, 4], [3, 5, 2])[0]
    floors = [round(rng.uniform(3.2, 3.6), 2)] + [round(rng.uniform(2.7, 3.1), 2) for _ in range(nf - 1)]
    style = rng.choices(['stone', 'timber', 'plaster', 'dark'], [3, 4, 3, 1.4])[0]
    roof = rng.choices(['front', 'side', 'stepped'], [4, 3, 2])[0]
    if style == 'timber' and roof == 'stepped': roof = 'front'
    roofm = 'roof_slate' if R() < 0.62 else 'roof_tiles'
    shop = R() < 0.5
    oriel = nf >= 3 and R() < 0.4
    balcony = not oriel and nf >= 2 and R() < 0.3
    turret = style in ('stone', 'dark') and R() < 0.18
    shutters = style in ('plaster', 'timber') and R() < 0.7
    flowers = R() < 0.4
    gothic_win = style in ('stone', 'dark')
    ground_m = {'stone': 'ashlar', 'dark': 'stone_dark', 'plaster': 'stone_wall', 'timber': rng.choice(['ashlar', 'stone_wall'])}[style]
    upper_m = {'stone': 'ashlar', 'dark': 'stone_dark', 'plaster': 'plaster', 'timber': 'plaster'}[style]
    jetty = 0.32 if style == 'timber' else 0.0
    return build_house(name, w, d, floors, style, roof, roofm, shop, oriel, balcony, turret, shutters, flowers,
                       gothic_win, ground_m, upper_m, jetty, rng, lit_p)


def build_house(name, w, d, floors, style, roof, roofm, shop, oriel, balcony, turret, shutters, flowers,
                gothic_win, ground_m, upper_m, jetty, rng, lit_p=0.8, chimneys=None):
    R = rng.random
    mb = MeshBuilder(name)
    mk = []
    x0, x1, y0, y1 = -w / 2, w / 2, -d / 2, d / 2
    nf = len(floors)
    # soco de pedra (desce 1,5 m: a casa assenta em rua inclinada sem fresta)
    mb.box((x0 - 0.1, y0 - 0.1, -1.5), (x1 + 0.1, y1 + 0.1, 0.6), 'trim', sides='xXyYZ')
    z = 0.6
    jet_prev = 0.0
    fy0 = y0
    for fi, fh in enumerate(floors):
        jet = jetty * min(fi, 2)
        fy0 = y0 - jet
        wm = ground_m if fi == 0 else upper_m
        mb.box((x0, fy0, z), (x1, y1, z + fh), wm, sides='xXyY')
        fs = G.faces_of(x0, x1, fy0, y1)
        if jet > jet_prev:
            # balanço do enxaimel: a viga do piso aparece embaixo, com mísulas de madeira
            mb.box((x0 - 0.04, fy0, z - 0.24), (x1 + 0.04, y0 - jet_prev, z + 0.02), 'timber', sides='xXyz')
            if G.DETAIL:
                for u in bays(w, max(2, int(w / 1.6))):
                    mb.beam((u, y0 - jet_prev, z - 0.9), (u, fy0 + 0.05, z - 0.18), 0.12, 'timber', 0.12)
        elif fi > 0:
            G.band(mb, x0, x1, fy0, y1, z, h=0.16, out=0.06)
        if G.DETAIL and style in ('stone', 'dark') and fi == 0:
            for (cx_, cy_, sx_, sy_) in ((x0, y0, -1, -1), (x1, y0, 1, -1), (x0, y1, -1, 1), (x1, y1, 1, 1)):
                G.quoins(mb, cx_, cy_, sx_, sy_, 0.7, 0.6 + sum(floors) - 0.3)
        if G.DETAIL and style == 'plaster':
            for (cx_, cy_, sx_, sy_) in ((x0, fy0, -1, -1), (x1, fy0, 1, -1), (x0, y1, -1, 1), (x1, y1, 1, 1)):
                G.quoins(mb, cx_, cy_, sx_, sy_, z + 0.05, z + fh - 0.05)
        for key, (F, L) in fs.items():
            if key in ('left', 'right') and (fi == 0 or R() < 0.35):
                continue   # casas geminadas: laterais quase cegas
            n = max(1, int(round(L / (2.0 if gothic_win else 1.75))))
            if key == 'back':
                n = max(1, n - 1)
            us = bays(L, n)
            if style == 'timber' and fi > 0:
                posts = [-L / 2 + 0.08, L / 2 - 0.08] + [ (us[k] + us[k + 1]) / 2 for k in range(len(us) - 1)]
                timber_frame(mb, F, L, z, z + fh, posts)
            for k, u in enumerate(us):
                if key == 'front' and fi == 0:
                    continue   # o térreo da frente é porta/loja (abaixo)
                lit = R() < (lit_p if key == 'front' else lit_p * 0.6)
                if gothic_win:
                    ww = 0.8 if fh < 3.1 else 0.92
                    gwin(mb, F, u, z + fh * 0.22, ww, fh * 0.42, lit, mk)
                else:
                    ww = rng.choice([0.85, 0.95, 1.05])
                    rect_window(mb, F, u, z + fh * 0.28, ww, fh * 0.5, lit, mk, shutters=shutters and key == 'front',
                                flowers=flowers and key == 'front' and fi >= 1, frame='timber' if style == 'timber' else 'trim')
        if fi == 0:
            F, L = fs['front']
            if shop and w >= 5.0:
                # loja: vitrine em arco, porta ao lado, toldo e placa de ferro
                su = -0.55 if w < 6.5 else -1.0
                sw = min(2.6, w * 0.42)
                h = G.opening(mb, F, su, 0.75, sw, 1.55, 'glass', frame='trim', t=0.2, depth=0.16, k=1.0, sill=True)
                mk.append(('WIN_L_%03d_%03d_%02d_%03d' % (int(sw * 100), int(h * 100), int(1.55 / h * 100), 100), tuple(F.P(su, 0.75 + h / 2, 0.02)), (0, -1)))
                du = min(w / 2 - 0.8, su + sw / 2 + 1.0)
                G.door(mb, F, du, w=1.1, h=1.9, mk=mk)
                cm = rng.choice(['cloth_red', 'cloth_blue'])
                a0, a1 = su - sw / 2 - 0.25, su + sw / 2 + 0.25
                za, zb = 0.75 + h + 0.25, 0.75 + h - 0.35
                q = [F.P(a0, za, 0.05), F.P(a1, za, 0.05), F.P(a1, zb, 1.25), F.P(a0, zb, 1.25)]
                mb.poly(q, cm); mb.poly(list(reversed(q)), cm)
                if G.DETAIL:
                    for uu in (a0, a1):
                        mb.beam(F.P(uu, za + 0.05, 0.0), F.P(uu, zb, 1.25), 0.04, 'iron', 0.04)
                    # placa da loja
                    mb.beam(F.P(x1 - 0.7, 3.45, 0.0), F.P(x1 - 0.7, 3.45, 1.2), 0.07, 'iron')
                    fbox(mb, F, x1 - 1.05, x1 - 0.35, 2.55, 3.35, 1.0, 1.05, 'planks')
            else:
                G.door(mb, F, rng.choice([-0.6, 0.0, 0.6]) if w > 5.5 else 0.0, mk=mk)
                if w >= 6.0:
                    for u in (x0 + 1.0, x1 - 1.0):
                        if gothic_win:
                            gwin(mb, F, u, 0.6 + fh * 0.25, 0.7, fh * 0.38, R() < lit_p, mk)
                        else:
                            rect_window(mb, F, u, 0.6 + fh * 0.3, 0.8, fh * 0.42, R() < lit_p, mk, shutters=shutters)
        if balcony and fi == 1:
            bz = z + 0.02
            mb.box((x0 + 0.5, fy0 - 1.0, bz - 0.2), (x1 - 0.5, fy0, bz), 'trim')
            if G.DETAIL:
                for u in [x0 + 0.6 + (w - 1.2) * k / 9 for k in range(10)]:
                    mb.beam((u, fy0 - 0.92, bz), (u, fy0 - 0.92, bz + 0.9), 0.04, 'iron', 0.04)
                mb.beam((x0 + 0.5, fy0 - 0.92, bz + 0.92), (x1 - 0.5, fy0 - 0.92, bz + 0.92), 0.06, 'iron', 0.06)
                for u in (x0 + 0.8, x1 - 0.8):
                    mb.beam((u, fy0 - 0.1, bz - 0.85), (u, fy0 - 0.85, bz - 0.2), 0.16, 'trim', 0.2)
        jet_prev = jet
        z += fh
    H = z
    # janela saliente (balcão fechado) nos andares de cima
    if oriel:
        ou = rng.choice([-1, 1]) * (w / 2 - 1.35)
        z1, z2 = 0.6 + floors[0] + 0.1, H - 0.15
        oy = fy0 - 0.75
        mb.box((ou - 0.95, oy, z1), (ou + 0.95, fy0, z2), upper_m, sides='xXyz')
        for i, (dz, ext) in enumerate(((0.0, 0.75), (-0.35, 0.5), (-0.7, 0.25))):
            mb.box((ou - 0.95 + i * 0.15, fy0 - ext, z1 + dz - 0.35), (ou + 0.95 - i * 0.15, fy0, z1 + dz), 'trim', sides='xXyz')
        OF = G.Face((ou, oy, 0), (1, 0, 0), (0, -1, 0))
        for fz in [z1 + 0.25 + k * floors[1] for k in range(len(floors) - 1)]:
            if fz + floors[1] * 0.5 > z2: break
            if gothic_win:
                gwin(mb, OF, 0, fz, 0.9, floors[1] * 0.42, R() < lit_p, mk)
            else:
                rect_window(mb, OF, 0, fz + 0.15, 1.0, floors[1] * 0.5, R() < lit_p, mk, frame='timber' if style == 'timber' else 'trim', shutters=False)
        if G.DETAIL:
            mb.pyramid(ou, oy + 0.375, z2, 1.05, 0.9, roofm, overhang=0.05)
    # topo: cornija (pedra/reboco) ou beiral (enxaimel)
    if style == 'timber':
        mb.box((x0 - 0.12, fy0 - 0.12, H - 0.05), (x1 + 0.12, y1 + 0.12, H + 0.12), 'timber')
    else:
        G.band(mb, x0, x1, fy0, y1, H, h=0.3, out=0.18)
        if G.DETAIL and style in ('stone', 'dark'):
            for key, (F, L) in G.faces_of(x0, x1, fy0, y1).items():
                if key in ('front', 'back'):
                    G.corbels(mb, F, L, H - 0.16)
    stone_gable = style in ('stone', 'dark', 'plaster')
    extra = None
    if roof == 'side':
        rise = rng.uniform(0.62, 0.8) * d
        ndorm = 0 if w < 5.5 else (1 if w < 7 else 2)
        dorms = [0.0] if ndorm == 1 else ([-w * 0.24, w * 0.24] if ndorm == 2 else [])
        extra, ze, zr = side_roof(mb, x0, x1, fy0, y1, H + 0.14, rise, roofm, R() < lit_p, mk, dorms, upper_m, stone_gable)
    else:
        rise = rng.uniform(0.9, 1.15) * w
        y_roof0 = fy0 + (0.42 if roof == 'stepped' else 0.0)
        ze, zr = G.steep_roof(mb, x0, x1, y_roof0, y1, H + 0.14, rise, roofm)
        if roof == 'stepped':
            # empena escalonada (frente) — a parede sobe em degraus acima do telhado
            n = max(4, int(rise / 0.8))
            sh = (rise + 0.5) / n
            sw = (w / 2) / (n + 0.3)
            for i in range(n):
                hw = w / 2 - i * sw
                mb.box((-hw, fy0, H + i * sh), (hw, fy0 + 0.42, H + (i + 1) * sh), upper_m if style != 'plaster' else 'stone_wall', sides='xXyYZ')
                top = H + (i + 1) * sh
                mb.box((-hw - 0.06, fy0 - 0.06, top - 0.02), (hw + 0.06, fy0 + 0.48, top + 0.1), 'trim')   # capa de pedra do degrau
            FW = G.Face((0, fy0, 0), (1, 0, 0), (0, -1, 0))
            if rise > 2.6:
                if gothic_win: gwin(mb, FW, 0, H + 0.4, 0.8, rise * 0.3, R() < lit_p, mk, k=1.0)
                else: rect_window(mb, FW, 0, H + 0.45, 0.8, min(1.2, rise * 0.3), R() < lit_p, mk, frame='trim')
            G.gable(mb, x0, x1, y1, H + 0.14, rise, 1, False, mk, win=False)
        elif stone_gable:
            G.gable(mb, x0, x1, fy0, H + 0.14, rise, -1, R() < lit_p, mk)
            G.gable(mb, x0, x1, y1, H + 0.14, rise, 1, False, mk, win=False)
        else:
            for yy, outward in ((fy0, -1), (y1, 1)):
                tri = [(x0, yy, H + 0.14), (x1, yy, H + 0.14), (0, yy, H + 0.14 + rise)]
                if outward > 0: tri = [tri[1], tri[0], tri[2]]
                mb.poly(tri, 'plaster')
                off = outward * 0.03
                mb.beam((0, yy + off, H + 0.2), (0, yy + off, H + rise - 0.1), 0.16, 'timber', 0.1)
                mb.beam((x0 + 0.3, yy + off, H + 0.6), (x1 - 0.3, yy + off, H + 0.6), 0.16, 'timber', 0.1)
                if G.DETAIL:
                    mb.beam((x0 + 0.2, yy + off, H + 0.25), (0, yy + off, H + rise * 0.8), 0.13, 'timber', 0.08)
                    mb.beam((x1 - 0.2, yy + off, H + 0.25), (0, yy + off, H + rise * 0.8), 0.13, 'timber', 0.08)
                if outward < 0 and rise > 2.6:
                    FW = G.Face((0, yy, 0), (1, 0, 0), (0, -1, 0))
                    rect_window(mb, FW, 0, H + 0.95, 0.75, min(1.1, rise * 0.28), R() < lit_p, mk, frame='timber')
            if G.DETAIL and rise >= 4.4:
                for side in (-1, 1):
                    G.dormer(mb, x0, x1, ze, zr, 0.3, (fy0 + y1) / 2 + 0.6, side, R() < lit_p, mk)
    # chaminés
    nch = chimneys if chimneys is not None else rng.choice([1, 1, 2])
    for i in range(nch):
        if roof == 'side':
            cx = (x0 + 0.7) if i == 0 else (x1 - 0.7)
            cy = (fy0 + y1) / 2 + 0.6
        else:
            cx = (x1 - 0.9) if i == 0 else (x0 + 0.9)
            cy = y1 - 1.4 - i * 1.5
        top = zr + 0.4
        mb.box((cx - 0.36, cy - 0.36, H - 0.4), (cx + 0.36, cy + 0.36, top), 'stone_wall' if style != 'dark' else 'stone_dark')
        mb.box((cx - 0.46, cy - 0.46, top), (cx + 0.46, cy + 0.46, top + 0.15), 'trim')
        if G.DETAIL:
            mb.box((cx - 0.12, cy - 0.12, top + 0.15), (cx + 0.12, cy + 0.12, top + 0.45), 'trim')
    if G.DETAIL and style in ('stone', 'dark'):
        # gárgulas nas quinas da cornija (canos de pedra com a boca para a rua)
        for gx, gy, dx, dy in ((x0, fy0, -0.7, -0.9), (x1, fy0, 0.7, -0.9), (x0, y1, -0.7, 0.9), (x1, y1, 0.7, 0.9)):
            mb.beam((gx, gy, H - 0.2), (gx + dx, gy + dy, H - 0.05), 0.22, 'trim', 0.28)
            mb.box((gx + dx - 0.14, gy + dy - 0.14, H - 0.25), (gx + dx + 0.14, gy + dy + 0.14, H + 0.12), 'trim')
    if turret:
        tx = x0 if rng.random() < 0.5 else x1
        G.turret(mb, tx, fy0, 0.6 + floors[0] + 0.4, H + 0.6, r=0.9, spire=(zr - H) * 0.95 + 1.2, mk=mk)
    obj = mb.finish(G.MATS)
    if extra is not None:
        eo = extra.finish(G.MATS)
        bpy.ops.object.select_all(action='DESELECT')
        eo.select_set(True); obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.join()
        obj = bpy.context.view_layer.objects.active
    for n, p, o in mk:
        G.marker(n if not n.startswith('WIN') else n + '_%d' % G.uid(), p, o, obj)
    return obj


def guild_hall(name):
    """Casa da guilda dos sineiros: fachada larga de pedra, térreo em arcada, empena escalonada."""
    rng = random.Random(501)
    o = build_house(name, 12.0, 10.0, [3.8, 3.3, 3.1], 'stone', 'stepped', 'roof_slate', False, False, True, True,
                    False, False, True, 'ashlar', 'ashlar', 0.0, rng, 0.9, chimneys=2)
    return o


def corner_tower(name):
    """Casa-torre de esquina (5 andares) com torreão e telhado de pavilhão."""
    rng = random.Random(777)
    return build_house(name, 6.2, 6.2, [3.5, 3.0, 3.0, 2.9, 2.8], 'dark', 'front', 'roof_slate', False, True, False, True,
                       False, False, True, 'stone_dark', 'stone_dark', 0.0, rng, 0.75, chimneys=1)


def figure(mb, x, y, z, h=1.9, m='trim'):
    """Estátua de manto e capuz (silhueta torneada, barata) — santos do portal, sineiros dos nichos."""
    k = h / 2.0
    prof = [(0.0, 0), (0.34, 0), (0.37, 0.35), (0.31, 1.1), (0.24, 1.42), (0.15, 1.56), (0.19, 1.7), (0.16, 1.88), (0.0, 2.0)]
    mb.lathe([(r * k, z + zz * k) for r, zz in prof], m, segs=8, center=(x, y, 0))


def statue_niche(mb, F, u, z, w=0.9, h=2.4, m='trim'):
    G.opening(mb, F, u, z, w, h * 0.7, 'dark', frame='trim', t=0.14, depth=0.18, k=1.0, sill=True)
    if G.DETAIL:
        p = F.P(u, 0, 0.18)
        figure(mb, p.x, p.y, z, h * 0.78, m)


def cathedral(name):
    """Catedral dos Sinos: fachada com duas torres e flechas, portal com arquivoltas e santos, galeria de nichos,
    rosácea; nave alta com clerestório, naves laterais com telhado de meia-água, arcobotantes com pináculos,
    gárgulas e abside. Frente para −Y (a Rua Principal)."""
    mb = MeshBuilder(name)
    mk = []
    L = 28.0                    # (cabe no quarteirão sudoeste: fachada no Largo da Fonte, abside antes da muralha sul)
    y0, y1 = -L / 2, L / 2
    nw, X = 5.0, 10.0          # meia largura da nave / do conjunto (naves laterais de 5 m)
    Ha, Hn = 9.5, 21.0          # altura das naves laterais / da nave
    td = 6.5                    # fundo das torres
    yn = y0 + td                # a nave começa atrás das torres
    mb.box((-X - 0.4, y0 - 0.4, -1.5), (X + 0.4, y1 + 0.4, 0.6), 'trim', sides='xXyYZ')
    # naves laterais e nave
    mb.box((-X, yn, 0.6), (-nw, y1, Ha), 'ashlar', sides='xyY')
    mb.box((nw, yn, 0.6), (X, y1, Ha), 'ashlar', sides='XyY')
    mb.box((-nw, yn, 0.6), (nw, y1, Hn), 'ashlar', sides='xXY')
    G.band(mb, -X, X, yn, y1, Ha, h=0.35, out=0.2)
    G.band(mb, -nw, nw, yn, y1, Hn, h=0.4, out=0.25)
    for sgn in (-1, 1):
        q = [(sgn * nw, yn, Ha + 3.6), (sgn * (X + 0.5), yn, Ha - 0.1), (sgn * (X + 0.5), y1 + 0.3, Ha - 0.1), (sgn * nw, y1 + 0.3, Ha + 3.6)]
        mb.poly(q if sgn > 0 else list(reversed(q)), 'roof_slate')
        mb.poly(list(reversed(q)) if sgn > 0 else q, 'dark')
    G.steep_roof(mb, -nw, nw, yn, y1, Hn + 0.15, 9.0, 'roof_slate')
    nb = 5
    bay = (y1 - yn) / nb
    for i in range(nb):
        yb = yn + bay * (i + 0.5)
        for sgn in (-1, 1):
            Fa = G.Face((sgn * X, yb, 0), (0, -sgn, 0), (sgn, 0, 0))
            G.window(mb, Fa, 0, 2.4, 1.7, 3.6, True, mk, k=1.25)
            Fn = G.Face((sgn * nw, yb, 0), (0, -sgn, 0), (sgn, 0, 0))
            G.window(mb, Fn, 0, Ha + 4.4, 1.6, 3.5, True, mk, k=1.3)
    for i in range(nb + 1):
        yk = yn + bay * i
        for sgn in (-1, 1):
            xo = sgn * (X + 1.7)
            xa, xb = sorted((sgn * X, xo))
            mb.box((xa, yk - 0.5, 0.0), (xb, yk + 0.5, Ha + 4.2), 'trim', sides='xXyYZ')
            G.pinnacle(mb, sgn * (X + 0.85), yk, Ha + 4.2, 0.7, 3.4)
            # arcobotante: do pilar até a parede da nave por cima do telhado da nave lateral
            mb.beam((sgn * (X + 0.6), yk, Ha + 3.2), (sgn * nw, yk, Hn - 2.6), 0.5, 'trim', 0.75)
            if G.DETAIL:
                mb.beam((sgn * (X + 0.6), yk, Ha + 4.0), (sgn * nw, yk, Hn - 1.7), 0.3, 'trim', 0.3)
                xa2, xb2 = sorted((sgn * nw, sgn * (nw + 0.55)))
                mb.box((xa2, yk - 0.35, Ha + 3.0), (xb2, yk + 0.35, Hn + 0.4), 'trim', sides='xXyYZ')
                G.pinnacle(mb, sgn * (nw + 0.28), yk, Hn + 0.4, 0.45, 2.4)
                # gárgula (cano de pedra saindo da cornija)
                mb.beam((sgn * nw, yk + bay * 0.5, Hn - 0.25), (sgn * (nw + 1.3), yk + bay * 0.5, Hn - 0.05), 0.24, 'trim', 0.3)
    # abside (cabeceira poligonal)
    mb.cylinder((0, y1, 0.6), nw, Hn - 0.6, 'ashlar', segs=10, cap_top=False)
    mb.cone((0, y1, Hn + 0.15), nw + 0.45, 7.5, 'roof_slate', segs=10)
    if G.DETAIL:
        for k in range(5):
            ang = math.pi * (0.1 + 0.8 * k / 4)          # meia volta de trás (y > y1)
            n = V((math.cos(ang), math.sin(ang), 0))
            Fp = G.Face(V((0, y1, 0)) + n * nw * 0.95, V((-n.y, n.x, 0)), n)
            G.window(mb, Fp, 0, 6.0, 1.3, 6.5, True, mk, k=1.3, mullion=False)
    # fachada oeste: torres
    tw = 6.0
    for sgn in (-1, 1):
        xa, xb = sorted((sgn * (X - tw), sgn * X))
        cx = (xa + xb) / 2
        H = 36.0
        mb.box((xa, y0, 0.6), (xb, y0 + td, H), 'ashlar', sides='xXyY')
        for zz in (10.0, 21.5, 30.0):
            G.band(mb, xa, xb, y0, y0 + td, zz, h=0.3, out=0.15)
        for (bx, by, o) in ((xa, y0, (0, -1)), (xb, y0, (0, -1)), (sgn * X, y0 + td * 0.5, (sgn, 0))):
            G.buttress(mb, bx + (0.4 if bx == xa else -0.4) * (o[0] == 0), by, o, 21.0, w=0.9, depth=1.2)
        F = G.Face((cx, y0, 0), (1, 0, 0), (0, -1, 0))
        G.window(mb, F, 0, 12.0, 1.6, 4.5, True, mk, k=1.25)
        G.door(mb, F, 0, w=1.6, h=2.8, mk=mk)
        for key, Ft in (('f', F), ('s', G.Face((sgn * X, y0 + td / 2, 0), (0, -sgn, 0), (sgn, 0, 0)))):
            G.window(mb, Ft, -1.1, 22.8, 1.2, 4.6, False, mk, k=1.2)
            G.window(mb, Ft, 1.1, 22.8, 1.2, 4.6, False, mk, k=1.2)
        G.band(mb, xa, xb, y0, y0 + td, H, h=0.5, out=0.35)
        for px in (xa - 0.1, xb + 0.1):
            for py in (y0 - 0.1, y0 + td + 0.1):
                G.pinnacle(mb, px, py, H + 0.25, 0.6, 4.2)
        # flecha octogonal com lucarnas
        mb.cylinder((cx, y0 + td / 2, H + 0.25), 2.6, 3.0, 'ashlar', segs=8, cap_top=False)
        mb.cone((cx, y0 + td / 2, H + 3.25), 2.9, 20.0, 'roof_slate', segs=8)
        mb.beam((cx, y0 + td / 2, H + 23.2), (cx, y0 + td / 2, H + 25.5), 0.12, 'iron')
        mb.beam((cx - 0.6, y0 + td / 2, H + 24.6), (cx + 0.6, y0 + td / 2, H + 24.6), 0.1, 'iron')
    # corpo central: portal, galeria de nichos, rosácea e empena
    xa, xb = -(X - tw), (X - tw)
    mb.box((xa, y0, 0.6), (xb, yn, Hn), 'ashlar', sides='y')
    F = G.Face((0, y0, 0), (1, 0, 0), (0, -1, 0))
    G.door(mb, F, 0, w=2.8, h=4.6, mk=mk)
    if G.DETAIL:
        for sgn in (-1, 1):   # santos nos umbrais do portal
            for k in range(2):
                u = sgn * (2.25 + 0.55 * k)
                p = F.P(u, 0, 0.62 + 0.1 * k)
                mb.box((p.x - 0.28, p.y - 0.25, 1.15), (p.x + 0.28, p.y + 0.25, 1.4), 'trim')
                figure(mb, p.x, p.y, 1.4, 2.0)
    for k in range(5):   # galeria dos sineiros
        statue_niche(mb, F, -2.8 + 1.4 * k, 8.4, 0.95, 2.5)
    G.band(mb, xa, xb, y0, y0 + 0.3, 8.1, h=0.25, out=0.2)
    G.band(mb, xa, xb, y0, y0 + 0.3, 11.2, h=0.25, out=0.2)
    G.rose(mb, F, 0, 15.0, 2.6, True, mk)
    G.gable(mb, xa, xb, y0, Hn, 5.5, -1, True, mk, win=False)
    obj = mb.finish(G.MATS)
    for n, p, o in mk:
        G.marker(n if not n.startswith('WIN') else n + '_%d' % G.uid(), p, o, obj)
    return obj


def street_arch(name, W, passage=False):
    """Arco ogival de pedra cruzando a rua (vão W, vencendo de fachada a fachada no eixo X); com passage, um
    passadiço coberto de enxaimel por cima, ligando as casas dos dois lados."""
    mb = MeshBuilder(name)
    mk = []
    th = 1.1 if not passage else 1.6
    zs = 3.8
    k = 0.85
    leg = 0.7
    inner = G.arch_pts(-W / 2, W / 2, zs, k, segs=8)
    ztop = zs + G.arch_rise(W, k) + 0.7
    xa, xb = -W / 2 - leg, W / 2 + leg
    for yy, outward in ((-th / 2, -1), (th / 2, 1)):
        def P(x, z): return (x, yy, z)
        # pernas
        for (x0, x1) in ((xa, -W / 2), (W / 2, xb)):
            q = [P(x0, -0.3), P(x1, -0.3), P(x1, zs), P(x0, zs)]
            mb.poly(q if outward < 0 else list(reversed(q)), 'ashlar')
        # tímpano: faixas verticais do arco até o topo
        pts = [(xa, zs)] + [(-W / 2, zs)] + inner[1:-1] + [(W / 2, zs)] + [(xb, zs)]
        for i in range(len(pts) - 1):
            (u0, z0), (u1, z1) = pts[i], pts[i + 1]
            q = [P(u0, z0), P(u1, z1), P(u1, ztop), P(u0, ztop)]
            mb.poly(q if outward < 0 else list(reversed(q)), 'ashlar')
    # intradorso (face de baixo do arco) e laterais
    for i in range(len(inner) - 1):
        (u0, z0), (u1, z1) = inner[i], inner[i + 1]
        mb.poly([(u1, -th / 2, z1), (u0, -th / 2, z0), (u0, th / 2, z0), (u1, th / 2, z1)], 'ashlar')
    mb.box((xa, -th / 2, -0.3), (-W / 2, th / 2, zs), 'ashlar', sides='X')
    mb.box((W / 2, -th / 2, -0.3), (xb, th / 2, zs), 'ashlar', sides='x')
    # moldura do arco (aduelas salientes) e cornija
    if G.DETAIL:
        outer = G.arch_pts(-W / 2 - 0.25, W / 2 + 0.25, zs, (k * W + 0.25) / (W + 0.5), segs=8)
        for yy in (-th / 2 - 0.08, th / 2 + 0.08):
            for i in range(len(outer) - 1):
                (u0, z0), (u1, z1) = outer[i], outer[i + 1]
                mb.beam((u0, yy, z0), (u1, yy, z1), 0.16, 'trim', 0.3)
        for x in (xa + leg / 2, xb - leg / 2):
            for yy in (-th / 2, th / 2):
                mb.box((x - leg / 2 - 0.05, yy - 0.12, zs - 0.25), (x + leg / 2 + 0.05, yy + 0.12, zs), 'trim')
    mb.box((xa - 0.1, -th / 2 - 0.15, ztop), (xb + 0.1, th / 2 + 0.15, ztop + 0.3), 'trim')
    extra = None
    if passage:
        # passadiço: sala de enxaimel com janelas dos dois lados e telhado de duas águas (cumeeira no eixo X)
        d = 3.4
        zf = ztop + 0.3
        mb.box((xa, -d / 2, zf), (xb, d / 2, zf + 2.9), 'plaster', sides='yY')
        mb.box((xa - 0.05, -d / 2 - 0.1, zf - 0.25), (xb + 0.05, d / 2 + 0.1, zf), 'timber', sides='xXyYz')
        for yy, n in ((-d / 2, (0, -1, 0)), (d / 2, (0, 1, 0))):
            F = G.Face((0, yy, 0), (1 if n[1] < 0 else -1, 0, 0), n)
            L = xb - xa
            us = bays(L, max(2, int(L / 1.8)))
            timber_frame(mb, F, L, zf, zf + 2.9, [-L / 2 + 0.08, L / 2 - 0.08] + [(us[i] + us[i + 1]) / 2 for i in range(len(us) - 1)])
            for u in us:
                rect_window(mb, F, u, zf + 0.85, 0.8, 1.3, random.random() < 0.7, mk, shutters=False, frame='timber')
        extra, ze, zr = side_roof(mb, xa, xb, -d / 2, d / 2, zf + 2.95, 2.4, 'roof_slate', False, mk, [], 'plaster', False)
    obj = mb.finish(G.MATS)
    if extra is not None:
        eo = extra.finish(G.MATS)
        bpy.ops.object.select_all(action='DESELECT')
        eo.select_set(True); obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.join()
        obj = bpy.context.view_layer.objects.active
    for n, p, o in mk:
        G.marker(n if not n.startswith('WIN') else n + '_%d' % G.uid(), p, o, obj)
    return obj


def banner_line(name, W, seed=5):
    """Corda atravessando a rua (de fachada a fachada, eixo X) com estandartes rasgados pendurados."""
    rng = random.Random(seed)
    mb = MeshBuilder(name)
    z0 = 6.6
    n = 12
    pts = [(-W / 2 - 0.3 + (W + 0.6) * i / n, 0.0, z0 - 0.55 * math.sin(math.pi * i / n)) for i in range(n + 1)]
    for i in range(n):
        mb.beam(pts[i], pts[i + 1], 0.035, 'iron', 0.035)
    for x in (-W / 2 - 0.3, W / 2 + 0.3):   # argolas presas na parede
        mb.beam((x, 0, z0 - 0.15), (x, 0, z0 + 0.15), 0.08, 'iron')
    k = 3 if W < 7 else 4
    for j in range(k):
        t = (j + 0.5) / k
        x = -W / 2 + W * t
        ztop = z0 - 0.55 * math.sin(math.pi * (x + W / 2 + 0.3) / (W + 0.6)) - 0.05
        w = rng.uniform(0.7, 1.0); h = rng.uniform(1.4, 2.1)
        m = rng.choice(['cloth_red', 'cloth_blue', 'banner'])
        # borda de baixo rasgada (zigue-zague irregular)
        bot = []
        nz = 5
        for i in range(nz + 1):
            u = x - w / 2 + w * i / nz
            bot.append((u, ztop - h + (rng.uniform(0.0, 0.45) if 0 < i < nz else rng.uniform(0.0, 0.2))))
        poly = [(x - w / 2, ztop), (x + w / 2, ztop)] + list(reversed(bot))
        for off, rev in ((0.01, False), (-0.01, True)):
            pp = [(px, off, pz) for px, pz in poly]
            f = mb.poly(list(reversed(pp)) if rev else pp, m)
            if m == 'banner':
                for loop in f.loops:
                    co = loop.vert.co
                    loop[mb.uv].uv = ((co.x - (x - w / 2)) / w if not rev else 1 - (co.x - (x - w / 2)) / w, (co.z - (ztop - h)) / h)
        mb.beam((x - w / 2 - 0.08, 0, ztop + 0.02), (x + w / 2 + 0.08, 0, ztop + 0.02), 0.04, 'timber', 0.04)
    obj = mb.finish(G.MATS)
    return obj


def cage(name):
    """Gaiola de ferro pendurada num braço de parede (a frente −Y encosta na fachada? não: o braço sai de +Y)."""
    mb = MeshBuilder(name)
    mk = []
    # braço de ferro preso na parede (parede em y = 0, braço para −Y)
    mb.box((-0.18, -0.08, 5.4), (0.18, 0.0, 6.4), 'iron')
    mb.beam((0, -0.05, 6.2), (0, -1.6, 6.2), 0.09, 'iron')
    mb.beam((0, -0.05, 5.5), (0, -1.2, 6.15), 0.06, 'iron')
    cy = -1.5
    zt, zb = 5.0, 3.3
    mb.beam((0, cy, 6.2), (0, cy, zt + 0.35), 0.035, 'iron')    # corrente
    n = 10
    r = 0.42
    for i in range(n):
        a = math.tau * i / n
        mb.beam((math.cos(a) * r, cy + math.sin(a) * r, zb), (math.cos(a) * r, cy + math.sin(a) * r, zt), 0.035, 'iron', 0.035)
    for z in (zb, (zb + zt) / 2, zt):
        for i in range(n):
            a0, a1 = math.tau * i / n, math.tau * (i + 1) / n
            mb.beam((math.cos(a0) * r, cy + math.sin(a0) * r, z), (math.cos(a1) * r, cy + math.sin(a1) * r, z), 0.04, 'iron', 0.04)
    mb.cone((0, cy, zt), r + 0.03, 0.35, 'iron', segs=10)
    mb.cylinder((0, cy, zb - 0.05), r + 0.02, 0.06, 'iron', segs=10, cap_bot=True)
    obj = mb.finish(G.MATS)
    return obj


# ------------------------------------------------------------------ oclusão assada (atlas da cidade)

def bake_town(o, name, far, samples=32, dist=2.4):
    i = TOWN_NAMES.index(name.replace('_LOD1', ''))
    res = 384 if far else 768
    os.makedirs(AO_DIR, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = samples
    if sc.world is None: sc.world = bpy.data.worlds.new('W')
    sc.world.light_settings.distance = dist
    me = o.data
    uv_ao = me.uv_layers.new(name='AO'); me.uv_layers.active = uv_ao
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004, area_weight=0.0)
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, 0.0))
    ground = bpy.context.object
    img = bpy.data.images.new('AO_' + name, res, res, alpha=False, float_buffer=False)
    img.generated_color = (1, 1, 1, 1)
    nodes = []
    for slot in o.material_slots:
        m = slot.material
        if m is None: continue
        m.use_nodes = True
        nt = m.node_tree
        n = nt.nodes.new('ShaderNodeTexImage'); n.image = img
        nt.nodes.active = n
        nodes.append((nt, n))
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.bake(type='AO', margin=10, use_clear=False)
    path = os.path.join(AO_DIR, name + '.png')
    img.filepath_raw = path; img.file_format = 'PNG'; img.save()
    for nt, n in nodes: nt.nodes.remove(n)
    bpy.data.objects.remove(ground, do_unlink=True)
    tx, ty = i % TOWN_GRID, i // TOWN_GRID
    ts = 1.0 / TOWN_GRID
    base = 8.0 if far else 6.0
    uv_ao = o.data.uv_layers['AO']
    for loop in uv_ao.data:
        u, v = loop.uv
        loop.uv = (base + (tx + 0.015 + u * 0.97) * ts, (ty + 0.015 + v * 0.97) * ts)
    o.data.uv_layers.active = o.data.uv_layers[0]
    print('AO', name, path)


def build_atlas():
    import subprocess
    for far, size, fname in ((False, 4096, 'ao_atlas_town.png'), (True, 2048, 'ao_atlas_town_far.png')):
        tile = size // TOWN_GRID
        args = ['convert', '-size', '%dx%d' % (size, size), 'xc:white']
        for i, n in enumerate(TOWN_NAMES):
            p = os.path.join(AO_DIR, n + ('_LOD1' if far else '') + '.png')
            if not os.path.exists(p): continue
            tx, ty = i % TOWN_GRID, i // TOWN_GRID
            x, y = tx * tile, size - (ty + 1) * tile
            args += ['(', p, '-resize', '%dx%d!' % (tile, tile), '-blur', '0x0.8', ')', '-geometry', '+%d+%d' % (x, y), '-composite']
        out = os.path.join(ROOT, '..', '..', 'Assets', 'Campanula', 'Textures', fname)
        args += ['-colorspace', 'gray', '-depth', '8', out]
        subprocess.run(args, check=True)
        print('ATLAS', out)


def main():
    builds = [(n, (lambda s: (lambda nm: thouse(nm, s)))(i)) for i, n in enumerate(TOWN_NAMES[:N_HOUSES])]
    builds += [('TGuild', guild_hall), ('TCornerTower', corner_tower), ('TCatedral', cathedral),
               ('TArch_5', lambda n: street_arch(n, 4.6)), ('TArch_8', lambda n: street_arch(n, 7.6)),
               ('TPassage_5', lambda n: street_arch(n, 4.6, True)), ('TPassage_6', lambda n: street_arch(n, 5.6, True)),
               ('TCage', cage), ('TBanners_5', lambda n: banner_line(n, 5.0, 5)), ('TBanners_8', lambda n: banner_line(n, 8.0, 9)),
               ('TBanners_6', lambda n: banner_line(n, 6.0, 13))]
    only = None
    if '--' in sys.argv:
        rest = [a for a in sys.argv[sys.argv.index('--') + 1:] if not a.startswith('--')]
        if rest: only = rest[0].split(',')
    total = 0
    for name, fn in builds:
        if only and name not in only:
            continue
        for lod in (0, 1):
            G.DETAIL = lod == 0
            clear_scene(); G.MATS = G.mats()
            o = fn(name)
            out = name if lod == 0 else name + '_LOD1'
            o.name = out
            if BAKE_AO:
                bake_town(o, out, lod == 1)
            total += export([o] + list(o.children), out)
        G.DETAIL = True
    print('TOTAL TRIS', total)
    if BAKE_AO:
        build_atlas()


if __name__ == '__main__':
    main()
