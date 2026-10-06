"""Rochas de Campânula (kit da natureza): penedos, afloramentos, lajes, pedras soltas, cascalho e entulho
de alvenaria, ESCULPIDOS por código e assados como num pipeline de fotogrametria.

Por peça:
1. ALTA (40–160 mil triângulos): icosfera densa levada a uma forma de rocha de verdade —
   elipsoide ∩ planos de fratura com mínimo suave (faces planas de quebra com arestas gastas), estratos
   com ressaltos e sulcos (afloramentos sedimentares), juntas verticais de Voronoi (blocos), fendas finas,
   fissura de rachado e ruído fractal com cristas.
2. BAIXA (jogo): a alta enterrada cortada e dizimada (0,5–3 mil triângulos); <nome>_LOD1 = a baixa
   dizimada de novo, com o mesmo UV.
3. UV único da baixa num ladrilho do atlas; o Cycles ASSA da alta para a baixa: normal (espaço tangente),
   oclusão de ambiente com o chão (a base escurece) e uma máscara (arestas pela "pointiness", fendas,
   tom de cada estrato).
4. O shader Campanula/NatureRock põe a foto do Poly Haven (lichen_rock, mossy_rock) em projeção
   triplanar no mundo por cima do relevo assado: sem costuras e na escala real.

Atlas: nature_a (afloramentos e penedos) e nature_b (lajes, pedras, cascalho, seixos, entulho), 2048 px,
em Assets/Campanula/Textures (nature_<a|b>_normal.png, nature_<a|b>_mask.png: R oclusão, G arestas,
B fendas, A tom). Origem de cada modelo = linha do chão (z = 0); a parte de baixo já nasce enterrada.

Rodar: blender -b --factory-startup --python ArtSource/Campanula/scripts/kit_nature.py [-- Nome1,Nome2] [--preview]
(sem nomes = tudo; os ladrilhos assados ficam em ArtSource/Campanula/nature/ e o atlas é remontado com
os que existirem)
"""
import bpy, bmesh, math, random, os, sys
import numpy as np
from mathutils import Vector, Matrix, Euler, noise
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import clear_scene, export, ROOT

V = Vector
TEX_OUT = os.path.join(ROOT, '..', '..', 'Assets', 'Campanula', 'Textures')
WORK = os.path.join(ROOT, 'nature')
ATLAS = 2048
GRID = 4                 # ladrilhos de 512 px
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
ONLY = next((a.split(',') for a in ARGS if not a.startswith('--')), None)
PREVIEW = '--preview' in ARGS
NO_BAKE = '--no-bake' in ARGS


def smin(a, b, k):
    if k <= 0.0:
        return min(a, b)
    h = max(k - abs(a - b), 0.0) / k
    return min(a, b) - h * h * k * 0.25


def sstep(e0, e1, x):
    t = min(max((x - e0) / (e1 - e0), 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


def h01(i, seed):
    return random.Random(int(i) * 7919 + seed * 104729).random()


def fbm(p, octaves=4):
    s, a, f = 0.0, 1.0, 1.0
    for _ in range(octaves):
        s += noise.noise(p * f) * a
        a *= 0.5; f *= 2.03
    return s


# ------------------------------------------------------------------ escultura de uma pedra (alta)

def sculpt(P):
    """bmesh da pedra em alta, centrada na origem, + atributos por vértice (fenda, tom).
    P: radii (meio-eixos), planes, cut (faixa da distância dos planos em fração do suporte), k (arredondamento),
    plane_z (achata a distribuição dos planos), bottom (plano de baixo, fração de c), lumpy, strata, joints,
    cracks, split, fbm (amplitude em fração do tamanho), freq, subdiv, seed."""
    rnd = random.Random(P['seed'])
    a, b, c = P['radii']
    size = (a + b + c) / 3.0
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=P.get('subdiv', 6), radius=1.0)
    planes = []
    for i in range(P.get('planes', 6)):
        n = V((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1) * P.get('plane_z', 1.0))).normalized()
        sup = math.sqrt((a * n.x) ** 2 + (b * n.y) ** 2 + (c * n.z) ** 2)
        planes.append((n, sup * rnd.uniform(*P.get('cut', (0.7, 0.92)))))
    for n, h in P.get('extra_planes', []):
        planes.append((V(n).normalized(), h))
    if P.get('bottom') is not None:
        planes.append((V((0, 0, -1)), c * P['bottom']))
    k = P.get('k', 0.12) * min(a, b, c)
    off = V((rnd.uniform(-90, 90), rnd.uniform(-90, 90), rnd.uniform(-90, 90)))
    off2 = V((rnd.uniform(-90, 90), rnd.uniform(-90, 90), rnd.uniform(-90, 90)))
    lumpy = P.get('lumpy', 0.1)
    # 1) forma: raio ao longo de cada direção (o centro está dentro: interseção radial é exata)
    for v in bm.verts:
        d = v.co.normalized()
        r = 1.0 / math.sqrt((d.x / a) ** 2 + (d.y / b) ** 2 + (d.z / c) ** 2)
        r *= 1.0 + lumpy * noise.noise(d * 1.2 + off) + lumpy * 0.4 * noise.noise(d * 2.6 + off2)
        for n, h in planes:
            dn = d.dot(n)
            if dn > 1e-4:
                r = smin(r, h / dn, k)
        v.co = d * r
    bm.normal_update()
    nv = len(bm.verts)
    crack = [0.0] * nv
    tint = [0.5] * nv
    bm.verts.ensure_lookup_table()
    # 2) estratos: camadas com recuo próprio, sulco na junta e lábio saliente embaixo de cada camada
    S = P.get('strata')
    if S:
        bn = V(S['normal']).normalized()
        disp = [0.0] * nv
        for v in bm.verts:
            p = v.co
            t = p.dot(bn) / S['thick'] + noise.noise(p * 0.28 + off) * S.get('wobble', 0.4)
            L = math.floor(t); f = t - L
            side = 1.0 - abs(v.normal.dot(bn)) ** 2
            g = 1.0 - sstep(0.0, S.get('gw', 0.16), f)
            inset = (h01(L, P['seed']) ** 1.5) * S['inset']
            lip = S.get('lip', 0.0) * sstep(0.55, 1.0, f)
            disp[v.index] = -(inset + g * S['groove'] - lip) * side
            crack[v.index] = max(crack[v.index], g * side)
            tint[v.index] = h01(L, P['seed'] + 1)
        for v in bm.verts:
            v.co += v.normal * disp[v.index]
        bm.normal_update()
    # 3) juntas (blocos) e fendas finas: Voronoi F2−F1 perto de 0 = linha da junta
    for key in ('joints', 'cracks'):
        J = P.get(key)
        if not J:
            continue
        cell = J['cell']; st = J.get('stretch', 1.0)
        o3 = off * (1.7 if key == 'cracks' else 1.0)
        disp = [0.0] * nv
        for v in bm.verts:
            p = v.co
            q = V((p.x / cell, p.y / cell, p.z / (cell * st))) + o3
            q += V((noise.noise(q * 0.7), noise.noise(q * 0.7 + V((5, 1, 3))), 0)) * J.get('warp', 0.25)
            ds, pts = noise.voronoi(q, distance_metric='DISTANCE', exponent=2.5)
            e = ds[1] - ds[0]
            w = 1.0 - sstep(0.0, J['width'], e)
            w = w * w
            # a junta só abre onde ela "atravessa" a superfície com força (some em alguns trechos)
            w *= sstep(-0.3, 0.3, noise.noise(q * 0.5 + V((3, 3, 3))) + J.get('coverage', 0.2))
            # cada bloco recua um tanto (degrau entre blocos vizinhos, como rocha diaclasada de verdade)
            c0 = pts[0]
            blk = h01(int(c0[0] * 131.0) * 73856093 ^ int(c0[1] * 131.0) * 19349663 ^ int(c0[2] * 131.0) * 83492791, P['seed']) ** 2
            disp[v.index] = -J['depth'] * w - J.get('block', 0.0) * blk
            crack[v.index] = max(crack[v.index], w)
        for v in bm.verts:
            v.co += v.normal * disp[v.index]
        bm.normal_update()
    # 4) fissura de rachado (a pedra partida ao meio)
    SP = P.get('split')
    if SP:
        sn = V(SP['normal']).normalized()
        for v in bm.verts:
            dd = v.co.dot(sn) - SP.get('offset', 0.0) + noise.noise(v.co * 1.5 + off) * SP['width'] * 0.8
            w = 1.0 - sstep(0.0, SP['width'], abs(dd))
            w *= sstep(-0.2, 0.4, v.co.z / c + 0.3)   # fecha embaixo
            v.co += v.normal * (-SP['depth'] * w * w)
            crack[v.index] = max(crack[v.index], w)
        bm.normal_update()
    # 5) superfície: ruído fractal + cristas (lascas) + pites
    amp = P.get('fbm', 0.03) * size
    fr = P.get('freq', 1.0) / size
    ridge = P.get('ridge', 0.35)
    disp = [0.0] * nv
    for v in bm.verts:
        q = v.co * fr * 2.0 + off
        n1 = fbm(q, 4)
        rr = 1.0 - abs(noise.noise(q * 1.9 + off2))
        n1 += (rr * rr - 0.5) * ridge
        pit = noise.noise(q * 9.0 + off2)
        n1 -= max(0.0, pit - 0.55) * 0.6
        disp[v.index] = n1 * amp
    for v in bm.verts:
        v.co += v.normal * disp[v.index]
    bm.normal_update()
    return bm, crack, tint


def wood(P):
    """Tronco (eixo X de 0 a length) em alta: casca sulcada ao longo do eixo, nós (calombo com cicatriz), rachas
    longitudinais, ponta partida em lascas (broken > 0) e topo serrado/gasto com anéis e rachas radiais; 'flare'
    alarga a base (toco). Atributo tint = 1 na madeira exposta (pontas): vira o tom de cerne no shader."""
    rnd = random.Random(P['seed'])
    L, r0 = P['length'], P['radius']
    na = P.get('around', 96)
    step = P.get('step', 0.022)
    off = V((rnd.uniform(-90, 90), rnd.uniform(-90, 90), rnd.uniform(-90, 90)))
    jag = P.get('broken', 0.0)
    taper = P.get('taper', 0.12)
    bend = P.get('bend', 0.0)
    flare = P.get('flare', 0.0)
    angles = [j / na * math.tau for j in range(na)]
    spl = []
    for a in angles:   # comprimento de cada lasca da ponta partida (0..1), com algumas lascas compridas
        v = noise.noise(V((math.cos(a) * 2.2, math.sin(a) * 2.2, 1.3)) + off) * 0.5 + 0.5
        v2 = max(0.0, noise.noise(V((math.cos(a) * 5.0, math.sin(a) * 5.0, 7.7)) + off))
        spl.append(min(1.0, v ** 1.6 + v2 * 0.9))
    knots = [(rnd.uniform(0.12, 0.88) * L, rnd.uniform(0, math.tau), rnd.uniform(0.035, 0.07)) for _ in range(P.get('knots', 3))]
    checks = [(rnd.uniform(0, math.tau), rnd.uniform(0.0, 0.3) * L, rnd.uniform(0.5, 0.95) * L) for _ in range(P.get('checks', 2))]
    bm = bmesh.new()
    crack, tint = [], []

    def bark_r(x, a, t):
        r = r0 * (1 - taper * t)
        if flare:   # base alargada em lobos (contrafortes das raízes que entram na terra)
            lobes = 0.55 + 0.45 * max(0.0, math.cos(P.get('lobes', 5) * a + noise.noise(V((a, 0.3, 0.7)) + off) * 1.5)) ** 2
            r *= 1 + flare * lobes * math.exp(-x / (0.24 * r0 / 0.35))
        q = V((math.cos(a) * 9.0, math.sin(a) * 9.0, x * 1.1)) + off
        fur = 1.0 - abs(noise.noise(q)); fur2 = 1.0 - abs(noise.noise(q * 2.1 + off))
        r += ((fur * fur - 0.5) * 0.7 + (fur2 * fur2 - 0.5) * 0.3) * P.get('bark', 0.014)
        c = 0.0
        for kx, ka, ks in knots:
            da = math.atan2(math.sin(a - ka), math.cos(a - ka)) * r0
            d = math.hypot(x - kx, da)
            r += ks * 0.55 * math.exp(-(d / ks) ** 2) - ks * 0.45 * math.exp(-(d / (ks * 0.35)) ** 2)
        for ca, c0, c1 in checks:
            if c0 < x < c1:
                da = abs(math.atan2(math.sin(a - ca), math.cos(a - ca))) * r0
                w = (1 - sstep(0.0, 0.012, da)) * sstep(c0, c0 + 0.2, x) * (1 - sstep(c1 - 0.2, c1, x))
                r -= 0.02 * w; c = max(c, w)
        return r, c

    nr = max(8, int(L / step) + 1)
    rings = []
    for i in range(nr):
        t = i / (nr - 1)
        ring = []
        for j, a in enumerate(angles):
            Lj = L + jag * spl[j]
            x = t * Lj
            r, c = bark_r(min(x, L), a, t)
            if jag > 0:   # as lascas afinam até a ponta
                tip = sstep(L - 0.06, Lj + 1e-4, x)
                r *= 1 - 0.4 * tip * spl[j]
            cy = bend * math.sin(math.pi * min(x, L) / L)
            cz = -P.get('droop', 0.0) * (min(x, L) / L) ** 2   # raiz: mergulha na terra
            ring.append(bm.verts.new(V((x, cy + math.cos(a) * r, cz + math.sin(a) * r))))
            crack.append(c); tint.append(0.0)
        rings.append(ring)
    for i in range(nr - 1):
        for j in range(na):
            k = (j + 1) % na
            bm.faces.new((rings[i][j], rings[i][k], rings[i + 1][k], rings[i + 1][j]))
    # pontas: discos com anéis gastos, rachas radiais e um miolo podre
    def cap(ring, x_end, sign, sawn):
        nk = P.get('cap_rings', 14)
        prev = ring
        cy = bend * math.sin(math.pi * min(x_end, L) / L)
        for k in range(1, nk + 1):
            f = 1 - k / nk
            cur = []
            for j, a in enumerate(angles):
                base = prev[j].co if k == 1 else None
                rr = (ring[j].co - V((ring[j].co.x, cy, 0))).length * f
                x = x_end
                if sawn:
                    x += sign * (-0.006 * math.sin(f * 42.0) - 0.02 * (1 - sstep(0.0, 0.25, f)))   # anéis e o miolo
                    rc = abs(math.sin(a * 3.0 + noise.noise(V((f * 3, a, 0)) + off) * 1.2))
                    x += sign * -0.018 * (1 - sstep(0.0, 0.08, rc)) * sstep(0.15, 0.5, f)   # rachas radiais
                else:   # partido: cerne em coroa, mais alto no meio
                    sp2 = max(0.0, noise.noise(V((math.cos(a) * 4, math.sin(a) * 4, f * 5)) + off)) ** 0.6
                    x += sign * (jag * 0.85 * spl[j] * f ** 0.5 + jag * 0.35 * sp2 * (1 - f) + noise.noise(V((f * 6, a * 2, 1)) + off) * 0.03)
                cur.append(bm.verts.new(V((x, cy + math.cos(a) * rr, math.sin(a) * rr))))
                crack.append(0.0); tint.append(1.0)
            for j in range(na):
                kk = (j + 1) % na
                q = (prev[j], prev[kk], cur[kk], cur[j])
                bm.faces.new(q if sign > 0 else tuple(reversed(q)))
            prev = cur
        ctr = bm.verts.new(V((x_end + (sign * jag * 0.2 if not sawn else -sign * 0.02), cy, 0)))
        crack.append(0.0); tint.append(1.0)
        for j in range(na):
            kk = (j + 1) % na
            tri = (prev[j], prev[kk], ctr)
            bm.faces.new(tri if sign > 0 else tuple(reversed(tri)))
    cap(rings[0], 0.0, -1, True)
    cap(rings[-1], L, 1, jag <= 0)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    return bm, crack, tint


def place_bm(bm, rot=(0, 0, 0), loc=(0, 0, 0), scale=1.0):
    M = Matrix.Translation(V(loc)) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Scale(scale, 4)
    bmesh.ops.transform(bm, matrix=M, verts=bm.verts)


# ------------------------------------------------------------------ catálogo

def boulder(seed, radii, **kw):
    P = dict(seed=seed, radii=radii, planes=9, cut=(0.6, 0.9), k=0.09, bottom=0.6, lumpy=0.06, fbm=0.035, freq=1.0,
             cracks=dict(cell=0.8, width=0.09, depth=0.025, warp=0.3, coverage=-0.18), subdiv=6)
    P.update(kw)
    return P


def stone(seed, r, flat=0.7, angular=True, sub=4):
    a = r; b = r * random.Random(seed).uniform(0.7, 0.95); c = r * flat
    return dict(seed=seed, radii=(a, b, c), planes=7 if angular else 3, cut=(0.62, 0.86) if angular else (0.85, 1.0),
                k=0.08 if angular else 0.4, bottom=None, lumpy=0.08, fbm=0.025 if angular else 0.012, freq=1.2,
                ridge=0.3 if angular else 0.05, subdiv=sub)


def block(seed, half, chips=3, broken=False):
    """bloco de cantaria (ashlar) com arestas lascadas; broken = partido num plano inclinado."""
    rnd = random.Random(seed)
    hx, hy, hz = half
    ex = [((1, 0, 0), hx), ((-1, 0, 0), hx), ((0, 1, 0), hy), ((0, -1, 0), hy), ((0, 0, 1), hz), ((0, 0, -1), hz)]
    for _ in range(chips):   # lascas nos cantos
        n = V((rnd.choice((-1, 1)) * rnd.uniform(0.4, 1), rnd.choice((-1, 1)) * rnd.uniform(0.4, 1), rnd.choice((-1, 1)) * rnd.uniform(0.4, 1))).normalized()
        sup = abs(n.x) * hx + abs(n.y) * hy + abs(n.z) * hz
        ex.append((tuple(n), sup * rnd.uniform(0.82, 0.93)))
    if broken:
        n = V((1, rnd.uniform(-0.3, 0.3), rnd.uniform(-0.5, 0.5))).normalized()
        ex.append((tuple(n), hx * rnd.uniform(0.05, 0.35)))
    return dict(seed=seed, radii=(hx * 1.9, hy * 1.9, hz * 1.9), planes=0, extra_planes=ex, k=0.03, bottom=None,
                lumpy=0.0, fbm=0.012, freq=1.4, ridge=0.25, subdiv=5,
                cracks=dict(cell=0.25, width=0.09, depth=0.008, coverage=-0.1))


def cluster(seed, n, spread, rmin, rmax, flat=0.65, angular=True, bury=(0.25, 0.5), pebbles=0, sub=4):
    rnd = random.Random(seed)
    pieces, placed = [], []
    for i in range(n):
        r = rnd.uniform(rmin, rmax) * (1.0 if i else 1.25)
        for _ in range(40):
            ang = rnd.uniform(0, math.tau); dist = math.sqrt(rnd.random()) * spread * (0.25 if i == 0 else 1.0)
            x, y = math.cos(ang) * dist, math.sin(ang) * dist
            if all((x - px) ** 2 + (y - py) ** 2 > (r + pr) ** 2 * 0.55 for px, py, pr in placed):
                break
        placed.append((x, y, r))
        P = stone(seed * 31 + i, r, flat * rnd.uniform(0.8, 1.15), angular, sub)
        P['rot'] = (rnd.uniform(-0.35, 0.35), rnd.uniform(-0.35, 0.35), rnd.uniform(0, math.tau))
        P['loc'] = (x, y, -P['radii'][2] * rnd.uniform(*bury) * 2 + P['radii'][2])
        pieces.append(P)
    for i in range(pebbles):
        r = rnd.uniform(0.035, 0.08)
        ang = rnd.uniform(0, math.tau); dist = rnd.uniform(0.2, 1.1) * spread
        P = stone(seed * 57 + i, r, 0.6, False, 2)
        P['rot'] = (0, 0, rnd.uniform(0, math.tau)); P['loc'] = (math.cos(ang) * dist, math.sin(ang) * dist, r * 0.15)
        pieces.append(P)
    return pieces


def rubble(seed, blocks, chips, spread):
    rnd = random.Random(seed)
    pieces = []
    for i, (half, broken) in enumerate(blocks):
        P = block(seed * 13 + i, half, chips=rnd.randint(2, 4), broken=broken)
        tilt = (rnd.uniform(-0.25, 0.25), rnd.uniform(-0.2, 0.2), rnd.uniform(0, math.tau))
        if i == len(blocks) - 1 and len(blocks) > 2:   # o último cai por cima, inclinado
            tilt = (rnd.uniform(0.3, 0.55), rnd.uniform(-0.3, 0.3), rnd.uniform(0, math.tau))
            P['loc'] = (rnd.uniform(-0.2, 0.2) * spread, rnd.uniform(-0.2, 0.2) * spread, half[2] * 1.6)
        else:
            ang = i / max(1, len(blocks) - 1) * math.tau + rnd.uniform(-0.4, 0.4)
            P['loc'] = (math.cos(ang) * spread * rnd.uniform(0.35, 0.8), math.sin(ang) * spread * rnd.uniform(0.35, 0.8), half[2] * 0.75)
        P['rot'] = tilt
        pieces.append(P)
    pieces += cluster(seed + 5, chips, spread * 1.1, 0.06, 0.16, 0.6, True, (0.2, 0.45), sub=3)
    return pieces


def tor(seed):
    """afloramento de granito em lajes empilhadas (como os 'tors' das charnecas): blocos quase cúbicos com as
    arestas muito gastas pelo tempo, cada andar deslocado e girado, alguns partidos ao meio."""
    rnd = random.Random(seed)
    tiers = [((2.9, 2.2, 0.75), (0, 0, 0.35), 0.0, False),
             ((2.3, 1.8, 0.62), (0.3, -0.15, 1.72), 0.12, True),
             ((1.7, 1.3, 0.55), (-0.15, 0.2, 2.9), -0.18, False),
             ((1.1, 0.9, 0.45), (0.25, 0.05, 3.9), 0.3, False),
             ((1.5, 1.1, 0.6), (2.9, 1.6, 0.25), 0.6, False)]
    pieces = []
    for i, (half, loc, yaw, split) in enumerate(tiers):
        hx, hy, hz = half
        ex = [((1, 0, 0), hx), ((-1, 0, 0), hx), ((0, 1, 0), hy), ((0, -1, 0), hy), ((0, 0, 1), hz), ((0, 0, -1), hz)]
        for _ in range(3):
            n = V((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-0.4, 0.6))).normalized()
            sup = abs(n.x) * hx + abs(n.y) * hy + abs(n.z) * hz
            ex.append((tuple(n), sup * rnd.uniform(0.78, 0.9)))
        P = dict(seed=seed * 10 + i, radii=(hx * 1.35, hy * 1.35, hz * 1.6), planes=0, extra_planes=ex, k=0.55, bottom=None, lumpy=0.05,
                 fbm=0.03, freq=1.0, ridge=0.4, subdiv=7 if i < 2 else 6,
                 strata=dict(normal=(0.04, 0.02, 1), thick=hz * 0.7, inset=0.06, groove=0.05, gw=0.1, lip=0.02, wobble=0.2),
                 cracks=dict(cell=0.9, width=0.08, depth=0.03, coverage=-0.15))
        if split:
            P['split'] = dict(normal=(1, 0.15, 0), width=0.05, depth=0.25)
        P['loc'] = loc
        P['rot'] = (rnd.uniform(-0.05, 0.05), rnd.uniform(-0.05, 0.05), yaw)
        pieces.append(P)
    return pieces


def cliff(seed, radii, strata, **kw):
    """Paredão para o cânion: laje alta e larga com as costas planas (y = 0, entram na parede de terra) e a face
    para −Y (vira a frente +Z do contêiner no Unity) com estratos horizontais, ressaltos e juntas verticais."""
    a, b, c = radii
    P = boulder(seed, radii, planes=11, plane_z=0.4, cut=(0.62, 0.92), k=0.05, lumpy=0.04, bottom=None, fbm=0.02, strata=strata, subdiv=7,
                extra_planes=[((0, 1, 0), b * 0.3)], cracks=None, joints=dict(cell=2.2, stretch=4.0, width=0.07, depth=0.2, warp=0.3, coverage=0.05, block=0.25))
    P.update(kw)
    P['loc'] = (0, -b * 0.3, 0)
    return P


def stump(seed, r, h, broken):
    """Toco: tronco curto em pé, 0,25 m enterrado, topo partido em lascas (broken > 0) ou serrado; a base abre em
    lobos (os contrafortes das raízes entrando na terra)."""
    rnd = random.Random(seed)
    trunk = dict(kind='wood', seed=seed, length=h + 0.25, radius=r, broken=broken, taper=0.05, flare=1.3, lobes=rnd.randint(4, 6), knots=2, checks=2,
                 radii=(h * 0.5, r, r), loc=(0, 0, -0.25), rot=(0, -math.pi / 2, 0), around=96, step=0.018)
    pieces = [trunk]
    return pieces


STRATA_A = dict(normal=(0.16, 0.05, 1), thick=0.55, inset=0.36, groove=0.08, gw=0.12, lip=0.05, wobble=0.3)
STRATA_C = dict(normal=(0.0, 0.48, 1), thick=0.4, inset=0.26, groove=0.07, gw=0.14, lip=0.04, wobble=0.3)

# nome: (atlas, ladrilho (tx, ty, lado em ladrilhos), triângulos da baixa, peças, profundidade do corte embaixo)
ASSETS = {
    'Outcrop_A': ('a', (0, 0, 2), 3200, lambda: [dict(boulder(101, (4.6, 2.9, 2.6), planes=13, plane_z=0.35, cut=(0.58, 0.92), k=0.05, bottom=0.62,
                                                              lumpy=0.04, fbm=0.022, strata=STRATA_A, subdiv=7,
                                                              extra_planes=[((0.16, 0.05, 1), 2.6 * 0.72)], cracks=None,
                                                              joints=dict(cell=2.4, stretch=5.0, width=0.07, depth=0.2, warp=0.3, coverage=0.0, block=0.22)),
                                                    loc=(0, 0, 1.0))], 0.35),
    'Outcrop_B': ('a', (2, 0, 2), 3200, lambda: tor(202), 0.25),
    'Outcrop_C': ('a', (0, 2, 2), 3000, lambda: [dict(boulder(303, (6.2, 2.2, 2.1), planes=13, cut=(0.58, 0.9), k=0.05, bottom=0.6,
                                                              lumpy=0.04, fbm=0.02, strata=STRATA_C, subdiv=7, plane_z=0.4,
                                                              extra_planes=[((0.0, 0.48, 1), 2.1 * 0.8)], cracks=None,
                                                              joints=dict(cell=2.0, stretch=4.0, width=0.07, depth=0.16, warp=0.35, coverage=0.0, block=0.18)),
                                                    loc=(0, 0, 0.8))], 0.3),
    'Boulder_A': ('a', (2, 2, 1), 1300, lambda: [dict(boulder(11, (1.35, 1.0, 0.85), planes=8, k=0.16, subdiv=7, fbm=0.045), loc=(0, 0, 0.42))], 0.12),
    'Boulder_B': ('a', (3, 2, 1), 1200, lambda: [dict(boulder(23, (0.95, 0.8, 0.72), planes=13, cut=(0.55, 0.82), k=0.035, bottom=0.5, fbm=0.03,
                                                              ridge=0.5, lumpy=0.03), loc=(0, 0, 0.3))], 0.1),
    'Boulder_C': ('a', (2, 3, 1), 1400, lambda: [dict(boulder(37, (1.6, 1.15, 0.78), planes=5, k=0.2, bottom=0.55,
                                                              split=dict(normal=(1, 0.25, 0.1), width=0.07, depth=0.22), subdiv=7, fbm=0.045), loc=(0, 0, 0.36))], 0.12),
    'Boulder_D': ('a', (3, 3, 1), 700, lambda: [dict(boulder(41, (0.6, 0.5, 0.42), planes=4, k=0.3, bottom=0.55, subdiv=5), loc=(0, 0, 0.2))], 0.08),
    'Slab_A': ('b', (0, 0, 1), 600, lambda: [dict(boulder(51, (1.15, 0.78, 0.22), planes=6, plane_z=0.15, cut=(0.75, 0.95), k=0.25, bottom=0.6,
                                                         fbm=0.04, subdiv=6), loc=(0, 0, 0.04), rot=(0.04, -0.03, 0))], 0.06),
    'Slab_B': ('b', (1, 0, 1), 500, lambda: [dict(boulder(57, (0.82, 0.62, 0.2), planes=6, plane_z=0.15, cut=(0.7, 0.92), k=0.2, bottom=0.6,
                                                         fbm=0.04, subdiv=5, split=dict(normal=(0.3, 1, 0), width=0.03, depth=0.08)),
                                                    loc=(0, 0, 0.05), rot=(-0.05, 0.03, 0))], 0.05),
    'Stones_A': ('b', (2, 0, 1), 900, lambda: cluster(61, 9, 1.0, 0.13, 0.36), 0.03),
    'Stones_B': ('b', (3, 0, 1), 800, lambda: cluster(67, 6, 0.7, 0.1, 0.28, pebbles=10), 0.03),
    'Rubble_A': ('b', (0, 1, 1), 1000, lambda: rubble(71, [((0.34, 0.2, 0.16), False), ((0.3, 0.18, 0.15), True), ((0.36, 0.19, 0.17), False),
                                                            ((0.32, 0.18, 0.16), True)], 6, 0.75), 0.03),
    'Rubble_B': ('b', (1, 1, 1), 900, lambda: rubble(79, [((0.55, 0.16, 0.14), True), ((0.3, 0.2, 0.16), False), ((0.33, 0.18, 0.15), True)], 5, 0.7), 0.03),
    'Scree_A': ('b', (2, 1, 1), 1000, lambda: cluster(83, 20, 1.6, 0.1, 0.3, flat=0.45, bury=(0.3, 0.55)), 0.03),
    'Pebbles_A': ('b', (3, 1, 1), 800, lambda: cluster(89, 12, 1.1, 0.1, 0.3, flat=0.5, angular=False, bury=(0.25, 0.45), sub=4), 0.03),
    'Cliff_A': ('b', (0, 2, 2), 3000, lambda: [cliff(401, (5.2, 2.4, 4.4), dict(normal=(0.03, 0.06, 1), thick=0.48, inset=0.28, groove=0.08,
                                                                                    gw=0.18, lip=0.05, wobble=0.35))], ('y', 0.35)),
    'Cliff_B': ('b', (2, 2, 2), 3000, lambda: [cliff(409, (4.0, 2.0, 5.2), dict(normal=(-0.05, 0.04, 1), thick=0.6, inset=0.3, groove=0.09,
                                                                                    gw=0.16, lip=0.06, wobble=0.4))], ('y', 0.3)),
    # madeira (atlas c): o 6º campo diz como fazer o UV2 em metros (veio da casca ao longo do eixo)
    'Log_A': ('c', (0, 0, 2), 1400, lambda: [dict(kind='wood', seed=501, length=4.2, radius=0.27, broken=0.45, taper=0.14, bend=0.12,
                                                     knots=4, checks=2, radii=(2.1, 0.27, 0.27), loc=(-2.1, 0, 0.12), rot=(0.03, 0, 0))], 0.12,
              dict(axis='x', r=0.27, x0=-2.1, zc=0.12, bend=0.12, L=4.2)),
    'Log_B': ('c', (2, 0, 2), 1200, lambda: [dict(kind='wood', seed=517, length=2.7, radius=0.34, broken=0.0, taper=0.08, bend=0.05,
                                                     knots=3, checks=3, radii=(1.35, 0.34, 0.34), loc=(-1.35, 0, 0.18), rot=(-0.02, 0, 0))], 0.16,
              dict(axis='x', r=0.34, x0=-1.35, zc=0.18, bend=0.05, L=2.7)),
    'Stump_A': ('c', (0, 2, 1), 1100, lambda: stump(531, 0.36, 0.8, 0.3), 0.08, dict(axis='z', r=0.36)),
    'Stump_B': ('c', (1, 2, 1), 900, lambda: stump(547, 0.28, 0.55, 0.0), 0.08, dict(axis='z', r=0.28)),
}


# ------------------------------------------------------------------ malhas (alta, baixa, LOD1)

def to_object(bm, name, attrs=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    if attrs is not None:
        ca = me.color_attributes.new('nat', 'FLOAT_COLOR', 'POINT')
        crack, tint = attrs
        cols = np.zeros((len(me.vertices), 4), dtype=np.float32)
        cols[:, 0] = np.clip(crack, 0, 1); cols[:, 2] = tint; cols[:, 3] = 1
        ca.data.foreach_set('color', cols.ravel())
    for p in me.polygons:
        p.use_smooth = True
    return o


def activate(o):
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def decimate(o, ratio):
    activate(o)
    m = o.modifiers.new('dec', 'DECIMATE')
    m.decimate_type = 'COLLAPSE'; m.ratio = max(0.0005, min(1.0, ratio)); m.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=m.name)


def tri_count(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name; o.data.name = name
    return o


def build_meshes(name, pieces, target, cut):
    """alta (com atributos) e baixa juntas; a baixa de cada peça leva triângulos proporcionais à área."""
    his, los, areas = [], [], []
    for i, P in enumerate(pieces):
        bm, crack, tint = (wood if P.get('kind') == 'wood' else sculpt)(P)
        place_bm(bm, P.get('rot', (0, 0, 0)), P.get('loc', (0, 0, 0)))
        areas.append(sum(f.calc_area() for f in bm.faces))
        # retopologia automática: a baixa NÃO sai da escultura dizimada direto (os degraus de bloco e de estrato
        # viravam dobras e lascas — centenas de ilhas de UV e normal assado errado), e sim de um remesh por
        # voxels suavizado; o detalhe todo volta pelo normal assado
        piece = sum(P['radii']) / 3.0 * 2.0
        tmp = to_object(bm.copy(), '%s_rm_%d' % (name, i))
        if P.get('kind') != 'wood':   # (a madeira já é uma grade de tubo limpa: o remesh picava as raízes finas em cacos)
            rm = tmp.modifiers.new('rm', 'REMESH'); rm.mode = 'VOXEL'; rm.voxel_size = max(0.004, piece * 0.022); rm.use_smooth_shade = True
            smo = tmp.modifiers.new('sm', 'SMOOTH'); smo.factor = 0.6; smo.iterations = 3
            activate(tmp)
            bpy.ops.object.modifier_apply(modifier=rm.name)
            bpy.ops.object.modifier_apply(modifier=smo.name)
        tmp.name = '%s_lo_%d' % (name, i); tmp.data.name = tmp.name
        his.append(to_object(bm, '%s_hi_%d' % (name, i), (crack, tint)))
        los.append(tmp)
        bm.free()

    # a baixa não precisa do que fica enterrado (abaixo de −cut m; paredões do cânion: cut = ('y', d) = as costas,
    # dentro da parede de terra). Dizima ANTES de cortar (a borda do corte travava o colapso das arestas) mirando
    # só a parte visível, e corta depois.
    def buried(f):
        if isinstance(cut, tuple):
            return min(v.co.y for v in f.verts) > -cut[1]
        return cut >= 0 and max(v.co.z for v in f.verts) < -cut
    total_area = sum(areas)
    for o, ar in zip(los, areas):
        bmv = bmesh.new(); bmv.from_mesh(o.data)
        a_all = sum(f.calc_area() for f in bmv.faces) or 1.0
        a_vis = sum(f.calc_area() for f in bmv.faces if not buried(f))
        bmv.free()
        want = max(16, target * ar / total_area) / max(0.15, a_vis / a_all)
        decimate(o, want / max(1, tri_count(o)))
        bmv = bmesh.new(); bmv.from_mesh(o.data)
        dead = [f for f in bmv.faces if buried(f)]
        if dead:
            bmesh.ops.delete(bmv, geom=dead, context='FACES')
        bmv.to_mesh(o.data); bmv.free()
    hi = join(his, name + '_hi')
    lo = join(los, name)
    return hi, lo


def unwrap(lo):
    activate(lo)
    me = lo.data
    while me.uv_layers:
        me.uv_layers.remove(me.uv_layers[0])
    me.uv_layers.new(name='bake')
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    # poucas ilhas grandes: projeção "inteligente" com ângulo largo (a superfície ruidosa da rocha com 64° virava
    # centenas de ilhas e lascas soltas com normal assado errado), depois as costuras dessas ilhas e um
    # desdobramento por ângulos (sem esticar) dentro de cada uma
    bpy.ops.uv.smart_project(angle_limit=math.radians(82), island_margin=0.004, area_weight=0.0, correct_aspect=True)
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.seams_from_islands()
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.unwrap(method='ANGLE_BASED', margin=0.004)
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, margin=0.006)
    bpy.ops.object.mode_set(mode='OBJECT')


# ------------------------------------------------------------------ cozimento (Cycles)

def hi_material():
    m = bpy.data.materials.new('HI')
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    em = nt.nodes.new('ShaderNodeEmission')
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    at = nt.nodes.new('ShaderNodeAttribute'); at.attribute_name = 'nat'
    sep = nt.nodes.new('ShaderNodeSeparateColor')
    comb = nt.nodes.new('ShaderNodeCombineColor')
    # arestas: pointiness acima de 0,5 (convexo); fendas: atributo da escultura + côncavo
    edge = nt.nodes.new('ShaderNodeMapRange'); edge.inputs['From Min'].default_value = 0.5; edge.inputs['From Max'].default_value = 0.62
    conc = nt.nodes.new('ShaderNodeMapRange'); conc.inputs['From Min'].default_value = 0.5; conc.inputs['From Max'].default_value = 0.4
    mx = nt.nodes.new('ShaderNodeMath'); mx.operation = 'MAXIMUM'
    nt.links.new(at.outputs['Color'], sep.inputs['Color'])
    nt.links.new(geo.outputs['Pointiness'], edge.inputs['Value'])
    nt.links.new(geo.outputs['Pointiness'], conc.inputs['Value'])
    nt.links.new(sep.outputs['Red'], mx.inputs[0])
    nt.links.new(conc.outputs['Result'], mx.inputs[1])
    nt.links.new(mx.outputs['Value'], comb.inputs['Red'])
    nt.links.new(edge.outputs['Result'], comb.inputs['Green'])
    nt.links.new(sep.outputs['Blue'], comb.inputs['Blue'])
    nt.links.new(comb.outputs['Color'], em.inputs['Color'])
    nt.links.new(em.outputs['Emission'], out.inputs['Surface'])
    return m


def bake_tiles(name, hi, lo, res, size, wall=False):
    """normal, oclusão e máscara da alta para a baixa, cada uma numa imagem do tamanho do ladrilho."""
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    if sc.world is None:
        sc.world = bpy.data.worlds.new('W')
    sc.world.light_settings.distance = max(0.4, size * 0.6)
    bk = sc.render.bake
    bk.use_selected_to_active = True
    bk.cage_extrusion = size * 0.05
    bk.max_ray_distance = size * 0.14
    bk.margin = 10
    bk.normal_space = 'TANGENT'
    hi.data.materials.clear(); hi.data.materials.append(hi_material())
    tmat = bpy.data.materials.new('BAKE_TARGET'); tmat.use_nodes = True
    lo.data.materials.clear(); lo.data.materials.append(tmat)
    tex = tmat.node_tree.nodes.new('ShaderNodeTexImage')
    tmat.node_tree.nodes.active = tex
    # chão para a oclusão (a base escurece onde encosta na terra)
    # (paredões do cânion: o "chão" é a parede de terra atrás, em y = 0)
    if wall:
        bpy.ops.mesh.primitive_plane_add(size=size * 8 + 6, location=(0, 0.02, 0), rotation=(math.pi / 2, 0, 0))
    else:
        bpy.ops.mesh.primitive_plane_add(size=size * 8 + 6, location=(0, 0, 0))
    ground = bpy.context.object
    out = {}
    for kind, samples in (('NORMAL', 4), ('AO', 64), ('EMIT', 4)):
        img = bpy.data.images.new('%s_%s' % (name, kind), res, res, alpha=False, float_buffer=(kind == 'NORMAL'))
        img.generated_color = (0.5, 0.5, 1, 1) if kind == 'NORMAL' else (1, 1, 1, 1) if kind == 'AO' else (0, 0, 0.5, 1)
        if kind == 'NORMAL':
            img.colorspace_settings.name = 'Non-Color'
        tex.image = img
        sc.cycles.samples = samples
        ground.hide_render = kind != 'AO'
        bpy.ops.object.select_all(action='DESELECT')
        hi.select_set(True); lo.select_set(True)
        bpy.context.view_layer.objects.active = lo
        bpy.ops.object.bake(type=kind, use_clear=False)
        px = np.array(img.pixels[:], dtype=np.float32).reshape(res, res, 4)
        out[kind] = px
        os.makedirs(WORK, exist_ok=True)
        np.save(os.path.join(WORK, '%s_%s.npy' % (name, kind.lower())), px[:, :, :3].astype(np.float16))
    bpy.data.objects.remove(ground, do_unlink=True)
    return out


def atlas_uv(lo, tile, size_m):
    """UVMap (atlas) = UV de cozimento posto no ladrilho; o de cozimento sai antes do export."""
    tx, ty, span = tile
    ts = span / GRID
    me = lo.data
    me.uv_layers.new(name='UVMap')
    src, dst = me.uv_layers['bake'], me.uv_layers['UVMap']   # (pegar de novo: criar uma camada realoca as outras)
    uv = np.zeros(len(src.data) * 2, dtype=np.float32)
    src.data.foreach_get('uv', uv)
    uv = uv.reshape(-1, 2)
    pad = 0.5 / (ATLAS * ts)   # meio pixel de folga na borda do ladrilho
    uv = pad + uv * (1 - 2 * pad)
    uv[:, 0] = (tx + uv[:, 0] * span) / GRID
    uv[:, 1] = (ty + uv[:, 1] * span) / GRID
    dst.data.foreach_set('uv', uv.ravel())
    me.uv_layers.remove(me.uv_layers['bake'])
    me.uv_layers.active = me.uv_layers['UVMap']


def wood_uv2(lo, U):
    """2º UV em metros, cilíndrico em volta do eixo (x deitado, z em pé): u = ângulo × raio, v = ao longo do eixo
    (nas raízes do toco o v desce junto com a raiz). Faces que cruzam a costura ganham +circunferência."""
    me = lo.data
    lay = me.uv_layers.new(name='UV2')
    lay = me.uv_layers['UV2']
    C = math.tau * U['r']
    for poly in me.polygons:
        us = []
        for li in poly.loop_indices:
            p = me.vertices[me.loops[li].vertex_index].co
            if U['axis'] == 'x':
                v = p.x - U.get('x0', 0.0)
                yc = U.get('bend', 0.0) * math.sin(math.pi * min(max(v, 0.0), U.get('L', 1.0)) / U.get('L', 1.0))
                a = math.atan2(p.z - U.get('zc', 0.0), p.y - yc)
            else:
                a = math.atan2(p.y, p.x); rho = math.hypot(p.x, p.y); v = p.z - max(0.0, rho - U['r'] * 1.1)
            us.append([(a / math.tau + 0.5) * C, v, li])
        lo_u = min(u[0] for u in us)
        if max(u[0] for u in us) - lo_u > C * 0.5:
            for u in us:
                if u[0] < C * 0.5:
                    u[0] += C
        for u, v, li in us:
            lay.data[li].uv = (u, v)
    me.uv_layers.active = me.uv_layers['UVMap']


def make_lod1(lo, name):
    o = lo.copy(); o.data = lo.data.copy(); o.name = name + '_LOD1'; o.data.name = o.name
    bpy.context.scene.collection.objects.link(o)
    decimate(o, 0.28)
    return o


def build(name):
    atlas, tile, target, pieces_fn, cut, *extra = ASSETS[name]
    uv2 = extra[0] if extra else None
    clear_scene()
    pieces = pieces_fn()
    hi, lo = build_meshes(name, pieces, target, cut)
    bpy.context.view_layer.update()
    dims = hi.dimensions.copy()   # (cópia: o objeto da alta é apagado antes do print)
    # tamanho típico de UMA pedra (nos montes de pedras, não o monte inteiro): alcance do cozimento
    size = max(0.12, sum(sum(P['radii']) / 3.0 * 2.0 for P in pieces) / len(pieces))
    unwrap(lo)
    res = ATLAS // GRID * tile[2]
    if not NO_BAKE:
        bake_tiles(name, hi, lo, res, size, isinstance(cut, tuple))
    atlas_uv(lo, tile, size)
    if uv2:
        wood_uv2(lo, uv2)
    mname = 'NAT_wood' if atlas == 'c' else 'NAT_rock_' + atlas
    mat = bpy.data.materials.get(mname) or bpy.data.materials.new(mname)
    lo.data.materials.clear(); lo.data.materials.append(mat)
    bpy.data.objects.remove(hi, do_unlink=True)
    lod = make_lod1(lo, 'Nature_' + name)
    lo.name = 'Nature_' + name; lo.data.name = lo.name
    t0 = export([lo], 'Nature_' + name)
    t1 = export([lod], 'Nature_' + name + '_LOD1')
    print('ROCK', name, 'tris', t0, 'lod1', t1, 'dims %.2f %.2f %.2f' % tuple(dims))
    return t0


# ------------------------------------------------------------------ atlas

def compose_atlas():
    for atlas in ('a', 'b', 'c'):
        nrm = np.zeros((ATLAS, ATLAS, 4), dtype=np.float32); nrm[:, :] = (0.5, 0.5, 1.0, 1.0)
        msk = np.zeros((ATLAS, ATLAS, 4), dtype=np.float32); msk[:, :] = (1.0, 0.0, 0.0, 0.5)
        n = 0
        for name, (at, (tx, ty, span), *_rest) in ASSETS.items():
            if at != atlas:
                continue
            paths = [os.path.join(WORK, '%s_%s.npy' % (name, k)) for k in ('normal', 'ao', 'emit')]
            if not all(os.path.exists(p) for p in paths):
                continue
            nn, ao, em = (np.load(p).astype(np.float32) for p in paths)
            res = nn.shape[0]
            y0, x0 = ty * ATLAS // GRID, tx * ATLAS // GRID
            sl = (slice(y0, y0 + res), slice(x0, x0 + res))
            nrm[sl][:, :, :3] = nn
            # oclusão: um borrão leve tira o chiado das 64 amostras
            a = ao[:, :, 0]
            a = (a + np.roll(a, 1, 0) + np.roll(a, -1, 0) + np.roll(a, 1, 1) + np.roll(a, -1, 1)) / 5.0
            msk[sl][:, :, 0] = a
            msk[sl][:, :, 1] = em[:, :, 1]
            msk[sl][:, :, 2] = em[:, :, 0]
            msk[sl][:, :, 3] = em[:, :, 2]
            n += 1
        for kind, arr in (('normal', nrm), ('mask', msk)):
            img = bpy.data.images.new('nature_%s_%s' % (atlas, kind), ATLAS, ATLAS, alpha=(kind == 'mask'), float_buffer=False)
            img.colorspace_settings.name = 'Non-Color'
            img.pixels.foreach_set(np.clip(arr, 0, 1).ravel())
            path = os.path.join(TEX_OUT, 'nature_%s_%s.png' % (atlas, kind))
            img.filepath_raw = path; img.file_format = 'PNG'
            img.save()
            print('ATLAS', path, n, 'peças')


# ------------------------------------------------------------------ prévia (Cycles)

def preview(out):
    """Todas as peças lado a lado com a foto do Poly Haven em projeção de caixa, relevo assado e musgo."""
    clear_scene()
    ph = os.path.join(TEX_OUT, 'PH')
    def img(p, data=False):
        i = bpy.data.images.load(p)
        if data:
            i.colorspace_settings.name = 'Non-Color'
        return i
    mats = {}
    for atlas in ('a', 'b', 'c'):
        m = bpy.data.materials.new('prev_' + atlas); m.use_nodes = True
        nt = m.node_tree; bs = nt.nodes['Principled BSDF']
        tc = nt.nodes.new('ShaderNodeTexCoord')
        sc = nt.nodes.new('ShaderNodeMapping'); sc.inputs['Scale'].default_value = (0.5, 0.5, 0.5)
        nt.links.new(tc.outputs['Object'], sc.inputs['Vector'])
        rk = nt.nodes.new('ShaderNodeTexImage'); rk.image = img(os.path.join(ph, 'nat_rock_albedo.png' if atlas != 'c' else 'nat_bark_albedo.png'))
        rk.projection = 'BOX'; rk.projection_blend = 0.3
        ms = nt.nodes.new('ShaderNodeTexImage'); ms.image = img(os.path.join(ph, 'nat_moss_albedo.png')); ms.projection = 'BOX'; ms.projection_blend = 0.3
        nt.links.new(sc.outputs['Vector'], ms.inputs['Vector'])
        if atlas == 'c':   # casca pelo UV2 em metros (veio ao longo do tronco)
            u2 = nt.nodes.new('ShaderNodeUVMap'); u2.uv_map = 'UV2'
            s2 = nt.nodes.new('ShaderNodeMapping'); s2.inputs['Scale'].default_value = (1 / 2.15, 1 / 2.15, 1)
            nt.links.new(u2.outputs['UV'], s2.inputs['Vector']); nt.links.new(s2.outputs['Vector'], rk.inputs['Vector'])
            rk.projection = 'FLAT'
        else:
            nt.links.new(sc.outputs['Vector'], rk.inputs['Vector'])
        nm = nt.nodes.new('ShaderNodeTexImage'); nm.image = img(os.path.join(TEX_OUT, 'nature_%s_normal.png' % atlas), True)
        mk = nt.nodes.new('ShaderNodeTexImage'); mk.image = img(os.path.join(TEX_OUT, 'nature_%s_mask.png' % atlas), True)
        nmap = nt.nodes.new('ShaderNodeNormalMap')
        nt.links.new(nm.outputs['Color'], nmap.inputs['Color'])
        nt.links.new(nmap.outputs['Normal'], bs.inputs['Normal'])
        sepm = nt.nodes.new('ShaderNodeSeparateColor'); nt.links.new(mk.outputs['Color'], sepm.inputs['Color'])
        # musgo por cima: normal do mundo para cima
        geo = nt.nodes.new('ShaderNodeNewGeometry')
        sepn = nt.nodes.new('ShaderNodeSeparateXYZ'); nt.links.new(nmap.outputs['Normal'], sepn.inputs['Vector'])
        mr = nt.nodes.new('ShaderNodeMapRange'); mr.inputs['From Min'].default_value = 0.55; mr.inputs['From Max'].default_value = 0.85
        nt.links.new(sepn.outputs['Z'], mr.inputs['Value'])
        mix = nt.nodes.new('ShaderNodeMix'); mix.data_type = 'RGBA'
        nt.links.new(mr.outputs['Result'], mix.inputs['Factor'])
        nt.links.new(rk.outputs['Color'], mix.inputs[6]); nt.links.new(ms.outputs['Color'], mix.inputs[7])
        aom = nt.nodes.new('ShaderNodeMix'); aom.data_type = 'RGBA'; aom.blend_type = 'MULTIPLY'; aom.inputs['Factor'].default_value = 1.0
        nt.links.new(mix.outputs[2], aom.inputs[6]); nt.links.new(sepm.outputs['Red'], aom.inputs[7])
        nt.links.new(aom.outputs[2], bs.inputs['Base Color'])
        bs.inputs['Roughness'].default_value = 0.85
        mats[atlas] = m
    objs = []
    for name, (atlas, *_r) in ASSETS.items():
        if ONLY and name not in ONLY:
            continue
        path = os.path.join(ROOT, '..', '..', 'Assets', 'Campanula', 'Models', 'Nature_%s.fbx' % name)
        if not os.path.exists(path):
            continue
        bpy.ops.import_scene.fbx(filepath=path)
        o = bpy.context.selected_objects[0]
        o.data.materials.clear(); o.data.materials.append(mats[atlas])
        o.hide_render = True
        objs.append((name, o))
    bpy.ops.mesh.primitive_plane_add(size=400, location=(0, 0, 0))
    gm = bpy.data.materials.new('chao'); gm.use_nodes = True
    gm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.09, 0.11, 0.06, 1)
    bpy.context.object.data.materials.append(gm)
    sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN')); bpy.context.scene.collection.objects.link(sun)
    sun.data.energy = 4.0; sun.rotation_euler = (math.radians(52), 0, math.radians(40)); sun.data.angle = math.radians(3)
    w = bpy.context.scene.world or bpy.data.worlds.new('W'); bpy.context.scene.world = w
    w.use_nodes = True; w.node_tree.nodes['Background'].inputs['Color'].default_value = (0.35, 0.42, 0.55, 1); w.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.7
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); bpy.context.scene.collection.objects.link(cam)
    cam.data.lens = 40
    sc = bpy.context.scene; sc.camera = cam
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 16; sc.cycles.use_denoising = True
    sc.render.resolution_x = 520; sc.render.resolution_y = 340
    frames = []
    for name, o in objs:
        o.hide_render = False
        bpy.context.view_layer.update()
        bb = [o.matrix_world @ V(c) for c in o.bound_box]
        mn = V((min(c.x for c in bb), min(c.y for c in bb), min(c.z for c in bb)))
        mx = V((max(c.x for c in bb), max(c.y for c in bb), max(c.z for c in bb)))
        ctr = (mn + mx) / 2; ext = max((mx - mn).length, 0.5)
        d = V((0.35, -1.0, 0.62)).normalized()
        cam.location = ctr + d * ext * 1.25
        cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        f = os.path.join(WORK, 'prev_%s.png' % name)
        sc.render.filepath = f
        bpy.ops.render.render(write_still=True)
        frames.append(f)
        o.hide_render = True
    import subprocess
    subprocess.run(['montage'] + sum([['-label', os.path.basename(f)[5:-4], f] for f in frames], []) +
                   ['-tile', '5x', '-geometry', '+3+3', '-pointsize', '16', out], check=True)
    print('PREVIEW', out)


def main():
    names = [n for n in ASSETS if not ONLY or n in ONLY]
    total = 0
    for n in names:
        total += build(n)
    print('TOTAL TRIS', total)
    compose_atlas()
    if PREVIEW:
        preview(os.path.join(WORK, 'preview.png'))


main()
