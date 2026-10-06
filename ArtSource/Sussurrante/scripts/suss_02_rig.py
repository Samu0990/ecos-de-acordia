"""Sussurrante — etapa 2: esqueleto UAL2/Quaternius encaixado na malha + pesos.

- Abre work/suss_low.blend (saída da etapa 1).
- Importa a armadura do personagem base Quaternius (MESMOS nomes/hierarquia da UAL2: as
  animações CC0 do projeto servem direto pelo Humanoid) e move cada osso para as articulações
  medidas (suss_common.joints()).
- Pesos: bone heat do Blender como primeira passada; depois correções (suss_weights.py).
- Salva work/suss_rig.blend.

Rodar: blender -b --factory-startup --python ArtSource/Sussurrante/scripts/suss_02_rig.py
"""
import bpy, os, sys, mathutils
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import suss_common as C

V = mathutils.Vector
bpy.ops.wm.open_mainfile(filepath=os.path.join(C.WORK, "suss_low.blend"))
body = bpy.data.objects["Sussurrante"]

before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=C.QUAT_BASE)
new = [o for o in bpy.data.objects if o not in before]
rig = next(o for o in new if o.type == 'ARMATURE')
for o in new:
    if o is not rig:
        bpy.data.objects.remove(o, do_unlink=True)
rig.name = "Armature"
rig.data.name = "Armature"
rig.data.display_type = 'STICK'

J = C.joints()
inv = rig.matrix_world.inverted()
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = rig.data.edit_bones
for b in eb:
    b.use_connect = False
missing = [n for n in J if n not in eb]
if missing:
    raise SystemExit("ossos ausentes na armadura base: %s" % missing)
for name, (h, t) in J.items():
    b = eb[name]
    roll = b.roll
    b.head = inv @ V(h)
    b.tail = inv @ V(t)
    b.roll = roll
# a raiz fica no chão, apontando para trás (como na base)
untouched = [b.name for b in eb if b.name not in J and b.name != "root"]
bpy.ops.object.mode_set(mode='OBJECT')
print("OSSOS", len(rig.data.bones), "sem medida:", untouched)

# ---------------------------------------------------------------- pesos (bone heat)
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
zero = 0
for v in body.data.vertices:
    if sum(g.weight for g in v.groups) < 1e-4:
        zero += 1
print("HEAT grupos", len(body.vertex_groups), "verts sem peso", zero)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(C.WORK, "suss_rig_heat.blend"), compress=True)
print("OK")
