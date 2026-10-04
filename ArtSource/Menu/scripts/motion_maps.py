"""Mapas de movimento do fundo da tela inicial ("motion" estilo After Effects, mas em tempo
real no shader Hidden/Aren/UITitleMotion). Tudo em pixels da arte (1672x941, y para baixo).

motion_a.png (RGBA, metade da resolução, filtro bilinear)
  R  paralaxe: 0.5 = plano do painel (parado); >0.5 perto da câmera (pedras das bordas, chão,
     estalactites); <0.5 longe (arquitetura ao fundo). A câmera "passeia" e cada pixel anda
     proporcional a isso — o truque do Displacement Map/2.5D do After Effects.
  G  lanterna em gaiola da esquerda (pêndulo no pivô da corrente)
  B  lustre da direita (pêndulo)
  A  estandarte vermelho da direita (onda descendo o pano)
motion_b.png
  R  ar quente tremendo acima das chamas
  G  cruz pendurada no teto, à direita (pêndulo curto)
  B  luz das velas da direita no cenário (pulsa com a tremulação)
  A  luz das velas da esquerda
O painel (com brasão, pináculos, correntes e estandarte de baixo) tem paralaxe zero e nenhuma
máscara: ele e os botões ficam parados e nítidos enquanto o mundo se move atrás.
"""
import numpy as np

W, H = 1672, 941

# contorno do painel (caixas que cobrem moldura, brasão, pináculos, correntes e estandarte)
PANEL_BOXES = [
    (528, 150, 1147, 780),     # corpo
    (733, 25, 930, 230),       # brasão + estandarte do topo
    (712, 80, 740, 160), (930, 80, 958, 160),          # agulhas ao lado do brasão
    (526, 125, 595, 290), (1078, 125, 1148, 290),      # pináculos de cima
    (526, 720, 595, 810), (1078, 720, 1148, 810),      # pináculos de baixo
    (585, 770, 1085, 818),     # correntes penduradas sob o painel
    (795, 740, 875, 897),      # ornamento de baixo + estandarte
]

# pêndulos: pivô (onde a corrente some no teto) e caixas do que balança
LANTERN_PIVOT = (340, 40)
LANTERN_BOXES = [(334, 40, 347, 152), (290, 145, 390, 328)]
CHANDELIER_PIVOT = (1275, 0)
CHANDELIER_BOXES = [(1269, 0, 1282, 332), (1203, 322, 1347, 458)]
CROSS_PIVOT = (1421, 0)
CROSS_BOXES = [(1404, 0, 1440, 92)]
BANNER_BOX = (1550, 342, 1632, 618)
BANNER_TOP = 345


def box_blur(img, r):
    if r <= 0:
        return img.copy()
    p = np.pad(img, ((r, r), (r, r)), mode='edge')
    c = np.cumsum(p, axis=0)
    c = np.concatenate([np.zeros_like(c[:1]), c], axis=0)
    p = (c[2 * r + 1:] - c[:-2 * r - 1]) / (2 * r + 1)
    c = np.cumsum(p, axis=1)
    c = np.concatenate([np.zeros_like(c[:, :1]), c], axis=1)
    return (c[:, 2 * r + 1:] - c[:, :-2 * r - 1]) / (2 * r + 1)


def blur(img, r, passes=3):
    for _ in range(passes):
        img = box_blur(img, r)
    return img


def boxes_mask(boxes, grow=0):
    m = np.zeros((H, W), np.float32)
    for x0, y0, x1, y1 in boxes:
        m[max(0, y0 - grow):min(H, y1 + 1 + grow), max(0, x0 - grow):min(W, x1 + 1 + grow)] = 1
    return m


def soft(mask, grow, ramp):
    """1 dentro de (máscara crescida `grow` px), caindo a 0 em ~`ramp` px para fora."""
    g = blur(mask, max(1, grow // 3), 3) > 0.02 if grow > 0 else mask > 0.5
    r = max(1, ramp // 4)
    return np.clip(blur(g.astype(np.float32), r, 3) * 2.0, 0, 1)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def ellipse(cx, cy, rx, ry):
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    d = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2
    return np.clip(1 - d, 0, 1)


def build(lights):
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    # ---------------- paralaxe
    # fundo distante recua; bordas da composição, chão e teto vêm para perto da câmera
    edge = smoothstep(0, 1, (np.abs(x - 836) - 400) / 436)
    floor = smoothstep(690, 941, y)
    ceil = 1 - smoothstep(0, 175, y)
    p = -0.5 + 1.35 * edge + 1.1 * floor + 0.85 * ceil
    # elementos do meio: plataforma e escada (meio-termo), estátua e torre distantes (fundo)
    p = p * (1 - ellipse(200, 640, 190, 70)) + 0.25 * ellipse(200, 640, 190, 70)
    p = p * (1 - ellipse(1400, 400, 90, 190)) - 0.2 * ellipse(1400, 400, 90, 190)
    p = p * (1 - ellipse(340, 190, 70, 150)) + 0.4 * ellipse(340, 190, 70, 150)     # lanterna
    p = p * (1 - ellipse(1275, 330, 90, 160)) + 0.35 * ellipse(1275, 330, 90, 160)  # lustre
    p = np.clip(p, -0.6, 1.0)
    p = blur(p, 9, 3)
    panel = boxes_mask(PANEL_BOXES)
    keep = soft(panel, 8, 46)          # 1 no painel e logo em volta → paralaxe zero
    p = p * (1 - keep)

    # ---------------- máscaras dos pêndulos (crescidas além do objeto: quando balança, o
    # objeto não pode encostar na borda da máscara, senão é cortado)
    lantern = soft(boxes_mask(LANTERN_BOXES), 14, 14)
    chand = soft(boxes_mask(CHANDELIER_BOXES), 12, 14)
    cross = soft(boxes_mask(CROSS_BOXES), 10, 10)
    banner = soft(boxes_mask([BANNER_BOX]), 8, 10)
    for m in (lantern, chand, cross, banner):
        m *= (1 - keep)

    # ---------------- ar quente acima das chamas e luz das velas no cenário
    heat = np.zeros((H, W), np.float32)
    light_r = np.zeros((H, W), np.float32)
    light_l = np.zeros((H, W), np.float32)
    for (lx, ly, lr, li, lt) in lights:
        if lt != 0:
            continue
        if li >= 0.6:
            heat = np.maximum(heat, ellipse(lx, ly - 15, 6 + lr * 0.15, 15 + lr * 0.4) * li)
        g = np.exp(-(((x - lx) ** 2 + (y - ly) ** 2) / (2 * (lr * 3.2) ** 2))) * li
        if lx > 836:
            light_r += g
        else:
            light_l += g
    heat = np.clip(heat * 1.2, 0, 1) * (1 - keep)
    light_r = np.clip(light_r * 0.8, 0, 1) * (1 - keep)
    light_l = np.clip(light_l * 0.8, 0, 1) * (1 - keep)

    ma = np.dstack([0.5 + 0.5 * p, lantern, chand, banner]).astype(np.float32)
    mb = np.dstack([heat, cross, light_r, light_l]).astype(np.float32)
    # motion_c: R = pode mexer (tudo menos o painel), G = fundo distante (o clarão violeta da
    # badalada acende só a arquitetura lá atrás), B/A livres
    far = np.clip(-p / 0.35, 0, 1) * (1 - keep)
    mc = np.dstack([1 - keep, blur(far, 6, 2), np.zeros_like(p), np.ones_like(p)]).astype(np.float32)

    def half(img):
        img = np.concatenate([img, img[-1:]], axis=0) if img.shape[0] % 2 else img
        return img.reshape(img.shape[0] // 2, 2, W // 2, 2, 4).mean(axis=(1, 3))

    def at(px, py):
        ix, iy = int(np.clip(px, 0, W - 1)), int(np.clip(py, 0, H - 1))
        return ma[iy, ix], mb[iy, ix]

    per_light = []
    for (lx, ly, *_rest) in lights:
        va, vb = at(lx, ly)
        per_light.append({'p': round(float(va[0] * 2 - 1), 4), 'g1': round(float(va[1]), 4),
                          'g2': round(float(va[2]), 4), 'g3': round(float(vb[1]), 4)})
    meta = {
        'pivots': [{'x': LANTERN_PIVOT[0], 'y': LANTERN_PIVOT[1]}, {'x': CHANDELIER_PIVOT[0], 'y': CHANDELIER_PIVOT[1]},
                   {'x': CROSS_PIVOT[0], 'y': CROSS_PIVOT[1]}],
        'bannerTop': BANNER_TOP,
        # olhos da estátua encapuzada e pontas de estalactite que pingam (x, y, chão onde a gota cai)
        'eyes': [{'x': ex, 'y': 244, 'p': round(float(p[244, ex]), 4)} for ex in (1405, 1412)],
        'drips': [dict(d, p=round(float(p[d['y'], d['x']]), 4)) for d in (
            {'x': 245, 'y': 162, 'land': 600}, {'x': 210, 'y': 128, 'land': 601}, {'x': 288, 'y': 84, 'land': 600},
            {'x': 372, 'y': 72, 'land': 846}, {'x': 512, 'y': 112, 'land': 868}, {'x': 1535, 'y': 115, 'land': 726},
            {'x': 1585, 'y': 165, 'land': 722}, {'x': 1565, 'y': 72, 'land': 724}, {'x': 1162, 'y': 50, 'land': 760})],
    }
    return half(ma), half(mb), half(mc), per_light, meta, p
