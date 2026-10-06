"""Constantes e utilitários do Sussurrante (scripts do Blender).

Convenção: personagem olhando para +Y no MUNDO do Blender (como a armadura Quaternius/UAL2
importada), lado direito em +X, pés em z = 0, altura 2,85 m.
"""
import os

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.dirname(HERE)
PROJ = os.path.dirname(os.path.dirname(ART))
WORK = os.path.join(ART, "work")
SRC = os.path.join(ART, "source")
OUT = os.path.join(PROJ, "Assets", "Aren", "Enemies", "Sussurrante")
OUT_TEX = os.path.join(OUT, "Textures")
QUAT_BASE = os.path.expanduser(
    "~/Downloads/QuaterniusChars/Base/Universal Base Characters[Standard]/Base Characters/Unity/Superhero_Male_FullBody.fbx")
UAL2 = os.path.join(PROJ, "Assets", "Aren", "ThirdParty", "Quaternius_UAL2", "UAL2_Standard.fbx")
ALTURA = 2.85

# Articulações medidas na malha (fatias em z + agrupamento; ver README.md). Lado DIREITO (+X);
# o esquerdo é o espelho em x. Formato: osso -> (cabeça, cauda) em metros, mundo do Blender.
_R = {
    "clavicle":   ((0.040, -0.030, 2.170), (0.330, -0.160, 2.120)),
    "upperarm":   ((0.340, -0.170, 2.100), (0.487, -0.186, 1.790)),
    "lowerarm":   ((0.487, -0.186, 1.790), (0.566, -0.030, 1.375)),
    "hand":       ((0.566, -0.030, 1.375), (0.605, 0.005, 1.190)),
    "thigh":      ((0.130, -0.020, 1.550), (0.265, 0.030, 0.910)),
    "calf":       ((0.265, 0.030, 0.910), (0.315, -0.160, 0.170)),
    "foot":       ((0.315, -0.160, 0.170), (0.385, 0.090, 0.040)),
    "ball":       ((0.385, 0.090, 0.040), (0.400, 0.220, 0.030)),
    "ball_leaf":  ((0.400, 0.220, 0.030), (0.405, 0.270, 0.030)),
}
# dedos: base (articulação da palma) e ponta da garra; 3 falanges + folha
_FINGERS_R = {
    "index":  ((0.600, 0.060, 1.185), (0.586, 0.042, 0.985)),
    "middle": ((0.612, 0.005, 1.180), (0.567, -0.027, 0.978)),
    "ring":   ((0.607, -0.050, 1.172), (0.570, -0.068, 1.025)),
    "pinky":  ((0.596, -0.095, 1.165), (0.540, -0.122, 1.040)),
}
_THUMB_R = [(0.540, 0.040, 1.300), (0.505, 0.075, 1.215), (0.482, 0.092, 1.150), (0.468, 0.102, 1.080)]
_CENTER = {
    "pelvis":   ((0.0, -0.030, 1.530), (0.0, -0.035, 1.660)),
    "spine_01": ((0.0, -0.035, 1.660), (0.0, -0.045, 1.800)),
    "spine_02": ((0.0, -0.045, 1.800), (0.0, -0.055, 1.960)),
    "spine_03": ((0.0, -0.055, 1.960), (0.0, -0.060, 2.210)),
    "neck_01":  ((0.0, -0.060, 2.210), (0.0, -0.045, 2.480)),
    "Head":     ((0.0, -0.045, 2.480), (0.0, -0.020, 2.800)),
}


def _mirror(p):
    return (-p[0], p[1], p[2])


def joints():
    """Dicionário osso -> (cabeça, cauda) no mundo, com os nomes da armadura Quaternius."""
    j = dict(_CENTER)
    for side, f in (("r", lambda p: p), ("l", _mirror)):
        for k, (h, t) in _R.items():
            j["%s_%s" % (k, side)] = (f(h), f(t))
        for k, (base, tip) in _FINGERS_R.items():
            pts = []
            for a in (0.0, 0.40, 0.72, 1.0):
                pts.append(tuple(base[i] + (tip[i] - base[i]) * a for i in range(3)))
            for n in range(3):
                j["%s_0%d_%s" % (k, n + 1, side)] = (f(pts[n]), f(pts[n + 1]))
            d = [tip[i] - pts[2][i] for i in range(3)]
            leaf = tuple(tip[i] + d[i] * 0.25 for i in range(3))
            j["%s_04_leaf_%s" % (k, side)] = (f(tip), f(leaf))
        for n in range(3):
            j["thumb_0%d_%s" % (n + 1, side)] = (f(_THUMB_R[n]), f(_THUMB_R[n + 1]))
        d = [_THUMB_R[3][i] - _THUMB_R[2][i] for i in range(3)]
        j["thumb_04_leaf_%s" % side] = (f(_THUMB_R[3]), f(tuple(_THUMB_R[3][i] + d[i] * 0.3 for i in range(3))))
    return j
