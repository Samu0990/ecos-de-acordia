"""Atlas das folhas das árvores de Campanula (cartões de folhas com recorte), renderizado no Blender.

Metade esquerda: um ramo de carvalho (galhinho + ~40 folhas lobadas, dobradas na nervura, cada uma
num tom). Metade direita: um ramo de pinheiro (galhinho + ~220 agulhas em leque). Câmera ortográfica
de cima, fundo transparente (alfa = recorte). Dois passes:
  · cor (emissão = cor da folha, sem luz: é albedo; folhas de baixo um pouco mais escuras);
  · normal (emissão = normal do mundo × 0,5 + 0,5; com a câmera olhando −Z o espaço tangente do
    cartão é o próprio mundo: T = +X, B = +Y, N = +Z), salvo sem conversão de cor.
Depois o RGB é "empurrado" para dentro das áreas transparentes (sem borda preta nos mipmaps).

Rodar: ~/.local/opt/blender-5.2/blender -b --factory-startup --python ArtSource/Campanula/scripts/leaf_atlas.py
Saída: Assets/Campanula/Textures/leaf_atlas.png e leaf_atlas_normal.png (2048×1024).
"""
import bpy, bmesh, math, random, os, subprocess
import numpy as np
from mathutils import Vector, Matrix

V = Vector
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.abspath(os.path.join(ROOT, '..', '..', 'Assets', 'Campanula', 'Textures'))
W, H = 2048, 1024


def new_obj(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def leaf_shape(bm, col_layer, center, rot, length, width, color, lobes=True, fold=0.18, tilt=(0, 0)):
    """Folha lobada (carvalho): tira com nervura central (dobra em V), contorno com lobos, pecíolo."""
    n = 16
    R = Matrix.Rotation(rot, 3, 'Z') @ Matrix.Rotation(tilt[0], 3, 'X') @ Matrix.Rotation(tilt[1], 3, 'Y')
    L, M, Rr = [], [], []
    for i in range(n + 1):
        t = i / n
        y = t * length
        wv = math.sin(math.pi * min(t, 0.999)) ** 0.75 * (0.85 + 1.2 * t * (1 - t)) * width * 0.5
        if lobes and 0.1 < t < 0.95:
            wv *= 1.0 + 0.24 * math.sin(t * math.pi * 7.0)
        M.append(bm.verts.new(center + R @ V((0, y, fold * width * 0.25))))
        L.append(bm.verts.new(center + R @ V((-wv, y, -wv * fold))))
        Rr.append(bm.verts.new(center + R @ V((wv, y, -wv * fold))))
    for i in range(n):
        for a, b, c, d in ((L[i], M[i], M[i + 1], L[i + 1]), (M[i], Rr[i], Rr[i + 1], M[i + 1])):
            try:
                bm.faces.new((a, b, c, d))
            except ValueError:
                pass
    p0 = center + R @ V((0, -length * 0.12, 0)); p1 = center + R @ V((0, 0.02, 0))
    sv = R @ V((0.012 * length, 0, 0))
    bm.faces.new((bm.verts.new(p0 - sv), bm.verts.new(p0 + sv), bm.verts.new(p1 + sv), bm.verts.new(p1 - sv)))
    return color


def twig(bm, pts, r0, r1):
    """Galhinho achatado (visto de cima basta uma fita)."""
    prev = None
    for i in range(len(pts) - 1):
        a, b = V(pts[i]), V(pts[i + 1])
        d = (b - a).normalized()
        s = V((-d.y, d.x, 0))
        ra = r0 + (r1 - r0) * i / (len(pts) - 1); rb = r0 + (r1 - r0) * (i + 1) / (len(pts) - 1)
        vs = [bm.verts.new(a - s * ra), bm.verts.new(a + s * ra), bm.verts.new(b + s * rb), bm.verts.new(b - s * rb)]
        bm.faces.new(vs)


def material(name, color_attr=True):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for nd in list(nt.nodes): nt.nodes.remove(nd)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    em = nt.nodes.new('ShaderNodeEmission')
    nt.links.new(em.outputs[0], out.inputs['Surface'])
    return m, nt, em


def build():
    random.seed(4)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    objs = []
    # ---------------- carvalho (metade esquerda: x ∈ [-1, 0], y ∈ [-0.5, 0.5])
    bm = bmesh.new()
    leaves_c = []
    stem = [(-0.5, -0.47, 0), (-0.5, -0.25, 0.01), (-0.49, 0.0, 0.02), (-0.5, 0.2, 0.03), (-0.52, 0.36, 0.04)]
    tw = bmesh.new()
    twig(tw, stem, 0.012, 0.005)
    # raminhos laterais
    for k in range(6):
        t = 0.15 + k * 0.13
        y = -0.47 + t * 0.85
        side = -1 if k % 2 else 1
        twig(tw, [(-0.5, y, 0.02), (-0.5 + side * 0.13, y + 0.1, 0.03), (-0.5 + side * 0.22, y + 0.2, 0.03)], 0.006, 0.003)
    o_tw = new_obj('galhos_carvalho', tw)
    objs.append(('bark', o_tw))
    for i in range(52):
        # folhas ao longo do galho e nas pontas dos raminhos, em camadas de altura (dentro da metade)
        y = random.uniform(-0.3, 0.3)
        x = -0.5 + random.gauss(0, 0.15)
        x = max(-0.8, min(-0.2, x))
        z = random.uniform(0.0, 0.25)
        ang = -(x + 0.5) * 3.0 + random.uniform(-0.8, 0.8) + (math.pi if random.random() < 0.12 else 0)
        L = random.uniform(0.15, 0.22); Wd = L * random.uniform(0.5, 0.62)
        g = random.uniform(0.0, 1.0)
        # verde-escuro de carvalho com algumas folhas mais amareladas/oliva
        col = (0.055 + 0.05 * g, 0.105 + 0.06 * g, 0.03 + 0.012 * g)
        if random.random() < 0.18: col = (0.11, 0.12, 0.035)
        shade = 0.7 + 0.3 * (z / 0.25)
        col = tuple(c * shade for c in col)
        lb = bmesh.new()
        leaf_shape(lb, None, V((x, y, z)), ang, L, Wd, col, True, 0.22, (random.uniform(-0.25, 0.25), random.uniform(-0.3, 0.3)))
        o = new_obj('folha_%d' % i, lb)
        objs.append((col, o))
    # ---------------- pinheiro (metade direita: x ∈ [0, 1])
    tw = bmesh.new()
    spine = [(0.5, -0.46, 0), (0.5, -0.2, 0.01), (0.51, 0.1, 0.02), (0.5, 0.34, 0.03)]
    twig(tw, spine, 0.011, 0.004)
    for k in range(5):
        y = -0.36 + k * 0.15
        for side in (-1, 1):
            twig(tw, [(0.5, y, 0.02), (0.5 + side * 0.17, y + 0.14, 0.02), (0.5 + side * 0.28, y + 0.26, 0.02)], 0.005, 0.002)
    o_tw = new_obj('galhos_pinheiro', tw)
    objs.append(('bark', o_tw))
    nb = bmesh.new()
    branches = [[V(spine[0]), V(spine[-1])]]
    for k in range(5):
        y = -0.36 + k * 0.15
        for side in (-1, 1):
            branches.append([V((0.5, y, 0.02)), V((0.5 + side * 0.28, y + 0.26, 0.02))])
    needles = []
    for br in branches:
        a, b = br
        L = (b - a).length
        cnt = int(L * 260)
        d = (b - a).normalized()
        for j in range(cnt):
            t = j / cnt
            p = a.lerp(b, t)
            for side in (-1, 1):
                ang = side * random.uniform(0.5, 1.05)
                dd = Matrix.Rotation(ang, 3, 'Z') @ d
                ln = random.uniform(0.07, 0.11) * (1.0 - 0.35 * t)
                q = p + dd * ln + V((0, 0, random.uniform(0.0, 0.04)))
                s = V((-dd.y, dd.x, 0)) * 0.0035
                v0 = nb.verts.new(p - s); v1 = nb.verts.new(p + s); v2 = nb.verts.new(q)
                nb.faces.new((v0, v1, v2))
    o_n = new_obj('agulhas', nb)
    objs.append(('needles', o_n))
    return objs


def setup_render(objs, normal_pass):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = 24
    sc.cycles.use_denoising = False
    sc.render.film_transparent = True
    sc.render.resolution_x, sc.render.resolution_y = W, H
    sc.render.resolution_percentage = 100
    sc.render.filter_size = 0.9
    sc.view_settings.view_transform = 'Raw' if normal_pass else 'Standard'
    sc.view_settings.look = 'None'
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.render.image_settings.color_depth = '8'
    cam = bpy.data.objects.get('cam')
    if cam is None:
        cd = bpy.data.cameras.new('cam'); cd.type = 'ORTHO'; cd.ortho_scale = 2.0
        cam = bpy.data.objects.new('cam', cd); sc.collection.objects.link(cam)
        cam.location = (0, 0, 5); cam.rotation_euler = (0, 0, 0)
        sc.camera = cam
    for key, o in objs:
        o.data.materials.clear()
        m, nt, em = material('m_' + o.name)
        if normal_pass:
            geo = nt.nodes.new('ShaderNodeNewGeometry')
            mul = nt.nodes.new('ShaderNodeVectorMath'); mul.operation = 'MULTIPLY_ADD'
            mul.inputs[1].default_value = (0.5, 0.5, 0.5); mul.inputs[2].default_value = (0.5, 0.5, 0.5)
            # o lado de trás da folha (se aparecer) usa a normal virada para a câmera
            nt.links.new(geo.outputs['Normal'], mul.inputs[0])
            nt.links.new(mul.outputs[0], em.inputs['Color'])
        else:
            if key == 'bark': c = (0.09, 0.06, 0.035)
            elif key == 'needles': c = (0.035, 0.075, 0.04)
            else: c = key
            em.inputs['Color'].default_value = (*c, 1)
        o.data.materials.append(m)
        if normal_pass:
            for p in o.data.polygons: p.use_smooth = True


def dilate(path):
    """Empurra a cor das folhas para as áreas transparentes (mipmaps sem halo escuro)."""
    raw = subprocess.run(['convert', path, '-depth', '8', 'rgba:-'], capture_output=True, check=True).stdout
    a = np.frombuffer(raw, np.uint8).reshape(H, W, 4).astype(np.float32)
    rgb, al = a[..., :3], a[..., 3:] / 255.0
    acc, wsum = rgb * al, al.copy()
    out = rgb.copy()
    filled = al[..., 0] > 0.5
    cur_rgb, cur_w = acc, wsum
    for it in range(9):
        k = 2 ** it
        pad = lambda x: np.pad(x, ((k, k), (k, k), (0, 0)), mode='edge')
        pr, pw = pad(cur_rgb), pad(cur_w)
        nr = (pr[:-2 * k or None, k:-k or None] + pr[2 * k:, k:-k or None] + pr[k:-k or None, :-2 * k or None] + pr[k:-k or None, 2 * k:] + cur_rgb)
        nw = (pw[:-2 * k or None, k:-k or None] + pw[2 * k:, k:-k or None] + pw[k:-k or None, :-2 * k or None] + pw[k:-k or None, 2 * k:] + cur_w)
        cur_rgb, cur_w = nr, nw
        m = (~filled) & (cur_w[..., 0] > 1e-3)
        out[m] = (cur_rgb[m] / cur_w[m])
        filled = filled | m
    a[..., :3] = out
    subprocess.run(['convert', '-size', f'{W}x{H}', '-depth', '8', 'rgba:-', path], input=a.clip(0, 255).astype(np.uint8).tobytes(), check=True)


def main():
    objs = build()
    sc = bpy.context.scene
    setup_render(objs, False)
    p1 = os.path.join(OUT, 'leaf_atlas.png')
    sc.render.filepath = p1
    bpy.ops.render.render(write_still=True)
    setup_render(objs, True)
    p2 = os.path.join(OUT, 'leaf_atlas_normal.png')
    sc.render.filepath = p2
    bpy.ops.render.render(write_still=True)
    dilate(p1)
    dilate(p2)
    print('atlas ok', p1, p2)


main()
