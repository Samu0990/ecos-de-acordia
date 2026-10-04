"""Tela inicial de Ecos de Acordia a partir da arte de referência do autor
(ArtSource/Menu/title_reference.webp, 1672x941).

Gera em Assets/Aren/Resources/UI/Title/:
  title_bg.png        a arte inteira com o miolo do painel LIMPO onde ficavam os botões
                      (textura do painel sintetizada a partir do próprio painel)
  btn_normal.png      placa de botão apagada (do SAIR), sem texto, com alfa
  btn_active.png      placa vermelha selecionada (do JOGAR), sem texto, com alfa
  lbl_*.png           os textos JOGAR / CONFIGURAÇÕES / SAIR recortados da arte (branco + alfa)
  title_mask.png      máscara das letras de ECOS DE ACORDIA (brilho que passa pelo título)
  glow.png            brilho em pixel art (anéis quantizados + pontilhado Bayer)
  fog.png             névoa em pixel art, repetível na horizontal
  title_layout.json   coordenadas (px da arte) de cada peça e das chamas para o TitleScreen

Rodar: ~/.local/opt/blender-5.2/5.2/python/bin/python3.13 ArtSource/Menu/scripts/build_title.py
(python do Blender por causa do numpy; E/S de imagem via ImageMagick, ver imgio.py)
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import imgio  # noqa: E402
import motion_maps  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
SRC = os.path.join(ROOT, 'ArtSource', 'Menu', 'title_reference.webp')
OUT = os.path.join(ROOT, 'Assets', 'Aren', 'Resources', 'UI', 'Title')
DEBUG = os.environ.get('TITLE_DEBUG')   # pasta para imagens de conferência
rng = np.random.default_rng(7)

# ------------------------------------------------------------------ geometria (px da arte)
# miolo do painel (entre os filetes dourados internos da moldura)
IN_X0, IN_X1 = 580, 1091
# caixas das placas como estão na referência (x0, y0, x1, y1 inclusivos)
S1 = (588, 467, 1082, 557)    # JOGAR (selecionado: placa vermelha com joias)
S2 = (614, 571, 1056, 646)    # CONFIGURAÇÕES (apagado)
S3 = (614, 659, 1056, 734)    # SAIR (apagado)
# miolo de cada placa onde fica o texto (para remover/recortar as letras)
T1 = (705, 492, 965, 536)
T2 = (683, 590, 990, 628)
T3 = (705, 678, 965, 716)
# letras do título
TITLE = (598, 222, 1074, 386)


def lum(a):
    return a[..., 0] * 0.299 + a[..., 1] * 0.587 + a[..., 2] * 0.114


def box_blur(img, r):
    """Desfoque de caixa separável (cumsum), bordas replicadas; img 2D ou 3D."""
    if r <= 0:
        return img.copy()
    pad = [(r, r), (r, r)] + [(0, 0)] * (img.ndim - 2)
    p = np.pad(img, pad, mode='edge')
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


def dilate(m, r):
    if r <= 0:
        return m.copy()
    return box_blur(m.astype(np.float32), r) > 1e-4


def erode(m, r):
    return ~dilate(~m, r)


def flood_exterior(open_mask):
    """Pixels de open_mask ligados à borda (4-vizinhança)."""
    h, w = open_mask.shape
    ext = np.zeros_like(open_mask)
    st = [(y, x) for y in range(h) for x in (0, w - 1) if open_mask[y, x]]
    st += [(y, x) for x in range(w) for y in (0, h - 1) if open_mask[y, x]]
    for y, x in st:
        ext[y, x] = True
    while st:
        y, x = st.pop()
        for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= yy < h and 0 <= xx < w and open_mask[yy, xx] and not ext[yy, xx]:
                ext[yy, xx] = True
                st.append((yy, xx))
    return ext


def rect_mask(shape, r, pad=0):
    m = np.zeros(shape, bool)
    m[r[1] - pad:r[3] + 1 + pad, r[0] - pad:r[2] + 1 + pad] = True
    return m


def dbg(name, img):
    if DEBUG:
        os.makedirs(DEBUG, exist_ok=True)
        imgio.save(os.path.join(DEBUG, name), img)


# ------------------------------------------------------------------ 1. painel limpo
def synth_fill(img, hole, clean, P=14, O=5, cands=260):
    """Preenche `hole` com retalhos do próprio painel (quilting simples): blocos PxP
    sobrepostos em O px, cada um escolhido entre `cands` candidatos limpos pelo menor
    erro na parte já conhecida do bloco, colado com borda suavizada."""
    out = img.copy()
    known = ~hole
    H, W = hole.shape
    # posições onde um retalho PxP inteiro é limpo (imagem integral)
    ci = np.pad(np.cumsum(np.cumsum(clean.astype(np.int32), 0), 1), ((1, 0), (1, 0)))
    full = ci[P:, P:] - ci[:-P, P:] - ci[P:, :-P] + ci[:-P, :-P]
    sy, sx = np.nonzero(full == P * P)
    print('  retalhos limpos disponíveis:', len(sy))
    ys, xs = np.nonzero(hole)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    step = P - O
    # peso de borda suave para a colagem
    ramp = np.minimum(np.arange(P) + 1, np.arange(P)[::-1] + 1).astype(np.float32)
    ramp = np.clip(ramp / O, 0, 1)
    wpatch = np.minimum.outer(ramp, ramp)[..., None]
    acc = np.zeros_like(out)
    wacc = np.zeros(hole.shape + (1,), np.float32)
    # brilho-alvo suave dentro do buraco (convolução normalizada só dos pixels conhecidos)
    kn_f = known.astype(np.float32)
    tmap = blur(lum(img) * kn_f, 16) / np.maximum(blur(kn_f, 16), 1e-4)
    smap = blur(lum(img), 6)
    for by in range(y0 - O, y1 + 1, step):
        for bx in range(x0 - O, x1 + 1, step):
            ya, xa = max(by, 0), max(bx, 0)
            yb, xb = min(by + P, H), min(bx + P, W)
            hb = hole[ya:yb, xa:xb]
            if not hb.any():
                continue
            # conhecido = fora do buraco ou já preenchido por blocos anteriores
            tgt = np.where(wacc[ya:yb, xa:xb] > 0, acc[ya:yb, xa:xb] / np.maximum(wacc[ya:yb, xa:xb], 1e-6), out[ya:yb, xa:xb])
            kn = (known[ya:yb, xa:xb] | (wacc[ya:yb, xa:xb, 0] > 0)).astype(np.float32)[..., None]
            idx = rng.integers(0, len(sy), cands)
            best, bi = 1e9, idx[0]
            tgain = tmap[ya:yb, xa:xb].mean()
            for i in idx:
                p = img[sy[i]:sy[i] + (yb - ya), sx[i]:sx[i] + (xb - xa)]
                if p.shape[:2] != hb.shape:
                    continue
                p = p * (tgain / max(smap[sy[i] + P // 2, sx[i] + P // 2], 1e-3))
                e = float((((p - tgt) ** 2) * kn).sum()) / (kn.sum() + 1.0)
                if e < best:
                    best, bi = e, i
            p = img[sy[bi]:sy[bi] + (yb - ya), sx[bi]:sx[bi] + (xb - xa)]
            p = p * (tgain / max(smap[sy[bi] + P // 2, sx[bi] + P // 2], 1e-3))
            wp = wpatch[ya - by:yb - by, xa - bx:xb - bx]
            acc[ya:yb, xa:xb] += p * wp
            wacc[ya:yb, xa:xb] += wp
    filled = acc / np.maximum(wacc, 1e-6)
    out[hole] = filled[hole]
    # tom de baixa frequência: casa o brilho local do preenchimento com o painel em volta
    l_out = lum(out)
    kn_f = known.astype(np.float32)
    target = blur(l_out * kn_f, 10) / np.maximum(blur(kn_f, 10), 1e-4)
    hf = hole.astype(np.float32)
    cur = blur(l_out * hf, 10) / np.maximum(blur(hf, 10), 1e-4)
    gain = np.clip(target / np.maximum(cur, 1e-4), 0.75, 1.3)
    out[hole] *= gain[hole][:, None]
    # costura: mistura suave de 2 px na borda do buraco
    soft = np.clip(blur(hole.astype(np.float32), 1, 2), 0, 1)[..., None]
    return img * (1 - soft) + out * soft


def clean_panel(a):
    L = lum(a)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    H, W = L.shape
    interior = np.zeros((H, W), bool)
    interior[150:890, IN_X0 + 2:IN_X1 - 1] = True
    plates = rect_mask(L.shape, S1, 4) | rect_mask(L.shape, S2, 4) | rect_mask(L.shape, S3, 4)
    # faíscas laranja soltas em volta do botão selecionado (serão animadas no jogo)
    sparks = (r - b > 0.1) & (r > 0.2) & ~plates
    band = np.zeros_like(sparks)
    band[436:762, IN_X0 + 2:IN_X1 - 1] = True
    sparks &= band
    # não apagar o ornamento do divisor nem os filetes da moldura
    divider = np.zeros_like(sparks)
    divider[430:462, 700:975] = True
    sparks &= ~divider
    sparks = dilate(sparks, 3)
    hole = (plates | sparks) & interior
    feature = (L > 0.11) | ((r - g > 0.05) & (r > 0.12))
    clean = interior & ~dilate(feature, 3) & ~dilate(hole, 2)
    dbg('hole.png', np.dstack([hole, clean * 0.5, np.zeros_like(L)]).astype(np.float32))
    out = synth_fill(a, hole, clean)
    return out, hole


# ------------------------------------------------------------------ 2. placas e textos
def plate_alpha(crop):
    """Silhueta da placa (ornamentos dourados + miolo) sobre o painel escuro."""
    L = lum(crop)
    r, g = crop[..., 0], crop[..., 1]
    feat = (L > 0.12) | ((r - g > 0.07) & (r > 0.14))
    m = dilate(feat, 2)
    m = ~flood_exterior(~m)          # fecha os vãos internos (miolo escuro da placa)
    m = erode(m, 1)
    a = np.clip(blur(m.astype(np.float32), 1, 2) * 1.25, 0, 1)
    return a


def remove_text(crop, box):
    """Apaga as letras (e a sombra escura delas) do miolo de uma placa: o retângulo do
    texto inteiro é refeito linha a linha — interpolação entre as bordas limpas à
    esquerda e à direita + a textura (resíduo de alta frequência) de faixas limpas
    vizinhas repetidas em vaivém, então o degradê vertical do miolo continua igual."""
    x0, y0, x1, y1 = box
    L = lum(crop)
    seg = L[y0:y1 + 1, x0:x1 + 1]
    base = np.median(seg, axis=1, keepdims=True)
    text = seg > base + 0.05
    ys, xs = np.nonzero(text)
    rx0, rx1 = x0 + xs.min() - 4, x0 + xs.max() + 4
    ry0, ry1 = y0 + ys.min() - 3, y0 + ys.max() + 3
    lowf = blur(crop, 2, 2)
    out = crop.copy()
    strip = 44
    lsrc = np.arange(rx0 - strip - 2, rx0 - 2)
    rsrc = np.arange(rx1 + 3, rx1 + strip + 3)
    w = rx1 - rx0
    for yy in range(ry0, ry1 + 1):
        left = lowf[yy, lsrc].mean(0)
        right = lowf[yy, rsrc].mean(0)
        for xx in range(rx0, rx1 + 1):
            t = (xx - rx0) / max(w, 1)
            smooth = left * (1 - t) + right * t
            k = xx - rx0
            ping = k % (2 * strip)
            ping = ping if ping < strip else 2 * strip - 1 - ping
            src = lsrc[ping] if t < 0.5 else rsrc[::-1][ping]
            resid = crop[yy, src] - lowf[yy, src]
            out[yy, xx] = smooth + resid
    m = np.zeros(L.shape, np.float32)
    m[ry0:ry1 + 1, rx0:rx1 + 1] = 1
    soft = np.clip(blur(m, 1, 2), 0, 1)[..., None]
    soft = np.maximum(soft * 1.0, m[..., None] * 0.0)
    return crop * (1 - soft) + out * soft


def key_text(orig, clean, box):
    """Alfa das letras: quanto o pixel original é mais claro que a placa sem texto."""
    x0, y0, x1, y1 = box
    lo = lum(orig[y0:y1 + 1, x0:x1 + 1])
    lc = lum(clean[y0:y1 + 1, x0:x1 + 1])
    d = lo - lc
    hi = np.percentile(d[d > 0.05], 92) if (d > 0.05).any() else 0.3
    alpha = np.clip((d - 0.025) / max(hi - 0.025, 1e-3), 0, 1)
    col = orig[y0:y1 + 1, x0:x1 + 1][d >= hi].mean(0)   # cor onde o alfa satura (= cor real da letra)
    ys, xs = np.nonzero(alpha > 0.03)
    by0, by1, bx0, bx1 = ys.min() - 2, ys.max() + 2, xs.min() - 2, xs.max() + 2
    a = alpha[by0:by1 + 1, bx0:bx1 + 1]
    return a, (x0 + bx0, y0 + by0), col


def crop(a, r, pad=0):
    return a[r[1] - pad:r[3] + 1 + pad, r[0] - pad:r[2] + 1 + pad].copy()


def rgba(rgb, alpha):
    return np.dstack([rgb, alpha])


def white(alpha):
    return np.dstack([np.ones(alpha.shape + (3,), np.float32), alpha])


# ------------------------------------------------------------------ 3. efeitos em pixel art
BAYER4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], np.float32) / 16.0


def make_glow(n=32, levels=5):
    y, x = np.mgrid[0:n, 0:n] + 0.5
    d = np.hypot(x - n / 2, y - n / 2) / (n / 2)
    v = np.clip(1 - d, 0, 1) ** 1.8
    q = v * levels
    dith = BAYER4[(np.arange(n)[:, None] % 4), (np.arange(n)[None, :] % 4)]
    q = np.floor(q + dith * 0.999) / levels
    return white(np.clip(q, 0, 1).astype(np.float32))


def make_fog(w=160, h=48, levels=4):
    """Névoa em fiapos posterizados (sem pontilhado: em área grande o Bayer vira tela de
    mosquiteiro). Ruído fractal periódico em x (repete sem emenda), some em cima e embaixo."""
    acc = np.zeros((h, w), np.float32)
    amp, tot = 1.0, 0.0
    for octave, (gx, gy) in enumerate([(4, 2), (9, 4), (18, 8)]):
        grid = rng.random((gy + 1, gx)).astype(np.float32)
        ys = np.linspace(0, gy, h, endpoint=False)
        xs = np.linspace(0, gx, w, endpoint=False)
        yi, xi = np.floor(ys).astype(int), np.floor(xs).astype(int)
        fy, fx = ys - yi, xs - xi
        fy = fy * fy * (3 - 2 * fy)
        fx = fx * fx * (3 - 2 * fx)
        a00 = grid[yi][:, xi]
        a01 = grid[yi][:, (xi + 1) % gx]
        a10 = grid[np.minimum(yi + 1, gy)][:, xi]
        a11 = grid[np.minimum(yi + 1, gy)][:, (xi + 1) % gx]
        v = (a00 * (1 - fx) + a01 * fx) * (1 - fy[:, None]) + (a10 * (1 - fx) + a11 * fx) * fy[:, None]
        acc += v * amp
        tot += amp
        amp *= 0.5
    acc /= tot
    vert = np.sin(np.linspace(0, np.pi, h)) ** 0.8
    v = np.clip((acc * vert[:, None] - 0.3) / 0.38, 0, 1)
    v = np.floor(v * levels + 0.5) / levels
    return white(v.astype(np.float32))


def title_mask(a):
    c = crop(a, TITLE)
    L = lum(c)
    m = np.clip((L - 0.24) / 0.22, 0, 1)
    return white(m.astype(np.float32))


# chamas e brilhos (px da arte): x, y, raio do halo, intensidade, tipo
# tipo 0 = vela/chama (tremula), 1 = brasa vermelha (pulsa devagar), 2 = joia/emblema
LIGHTS = [
    # lustre da direita
    (1274, 350, 26, 1.0, 0), (1281, 368, 14, 0.7, 0), (1266, 367, 14, 0.7, 0),
    # velas do altar da estátua
    (1348, 513, 16, 0.8, 0), (1409, 529, 18, 0.9, 0), (1458, 510, 16, 0.8, 0), (1483, 516, 12, 0.6, 0),
    (1382, 544, 13, 0.7, 0), (1395, 554, 13, 0.7, 0), (1362, 560, 11, 0.55, 0), (1406, 544, 11, 0.6, 0),
    (1422, 546, 11, 0.6, 0), (1432, 541, 11, 0.55, 0),
    # vela da escada
    (1203, 632, 14, 0.75, 0),
    # velas do canto inferior direito
    (1484, 675, 20, 1.0, 0), (1484, 688, 14, 0.6, 0), (1482, 655, 10, 0.5, 0), (1512, 689, 16, 0.85, 0),
    (1512, 700, 10, 0.45, 0), (1540, 686, 16, 0.85, 0), (1537, 702, 10, 0.5, 0), (1526, 709, 14, 0.8, 0),
    (1576, 698, 16, 0.85, 0), (1573, 712, 9, 0.4, 0), (1499, 714, 10, 0.45, 0), (1507, 723, 9, 0.4, 0),
    (1519, 723, 9, 0.4, 0),
    # lanterna pendurada e varanda da esquerda
    (340, 266, 18, 0.75, 0), (342, 275, 10, 0.5, 0), (426, 453, 12, 0.6, 0), (433, 446, 10, 0.5, 0),
    # vela do canto inferior esquerdo
    (420, 807, 20, 1.0, 0), (421, 821, 12, 0.55, 0), (388, 839, 11, 0.55, 0), (447, 842, 9, 0.45, 0),
]


def find_embers(a):
    """Brasas vermelhas apagadas do cenário (velas distantes, plantas do chão): pulsam devagar."""
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    m = (r > 0.26) & (r - g > 0.13) & (r - b > 0.1) & (lum(a) < 0.3)
    m[0:941, 520:1152] = False
    pts = []
    taken = np.zeros_like(m)
    ys, xs = np.nonzero(m)
    order = np.argsort(-r[ys, xs])
    for i in order:
        y, x = ys[i], xs[i]
        if taken[y, x]:
            continue
        taken[max(0, y - 14):y + 15, max(0, x - 14):x + 15] = True
        if any(abs(x - lx) < 14 and abs(y - ly) < 14 for lx, ly, *_ in LIGHTS):
            continue
        pts.append((int(x), int(y), 9, 0.35, 1))
        if len(pts) >= 26:
            break
    return pts


# ------------------------------------------------------------------ principal
def main():
    os.makedirs(OUT, exist_ok=True)
    a = imgio.load(SRC)[..., :3]
    H, W = a.shape[:2]
    print('referência', W, 'x', H)

    print('1. painel limpo')
    bg, hole = clean_panel(a)
    imgio.save(os.path.join(OUT, 'title_bg.png'), bg)
    dbg('bg_center.png', np.repeat(np.repeat(crop(bg, (560, 430, 1110, 770)), 2, 0), 2, 1))

    print('2. placas')
    PAD = 4
    act = crop(a, S1, PAD)
    act_clean = remove_text(act, (T1[0] - S1[0] + PAD, T1[1] - S1[1] + PAD, T1[2] - S1[0] + PAD, T1[3] - S1[1] + PAD))
    act_a = plate_alpha(act)
    nor = crop(a, S3, PAD)
    nor_clean = remove_text(nor, (T3[0] - S3[0] + PAD, T3[1] - S3[1] + PAD, T3[2] - S3[0] + PAD, T3[3] - S3[1] + PAD))
    nor_a = plate_alpha(nor)
    imgio.save(os.path.join(OUT, 'btn_active.png'), rgba(act_clean, act_a))
    imgio.save(os.path.join(OUT, 'btn_normal.png'), rgba(nor_clean, nor_a))
    dbg('plates.png', np.repeat(np.repeat(np.concatenate([
        np.pad(rgba(act_clean, act_a), ((0, 0), (0, 0), (0, 0))),
        np.pad(rgba(nor_clean, nor_a), ((0, act.shape[0] - nor.shape[0]), (0, act.shape[1] - nor.shape[1]), (0, 0)))], 0), 2, 0), 2, 1))

    print('3. textos')
    # placa limpa posicionada sobre cada slot da arte original, para recortar as letras
    full_act = a.copy()
    full_act[S1[1] - PAD:S1[3] + 1 + PAD, S1[0] - PAD:S1[2] + 1 + PAD] = act_clean
    # CONFIGURAÇÕES: a placa apagada limpa (vinda do SAIR) alinhada no slot 2
    best = None
    for dy in range(-3, 4):
        for dx in range(-3, 4):
            y0 = S2[1] - PAD + dy
            x0 = S2[0] - PAD + dx
            reg = a[y0:y0 + nor.shape[0], x0:x0 + nor.shape[1]]
            m = np.ones(nor.shape[:2], bool)
            m[T2[1] - S2[1]:T2[3] - S2[1] + 2 * PAD, :] = False   # ignora a faixa do texto
            e = float((((reg - nor) ** 2).sum(-1))[m].mean())
            if best is None or e < best[0]:
                best = (e, dx, dy)
    print('  alinhamento placa 2:', best)
    full_n2 = a.copy()
    y0 = S2[1] - PAD + best[2]
    x0 = S2[0] - PAD + best[1]
    full_n2[y0:y0 + nor.shape[0], x0:x0 + nor.shape[1]] = nor_clean
    full_n3 = a.copy()
    full_n3[S3[1] - PAD:S3[3] + 1 + PAD, S3[0] - PAD:S3[2] + 1 + PAD] = nor_clean

    labels = {}
    for name, clean_full, box in (('jogar', full_act, T1), ('config', full_n2, T2), ('sair', full_n3, T3)):
        alpha, (lx, ly), col = key_text(a, clean_full, box)
        imgio.save(os.path.join(OUT, 'lbl_%s.png' % name), white(alpha))
        labels[name] = {'x': int(lx), 'y': int(ly), 'w': int(alpha.shape[1]), 'h': int(alpha.shape[0]),
                        'color': [round(float(c), 4) for c in col]}
        print('  ', name, labels[name])
        dbg('lbl_%s.png' % name, np.repeat(np.repeat(white(alpha), 3, 0), 3, 1))

    print('4. efeitos')
    imgio.save(os.path.join(OUT, 'glow.png'), make_glow())
    imgio.save(os.path.join(OUT, 'fog.png'), make_fog())
    imgio.save(os.path.join(OUT, 'title_mask.png'), title_mask(a))
    embers = find_embers(a)
    print('  brasas:', len(embers))

    print('5. mapas de movimento do fundo')
    ma, mb, per_light, motion_meta, par = motion_maps.build(LIGHTS + embers)
    imgio.save(os.path.join(OUT, 'motion_a.png'), ma)
    imgio.save(os.path.join(OUT, 'motion_b.png'), mb)
    dbg('parallax.png', np.dstack([np.clip(par, 0, 1), np.zeros_like(par), np.clip(-par, 0, 1) * 1.5]) * 0.8 + a * 0.35)
    dbg('masks.png', np.clip(a * 0.5 + np.dstack([ma[..., 1].repeat(2, 0).repeat(2, 1)[:H], ma[..., 2].repeat(2, 0).repeat(2, 1)[:H], ma[..., 3].repeat(2, 0).repeat(2, 1)[:H]]) * 0.5
                                + np.dstack([mb[..., 0].repeat(2, 0).repeat(2, 1)[:H]] * 3) * 0.5, 0, 1))

    # posições inteiras (canto superior esquerdo, px da arte) para casar a grade de pixels
    a_tl = (S1[0] - PAD, S1[1] - PAD)
    n3_tl = (S3[0] - PAD, S3[1] - PAD)
    n2_tl = (S2[0] - PAD + best[1], S2[1] - PAD + best[2])
    # slot 1 apagado: centro da placa apagada no centro da placa vermelha (arredonda para baixo)
    n1_tl = (int(a_tl[0] + (act.shape[1] - nor.shape[1]) // 2), int(a_tl[1] + (act.shape[0] - nor.shape[0] + 1) // 2))
    off = (a_tl[0] - n1_tl[0], a_tl[1] - n1_tl[1])
    lab_order = ['jogar', 'config', 'sair']
    layout = {
        'width': W, 'height': H,
        'activeW': int(act.shape[1]), 'activeH': int(act.shape[0]),
        'normalW': int(nor.shape[1]), 'normalH': int(nor.shape[0]),
        'activeOffX': int(off[0]), 'activeOffY': int(off[1]),
        'slots': [{'x': int(t[0]), 'y': int(t[1]), 'label': n} for t, n in zip((n1_tl, n2_tl, n3_tl), lab_order)],
        'labels': [{'name': n, 'x': labels[n]['x'], 'y': labels[n]['y'], 'w': labels[n]['w'], 'h': labels[n]['h'],
                    'r': labels[n]['color'][0], 'g': labels[n]['color'][1], 'b': labels[n]['color'][2]} for n in lab_order],
        # joias das pontas da placa vermelha, relativas ao canto da placa
        'gems': [{'x': 599 - a_tl[0], 'y': 512 - a_tl[1]}, {'x': 1071 - a_tl[0], 'y': 513 - a_tl[1]}],
        'title': {'x0': TITLE[0], 'y0': TITLE[1], 'x1': TITLE[2] + 1, 'y1': TITLE[3] + 1},
        'panel': {'x0': 528, 'y0': 22, 'x1': 1146, 'y1': 905},
        'emblem': {'x': 836, 'y': 116},
        'lights': [dict({'x': l[0], 'y': l[1], 'r': l[2], 'i': l[3], 't': l[4]}, **m) for l, m in zip(LIGHTS + embers, per_light)],
        'pivots': motion_meta['pivots'],
        'bannerTop': motion_meta['bannerTop'],
    }
    with open(os.path.join(OUT, 'title_layout.json'), 'w') as f:
        json.dump(layout, f, indent=1)
    print('ok ->', OUT)


main()
