"""Sussurrante — etapa 5: texturas finais na paleta da prancha do autor.

Paleta da prancha (PALETA): quase preto #1b1c22, ardósia #2c2f3c, cinza #5b5d5c, bege #b9ab98,
violeta #5c4588. Pele bege-acinzentada MANCHADA de ardósia com rachaduras; cachecol índigo;
saia azul-carvão com tom roxo; corda arroxeada; fendas violetas acesas no peito e no braço
esquerdo (relâmpagos) — "Ressonância Distorcida".

1. Bake (Cycles, EMIT) de máscaras 3D em coordenadas de objeto direto no atlas do Tripo (sem
   emenda nas costuras): manchas (ruído), rachaduras (Voronoi distância à borda), veias,
   fendas acesas (só nas regiões certas), cobertura do atlas e a região de cada vértice
   (atributo Region gravado pela etapa 3).
2. Composição em numpy: gradação da cor do Tripo para a paleta, por região/material.
3. Saída em Assets/Aren/Enemies/Sussurrante/Textures:
   Sussurrante_Albedo.png (sRGB), Sussurrante_Normal.png (OpenGL), Sussurrante_Mask.png
   (linear: R = AO, G = suavidade, B = brilho das fendas, A = rede de veias que acende no
   aviso de golpe/grito).

Rodar: blender -b --factory-startup --python ArtSource/Sussurrante/scripts/suss_05_textures.py
"""
import bpy, os, sys, math
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import suss_common as C

RES = 2048
os.makedirs(C.OUT_TEX, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=os.path.join(C.WORK, "suss_uv.blend"))
low = bpy.data.objects["Sussurrante"]
for m in low.modifiers:
    m.show_render = m.show_viewport = False


# ---------------------------------------------------------------- 1. máscaras 3D
def mask_material():
    m = bpy.data.materials.new("MaskBake")
    m.use_nodes = True
    nt = m.node_tree; N = nt.nodes; L = nt.links
    for n in list(N):
        N.remove(n)
    out = N.new("ShaderNodeOutputMaterial")
    em = N.new("ShaderNodeEmission")
    L.new(em.outputs[0], out.inputs[0])
    tc = N.new("ShaderNodeTexCoord")
    img = N.new("ShaderNodeTexImage")
    N.active = img
    return m, nt, em, tc, img


def node(nt, kind, **kw):
    n = nt.nodes.new(kind)
    for k, v in kw.items():
        if k.startswith("in_"):
            n.inputs[k[3:].replace("_", " ")].default_value = v
        else:
            setattr(n, k, v)
    return n


def math_node(nt, op, a, b=None, clamp=False):
    n = nt.nodes.new("ShaderNodeMath"); n.operation = op; n.use_clamp = clamp
    L = nt.links
    if hasattr(a, "bl_idname") or not isinstance(a, (int, float)):
        L.new(a, n.inputs[0])
    else:
        n.inputs[0].default_value = a
    if b is not None:
        if not isinstance(b, (int, float)):
            L.new(b, n.inputs[1])
        else:
            n.inputs[1].default_value = b
    return n.outputs[0]


def smooth(nt, x, e0, e1):
    n = nt.nodes.new("ShaderNodeMapRange")
    n.interpolation_type = 'SMOOTHSTEP'
    n.inputs["From Min"].default_value = e0
    n.inputs["From Max"].default_value = e1
    nt.links.new(x, n.inputs["Value"])
    return n.outputs["Result"]


mat, nt, em, tc, img_node = mask_material()
L = nt.links
obj = tc.outputs["Object"]
sep = nt.nodes.new("ShaderNodeSeparateXYZ"); L.new(obj, sep.inputs[0])
X, Y, Z = sep.outputs[0], sep.outputs[1], sep.outputs[2]

# manchas: ruído fBm em 3D, limiar suave (manchas grandes e irregulares)
nz = node(nt, "ShaderNodeTexNoise", in_Scale=2.6, in_Detail=6.0, in_Roughness=0.62)
L.new(obj, nz.inputs["Vector"])
blotch = smooth(nt, nz.outputs["Fac"], 0.44, 0.555)
# linhas orgânicas: "ridge" de um ruído com distorção (1 - |2n - 1|): curvas finas que
# serpenteiam e se ramificam, como rachadura de barro / raio — sem o desenho de favo do Voronoi
def ridge_lines(scale, distortion, e0, e1, detail=4.0, offset=(0.0, 0.0, 0.0)):
    mp = nt.nodes.new("ShaderNodeMapping"); mp.inputs["Location"].default_value = offset
    L.new(obj, mp.inputs["Vector"])
    n = node(nt, "ShaderNodeTexNoise", in_Scale=scale, in_Detail=detail, in_Roughness=0.55, in_Distortion=distortion)
    L.new(mp.outputs[0], n.inputs["Vector"])
    r = math_node(nt, 'SUBTRACT', 1.0, math_node(nt, 'ABSOLUTE', math_node(nt, 'SUBTRACT', math_node(nt, 'MULTIPLY', n.outputs["Fac"], 2.0), 1.0)))
    return smooth(nt, r, e0, e1)


# rachaduras escuras: esparsas (um 2º ruído decide onde aparecem)
crack_line = ridge_lines(3.2, 0.6, 0.955, 0.992)
nz2 = node(nt, "ShaderNodeTexNoise", in_Scale=3.0, in_Detail=2.0)
L.new(obj, nz2.inputs["Vector"])
cracks = math_node(nt, 'MULTIPLY', crack_line, smooth(nt, nz2.outputs["Fac"], 0.44, 0.60))
# veias: mais finas e densas (acendem no aviso de golpe)
vein_line = ridge_lines(6.5, 0.9, 0.965, 0.995, offset=(3.1, 1.7, 0.4))
nz3 = node(nt, "ShaderNodeTexNoise", in_Scale=1.7, in_Detail=2.0)
L.new(obj, nz3.inputs["Vector"])
veins = math_node(nt, 'MULTIPLY', vein_line, smooth(nt, nz3.outputs["Fac"], 0.40, 0.58))

# fendas acesas ("relâmpagos" da prancha): antebraço/mão ESQUERDOS (x < 0) e ramos no peito
glow_line = ridge_lines(5.5, 1.4, 0.95, 0.99, detail=6.0, offset=(7.3, 2.2, 5.1))
left_arm = math_node(nt, 'MULTIPLY', smooth(nt, X, -0.40, -0.47), math_node(nt, 'MULTIPLY', smooth(nt, Z, 0.98, 1.06), smooth(nt, Z, 1.80, 1.30)))   # mais forte perto da mão
chest = math_node(nt, 'MULTIPLY', smooth(nt, math_node(nt, 'ABSOLUTE', X), 0.17, 0.07),
                  math_node(nt, 'MULTIPLY', smooth(nt, Z, 1.80, 1.88), math_node(nt, 'MULTIPLY', smooth(nt, Z, 2.24, 2.12), smooth(nt, Y, -0.02, 0.06))))
regions_glow = math_node(nt, 'MAXIMUM', left_arm, math_node(nt, 'MULTIPLY', chest, 0.6))
glow = math_node(nt, 'MULTIPLY', glow_line, regions_glow)

attr = nt.nodes.new("ShaderNodeAttribute"); attr.attribute_name = "Region"
attr_sep = nt.nodes.new("ShaderNodeSeparateColor"); L.new(attr.outputs["Color"], attr_sep.inputs[0])
region_code = attr_sep.outputs[0]


def combine(r, g, b):
    c = nt.nodes.new("ShaderNodeCombineColor")
    for i, s in enumerate((r, g, b)):
        if isinstance(s, (int, float)):
            c.inputs[i].default_value = s
        else:
            L.new(s, c.inputs[i])
    return c.outputs[0]


low.data.materials.clear()
low.data.materials.append(mat)
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 4
sc.render.bake.margin = 16
bpy.ops.object.select_all(action='DESELECT')
low.select_set(True); bpy.context.view_layer.objects.active = low


FORCE = "--rebake" in sys.argv


def bake_to(name, color_socket):
    cache = os.path.join(C.WORK, name + ".png")
    if os.path.exists(cache) and not FORCE:
        im = bpy.data.images.load(cache)
        im.colorspace_settings.name = 'Non-Color'
        arr = np.empty(RES * RES * 4, dtype=np.float32)
        im.pixels.foreach_get(arr)
        print("MASCARA (cache)", name)
        return arr.reshape(RES, RES, 4)
    im = bpy.data.images.new(name, RES, RES, alpha=False)
    im.colorspace_settings.name = 'Non-Color'
    img_node.image = im
    nt.nodes.active = img_node
    L.new(color_socket, em.inputs["Color"])
    bpy.ops.object.bake(type='EMIT')
    im.filepath_raw = cache; im.file_format = 'PNG'; im.save()
    arr = np.empty(RES * RES * 4, dtype=np.float32)
    im.pixels.foreach_get(arr)
    print("MASCARA", name)
    return arr.reshape(RES, RES, 4)


mA = bake_to("mask_a", combine(blotch, cracks, veins))
mB = bake_to("mask_b", combine(glow, 1.0, region_code))
sc.render.bake.margin = 0
mC = bake_to("mask_c", combine(1.0, 1.0, 1.0))       # cobertura do atlas (sem margem)


def load(path):
    im = bpy.data.images.load(path)
    im.colorspace_settings.name = 'Non-Color'      # valores crus (sRGB codificado na cor)
    a = np.empty(im.size[0] * im.size[1] * 4, dtype=np.float32)
    im.pixels.foreach_get(a)
    return a.reshape(im.size[1], im.size[0], 4)


col = load(os.path.join(C.WORK, "tripo_color.png"))[..., :3]
orm = load(os.path.join(C.WORK, "tripo_orm.png"))
ao = load(os.path.join(C.WORK, "bake_ao.png"))[..., 0]
nrm = load(os.path.join(C.WORK, "bake_normal.png"))[..., :3]


# ---------------------------------------------------------------- 2. composição
def sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def rgb_to_hsv(c):
    r, g, b = c[..., 0], c[..., 1], c[..., 2]
    mx = c.max(-1); mn = c.min(-1); d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    rc = np.where(m & (mx == r), ((g - b) / np.maximum(d, 1e-6)) % 6, 0)
    gc = np.where(m & (mx == g), (b - r) / np.maximum(d, 1e-6) + 2, 0)
    bc = np.where(m & (mx == b), (r - g) / np.maximum(d, 1e-6) + 4, 0)
    h = np.where(m & (mx == r), rc, np.where(m & (mx == g), gc, np.where(m, bc, 0))) * 60.0
    s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0)
    return h, s, mx


def hexc(h):
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (1, 3, 5)], dtype=np.float32)


def lerp(a, b, t):
    return a + (b - a) * t[..., None]


H, S, Vv = rgb_to_hsv(col)
lum = col @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
code = np.rint(mB[..., 2] * 8).astype(np.int32)
cover = mC[..., 0] > 0.5
blot, crk, vein = mA[..., 0], mA[..., 1], mA[..., 2]
glow3d = mB[..., 0]

hue_d = lambda c: np.minimum(np.abs(H - c), 360 - np.abs(H - c))
glow_px = (hue_d(275) < 35) & (S > 0.30) & (Vv > 0.42)
rope_px = (hue_d(30) < 22) & (S > 0.28) & (Vv > 0.12) & (Vv < 0.65)
teal_t = sstep(45, 15, hue_d(188)) * sstep(0.10, 0.30, S) * sstep(0.12, 0.25, Vv)
skin_region = (code >= 1) & (code <= 4)
cloth_region = code >= 5
# tecido do Tripo: roxo-acinzentado/azul-marinho (matiz 215-320) ou bem escuro; pele: cinza claro
# quase sem cor OU mancha azul-petróleo (matiz ~188). A região só desempata os cinzas médios.
# medido por região (histograma matiz x luminância do atlas): tecido = quase preto (lum < 0,1,
# matiz 210-300, saturado); pele = matiz 20-215 (cinza esverdeado + manchas azul-petróleo), inclusive
# nas costas, que o Tripo pintou mais escuras; corda = matiz < 60 e saturação alta na região da saia
glow_px = (hue_d(270) < 35) & (S > 0.30) & (lum > 0.30)
rope_px = (code == 6) & (H < 60) & (S > 0.40) & (lum > 0.08)
skin_px = (H >= 20) & (H <= 215) & (lum > 0.085) & ~rope_px & ~glow_px
cloth_px = ~skin_px & ~glow_px & ~rope_px

BEIGE = hexc("#b9ab98"); GREY = hexc("#5b5d5c"); SLATE = hexc("#2c2f3c"); NEAR = hexc("#1b1c22")
VIOLET = hexc("#5c4588")
out = col.copy()

# pele: tom do Tripo (sombreado/oclusão pintados) remapeado para cinza -> bege
t = np.clip((lum - 0.07) / 0.46, 0, 1) ** 0.85
skin = lerp(lerp(NEAR, GREY, np.clip(t * 2, 0, 1)), BEIGE, np.clip(t * 2 - 1, 0, 1))
skin = lerp(skin, SLATE * 1.08, teal_t * 0.92)                     # manchas do Tripo -> ardósia
head = code == 1
BONE = hexc("#a69c8e")
skin = np.where(head[..., None], lerp(skin, BONE * (0.55 + 0.5 * t)[..., None], 0.55 * np.ones_like(t)), skin)   # osso velho, não branco
skin = lerp(skin, SLATE * 0.95, blot * np.where(head, 0.45, 0.80))   # manchas de ardósia (como na prancha)
skin = skin * (1 - 0.55 * crk)[..., None]                          # rachaduras
skin = lerp(skin, VIOLET * 0.55, vein * 0.30)                      # veias (sutil; acendem no shader)
out = np.where(skin_px[..., None], skin, out)

# tecidos (dobras = luminância do Tripo)
fold = np.clip(lum / 0.28, 0, 1.4)[..., None]
SCARF = np.array([0.20, 0.14, 0.31], dtype=np.float32)
SKIRT = np.array([0.125, 0.12, 0.185], dtype=np.float32)
BAND = np.array([0.17, 0.12, 0.23], dtype=np.float32)
cloth = np.where((code == 5)[..., None], SCARF * (0.35 + 0.85 * fold),
                 np.where((code == 6)[..., None], SKIRT * (0.40 + 0.85 * fold), BAND * (0.40 + 0.85 * fold)))
out = np.where(cloth_px[..., None], cloth, out)
# corda: castanho arroxeado
ROPE = np.array([0.27, 0.165, 0.185], dtype=np.float32)
out = np.where(rope_px[..., None], ROPE * (0.45 + 0.9 * np.clip(lum / 0.35, 0, 1.3))[..., None], out)
# fendas acesas: violeta claro no albedo (a emissão vem da máscara)
out = np.where(glow_px[..., None], lerp(VIOLET, np.array([0.80, 0.62, 1.0], dtype=np.float32), sstep(0.45, 0.9, Vv)), out)
out = np.clip(out, 0, 1)
out = np.where(cover[..., None], out, col)                         # fora do atlas: intacto

# máscara: R = AO, G = suavidade, B = brilho, A = veias
rough = orm[..., 1]
smooth_ = np.where(skin_px, 0.30, np.where(rope_px, 0.18, 0.12)) * (0.7 + 0.6 * (1 - rough))
glow_m = np.maximum(glow_px * sstep(0.40, 0.75, Vv), glow3d * skin_px)
mask = np.stack([ao, np.clip(smooth_, 0, 1), np.clip(glow_m, 0, 1), np.clip(vein * skin_px + crk * 0.12 * skin_px, 0, 1)], -1)

# normal: pixels inválidos do bake (raio pegou a outra face de tiras finas) -> plano
nz_ = nrm[..., 2]
bad = nz_ < 0.38
print("NORMAL pixels ruins", int(bad.sum()))
nrm_fix = np.where(bad[..., None], np.array([0.5, 0.5, 1.0], dtype=np.float32), nrm)


def save(name, arr, srgb):
    h, w = arr.shape[:2]
    ch = arr.shape[2]
    im = bpy.data.images.new(name, w, h, alpha=(ch == 4))
    im.colorspace_settings.name = 'sRGB' if srgb else 'Non-Color'
    rgba = np.ones((h, w, 4), dtype=np.float32)
    rgba[..., :ch] = arr
    im.pixels.foreach_set(rgba.reshape(-1))
    im.filepath_raw = os.path.join(C.OUT_TEX, name + ".png")
    im.file_format = 'PNG'
    if ch == 4:
        im.alpha_mode = 'STRAIGHT'
    im.save()
    print("SALVO", im.filepath_raw)


save("Sussurrante_Albedo", out, True)
save("Sussurrante_Normal", nrm_fix, False)
save("Sussurrante_Mask", mask, False)
print("pele %.1f%% tecido %.1f%% corda %.2f%% brilho %.2f%%" % (
    100 * (skin_px & cover).mean(), 100 * (cloth_px & cover).mean(), 100 * (rope_px & cover).mean(), 100 * (glow_m > 0.3).mean()))
print("OK")
