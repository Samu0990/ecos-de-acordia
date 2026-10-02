"""Sprites da UI de Ecos de Acordia (PIL, superamostrado 4x). Roda no python3 do sistema."""
import math, os
from PIL import Image, ImageDraw, ImageFilter
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Aren', 'Resources', 'UI', 'Sprites')
SS = 4

def canvas(w, h): return Image.new('RGBA', (w * SS, h * SS), (0, 0, 0, 0))
def done(img, w, h, name, blur=0):
    if blur: img = img.filter(ImageFilter.GaussianBlur(blur * SS))
    img.resize((w, h), Image.LANCZOS).save(os.path.join(OUT, name + '.png'))

W = (255, 255, 255, 255)

# painel 9-slice: escuro translúcido com borda fina dourada e brilho no topo
w, h = 128, 128
im = canvas(w, h); d = ImageDraw.Draw(im)
for y in range(h * SS):
    t = y / (h * SS)
    a = int(205 - 25 * t)
    d.line([(0, y), (w * SS, y)], fill=(12, 10, 16, a))
d.rounded_rectangle([1 * SS, 1 * SS, (w - 2) * SS, (h - 2) * SS], radius=6 * SS, outline=(200, 160, 90, 150), width=int(1.2 * SS))
d.line([(14 * SS, 3 * SS), ((w - 14) * SS, 3 * SS)], fill=(255, 220, 150, 70), width=SS)
mask = canvas(w, h); ImageDraw.Draw(mask).rounded_rectangle([0, 0, (w - 1) * SS, (h - 1) * SS], radius=7 * SS, fill=W)
im.putalpha(Image.composite(im.split()[3], Image.new('L', im.size, 0), mask.split()[3]))
done(im, w, h, 'ui_panel')

# botão (barra larga, transparente no centro, brilho nas pontas) 9-slice
w, h = 256, 64
im = canvas(w, h); d = ImageDraw.Draw(im)
for x in range(w * SS):
    t = abs(x / (w * SS) - 0.5) * 2
    d.line([(x, 0), (x, h * SS)], fill=(255, 255, 255, int(255 * (1 - t ** 1.5))))
done(im, w, h, 'ui_button')

# brilho radial
w = h = 128
im = canvas(w, h); d = ImageDraw.Draw(im)
for r in range(w * SS // 2, 0, -2):
    t = r / (w * SS / 2)
    d.ellipse([w * SS / 2 - r, h * SS / 2 - r, w * SS / 2 + r, h * SS / 2 + r], fill=(255, 255, 255, int(255 * (1 - t) ** 2.2)))
done(im, w, h, 'ui_glow')

# disco e anel
for name, width in (('ui_disc', 0), ('ui_ring', 5), ('ui_ring_thin', 2)):
    w = h = 128
    im = canvas(w, h); d = ImageDraw.Draw(im)
    if width == 0: d.ellipse([2 * SS, 2 * SS, (w - 2) * SS, (h - 2) * SS], fill=W)
    else: d.ellipse([2 * SS, 2 * SS, (w - 2) * SS, (h - 2) * SS], outline=W, width=width * SS)
    done(im, w, h, name)

# losango (marcador de alvo / ornamento)
w = h = 64
im = canvas(w, h); d = ImageDraw.Draw(im)
c = w * SS / 2
d.polygon([(c, 4 * SS), (w * SS - 4 * SS, c), (c, h * SS - 4 * SS), (4 * SS, c)], outline=W, width=3 * SS)
d.polygon([(c, 18 * SS), (w * SS - 18 * SS, c), (c, h * SS - 18 * SS), (18 * SS, c)], fill=W)
done(im, w, h, 'ui_diamond')

# ornamento horizontal (título): linha que afina + losango no meio + pequenas ondas
w, h = 512, 32
im = canvas(w, h); d = ImageDraw.Draw(im)
cy = h * SS / 2
for x in range(w * SS):
    t = abs(x / (w * SS) - 0.5) * 2
    a = int(230 * (1 - t) ** 1.2)
    d.point((x, cy), fill=(255, 255, 255, a))
    d.point((x, cy + 1), fill=(255, 255, 255, a))
cx = w * SS / 2
d.polygon([(cx, cy - 9 * SS), (cx + 9 * SS, cy), (cx, cy + 9 * SS), (cx - 9 * SS, cy)], fill=W)
for side in (-1, 1):
    pts = [(cx + side * (16 + i) * SS, cy + math.sin(i * 0.5) * 4 * SS * (1 - i / 60)) for i in range(60)]
    d.line(pts, fill=(255, 255, 255, 200), width=int(1.5 * SS))
done(im, w, h, 'ui_ornament')

# barra (fundo e preenchimento) — 9-slice horizontal
w, h = 128, 16
im = canvas(w, h); d = ImageDraw.Draw(im)
d.rounded_rectangle([0, 0, (w - 1) * SS, (h - 1) * SS], radius=3 * SS, fill=W)
done(im, w, h, 'ui_bar')
im = canvas(w, h); d = ImageDraw.Draw(im)
d.rounded_rectangle([0, 0, (w - 1) * SS, (h - 1) * SS], radius=3 * SS, outline=W, width=int(1.5 * SS))
done(im, w, h, 'ui_bar_frame')

# vinheta (dano / vida baixa)
w = h = 256
im = Image.new('L', (w, h), 0)
px = im.load()
for y in range(h):
    for x in range(w):
        r = math.hypot((x - w / 2) / (w / 2), (y - h / 2) / (h / 2))
        px[x, y] = int(255 * max(0, min(1, (r - 0.55) / 0.6)) ** 1.6)
rgba = Image.new('RGBA', (w, h), (255, 255, 255, 0)); rgba.putalpha(im)
rgba.save(os.path.join(OUT, 'ui_vignette.png'))

# ---------------------------------------------------------------- ícones das habilidades (128, branco)
def icon(name, draw):
    w = h = 128
    im = canvas(w, h); d = ImageDraw.Draw(im)
    draw(d, w * SS, h * SS)
    done(im, w, h, name)

def pulso(d, W_, H_):
    c = W_ / 2
    for r, wd in ((14, 0), (28, 6), (44, 5), (58, 3)):
        R = r * SS
        if wd == 0: d.ellipse([c - R, c - R, c + R, c + R], fill=W)
        else: d.ellipse([c - R, c - R, c + R, c + R], outline=(255, 255, 255, 255 - r * 2), width=wd * SS)
    for k in range(8):
        a = k * math.pi / 4
        d.line([(c + math.cos(a) * 34 * SS, c + math.sin(a) * 34 * SS), (c + math.cos(a) * 40 * SS, c + math.sin(a) * 40 * SS)], fill=W, width=3 * SS)

def lamina(d, W_, H_):
    c = W_ / 2
    # meia-lua: diferença de dois círculos
    big = [c - 46 * SS, c - 50 * SS, c + 38 * SS, c + 34 * SS]
    d.pieslice(big, -70, 160, fill=W)
    d.ellipse([c - 52 * SS, c - 62 * SS, c + 22 * SS, c + 12 * SS], fill=(0, 0, 0, 0))
    for i in range(3):   # linhas de frequência atrás
        y = c + (10 + i * 12) * SS
        d.line([(c - 58 * SS, y + 14 * SS), (c - 22 * SS + i * 6 * SS, y - 2 * SS)], fill=(255, 255, 255, 200 - i * 50), width=4 * SS)

def eco(d, W_, H_):
    c = W_ / 2
    def figure(dx, a):
        col = (255, 255, 255, a)
        # capuz pontudo + manto que abre embaixo (silhueta do Aren)
        d.polygon([(c + dx, c - 58 * SS), (c + 15 * SS + dx, c - 30 * SS), (c + 13 * SS + dx, c - 18 * SS),
                   (c + 26 * SS + dx, c - 10 * SS), (c + 34 * SS + dx, c + 52 * SS), (c + 6 * SS + dx, c + 44 * SS),
                   (c + dx, c + 54 * SS), (c - 6 * SS + dx, c + 44 * SS), (c - 34 * SS + dx, c + 52 * SS),
                   (c - 26 * SS + dx, c - 10 * SS), (c - 13 * SS + dx, c - 18 * SS), (c - 15 * SS + dx, c - 30 * SS)], fill=col)
        d.ellipse([c - 7 * SS + dx, c - 34 * SS, c + 7 * SS + dx, c - 22 * SS], fill=(0, 0, 0, 0) if a == 255 else (255, 255, 255, a // 2))
    figure(-22 * SS, 90)
    figure(12 * SS, 255)
    for i in range(3):
        r = (40 + i * 12) * SS
        d.arc([c + 12 * SS - r, c - r, c + 12 * SS + r, c + r], 150, 210, fill=(255, 255, 255, 170 - i * 40), width=3 * SS)

def contracanto(d, W_, H_):
    c = W_ / 2
    pts = []
    for i in range(121):
        t = i / 120
        x = c - 54 * SS + t * 108 * SS
        amp = (0.2 + t * 0.8) * 30 * SS
        pts.append((x, c + 18 * SS - math.sin(t * math.pi * 6) * amp))
    d.line(pts, fill=W, width=5 * SS)
    d.arc([c - 50 * SS, c - 56 * SS, c + 50 * SS, c + 44 * SS], 200, 340, fill=W, width=5 * SS)
    d.polygon([(c, c - 62 * SS), (c + 8 * SS, c - 48 * SS), (c - 8 * SS, c - 48 * SS)], fill=W)

icon('icon_pulso', pulso); icon('icon_lamina', lamina); icon('icon_eco', eco); icon('icon_contracanto', contracanto)

# emblema do Aren: anel duplo, flauta diagonal e três "notas" (os doze sinos estilizados)
w = h = 192
im = canvas(w, h); d = ImageDraw.Draw(im)
c = w * SS / 2
d.ellipse([10 * SS, 10 * SS, (w - 10) * SS, (h - 10) * SS], outline=W, width=4 * SS)
d.ellipse([24 * SS, 24 * SS, (w - 24) * SS, (h - 24) * SS], outline=(255, 255, 255, 140), width=2 * SS)
for k in range(12):
    a = k * math.pi / 6
    r0, r1 = 74 * SS, 84 * SS
    d.line([(c + math.cos(a) * r0, c + math.sin(a) * r0), (c + math.cos(a) * r1, c + math.sin(a) * r1)], fill=W, width=3 * SS)
a = -math.pi / 4
p0 = (c - math.cos(a) * 52 * SS, c - math.sin(a) * 52 * SS); p1 = (c + math.cos(a) * 52 * SS, c + math.sin(a) * 52 * SS)
d.line([p0, p1], fill=W, width=9 * SS)
for i in range(5):
    t = 0.25 + i * 0.12
    px_, py_ = p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t
    d.ellipse([px_ - 3 * SS, py_ - 3 * SS, px_ + 3 * SS, py_ + 3 * SS], fill=(0, 0, 0, 0))
done(im, w, h, 'ui_emblem')
print('ui ok')

# degradê horizontal (opaco à esquerda -> transparente à direita) para painéis de menu
w, h = 256, 8
im = Image.new('RGBA', (w, h), (255, 255, 255, 0))
px = im.load()
for x in range(w):
    a = int(255 * (1 - x / (w - 1)) ** 1.6)
    for y in range(h): px[x, y] = (255, 255, 255, a)
im.save(os.path.join(OUT, 'ui_fade_h.png'))
print('fade ok')
