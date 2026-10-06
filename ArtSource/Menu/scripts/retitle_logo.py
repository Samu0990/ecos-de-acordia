"""Troca o logotipo da arte da tela inicial para o título do cânone (Bíblia v2): "ECOS DO CONTRACANTO".

A arte original do autor (title_bg.png, gerada por build_title.py) trazia "Ecos de Acordia — A Ruptura do
Contracanto"; na Bíblia v2 Acordia deixou de ser o nome do mundo (agora é Elyndra). Este script NÃO altera o
original: cobre a área do texto com o próprio fundo do painel (copiado de baixo, com borda suavizada) e
escreve o novo título em Cinzel (SIL OFL) com o mesmo tratamento dourado em pixel art, salvando
title_bg_elyndra.png ao lado. Rodar com o python3 do sistema (PIL):
    python3 ArtSource/Menu/scripts/retitle_logo.py
"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
SRC = os.path.join(ROOT, 'Assets/Aren/Resources/UI/Title/title_bg.png')
OUT = os.path.join(ROOT, 'Assets/Aren/Resources/UI/Title/title_bg_elyndra.png')
FONT = os.path.join(ROOT, 'Assets/Aren/Resources/UI/Fonts/Cinzel.ttf')
GARA = os.path.join(ROOT, 'Assets/Aren/Resources/UI/Fonts/EBGaramond.ttf')

img = Image.open(SRC).convert('RGBA')
W, H = img.size
x0, y0, x1, y1 = 598, 226, 1070, 424          # área do texto antigo
sy = 468                                       # miolo vazio do painel (onde ficavam os botões)

# 1) cobre o texto antigo com o fundo do painel, borda em degradê para não marcar costura
patch = img.crop((x0, sy, x1, sy + (y1 - y0)))
mask = Image.new('L', patch.size, 255)
d = ImageDraw.Draw(mask)
f = 14
for i in range(f):
    a = int(255 * (i + 1) / (f + 1))
    d.rectangle([i, i, patch.size[0] - 1 - i, patch.size[1] - 1 - i], outline=a)
img.paste(patch, (x0, y0), mask)


def gold_text(text, font, cx, cy, top=(238, 200, 128), bottom=(140, 88, 44), outline=(20, 12, 8), shadow=True):
    tmp = Image.new('L', (W, H), 0)
    dd = ImageDraw.Draw(tmp)
    bb = dd.textbbox((0, 0), text, font=font)
    tw, th = bb[2] - bb[0], bb[3] - bb[1]
    ox, oy = int(cx - tw / 2 - bb[0]), int(cy - th / 2 - bb[1])
    dd.text((ox, oy), text, font=font, fill=255)
    # pixel art: alfa binário (bordas duras, como as letras da arte original)
    tmp = tmp.point(lambda v: 255 if v > 110 else 0)
    ol = tmp.filter(ImageFilter.MaxFilter(5))
    if shadow:
        sh = ol.copy().transform(ol.size, Image.AFFINE, (1, 0, -3, 0, 1, -4))
        img.paste(Image.new('RGBA', (W, H), (5, 3, 2, 200)), (0, 0), sh)
    img.paste(Image.new('RGBA', (W, H), outline + (255,)), (0, 0), ol)
    # degradê vertical + filete claro no topo das letras
    grad = Image.new('RGBA', (W, H))
    gp = grad.load()
    yA, yB = oy + bb[1], oy + bb[1] + th
    for y in range(max(0, yA - 2), min(H, yB + 3)):
        t = min(1, max(0, (y - yA) / max(1, th)))
        t = t * t * (3 - 2 * t)
        c = tuple(int(top[k] * (1 - t) + bottom[k] * t) for k in range(3))
        for x in range(max(0, ox - 4), min(W, ox + tw + bb[0] + 6)):
            gp[x, y] = c + (255,)
    img.paste(grad, (0, 0), tmp)
    hl = ImageChops.subtract(tmp, tmp.transform(tmp.size, Image.AFFINE, (1, 0, 0, 0, 1, -2)))
    img.paste(Image.new('RGBA', (W, H), (255, 236, 190, 255)), (0, 0), hl)
    return tw


big = ImageFont.truetype(FONT, 56)
small = ImageFont.truetype(FONT, 42)
try:
    big.set_variation_by_axes([700]); small.set_variation_by_axes([600])
except Exception:
    pass
sub = ImageFont.truetype(GARA, 25)
cx = (x0 + x1) // 2
gold_text("ECOS DO", small, cx, 276)
tw = gold_text("CONTRACANTO", big, cx, 336)
# subtítulo e filetes (como o original)
dd = ImageDraw.Draw(img)
st = 'Campânula canta. Aren não.'
bb = dd.textbbox((0, 0), st, font=sub)
sw = bb[2] - bb[0]
dd.text((cx - sw / 2 - bb[0] + 1, 398 - bb[1] + 1), st, font=sub, fill=(10, 6, 4, 255))
dd.text((cx - sw / 2 - bb[0], 398 - bb[1]), st, font=sub, fill=(200, 168, 120, 255))
for side in (-1, 1):
    xa = cx + side * (sw / 2 + 14)
    xb = cx + side * (sw / 2 + 70)
    yy = 398 + (bb[3] - bb[1]) // 2 + 2
    dd.line([(min(xa, xb), yy), (max(xa, xb), yy)], fill=(120, 60, 40, 255), width=2)
    dd.polygon([(xb, yy - 4), (xb + side * 6, yy), (xb, yy + 4), (xb - side * 6, yy)], fill=(150, 80, 50, 255))
img.save(OUT)
print('ok', OUT)

# máscara do brilho que passa pelas letras (mesma regra do build_title.py: luminância das letras douradas)
TITLE = (598, 222, 1074, 386)
c = img.crop(TITLE).convert('RGB')
m = Image.new('RGBA', c.size)
px, mp = c.load(), m.load()
for y in range(c.size[1]):
    for x in range(c.size[0]):
        r, g, b = px[x, y]
        L = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0
        a = min(1.0, max(0.0, (L - 0.24) / 0.22))
        mp[x, y] = (255, 255, 255, int(a * 255))
m.save(os.path.join(ROOT, 'Assets/Aren/Resources/UI/Title/title_mask_elyndra.png'))
print('ok máscara')
