"""Sussurrante — etapa 4: bake da malha densa para a malha do jogo (Cycles).

A malha do jogo herda o atlas UV do Tripo (415 ilhas; o decimate da malha SOLDADA preserva
as costuras), então a cor e o ORM do Tripo servem direto (sem reamostrar). Aqui só se assa:
- normal (tangente, convenção OpenGL = Unity): o relevo que o decimate tirou (costelas,
  músculos, dobras) + o normal map do próprio Tripo;
- AO na própria malha leve (axilas, debaixo do cachecol e da saia).
Saída: work/bake_normal.png, work/bake_ao.png, work/tripo_color.png, work/tripo_orm.png
e work/suss_uv.blend (malha com material M_Sussurrante).

Rodar: blender -b --factory-startup --python ArtSource/Sussurrante/scripts/suss_04_bake.py
"""
import bpy, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import suss_common as C

RES = 2048
bpy.ops.wm.open_mainfile(filepath=os.path.join(C.WORK, "suss_rig.blend"))
low = bpy.data.objects["Sussurrante"]
with bpy.data.libraries.load(os.path.join(C.WORK, "suss_hi.blend"), link=False) as (src, dst):
    dst.objects = ["Sussurrante_Hi"]
hi = dst.objects[0]
bpy.context.scene.collection.objects.link(hi)
print("UV", [u.name for u in low.data.uv_layers])

# texturas originais do Tripo (embutidas no GLB) como arquivos de trabalho
hm = hi.data.materials[0]
hnt = hm.node_tree
imgs = {n.image.name.split("_")[0]: n for n in hnt.nodes if n.type == 'TEX_IMAGE' and n.image}
for key, fname in (("Color", "tripo_color.png"), ("ORM", "tripo_orm.png")):
    im = imgs[key].image
    im.filepath_raw = os.path.join(C.WORK, fname)
    im.file_format = 'PNG'
    im.save()


def image(name, non_color):
    img = bpy.data.images.new(name, RES, RES, alpha=False, float_buffer=False)
    img.colorspace_settings.name = 'Non-Color' if non_color else 'sRGB'
    return img


mat = bpy.data.materials.new("M_Sussurrante")
mat.use_nodes = True
nt = mat.node_tree
tex_node = nt.nodes.new("ShaderNodeTexImage")
nt.nodes.active = tex_node
low.data.materials.clear()
low.data.materials.append(mat)

sc = bpy.context.scene
sc.render.engine = 'CYCLES'
sc.cycles.device = 'CPU'
sc.cycles.samples = 1
sc.render.bake.margin = 12
for m in low.modifiers:      # repouso: malha avaliada = malha de repouso
    m.show_render = m.show_viewport = False

nrm = image("bake_normal", True)
tex_node.image = nrm
bpy.ops.object.select_all(action='DESELECT')
hi.select_set(True); low.select_set(True)
bpy.context.view_layer.objects.active = low
bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT', use_selected_to_active=True,
                    cage_extrusion=0.012, max_ray_distance=0.035)
nrm.filepath_raw = os.path.join(C.WORK, "bake_normal.png"); nrm.file_format = 'PNG'; nrm.save()
print("BAKE normal")

hi.hide_render = True
sc.cycles.samples = 64
# AO de personagem: só o que está perto (axila, dobra do cachecol); o padrão (10 m) escurecia tudo
if sc.world is None:
    sc.world = bpy.data.worlds.new("W")
sc.world.light_settings.distance = 0.35
ao = image("bake_ao", True)
tex_node.image = ao
bpy.ops.object.select_all(action='DESELECT')
low.select_set(True)
bpy.context.view_layer.objects.active = low
bpy.ops.object.bake(type='AO', use_selected_to_active=False)
ao.filepath_raw = os.path.join(C.WORK, "bake_ao.png"); ao.file_format = 'PNG'; ao.save()
print("BAKE ao")

for m in low.modifiers:
    m.show_render = m.show_viewport = True
bpy.data.objects.remove(hi, do_unlink=True)
tex_node.image = None
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(C.WORK, "suss_uv.blend"), compress=True)
print("OK")
