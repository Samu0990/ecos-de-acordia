"""Mede seções transversais do Aren para posicionar juntas no centro real dos membros.
Imprime, por fatia de altura, o centróide (x, y) e a largura de cada região.
Rodar: blender -b Aren_01_prep.blend --python aren_measure.py
"""
import bpy

body = bpy.data.objects["Aren_Body"]
verts = [body.matrix_world @ v.co for v in body.data.vertices]

def slice_stats(zc, dz, cond):
    pts = [p for p in verts if abs(p.z - zc) < dz and cond(p)]
    if len(pts) < 4:
        return None
    xs = [p.x for p in pts]; ys = [p.y for p in pts]
    return (len(pts), (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, max(xs) - min(xs), max(ys) - min(ys), min(xs), max(xs), min(ys), max(ys))

def table(name, zs, cond, dz=0.012):
    print("==", name)
    for z in zs:
        s = slice_stats(z, dz, cond)
        if s:
            print("  z=%.2f n=%4d cx=%+.3f cy=%+.3f w=%.3f d=%.3f  x[%+.3f,%+.3f] y[%+.3f,%+.3f]" % ((z,) + s))

zs_arm = [round(1.50 - i * 0.03, 2) for i in range(26)]
table("BRAÇO ESQUERDO (x>0.17)", zs_arm, lambda p: p.x > 0.17)
table("BRAÇO DIREITO (x<-0.17)", zs_arm, lambda p: p.x < -0.17)
zs_leg = [round(1.00 - i * 0.04, 2) for i in range(26)]
table("PERNA ESQUERDA (0.02<x<0.2, y>-0.07)", zs_leg, lambda p: 0.02 < p.x < 0.2 and p.y > -0.07)
table("PERNA DIREITA (-0.2<x<-0.02, y>-0.07)", zs_leg, lambda p: -0.2 < p.x < -0.02 and p.y > -0.07)
zs_torso = [round(1.80 - i * 0.04, 2) for i in range(22)]
table("TRONCO/CABEÇA (|x|<0.16)", zs_torso, lambda p: abs(p.x) < 0.16)
table("ABA FRONTAL (|x|<0.08, y<-0.07)", [round(1.0 - i * 0.04, 2) for i in range(14)], lambda p: abs(p.x) < 0.08 and p.y < -0.07)
table("ABA TRASEIRA (|x|<0.16, y>0.08)", [round(1.0 - i * 0.04, 2) for i in range(14)], lambda p: abs(p.x) < 0.16 and p.y > 0.08)
# pés: ponta e calcanhar
for side, cond in (("E", lambda p: p.x > 0), ("D", lambda p: p.x < 0)):
    foot = [p for p in verts if p.z < 0.12 and cond(p)]
    print("PÉ %s: y[%+.3f,%+.3f] x[%+.3f,%+.3f]" % (side, min(p.y for p in foot), max(p.y for p in foot), min(p.x for p in foot), max(p.x for p in foot)))
# mão: ponta dos dedos
for side, cond in (("E", lambda p: p.x > 0.2), ("D", lambda p: p.x < -0.2)):
    hand = [p for p in verts if p.z < 0.95 and cond(p)]
    lo = min(hand, key=lambda p: p.z)
    print("MÃO %s: z_min=%.3f em (%+.3f,%+.3f)  x[%+.3f,%+.3f] y[%+.3f,%+.3f]" % (side, lo.z, lo.x, lo.y, min(p.x for p in hand), max(p.x for p in hand), min(p.y for p in hand), max(p.y for p in hand)))
