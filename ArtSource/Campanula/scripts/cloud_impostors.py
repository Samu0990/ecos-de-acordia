"""Nuvens volumétricas de Campânula (impostores com luz de 6 direções), renderizadas no Cycles.

A referência do autor: nuvem de volume no Blender (Principled Volume com densidade de ruído/Voronoi).
Num Intel UHD não dá para fazer raymarching de volume no jogo; então cada nuvem é renderizada aqui como
volume de verdade e vira um cartão no céu, com a técnica "6-way lighting" (usada em fumaça nos jogos):
a mesma nuvem é renderizada com uma luz de cada direção — direita, esquerda, cima, baixo, de trás
(contraluz, a borda prateada) e de frente — mais um passe de luz ambiente. O shader do jogo
(Hidden/Campanula/CloudImpostor) mistura esses passes pela direção da lua, da Fenda e do brilho da
cidade embaixo, e a nuvem é iluminada de verdade por elas.

Saída (4 nuvens num atlas 2×2, 1024²):
  Assets/Aren/Resources/VFX/clouds_a.png  RGBA = direita, esquerda, cima, baixo
  Assets/Aren/Resources/VFX/clouds_b.png  RGBA = trás, frente, ambiente, alfa (densidade)

Rodar: ~/.local/opt/blender-5.2/blender -b --factory-startup --python ArtSource/Campanula/scripts/cloud_impostors.py
"""
import bpy, math, random, os, subprocess, sys
import numpy as np
from mathutils import Vector

V = Vector
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.abspath(os.path.join(ROOT, '..', '..', 'Assets', 'Aren', 'Resources', 'VFX'))
TMP = os.path.join('/tmp', 'eda_clouds')
RES = 512
SAMPLES = int(os.environ.get('CLOUD_SAMPLES', '48'))


def cloud(seed, kind):
    """Nuvem de metabolas: base achatada, topo em couve-flor (cúmulo) ou alongada (estrato-cúmulo)."""
    random.seed(seed)
    mb = bpy.data.metaballs.new('nuvem')
    mb.resolution = 0.12
    mb.threshold = 0.6
    o = bpy.data.objects.new('nuvem', mb)
    bpy.context.scene.collection.objects.link(o)
    n = 30 if kind == 0 else 24
    for i in range(n):
        e = mb.elements.new()
        if kind == 0:     # cúmulo alto
            x = random.gauss(0, 0.42); y = random.gauss(0, 0.3)
            z = abs(random.gauss(0, 0.45)) * (1.2 - abs(x) * 0.5)
        else:             # estendida, mais baixa
            x = random.uniform(-1.05, 1.05); y = random.gauss(0, 0.28)
            z = abs(random.gauss(0, 0.22)) * (1.0 - abs(x) * 0.4)
        e.co = (x, y, z - 0.25)
        e.radius = random.uniform(0.5, 0.8) * (1.15 if z < 0.2 else 0.9)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target='MESH')
    m = bpy.context.active_object
    # base reta (cúmulo tem fundo plano)
    for v in m.data.vertices:
        if v.co.z < -0.32: v.co.z = -0.32 + (v.co.z + 0.32) * 0.15
    return m


def volume_material(seed):
    """Densidade = ruído fractal × Voronoi (bordas fofas e "couve-flor"), como na referência."""
    mat = bpy.data.materials.new('vol')
    nt = mat.node_tree
    for nd in list(nt.nodes): nt.nodes.remove(nd)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    vol = nt.nodes.new('ShaderNodeVolumePrincipled')
    vol.inputs['Color'].default_value = (1, 1, 1, 1)
    vol.inputs['Anisotropy'].default_value = 0.35
    tc = nt.nodes.new('ShaderNodeTexCoord')
    mp = nt.nodes.new('ShaderNodeMapping'); mp.inputs['Location'].default_value = (seed * 1.7, seed * 0.3, 0)
    nz = nt.nodes.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 2.6; nz.inputs['Detail'].default_value = 6.0
    nz.inputs['Roughness'].default_value = 0.62
    vr = nt.nodes.new('ShaderNodeTexVoronoi'); vr.inputs['Scale'].default_value = 3.2
    vr.feature = 'F1'
    inv = nt.nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
    mul = nt.nodes.new('ShaderNodeMath'); mul.operation = 'MULTIPLY'
    sub = nt.nodes.new('ShaderNodeMath'); sub.operation = 'SUBTRACT'; sub.inputs[1].default_value = 0.2
    k = nt.nodes.new('ShaderNodeMath'); k.operation = 'MULTIPLY'; k.inputs[1].default_value = 55.0; k.use_clamp = False
    mx = nt.nodes.new('ShaderNodeMath'); mx.operation = 'MAXIMUM'; mx.inputs[1].default_value = 0.0
    nt.links.new(tc.outputs['Object'], mp.inputs['Vector'])
    nt.links.new(mp.outputs['Vector'], nz.inputs['Vector'])
    nt.links.new(mp.outputs['Vector'], vr.inputs['Vector'])
    nt.links.new(vr.outputs['Distance'], inv.inputs[1])
    nt.links.new(nz.outputs['Fac'], mul.inputs[0])
    nt.links.new(inv.outputs[0], mul.inputs[1])
    nt.links.new(mul.outputs[0], sub.inputs[0])
    nt.links.new(sub.outputs[0], k.inputs[0])
    nt.links.new(k.outputs[0], mx.inputs[0])
    nt.links.new(mx.outputs[0], vol.inputs['Density'])
    nt.links.new(vol.outputs[0], out.inputs['Volume'])
    return mat


def setup():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = SAMPLES
    sc.cycles.use_denoising = True
    sc.cycles.volume_bounces = 2
    sc.cycles.max_bounces = 4
    sc.cycles.volume_step_rate = 1.5
    sc.cycles.volume_max_steps = 256
    sc.render.film_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = RES
    sc.view_settings.view_transform = 'Standard'
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.render.image_settings.color_depth = '16'
    cd = bpy.data.cameras.new('cam'); cd.type = 'ORTHO'; cd.ortho_scale = 3.8
    cam = bpy.data.objects.new('cam', cd); sc.collection.objects.link(cam)
    cam.location = (0, -10, 0.25); cam.rotation_euler = (math.radians(90), 0, 0)
    sc.camera = cam
    w = bpy.data.worlds.new('w'); sc.world = w
    w.use_nodes = True
    sun = bpy.data.lights.new('sol', 'SUN'); sun.energy = 4.0; sun.angle = math.radians(3)
    so = bpy.data.objects.new('sol', sun); sc.collection.objects.link(so)
    return sc, so, w


# direção DE ONDE a luz vem (câmera em −Y olhando +Y: direita = +X, cima = +Z, trás = +Y)
DIRS = {'right': V((1, 0, 0.05)), 'left': V((-1, 0, 0.05)), 'top': V((0, 0, 1)), 'bottom': V((0, 0, -1)),
        'back': V((0, 1, 0.08)), 'front': V((0, -1, 0.08))}


def aim(obj, from_dir):
    d = -from_dir.normalized()   # a luz anda de from_dir para o centro
    obj.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()


def render_cloud(idx, seed, kind):
    sc, so, w = setup()
    m = cloud(seed, kind)
    m.data.materials.append(volume_material(seed))
    os.makedirs(TMP, exist_ok=True)
    bg = w.node_tree.nodes['Background']
    for name, d in DIRS.items():
        so.hide_render = False
        bg.inputs['Strength'].default_value = 0.0
        aim(so, d)
        sc.render.filepath = os.path.join(TMP, f'c{idx}_{name}.png')
        bpy.ops.render.render(write_still=True)
    # ambiente: céu uniforme, sem sol
    so.hide_render = True
    bg.inputs['Color'].default_value = (1, 1, 1, 1)
    bg.inputs['Strength'].default_value = 1.0
    sc.render.filepath = os.path.join(TMP, f'c{idx}_amb.png')
    bpy.ops.render.render(write_still=True)


def load(p):
    raw = subprocess.run(['convert', p, '-depth', '16', 'rgba:-'], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.uint16).reshape(RES, RES, 4).astype(np.float32) / 65535.0


def pack():
    A = np.zeros((RES * 2, RES * 2, 4), np.float32)
    B = np.zeros((RES * 2, RES * 2, 4), np.float32)
    for idx in range(4):
        oy, ox = (idx // 2) * RES, (idx % 2) * RES
        imgs = {k: load(os.path.join(TMP, f'c{idx}_{k}.png')) for k in list(DIRS) + ['amb']}
        alpha = imgs['amb'][..., 3]
        # luz "despremultiplicada"; na borda (alfa quase 0) a divisão vira ruído branco: lá vale a média da nuvem
        wgt = np.clip((alpha - 0.02) / 0.23, 0, 1); wgt = wgt * wgt * (3 - 2 * wgt)
        lum = {}
        for k in imgs:
            l = np.clip(imgs[k][..., :3].mean(-1) / np.maximum(alpha, 1e-3), 0, 1)
            mean = float((l * (alpha > 0.3)).sum() / max(1, (alpha > 0.3).sum()))
            lum[k] = l * wgt + mean * (1 - wgt)
        A[oy:oy + RES, ox:ox + RES] = np.stack([lum['right'], lum['left'], lum['top'], lum['bottom']], -1)
        B[oy:oy + RES, ox:ox + RES] = np.stack([lum['back'], lum['front'], lum['amb'], alpha], -1)
    for arr, name in ((A, 'clouds_a.png'), (B, 'clouds_b.png')):
        img = (np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8)
        subprocess.run(['convert', '-size', f'{RES * 2}x{RES * 2}', '-depth', '8', 'rgba:-', os.path.join(OUT, name)], input=img.tobytes(), check=True)
    print('pack ok')


def main():
    only = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv and len(sys.argv) > sys.argv.index('--') + 1 else 'all'
    jobs = [(0, 11, 0), (1, 23, 0), (2, 37, 1), (3, 41, 1)]
    if only != 'pack':
        for idx, seed, kind in jobs:
            if only not in ('all', str(idx)): continue
            render_cloud(idx, seed, kind)
    if only in ('all', 'pack'):
        pack()


main()
