"""Campânula gótica (painel "Referência de Campânula" do storyboard do autor): cidade de pedra
"baseada em som e sinos, com pontes, torres e vida noturna".

Sobrados de pedra lavrada com empena virada para a rua, janelas em arco ogival com moldura de
pedra, cornijas, contrafortes com pináculos, torreões de canto com agulha de ardósia; torres para
a silhueta da cidade; a ponte-aqueduto de dois andares de arcos sobre o desfiladeiro; o muro de
arrimo do desfiladeiro com bueiros (de onde saem as cachoeiras); estandarte com a clave de sol.

Mesmas convenções do kit_lib (metros, Z para cima, frente = −Y, UV em escala de mundo, materiais
CMP_*). Marcadores exportados como empties:
  LEDGE_<dm>_<n>   bordas de escalada (o builder cria as bordas do DPS)
  WIN_<L|D>_<largura cm>_<altura cm>_<nascente do arco, %>_<raio × 100>   janela (o VillageLights
                   cola ali a janela acesa da noite, recortada em arco)
  LAMP_<n>         lanterna (luz quente) — ponte e muro do desfiladeiro
  FALL_<largura dm>_<n>   boca de bueiro: o builder põe uma cachoeira ali
Rodar: ~/.local/opt/blender-5.2/blender -b --factory-startup --python ArtSource/Campanula/scripts/kit_gothic.py
"""
import bpy, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_lib import *

_LEDGE_N = [0]


def ledge_name(length):
    """Igual ao kit_buildings (importar aquele módulo rodaria o main dele)."""
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

TILE['banner'] = 1.0
TILE['ashlar'] = 2.6   # fotos Poly Haven (fetch_polyhaven.py): pedra lavrada, molduras escuras, rocha
TILE['trim'] = 2.0
TILE['rock'] = 3.0
MATS = None
_N = [0]
DETAIL = True   # False = versão simples (LOD1, vista de longe)


def uid():
    _N[0] += 1
    return _N[0]


def mats():
    m = get_materials()
    b = bpy.data.materials.get('CMP_banner') or bpy.data.materials.new('CMP_banner')
    b.diffuse_color = (0.32, 0.12, 0.28, 1)
    m['banner'] = b
    for k, c in (('ashlar', (0.5, 0.48, 0.45)), ('trim', (0.3, 0.29, 0.28)), ('rock', (0.35, 0.3, 0.26))):
        mm = bpy.data.materials.get('CMP_' + k) or bpy.data.materials.new('CMP_' + k)
        mm.diffuse_color = (*c, 1)
        m[k] = mm
    return m


# ------------------------------------------------------------------ geometria de fachada

class Face:
    """Plano de uma fachada: origem O, direção ao longo da parede R (horizontal), normal para fora N.
    P(u, z, off) = ponto na parede (u ao longo, z altura, off para fora)."""

    def __init__(self, O, R, N):
        self.O, self.R, self.N = V(O), V(R).normalized(), V(N).normalized()

    def P(self, u, z, off=0.0):
        return self.O + self.R * u + V((0, 0, z)) + self.N * off


def faces_of(x0, x1, y0, y1):
    """As 4 fachadas de uma caixa (frente −Y, fundos +Y, esquerda −X, direita +X), u crescendo
    da esquerda para a direita de quem olha de fora, centrado no meio da parede."""
    return {
        'front': (Face(((x0 + x1) / 2, y0, 0), (1, 0, 0), (0, -1, 0)), x1 - x0),
        'back': (Face(((x0 + x1) / 2, y1, 0), (-1, 0, 0), (0, 1, 0)), x1 - x0),
        'left': (Face((x0, (y0 + y1) / 2, 0), (0, -1, 0), (-1, 0, 0)), y1 - y0),
        'right': (Face((x1, (y0 + y1) / 2, 0), (0, 1, 0), (1, 0, 0)), y1 - y0),
    }


def arch_pts(u0, u1, zs, k=1.0, segs=6, round_=False):
    """Curva do arco de u0 a u1 nascendo em zs. Ogival: raio = k × vão (k=1 equilátero, >1 lanceta).
    round_: arco pleno (semicírculo). Devolve [(u, z)] da nascente esquerda à direita."""
    w = u1 - u0
    pts = []
    if round_:
        c = (u0 + u1) / 2; r = w / 2
        for i in range(segs * 2 + 1):
            a = math.pi - math.pi * i / (segs * 2)
            pts.append((c + math.cos(a) * r, zs + math.sin(a) * r))
        return pts
    R = k * w
    cl, cr = u0 + R, u1 - R
    mid = (u0 + u1) / 2
    a_end = math.acos(max(-1.0, min(1.0, (mid - cl) / R)))
    for i in range(segs + 1):
        a = math.pi - (math.pi - a_end) * i / segs
        pts.append((cl + math.cos(a) * R, zs + math.sin(a) * R))
    for i in range(1, segs + 1):
        a = (math.pi - a_end) - (math.pi - a_end) * i / segs
        pts.append((cr + math.cos(a) * R, zs + math.sin(a) * R))
    return pts


def arch_rise(w, k=1.0):
    R = k * w
    return math.sqrt(max(0.0, R * R - (R - w / 2) ** 2))


def opening(mb, F, u, z0, w, hrect, fill, frame='trim', t=0.15, depth=0.12, k=1.15, mullion=False, sill=True, round_=False):
    """Abertura em arco numa fachada: preenchimento (vidro/porta) rente à parede, moldura de pedra
    saliente em volta (face da frente + revés). Devolve a altura total (até o fecho)."""
    u0, u1 = u - w / 2, u + w / 2
    zs = z0 + hrect
    inner = [(u1, z0), (u1, zs)] + list(reversed(arch_pts(u0, u1, zs, k, round_=round_)))[1:-1] + [(u0, zs), (u0, z0)]
    # (sentido anti-horário visto de fora: direita-baixo → direita-nascente → arco → esquerda → baixo)
    mb.poly([F.P(a, b, 0.012) for a, b in inner], fill)   # (anti-horário visto de fora: normal para fora)
    o0, o1 = u0 - t, u1 + t
    outer = [(o1, z0 - (t if sill else 0)), (o1, zs)] + list(reversed(arch_pts(o0, o1, zs, (k * w + t) / (w + 2 * t), round_=round_)))[1:-1] + [(o0, zs), (o0, z0 - (t if sill else 0))]
    # anel da moldura (as duas curvas têm o mesmo número de pontos)
    n = len(inner)
    for i in range(n - 1):
        a, b = inner[i], inner[i + 1]
        c, d = outer[i + 1], outer[i]
        mb.poly([F.P(*d, depth), F.P(*c, depth), F.P(*b, depth), F.P(*a, depth)], frame)
        mb.poly([F.P(*b, 0.01), F.P(*a, 0.01), F.P(*a, depth), F.P(*b, depth)], frame)      # revés interno (virado para o vão)
        mb.poly([F.P(*d, 0.0), F.P(*c, 0.0), F.P(*c, depth), F.P(*d, depth)], frame)        # borda externa
    if sill:
        mb.box(tuple(min(p, q) for p, q in zip(F.P(o0 - 0.08, z0 - t - 0.1, 0), F.P(o1 + 0.08, z0 - t, 0.22))),
               tuple(max(p, q) for p, q in zip(F.P(o0 - 0.08, z0 - t - 0.1, 0), F.P(o1 + 0.08, z0 - t, 0.22))), frame)
    if mullion:
        a, b = F.P(u, z0, 0.05), F.P(u, zs + arch_rise(w, k) * 0.55, 0.05)
        mb.beam(a, b, 0.07, frame, 0.08)
        mb.beam(F.P(u0, zs, 0.05), F.P(u1, zs, 0.05), 0.06, frame, 0.07)   # travessa na nascente
    return hrect + arch_rise(w, k if not round_ else 0.5)


def hood_mould(mb, F, u, z0, w, hrect, k=1.15, t=0.15):
    """Pingadeira de pedra acompanhando o arco por fora (com as voltas horizontais nas pontas)."""
    e = t + 0.07
    u0, u1 = u - w / 2 - e, u + w / 2 + e
    zs = z0 + hrect
    arc = arch_pts(u0, u1, zs, (k * w + e) / (w + 2 * e), segs=4)
    for i in range(len(arc) - 1):
        a, b = arc[i], arc[i + 1]
        mb.beam(F.P(a[0], a[1], 0.15), F.P(b[0], b[1], 0.15), 0.09, 'trim', 0.11)
    for uu, sg in ((u0, -1), (u1, 1)):
        mb.beam(F.P(uu - sg * 0.02, zs, 0.15), F.P(uu + sg * 0.2, zs, 0.15), 0.09, 'trim', 0.11)


def tracery(mb, F, u, zs, w, k=1.15):
    """Rendilhado: um óculo de pedra na cabeça do arco, sobre o mainel."""
    rise = arch_rise(w, k)
    r = min(w * 0.23, rise * 0.32)
    cz = zs + rise * 0.42
    n = 8
    pts = [(u + math.cos(math.tau * i / n) * r, cz + math.sin(math.tau * i / n) * r) for i in range(n)]
    for i in range(n):
        mb.beam(F.P(*pts[i], 0.05), F.P(*pts[(i + 1) % n], 0.05), 0.05, 'trim', 0.07)


def quoins(mb, x, y, sx, sy, z0, z1, m='trim'):
    """Pedras de cunhal na quina (x, y): blocos alternados (longo de um lado, curto do outro), salientes."""
    h, gap, z, i = 0.34, 0.05, z0, 0
    while z + h < z1:
        lx, ly = (0.55, 0.3) if i % 2 == 0 else (0.3, 0.55)
        xa, xb = sorted((x - sx * lx, x + sx * 0.035))
        ya, yb = sorted((y - sy * ly, y + sy * 0.035))
        mb.box((xa, ya, z), (xb, yb, z + h), m)
        z += h + gap; i += 1


def corbels(mb, F, L, z, step=0.62):
    """Cachorrada sob a cornija: mísulas de pedra em degraus ao longo da fachada."""
    n = max(2, int(L / step))
    for i in range(n):
        u = -L / 2 + L * (i + 0.5) / n
        for (dz0, dz1, out, ww) in ((-0.34, -0.2, 0.12, 0.13), (-0.2, -0.04, 0.2, 0.16)):
            a, b = F.P(u - ww / 2, z + dz0, 0.0), F.P(u + ww / 2, z + dz1, out)
            mb.box(tuple(map(min, a, b)), tuple(map(max, a, b)), 'trim', sides='xXyYz' if F.N.y != 0 else 'xXyYz')


def rose(mb, F, cx, cz, r, lit, mk):
    """Rosácea: vidro redondo, anel de pedra, raios e miolo (WIN com forma redonda para a luz da noite)."""
    n = 12
    ring_in = [(cx + math.cos(math.tau * i / n) * r, cz + math.sin(math.tau * i / n) * r) for i in range(n)]
    ring_out = [(cx + math.cos(math.tau * i / n) * (r + 0.16), cz + math.sin(math.tau * i / n) * (r + 0.16)) for i in range(n)]
    mb.poly([F.P(a, b, 0.012) for a, b in ring_in], 'glass' if lit else 'dark')
    for i in range(n):
        j = (i + 1) % n
        a, b, c, d = ring_in[i], ring_in[j], ring_out[j], ring_out[i]
        mb.poly([F.P(*d, 0.12), F.P(*c, 0.12), F.P(*b, 0.12), F.P(*a, 0.12)], 'trim')
        mb.poly([F.P(*b, 0.01), F.P(*a, 0.01), F.P(*a, 0.12), F.P(*b, 0.12)], 'trim')
        mb.poly([F.P(*d, 0.0), F.P(*c, 0.0), F.P(*c, 0.12), F.P(*d, 0.12)], 'trim')
    for i in range(0, n, 2):
        a = ring_in[i]
        mb.beam(F.P(cx, cz, 0.06), F.P(a[0], a[1], 0.06), 0.05, 'trim', 0.07)
    mb.beam(F.P(cx, cz, 0.02), F.P(cx, cz, 0.12), 0.18, 'trim', 0.18)
    p = F.P(cx, cz, 0.02)
    d = int(2 * r * 100)
    mk.append(('WIN_%s_%03d_%03d_00_000' % ('L' if lit else 'D', d, d), tuple(p), (F.N.x, F.N.y)))


def window(mb, F, u, z0, w, hrect, lit, mk, k=1.15, mullion=True):
    h = opening(mb, F, u, z0, w, hrect, 'glass' if lit else 'dark', mullion=mullion, k=k)
    if DETAIL:
        hood_mould(mb, F, u, z0, w, hrect, k)
        if mullion and w >= 0.75:
            tracery(mb, F, u, z0 + hrect, w, k)
    p = F.P(u, z0 + h / 2, 0.02)
    mk.append(('WIN_%s_%03d_%03d_%02d_%03d' % ('L' if lit else 'D', int(w * 100), int(h * 100), int(hrect / h * 100), int(k * 100)), tuple(p), (F.N.x, F.N.y)))
    return h


def door(mb, F, u, w=1.3, h=2.0, mk=None):
    opening(mb, F, u, 0.6, w, h, 'planks', frame='trim', t=0.22, depth=0.18, k=1.0, sill=False)
    if DETAIL:
        # arquivoltas recuadas (dois arcos a mais) apoiadas em colunelos
        for (t, off) in ((0.36, 0.22), (0.5, 0.27)):
            u0, u1 = u - w / 2 - t, u + w / 2 + t
            arc = arch_pts(u0, u1, 0.6 + h, (w + t) / (w + 2 * t), segs=4)
            for i in range(len(arc) - 1):
                a, b = arc[i], arc[i + 1]
                mb.beam(F.P(a[0], a[1], off), F.P(b[0], b[1], off), 0.1, 'trim', 0.1)
            for uu in (u0, u1):
                mb.beam(F.P(uu, 0.6, off), F.P(uu, 0.6 + h, off), 0.1, 'trim', 0.1)
    if mk is not None:
        # lanterna de parede ao lado da porta (a luz quente da rua)
        lu = u + w / 2 + 0.8
        mb.beam(F.P(lu, 2.95, 0.0), F.P(lu, 2.95, 0.42), 0.04, 'iron', 0.04)
        a, b = F.P(lu - 0.11, 2.42, 0.31), F.P(lu + 0.11, 2.78, 0.53)
        mb.box(tuple(map(min, a, b)), tuple(map(max, a, b)), 'iron')
        c = F.P(lu, 2.6, 0.42)
        mk.append(('LAMP_%d' % uid(), (c.x, c.y, c.z), (F.N.x, F.N.y)))
    for kz in (0.35, 0.65):   # ferragens
        a, b = F.P(u - w / 2 + 0.08, 0.6 + h * kz, 0.03), F.P(u + w / 2 - 0.08, 0.6 + h * kz, 0.03)
        mb.beam(a, b, 0.05, 'iron', 0.05)
    # degrau
    p0, p1 = F.P(u - w / 2 - 0.35, 0.0, 0.0), F.P(u + w / 2 + 0.35, 0.6, 0.55)
    mb.box(tuple(map(min, p0, p1)), tuple(map(max, p0, p1)), 'trim', sides='xXyYZ')


def band(mb, x0, x1, y0, y1, z, h=0.18, out=0.08, m='trim'):
    mb.box((x0 - out, y0 - out, z - h / 2), (x1 + out, y1 + out, z + h / 2), m)


def pinnacle(mb, x, y, z, s=0.28, h=1.6, m='trim'):
    mb.box((x - s / 2, y - s / 2, z), (x + s / 2, y + s / 2, z + h * 0.45), m, sides='xXyYZ')
    mb.pyramid(x, y, z + h * 0.45, s / 2 + 0.03, h * 0.55, m)
    mb.beam((x, y, z + h), (x, y, z + h + 0.25), 0.05, 'iron')


def buttress(mb, x, y, outward, z1, w=0.6, depth=0.75, m='trim'):
    """Contraforte escalonado encostado na parede no ponto (x, y), saindo na direção outward."""
    ox, oy = outward
    def bx(d0, d1, za, zb, ww):
        if ox == 0:
            ya, yb = sorted((y + oy * d0, y + oy * d1))
            mb.box((x - ww / 2, ya, za), (x + ww / 2, yb, zb), m, sides='xXyYZ')
        else:
            xa, xb = sorted((x + ox * d0, x + ox * d1))
            mb.box((xa, y - ww / 2, za), (xb, y + ww / 2, zb), m, sides='xXyYZ')
    bx(0, depth, 0, z1 * 0.55, w)
    bx(0, depth * 0.62, z1 * 0.55, z1 * 0.9, w * 0.85)
    bx(0, depth * 0.35, z1 * 0.9, z1 + 0.15, w * 0.7)


def steep_roof(mb, x0, x1, y0, y1, z, rise, m='roof_slate', over=0.3, thick=0.18):
    """Telhado íngreme com cumeeira ao longo de Y (a empena fica virada para a frente/rua)."""
    xc = (x0 + x1) / 2
    xa, xb = x0 - over, x1 + over
    ya, yb = y0 - over, y1 + over
    drop = over * rise / ((x1 - x0) / 2)
    ze, zr = z - drop, z + rise
    # águas: esquerda (x de xa a xc) e direita
    for (xe, s) in ((xa, -1), (xb, 1)):
        q = [(xe, ya, ze), (xc, ya, zr), (xc, yb, zr), (xe, yb, ze)]
        if s > 0: q = [q[3], q[2], q[1], q[0]]
        mb.poly([(p[0], p[1], p[2] + thick) for p in q], m)
        mb.poly(list(reversed(q)), 'dark')
        mb.poly([(q[0][0], q[0][1], q[0][2] + thick), (q[3][0], q[3][1], q[3][2] + thick), q[3], q[0]], 'trim')
    mb.beam((xc, ya, zr + thick * 0.6), (xc, yb, zr + thick * 0.6), 0.2, 'iron')   # cumeeira de chumbo
    if DETAIL:   # crista de ferro forjado na cumeeira
        n = max(3, int((yb - ya) / 0.5))
        for i in range(n + 1):
            y = ya + (yb - ya) * i / n
            mb.beam((xc, y, zr + thick), (xc, y, zr + thick + (0.32 if i % 2 == 0 else 0.2)), 0.03, 'iron', 0.03)
    return ze, zr


def dormer(mb, x0, x1, ze, zr, over, y, side, lit, mk):
    """Trapeira numa água do telhado (ridge ao longo de Y; side −1 = água da esquerda, virada para −X)."""
    xc = (x0 + x1) / 2
    xa = (x0 - over) if side < 0 else (x1 + over)
    zd = ze + (zr - ze) * 0.22
    xf = xa + (xc - xa) * (zd - ze) / (zr - ze)
    dd = 1.8
    hw, hh, ga = 0.62, 1.35, 0.6
    if side < 0:
        mb.box((xf, y - hw, zd), (xf + dd, y + hw, zd + hh), 'ashlar', sides='xyY')
        tri = [(xf, y + hw, zd + hh), (xf, y - hw, zd + hh), (xf, y, zd + hh + ga)]
        F = Face((xf, y, 0), (0, -1, 0), (-1, 0, 0))
        xs0, xs1 = xf - 0.15, xf + dd
    else:
        mb.box((xf - dd, y - hw, zd), (xf, y + hw, zd + hh), 'ashlar', sides='XyY')
        tri = [(xf, y - hw, zd + hh), (xf, y + hw, zd + hh), (xf, y, zd + hh + ga)]
        F = Face((xf, y, 0), (0, 1, 0), (1, 0, 0))
        xs0, xs1 = xf - dd, xf + 0.15
    mb.poly(tri, 'ashlar')
    z1, z2 = zd + hh, zd + hh + ga + 0.08
    mb.poly([(xs0, y - hw - 0.15, z1), (xs1, y - hw - 0.15, z1), (xs1, y, z2), (xs0, y, z2)], 'roof_slate')
    mb.poly([(xs1, y + hw + 0.15, z1), (xs0, y + hw + 0.15, z1), (xs0, y, z2), (xs1, y, z2)], 'roof_slate')
    window(mb, F, 0, zd + 0.25, 0.62, 0.62, lit, mk, k=1.0, mullion=False)


def gable(mb, x0, x1, y, z, rise, outward, lit, mk, win=True):
    """Empena de pedra (triângulo) com rufo de pedra nas bordas, pináculos e uma janela/óculo."""
    xc = (x0 + x1) / 2
    tri = [(x0, y, z), (x1, y, z), (xc, y, z + rise)]
    if outward > 0: tri = [tri[1], tri[0], tri[2]]
    mb.poly(tri, 'ashlar')
    # rufo (coping) saliente sobre as bordas inclinadas
    for a, b in (((x0 - 0.25, y, z - 0.05), (xc, y, z + rise + 0.2)), ((x1 + 0.25, y, z - 0.05), (xc, y, z + rise + 0.2))):
        mb.beam(V(a) + V((0, outward * 0.1, 0)), V(b) + V((0, outward * 0.1, 0)), 0.32, 'trim', 0.22)
    pinnacle(mb, xc, y + outward * 0.08, z + rise + 0.25, 0.32, 1.9)
    for x in (x0, x1):
        pinnacle(mb, x, y + outward * 0.1, z - 0.1, 0.3, 1.5)
    if win and rise > 2.6:
        F = Face((xc, y, 0), (1 if outward < 0 else -1, 0, 0), (0, outward, 0))
        if DETAIL and (x1 - x0) >= 6.4 and rise > 4.5:
            rose(mb, F, 0, z + rise * 0.36, min(0.95, (x1 - x0) * 0.13), lit, mk)
        else:
            ww = min(1.1, (x1 - x0) * 0.22)
            window(mb, F, 0, z + 0.45, ww, max(0.5, rise * 0.32), lit, mk, k=1.0)


def turret(mb, cx, cy, z0, z1, r=0.95, spire=4.6, lit=True, mk=None):
    """Torreão de canto: mísula cônica, fuste cilíndrico com frestas, cornija e agulha de ardósia."""
    mb.lathe([(0.05, z0 - 1.4), (r * 0.55, z0 - 0.8), (r, z0)], 'trim', segs=12, center=(cx, cy, 0))
    mb.cylinder((cx, cy, z0), r, z1 - z0, 'ashlar', segs=12, cap_top=False)
    mb.cylinder((cx, cy, z1), r + 0.15, 0.3, 'trim', segs=12)
    mb.cone((cx, cy, z1 + 0.3), r + 0.2, spire, 'roof_slate', segs=12)
    mb.beam((cx, cy, z1 + 0.3 + spire), (cx, cy, z1 + 0.6 + spire), 0.05, 'iron')
    if mk is not None:
        for a in (0.6, 2.4, 4.1):
            n = V((math.cos(a), math.sin(a), 0))
            F = Face(V((cx, cy, 0)) + n * r, V((-n.y, n.x, 0)), n)
            window(mb, F, 0, z0 + (z1 - z0) * 0.35, 0.36, 0.6, lit, mk, k=1.0, mullion=False)


def banner(mb, F, u, z_top, w=1.0, h=2.8):
    """Estandarte pendurado numa haste de ferro: tecido com a clave de sol (UV 0..1 na textura)."""
    a = F.P(u - w / 2 - 0.15, z_top + 0.08, 0.45)
    b = F.P(u + w / 2 + 0.15, z_top + 0.08, 0.45)
    mb.beam(a, b, 0.05, 'iron')
    for s in (-1, 1):
        mb.beam(F.P(u + s * (w / 2 + 0.1), z_top + 0.08, 0.0), F.P(u + s * (w / 2 + 0.1), z_top + 0.08, 0.45), 0.04, 'iron')
    # tecido: retângulo + ponta em V embaixo
    pts = [(u - w / 2, z_top), (u + w / 2, z_top), (u + w / 2, z_top - h), (u, z_top - h - w * 0.35), (u - w / 2, z_top - h)]
    uvs = [(0, 1), (1, 1), (1, 0.12), (0.5, 0.0), (0, 0.12)]
    for off, rev in ((0.42, False), (0.4, True)):
        f = mb.poly([F.P(p[0], p[1], off) for p in (reversed(pts) if rev else pts)], 'banner')
        for loop, uv in zip(f.loops, (list(reversed(uvs)) if rev else uvs)):
            loop[mb.uv].uv = uv if not rev else (1 - uv[0], uv[1])


# ------------------------------------------------------------------ sobrado gótico

def ghouse(name, w, d, floors, turret_side=None, balcony=False, shed=None, sign=False,
           banners=0, lit_rows=(True, True, True, True), roof_rise=None):
    mb = MeshBuilder(name)
    mk = []
    x0, x1, y0, y1 = -w / 2, w / 2, -d / 2, d / 2
    H = 0.6 + sum(floors)
    mb.box((x0 - 0.1, y0 - 0.1, 0), (x1 + 0.1, y1 + 0.1, 0.6), 'trim', sides='xXyYZ')
    mb.box((x0, y0, 0.6), (x1, y1, H), 'ashlar', sides='xXyY')
    fs = faces_of(x0, x1, y0, y1)
    if DETAIL:
        band(mb, x0, x1, y0, y1, 0.64, h=0.08, out=0.05)    # chanfro do soco
        for (cx_, cy_, sx_, sy_) in ((x0, y0, -1, -1), (x1, y0, 1, -1), (x0, y1, -1, 1), (x1, y1, 1, 1)):
            quoins(mb, cx_, cy_, sx_, sy_, 0.7, H - 0.3)
    z = 0.6
    for fi, fh in enumerate(floors):
        if fi > 0:
            band(mb, x0, x1, y0, y1, z)
            if DETAIL:
                band(mb, x0, x1, y0, y1, z + 0.12, h=0.05, out=0.12)   # pingadeira da cinta
        lit = lit_rows[min(fi, len(lit_rows) - 1)]
        for key, (F, L) in fs.items():
            n = max(1, int(round(L / 2.2)))
            for k in range(n):
                u = -L / 2 + L * (k + 0.5) / n
                if key == 'front' and fi == 0 and abs(u) < 1.1:
                    continue   # porta
                if key in ('left', 'right') and L < 5 and fi == 0:
                    continue
                if shed and fi == 0 and ((shed == 'right' and key == 'right') or (shed == 'left' and key == 'left')):
                    continue
                ww = 0.78 if fh < 3.2 else 0.9
                win_lit = lit if key in ('front', 'right') else (lit and (k + fi) % 2 == 0)
                window(mb, F, u, z + fh * 0.22, ww, fh * 0.42, win_lit, mk)
        if fi == 0:
            door(mb, fs['front'][0], 0.0, mk=mk)
        if balcony and fi == 1:
            bz = z + 0.02
            mb.box((x0 + 0.5, y0 - 1.0, bz - 0.2), (x1 - 0.5, y0, bz), 'trim')
            for u in [x0 + 0.6 + (w - 1.2) * k / 7 for k in range(8)]:
                mb.box((u - 0.07, y0 - 0.95, bz), (u + 0.07, y0 - 0.81, bz + 0.85), 'ashlar', sides='xXyY')
            mb.box((x0 + 0.5, y0 - 1.0, bz + 0.85), (x1 - 0.5, y0 - 0.76, bz + 0.97), 'trim')
            for u in (x0 + 0.8, x1 - 0.8, 0.0):   # mísulas
                mb.beam((u, y0 - 0.1, bz - 0.85), (u, y0 - 0.85, bz - 0.2), 0.18, 'trim', 0.22)
            mk.append((ledge_name(w - 1.2), (0, y0 - 1.0, bz - 0.06), (0, -1)))
        z += fh
    # cornija e telhado íngreme com a empena para a rua
    band(mb, x0, x1, y0, y1, H, h=0.32, out=0.2)
    if DETAIL:
        band(mb, x0, x1, y0, y1, H + 0.2, h=0.08, out=0.26)   # rufo da cornija
        for key, (F, L) in fs.items():
            corbels(mb, F, L, H - 0.16)
    for xb, yb, o in ((x0 + 0.3, y0, (0, -1)), (x1 - 0.3, y0, (0, -1)), (x0 + 0.3, y1, (0, 1)), (x1 - 0.3, y1, (0, 1))):
        buttress(mb, xb, yb, o, H - 0.2)
    rise = roof_rise or w * 0.95
    ze, zr = steep_roof(mb, x0, x1, y0, y1, H + 0.16, rise)
    if DETAIL and rise >= 4.6:
        for side in (-1, 1):
            dormer(mb, x0, x1, ze, zr, 0.3, -d * 0.2, side, True, mk)
    gable(mb, x0, x1, y0, H + 0.16, rise, -1, True, mk)
    gable(mb, x0, x1, y1, H + 0.16, rise, 1, False, mk, win=False)
    # chaminé
    cx = x1 - 0.9
    mb.box((cx - 0.38, 0.4, H - 0.4), (cx + 0.38, 1.2, zr - 0.6), 'trim')
    mb.box((cx - 0.48, 0.3, zr - 0.6), (cx + 0.48, 1.3, zr - 0.45), 'trim')
    if turret_side:
        tx = x0 if turret_side == 'left' else x1
        turret(mb, tx, y0, 0.6 + floors[0] + 0.4, H + 0.6, r=0.95, spire=rise * 0.95 + 1.0, mk=mk)
    if shed:
        s = 1 if shed == 'right' else -1
        sx0, sx1 = (x1, x1 + 3.0) if s > 0 else (x0 - 3.0, x0)
        sy0, sy1 = y0 + 0.4, y1 - 0.4
        mb.box((sx0, sy0, 0), (sx1, sy1, 2.1), 'ashlar', sides='xXyY')
        mb.box((sx0 - 0.1 * (s < 0), sy0 - 0.12, 2.1), (sx1 + 0.1 * (s > 0), sy1 + 0.1, 2.25), 'trim')
        F = Face(((sx0 + sx1) / 2, sy0, 0), (1, 0, 0), (0, -1, 0))
        opening(mb, F, 0, 0.0, 1.3, 1.25, 'planks', t=0.15, depth=0.1, k=1.0, sill=False)
        mk.append((ledge_name(2.6), ((sx0 + sx1) / 2, sy0 - 0.12, 2.17), (0, -1)))
        mk.append(('TOP_%.1f' % 2.25, ((sx0 + sx1) / 2, (sy0 + sy1) / 2, 2.25), (0, -1)))
    if sign:
        mb.beam((x1 - 0.9, y0 - 0.1, 3.6), (x1 - 0.9, y0 - 1.5, 3.6), 0.08, 'iron')
        mb.beam((x1 - 0.9, y0 - 0.1, 2.9), (x1 - 0.9, y0 - 1.0, 3.55), 0.05, 'iron')
        mb.box((x1 - 1.35, y0 - 1.45, 2.55), (x1 - 0.45, y0 - 1.4, 3.45), 'planks')
    F = fs['front'][0]
    for i in range(banners):
        u = (-1 if i == 0 else 1) * (w / 2 - 0.95)
        banner(mb, F, u, H - 0.5, 0.85, min(2.6, floors[-1] + floors[-2] * 0.5 if len(floors) > 1 else 2.0))
    obj = mb.finish(MATS)
    for n, p, o in mk:
        marker(n if not n.startswith('WIN') else n + '_%d' % uid(), p, o, obj)
    return obj


# ------------------------------------------------------------------ torres da silhueta

def gtower(name, s, H, top='spire', lit=True):
    """Torre gótica: fuste quadrado com contrafortes, janelas em várias alturas, sineira com
    grandes arcos, pináculos nos cantos e agulha octogonal (ou cúpula com lanterna)."""
    mb = MeshBuilder(name)
    mk = []
    mb.box((-s - 0.3, -s - 0.3, 0), (s + 0.3, s + 0.3, 1.2), 'trim', sides='xXyYZ')
    mb.box((-s, -s, 1.2), (s, s, H), 'ashlar', sides='xXyY')
    fs = faces_of(-s, s, -s, s)
    for zb in [z for z in (H * 0.33, H * 0.62) if z > 4]:
        band(mb, -s, s, -s, s, zb, 0.25, 0.12)
    for key, (F, L) in fs.items():
        for zi, zw in enumerate((H * 0.12, H * 0.4, H * 0.68)):
            n = 1 if s < 2.6 else 2
            for k in range(n):
                u = -L / 2 + L * (k + 0.5) / n if n > 1 else 0
                window(mb, F, u, zw, 0.8, min(2.2, H * 0.14), lit and (zi + k) % 3 != 2, mk, k=1.2)
    for (cx, cy, o) in ((-s, -s, (0, -1)), (s, -s, (0, -1)), (-s, s, (0, 1)), (s, s, (0, 1))):
        buttress(mb, cx + (0.3 if cx < 0 else -0.3), cy, o, H)
        buttress(mb, cx, cy + (0.3 if cy < 0 else -0.3), ((-1 if cx < 0 else 1), 0), H)
    # sineira
    zb, zt = H, H + s * 2.2
    mb.box((-s - 0.25, -s - 0.25, zb - 0.3), (s + 0.25, s + 0.25, zb), 'trim')
    for (cx, cy) in ((-s, -s), (s, -s), (s, s), (-s, s)):
        mb.box((cx - 0.5, cy - 0.5, zb), (cx + 0.5, cy + 0.5, zt), 'ashlar')
    for key, (F, L) in fs.items():
        opening(mb, F, 0, zb + 0.4, L - 1.6, (zt - zb) * 0.45, 'dark', t=0.25, depth=0.2, k=1.05, mullion=True, sill=False)
        # parede da sineira em volta do arco
        mb.poly([F.P(-L / 2 + 0.5, zb, 0), F.P(L / 2 - 0.5, zb, 0), F.P(L / 2 - 0.5, zt, 0), F.P(-L / 2 + 0.5, zt, 0)], 'ashlar')
    mb.box((-s - 0.35, -s - 0.35, zt), (s + 0.35, s + 0.35, zt + 0.45), 'trim')
    for (cx, cy) in ((-s, -s), (s, -s), (s, s), (-s, s)):
        pinnacle(mb, cx, cy, zt + 0.45, 0.55, s * 1.4)
    if top == 'spire':
        mb.lathe([(s * 0.92, zt + 0.45), (s * 0.85, zt + 1.4), (0.08, zt + 1.4 + s * 4.2)], 'roof_slate', segs=8)
        if DETAIL:   # crochês de pedra subindo pelas arestas da agulha
            for i in range(8):
                a = math.tau * i / 8
                for t in (0.15, 0.3, 0.45, 0.6, 0.75):
                    r = s * 0.85 + (0.08 - s * 0.85) * t
                    zz = zt + 1.4 + s * 4.2 * t
                    mb.pyramid(math.cos(a) * (r + 0.05), math.sin(a) * (r + 0.05), zz, 0.09, 0.26, 'trim')
        mb.beam((0, 0, zt + 1.4 + s * 4.2), (0, 0, zt + 2.6 + s * 4.2), 0.07, 'iron')
        mb.beam((-0.45, 0, zt + 2.1 + s * 4.2), (0.45, 0, zt + 2.1 + s * 4.2), 0.06, 'iron')
    else:
        # tambor + cúpula + lanterna
        mb.cylinder((0, 0, zt + 0.45), s * 0.8, s * 0.9, 'ashlar', segs=16, cap_top=False)
        mb.lathe([(s * 0.86, zt + 0.45 + s * 0.9)] + [(s * 0.86 * math.cos(a), zt + 0.45 + s * 0.9 + s * 0.95 * math.sin(a)) for a in [i / 6 * math.pi / 2 for i in range(1, 7)]], 'roof_slate', segs=16)
        lz = zt + 0.45 + s * 1.85
        mb.cylinder((0, 0, lz - 0.1), s * 0.25, s * 0.55, 'trim', segs=8)
        mb.cone((0, 0, lz + s * 0.45), s * 0.3, s * 0.9, 'roof_slate', segs=8)
        mb.beam((0, 0, lz + s * 1.3), (0, 0, lz + s * 1.3 + 0.9), 0.06, 'iron')
        for a in range(4):
            n = V((math.cos(a * math.pi / 2), math.sin(a * math.pi / 2), 0))
            F = Face(n * s * 0.8, V((-n.y, n.x, 0)), n)
            window(mb, F, 0, zt + 0.7, 0.55, s * 0.35, lit, mk, k=1.0, mullion=False)
    obj = mb.finish(MATS)
    for n, p, o in mk:
        marker(n + '_%d' % uid(), p, o, obj)
    return obj


# ------------------------------------------------------------------ ponte-aqueduto

def aqueduct(name, bays=3, bay=6.0, deck_w=5.2, depth_total=22.0, abut=2.2):
    """Ponte de dois andares de arcos plenos (como na referência): tabuleiro calçado no z = 0,
    parapeitos com pilaretes e lanternas; arcos de cima pequenos, de baixo altos; pilares descem
    até −depth_total (o terreno do desfiladeiro esconde o pé). Comprimento = bays × bay (+ cabeceiras)."""
    mb = MeshBuilder(name)
    mk = []
    L = bays * bay
    xa, xb = -L / 2 - abut, L / 2 + abut    # cabeceiras entram na terra
    yd = deck_w / 2
    # tabuleiro + faixa de cornija
    mb.box((xa, -yd, -1.3), (xb, yd, 0.0), 'ashlar', sides='yYz')
    mb.box((xa, -yd + 0.5, -0.02), (xb, yd - 0.5, 0.0), 'cobble', sides='Z')
    band(mb, xa, xb, -yd, yd, -1.3, 0.3, 0.15)
    # parapeitos (1,1 m) com pilaretes e pináculos baixos
    for s in (-1, 1):
        y0, y1 = (yd - 0.5, yd) if s > 0 else (-yd, -yd + 0.5)
        mb.box((xa, y0, 0.0), (xb, y1, 1.0), 'ashlar', sides='xXyYZ')
        mb.box((xa, y0 - 0.06, 1.0), (xb, y1 + 0.06, 1.12), 'trim')
        n = int((xb - xa) / 4.0)
        for k in range(n + 1):
            x = xa + (xb - xa) * k / n
            mb.box((x - 0.3, y0 - 0.1, 0), (x + 0.3, y1 + 0.1, 1.5), 'trim', sides='xXyYZ')
            mb.pyramid(x, (y0 + y1) / 2, 1.5, 0.32, 0.55, 'trim')
            if k % 2 == 1:
                mk.append(('LAMP_%d' % uid(), (x, (y0 + y1) / 2, 2.4), (0, s)))
                mb.beam((x, (y0 + y1) / 2, 1.5), (x, (y0 + y1) / 2, 2.6), 0.07, 'iron')
    # arcadas: (nascente, raio, base do pilar, largura do pilar, profundidade)
    tiers = [(-3.9, None, -6.4, 1.2, deck_w), (-12.0, None, -depth_total, 1.7, deck_w + 0.8)]
    for ti, (zs, _, zbot, pw, pd) in enumerate(tiers):
        ztop = -1.3 if ti == 0 else -6.4
        yy = pd / 2
        if ti == 1:
            band(mb, -L / 2 - pw, L / 2 + pw, -yy, yy, -6.4, 0.35, 0.1)
        for b in range(bays + 1):
            x = -L / 2 + b * bay
            mb.box((x - pw / 2, -yy, zbot), (x + pw / 2, yy, ztop), 'ashlar' if ti == 0 else 'trim', sides='xXyY')
            if ti == 1:   # quebra-mar em cunha no pé (aparece na névoa)
                mb.box((x - pw / 2 - 0.2, -yy - 0.2, zbot), (x + pw / 2 + 0.2, yy + 0.2, -16.0), 'trim', sides='xXyYZ')
        for b in range(bays):
            u0 = -L / 2 + b * bay + pw / 2
            u1 = -L / 2 + (b + 1) * bay - pw / 2
            arc = arch_pts(u0, u1, zs, round_=True, segs=5)
            for s in (-1, 1):
                y = s * yy
                for i in range(len(arc) - 1):
                    (ua, za), (ub, zb_) = arc[i], arc[i + 1]
                    q = [(ua, y, za), (ub, y, zb_), (ub, y, ztop), (ua, y, ztop)]
                    mb.poly(list(reversed(q)) if s > 0 else q, 'ashlar' if ti == 0 else 'trim')
                # pés-direitos entre a nascente e a base do pilar já são as faces do pilar
                # aduelas (arquivolta saliente)
                for i in range(len(arc) - 1):
                    mb.beam((arc[i][0], y + s * 0.06, arc[i][1]), (arc[i + 1][0], y + s * 0.06, arc[i + 1][1]), 0.3, 'trim', 0.14)
            for i in range(len(arc) - 1):   # intradorso
                (ua, za), (ub, zb_) = arc[i], arc[i + 1]
                mb.poly([(ub, -yy, zb_), (ua, -yy, za), (ua, yy, za), (ub, yy, zb_)], 'trim')
    # cabeceiras: blocos maciços com contraforte
    for s in (-1, 1):
        xe = s * (L / 2 + abut / 2)
        mb.box((xe - abut / 2, -yd - 0.4, -depth_total), (xe + abut / 2, yd + 0.4, -1.3), 'trim', sides='xXyY')
    obj = mb.finish(MATS)
    for n, p, o in mk:
        marker(n, p, o, obj)
    return obj


# ------------------------------------------------------------------ muro de arrimo do desfiladeiro

def arcade_tier(mb, x0, x1, bays, pier_w, zbot, zspring, ztop, depth, mat='ashlar', trim='trim', piers_from=None):
    """Um andar de arcos plenos entre x0 e x1: pilares (largura pier_w), arcos nascendo em zspring,
    tímpano até ztop, intradorso e arquivolta saliente. piers_from: base dos pilares (padrão zbot)."""
    yy = depth / 2
    bay = (x1 - x0) / bays
    pb = zbot if piers_from is None else piers_from
    for b in range(bays + 1):
        x = x0 + b * bay
        mb.box((x - pier_w / 2, -yy, pb), (x + pier_w / 2, yy, ztop), mat, sides='xXyY')
    for b in range(bays):
        u0 = x0 + b * bay + pier_w / 2
        u1 = x0 + (b + 1) * bay - pier_w / 2
        arc = arch_pts(u0, u1, zspring, round_=True, segs=6)
        for sgn in (-1, 1):
            y = sgn * yy
            for i in range(len(arc) - 1):
                (ua, za), (ub, zb) = arc[i], arc[i + 1]
                q = [(ua, y, za), (ub, y, zb), (ub, y, ztop), (ua, y, ztop)]
                mb.poly(list(reversed(q)) if sgn > 0 else q, mat)
                mb.beam((ua, y + sgn * 0.08, za), (ub, y + sgn * 0.08, zb), 0.4, trim, 0.18)
            # pés-direitos (faces internas dos pilares, abaixo da nascente) já são as faces dos pilares
        for i in range(len(arc) - 1):
            (ua, za), (ub, zb) = arc[i], arc[i + 1]
            mb.poly([(ub, -yy, zb), (ua, -yy, za), (ua, yy, za), (ub, yy, zb)], trim)
    band(mb, x0 - pier_w / 2, x1 + pier_w / 2, -yy, yy, ztop, 0.4, 0.18, trim)


def great_aqueduct(name, L=30.0):
    """Segmento do Grande Aqueduto (a ponte de arcos da referência, monumental): três andares —
    2 arcos enormes, 4 médios e 8 pequenos —, canal de água no alto entre muretas com lanternas.
    Altura do canal ~24 m. Os segmentos se encaixam pelas pontas (x = ±L/2)."""
    mb = MeshBuilder(name)
    mk = []
    x0, x1 = -L / 2, L / 2
    arcade_tier(mb, x0, x1, 2, 2.4, 0.0, 7.6, 14.0, 6.0, piers_from=-4.0)
    arcade_tier(mb, x0, x1, 4, 1.5, 14.2, 17.2, 20.2, 4.6)
    arcade_tier(mb, x0, x1, 8, 0.9, 20.4, 22.0, 23.6, 3.4)
    # base dos pilares grandes (quebra-mar escalonado)
    for b in range(3):
        x = x0 + b * L / 2
        mb.box((x - 1.6, -3.4, -4.0), (x + 1.6, 3.4, 1.2), 'trim', sides='xXyYZ')
    # canal no alto: fundo, muretas e a água (o builder põe a malha de água por cima)
    zc = 23.8
    mb.box((x0, -1.7, zc - 0.2), (x1, 1.7, zc), 'ashlar', sides='Z')
    for sy in (-1, 1):
        ya, yb = (1.05, 1.7) if sy > 0 else (-1.7, -1.05)
        mb.box((x0, ya, zc), (x1, yb, zc + 1.0), 'ashlar', sides='yYZ')
        mb.box((x0, ya - 0.08, zc + 1.0), (x1, yb + 0.08, zc + 1.12), 'trim')
        for k in range(4):
            x = x0 + L * (k + 0.5) / 4
            mb.box((x - 0.3, ya - 0.12, zc), (x + 0.3, yb + 0.12, zc + 1.6), 'trim', sides='xXyYZ')
            mb.pyramid(x, (ya + yb) / 2, zc + 1.6, 0.33, 0.6, 'trim')
            if k % 2 == 0:
                mk.append(('LAMP_%d' % uid(), (x, (ya + yb) / 2, zc + 2.6), (0, sy)))
                mb.beam((x, (ya + yb) / 2, zc + 1.6), (x, (ya + yb) / 2, zc + 2.7), 0.07, 'iron')
    mb.box((x0, -1.05, zc - 0.05), (x1, 1.05, zc + 0.05), 'dark', sides='Z')   # leito escuro sob a água
    # bicas: a água escapa pelo alto em dois pontos (cachoeiras saindo da face do aqueduto)
    for u in (-L / 4, L / 4 + 1.9):
        mk.append(('FALL_%02d_%d' % (16, uid()), (u, -1.75, zc + 0.3), (0, -1)))
    obj = mb.finish(MATS)
    for n, p, o in mk:
        marker(n, p, o, obj)
    return obj


def gorge_wall(name, L=18.0, H=17.0, falls=(-4.5, 4.5), deck=5.0):
    """Muro de arrimo de pedra (frente = −Y = para o desfiladeiro), inclinado, com contrafortes,
    parapeito no alto (z = 0 é o chão da cidade) e bueiros em arco de onde caem cachoeiras."""
    mb = MeshBuilder(name)
    mk = []
    x0, x1 = -L / 2, L / 2
    batter = 1.6
    # face inclinada (mais grossa embaixo)
    mb.poly([(x0, -batter, -H), (x1, -batter, -H), (x1, 0, 0), (x0, 0, 0)], 'ashlar')
    mb.poly([(x0, 0, 0), (x1, 0, 0), (x1, deck, 0), (x0, deck, 0)], 'cobble')
    for s, xx in ((-1, x0), (1, x1)):
        q = [(xx, -batter, -H), (xx, 0, 0), (xx, deck, 0), (xx, deck, -H)]
        mb.poly(list(reversed(q)) if s > 0 else q, 'trim')
    band(mb, x0, x1, -0.1, 0.0, -0.4, 0.3, 0.12)
    # parapeito com pilaretes
    mb.box((x0, -0.1, 0), (x1, 0.45, 0.95), 'ashlar', sides='xXyYZ')
    mb.box((x0, -0.16, 0.95), (x1, 0.51, 1.07), 'trim')
    n = int(L / 4.5)
    for k in range(n + 1):
        x = x0 + L * k / n
        mb.box((x - 0.28, -0.2, 0), (x + 0.28, 0.55, 1.4), 'trim', sides='xXyYZ')
        if k % 2 == 1:
            mk.append(('LAMP_%d' % uid(), (x, 0.17, 2.2), (0, -1)))
            mb.beam((x, 0.17, 1.4), (x, 0.17, 2.4), 0.07, 'iron')
    # contrafortes
    for x in [x0 + L * (k + 0.5) / 4 for k in range(4)]:
        if any(abs(x - f) < 2.2 for f in falls):
            continue
        mb.poly([(x - 0.6, -batter - 1.0, -H), (x + 0.6, -batter - 1.0, -H), (x + 0.6, -0.15, -1.0), (x - 0.6, -0.15, -1.0)], 'trim')
        for s in (-1, 1):
            q = [(x + s * 0.6, -batter - 1.0, -H), (x + s * 0.6, -batter, -H), (x + s * 0.6, 0, -1.0), (x + s * 0.6, -0.15, -1.0)]
            mb.poly(q if s > 0 else list(reversed(q)), 'trim')
    # bueiros: arco escuro na face, a ~3,5 m abaixo do topo
    for f in falls:
        zc = -3.2
        yface = -batter * (-zc) / H
        F = Face((0, yface, 0), (1, 0, 0), (0, -1, 0))
        opening(mb, F, f, zc - 1.4, 2.2, 0.9, 'dark', t=0.3, depth=0.25, k=1.0, sill=False, round_=True)
        mk.append(('FALL_%02d_%d' % (22, uid()), (f, yface - 0.3, zc - 1.35), (0, -1)))
    obj = mb.finish(MATS)
    for n, p, o in mk:
        marker(n, p, o, obj)
    return obj


def cliff_rock(name, seed, size=(7.0, 3.2, 9.0)):
    """Penedo para revestir as encostas do cânion: fecho convexo de uma nuvem de pontos achatada,
    subdividido e deformado por ruído em duas escalas (lascas, saliências, estrias de estratificação),
    pedra com UV em escala de mundo. ~1–2 mil triângulos (antes 40: parecia caixa)."""
    import bmesh, random
    from mathutils import noise as mnoise
    rnd = random.Random(seed)
    mb = MeshBuilder(name)
    pts = []
    for _ in range(70):
        while True:
            v = V((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1)))
            if v.length <= 1: break
        v = v.normalized() * (0.72 + 0.28 * v.length)
        pts.append(mb.bm.verts.new(V((v.x * size[0] / 2, v.y * size[1] / 2, v.z * size[2] / 2))))
    bmesh.ops.convex_hull(mb.bm, input=pts)
    loose = [v for v in mb.bm.verts if not v.link_faces]
    bmesh.ops.delete(mb.bm, geom=loose, context='VERTS')
    bmesh.ops.subdivide_edges(mb.bm, edges=mb.bm.edges[:], cuts=2, use_grid_fill=True)
    bmesh.ops.triangulate(mb.bm, faces=mb.bm.faces[:])
    mb.bm.normal_update()
    off = V((seed * 1.7, seed * 0.3, seed * 2.1))
    for vv in mb.bm.verts:
        n = vv.normal
        p = vv.co * 0.35 + off
        d = mnoise.noise(p) * 0.55 + mnoise.noise(p * 3.1) * 0.18
        d += 0.12 * math.sin(vv.co.z * 2.2 + mnoise.noise(p * 0.5) * 2.0)   # estratos
        vv.co += n * d
    idx = mb.mat('rock')
    for f in mb.bm.faces:
        f.material_index = idx
        f.normal_update()
        mb._uv_face(f, 'rock')
    return mb.finish(MATS)


def bell_pavilion(name):
    """Pórtico gótico do sino da estrada (painel 01 da referência): dois pilares de pedra com base e
    capitel, arco ogival com fecho, telhadinho íngreme de ardósia com pináculos e dois estandartes
    da clave pendurados por fora dos pilares. Coordenadas = as do BellShrine (pilares em x = ±1,0;
    o sino pende de uma barra de ferro a 2,62 m; a lanterna fica por conta do script)."""
    mb = MeshBuilder(name)
    px, pw = 1.0, 0.3
    mb.box((-1.45, -0.42, 0), (1.45, 0.42, 0.18), 'trim', sides='xXyYZ')            # soco
    for sx in (-1, 1):
        x = sx * px
        mb.box((x - 0.26, -0.26, 0.18), (x + 0.26, 0.26, 0.55), 'trim', sides='xXyYZ')   # base
        mb.box((x - pw / 2, -pw / 2, 0.55), (x + pw / 2, pw / 2, 2.3), 'ashlar', sides='xXyY')
        for cx in (x - pw / 2, x + pw / 2):   # colunelos nos cantos (feixe gótico)
            for cy in (-pw / 2, pw / 2):
                mb.cylinder((cx, cy, 0.55), 0.055, 1.75, 'ashlar', segs=6, cap_top=False)
        mb.box((x - 0.24, -0.24, 2.3), (x + 0.24, 0.24, 2.48), 'trim', sides='xXyYzZ')   # capitel
        mb.box((x - pw / 2, -pw / 2, 2.48), (x + pw / 2, pw / 2, 3.55), 'ashlar', sides='xXyY')
        pinnacle(mb, x, 0, 3.55, 0.34, 1.5)
        # estandarte por fora do pilar (face lateral)
        F = Face((x + sx * pw / 2, 0, 0), (0, sx, 0), (sx, 0, 0))
        banner(mb, F, 0.0, 3.25, 0.62, 1.85)
    # arco ogival entre os pilares (face da frente e de trás) + fecho
    for sy in (-1, 1):
        y = sy * pw / 2
        arc = arch_pts(-px + pw / 2, px - pw / 2, 2.48, k=1.0, segs=6)
        for i in range(len(arc) - 1):
            (ua, za), (ub, zb) = arc[i], arc[i + 1]
            q = [(ua, y, za), (ub, y, zb), (ub, y, 3.55), (ua, y, 3.55)]
            mb.poly(list(reversed(q)) if sy > 0 else q, 'ashlar')
            mb.beam((ua, y + sy * 0.05, za), (ub, y + sy * 0.05, zb), 0.16, 'trim', 0.1)
    for i in range(len(arc) - 1):   # intradorso
        (ua, za), (ub, zb) = arc[i], arc[i + 1]
        mb.poly([(ub, -pw / 2, zb), (ua, -pw / 2, za), (ua, pw / 2, za), (ub, pw / 2, zb)], 'trim')
    band(mb, -px - pw / 2, px + pw / 2, -pw / 2, pw / 2, 3.55, 0.16, 0.08)
    # barra de ferro do sino
    mb.beam((-px + pw / 2, 0, 2.66), (px - pw / 2, 0, 2.66), 0.09, 'iron', 0.12)
    # telhadinho íngreme (cumeeira ao longo de X) com rufo
    ze, zr = 3.63, 4.75
    for sy in (-1, 1):
        q = [(-1.55, sy * 0.62, ze), (1.55, sy * 0.62, ze), (1.55, 0, zr), (-1.55, 0, zr)]
        mb.poly(q if sy < 0 else list(reversed(q)), 'roof_slate')
        mb.poly(list(reversed(q)) if sy < 0 else q, 'dark')
    for sx in (-1, 1):
        tri = [(sx * 1.5, -0.62, ze), (sx * 1.5, 0.62, ze), (sx * 1.5, 0, zr)]
        mb.poly(tri if sx > 0 else list(reversed(tri)), 'ashlar')
    mb.beam((-1.55, 0, zr + 0.05), (1.55, 0, zr + 0.05), 0.12, 'iron')
    pinnacle(mb, 0, 0, zr, 0.22, 1.1)
    return mb.finish(MATS)


def banner_pole(name):
    """Estandarte solto (praça, ponte): mastro de ferro com o pano da clave."""
    mb = MeshBuilder(name)
    mb.cylinder((0, 0, 0), 0.09, 6.2, 'iron', segs=8)
    mb.box((-0.3, -0.3, 0), (0.3, 0.3, 0.3), 'trim')
    F = Face((0, -0.02, 0), (1, 0, 0), (0, -1, 0))
    banner(mb, F, 0.0, 5.9, 1.1, 3.0)
    return mb.finish(MATS)


# ------------------------------------------------------------------ oclusão de ambiente assada (atlas)
# Cada modelo ganha um 2º mapa UV (desdobrado só para isso) e a oclusão de ambiente do Cycles é assada
# numa imagem própria (ArtSource/Campanula/ao/<nome>.png). O UV2 vai para o ladrilho do modelo num
# atlas 5×5 (Assets/Campanula/Textures/ao_atlas.png, montado no fim) deslocado de +2 em u: o shader
# Campanula/CityLit só lê o atlas quando uv2.x ≥ 2 (os outros modelos têm o UV2 de lightmap do Unity).
# dois atlas: perto (4096, ladrilhos de 1024) para os modelos detalhados; longe (2048, ladrilhos de 512)
# para as versões LOD1. u + 2 = atlas de perto, u + 4 = atlas de longe.
AO_GRID = 4
AO_NEAR = ['GHouse_A', 'GHouse_B', 'GHouse_C', 'GHouse_D', 'GTavern', 'GTower_A', 'GTower_B', 'GTower_C',
           'Aqueduct', 'Great_Aqueduct', 'Gorge_Wall', 'Bell_Pavilion']
AO_FAR = ['GHouse_A_LOD1', 'GHouse_B_LOD1', 'GHouse_C_LOD1', 'GHouse_D_LOD1', 'GTavern_LOD1',
          'GTower_A_LOD1', 'GTower_B_LOD1', 'GTower_C_LOD1']
AO_TILES = AO_NEAR + AO_FAR
AO_DIR = os.path.join(ROOT, 'ao')
BAKE_AO = '--no-ao' not in sys.argv


def bake_ao(o, name, samples=40, dist=2.4):
    if name not in AO_TILES:
        return
    far = name in AO_FAR
    res = 512 if far else 1024
    os.makedirs(AO_DIR, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = samples
    if sc.world is None:
        sc.world = bpy.data.worlds.new('W')
    sc.world.light_settings.distance = dist
    me = o.data
    # 2º UV: desdobramento automático só para a oclusão
    uv_ao = me.uv_layers.new(name='AO')
    me.uv_layers.active = uv_ao
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004, area_weight=0.0)
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')
    me = o.data
    # chão (as bases escurecem junto do piso); a ponte e os muros do cânion não têm piso embaixo
    ground = None
    if not name.startswith(('Aqueduct', 'Great_Aqueduct', 'Gorge_Wall', 'Cliff_Rock')):
        bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, 0.0))
        ground = bpy.context.object
    img = bpy.data.images.new('AO_' + name, res, res, alpha=False, float_buffer=False)
    img.generated_color = (1, 1, 1, 1)   # fundo branco: o que vazar entre ilhas (mipmap) clareia em vez de escurecer
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
    bpy.ops.object.bake(type='AO', margin=12, use_clear=False)
    path = os.path.join(AO_DIR, name + '.png')
    img.filepath_raw = path; img.file_format = 'PNG'; img.save()
    for nt, n in nodes: nt.nodes.remove(n)
    if ground is not None: bpy.data.objects.remove(ground, do_unlink=True)
    # UV2 para o ladrilho do atlas (+2 em u = "este modelo tem oclusão assada")
    i = (AO_FAR if far else AO_NEAR).index(name)
    tx, ty = i % AO_GRID, i // AO_GRID
    ts = 1.0 / AO_GRID
    base = 4.0 if far else 2.0
    uv_ao = o.data.uv_layers['AO']   # (a referência antiga fica inválida depois de trocar de modo)
    for loop in uv_ao.data:
        u, v = loop.uv
        loop.uv = (base + (tx + 0.015 + u * 0.97) * ts, (ty + 0.015 + v * 0.97) * ts)
    o.data.uv_layers.active = o.data.uv_layers[0]
    print('AO', name, path)


def build_atlas():
    import subprocess
    for names, size, fname in ((AO_NEAR, 4096, 'ao_atlas.png'), (AO_FAR, 2048, 'ao_atlas_far.png')):
        tile = size // AO_GRID
        args = ['convert', '-size', '%dx%d' % (size, size), 'xc:white']
        for i, n in enumerate(names):
            p = os.path.join(AO_DIR, n + '.png')
            if not os.path.exists(p): continue
            tx, ty = i % AO_GRID, i // AO_GRID
            x, y = tx * tile, size - (ty + 1) * tile      # (v cresce para cima no UV, para baixo na imagem)
            args += ['(', p, '-resize', '%dx%d!' % (tile, tile), '-blur', '0x0.8', ')', '-geometry', '+%d+%d' % (x, y), '-composite']
        out = os.path.join(ROOT, '..', '..', 'Assets', 'Campanula', 'Textures', fname)
        args += ['-colorspace', 'gray', '-depth', '8', out]
        subprocess.run(args, check=True)
        print('ATLAS', out)


def main():
    global MATS
    builds = [
        ('GHouse_A', lambda: ghouse('GHouse_A', 6.0, 7.0, [3.2, 3.0, 2.9], shed='right', turret_side='left', banners=1)),
        ('GHouse_B', lambda: ghouse('GHouse_B', 5.0, 6.0, [3.1, 2.9, 2.8, 2.7], balcony=True, roof_rise=5.2)),
        ('GHouse_C', lambda: ghouse('GHouse_C', 7.0, 5.5, [3.4, 3.0], shed='left', banners=2, roof_rise=5.0)),
        ('GHouse_D', lambda: ghouse('GHouse_D', 6.5, 7.5, [3.1, 3.0, 2.9], balcony=True, turret_side='right')),
        ('GTavern', lambda: ghouse('GTavern', 10.0, 8.0, [3.2, 3.2, 3.0], balcony=True, sign=True, shed='left', banners=2, turret_side='right', roof_rise=6.5)),
        ('GTower_A', lambda: gtower('GTower_A', 3.0, 22.0, 'spire')),
        ('GTower_B', lambda: gtower('GTower_B', 2.6, 17.0, 'dome')),
        ('GTower_C', lambda: gtower('GTower_C', 2.2, 13.0, 'spire')),
        ('Aqueduct', lambda: aqueduct('Aqueduct')),
        ('Gorge_Wall', lambda: gorge_wall('Gorge_Wall')),
        ('Banner_Clef', lambda: banner_pole('Banner_Clef')),
        ('Bell_Pavilion', lambda: bell_pavilion('Bell_Pavilion')),
        ('Great_Aqueduct', lambda: great_aqueduct('Great_Aqueduct')),
        ('Cliff_Rock_A', lambda: cliff_rock('Cliff_Rock_A', 11)),
        ('Cliff_Rock_B', lambda: cliff_rock('Cliff_Rock_B', 23, (6.0, 2.6, 7.5))),
        ('Cliff_Rock_C', lambda: cliff_rock('Cliff_Rock_C', 37, (8.5, 3.6, 6.0))),
    ]
    only = sys.argv[sys.argv.index('--') + 1].split(',') if '--' in sys.argv else None
    total = 0
    global DETAIL
    LODS = ('GHouse_A', 'GHouse_B', 'GHouse_C', 'GHouse_D', 'GTavern', 'GTower_A', 'GTower_B', 'GTower_C')
    for name, fn in builds:
        if only and name not in only:
            continue
        for lod in ((0, 1) if name in LODS else (0,)):
            DETAIL = lod == 0
            clear_scene(); MATS = mats()
            o = fn()
            out = name if lod == 0 else name + '_LOD1'
            o.name = out
            if BAKE_AO:
                bake_ao(o, out)
            total += export([o] + list(o.children), out)
        DETAIL = True
    print('TOTAL TRIS', total)
    if BAKE_AO:
        build_atlas()


if __name__ == '__main__':   # (o kit_town.py importa as funções daqui sem rodar o kit inteiro)
    main()
