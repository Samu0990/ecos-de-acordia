# Reduz os modelos escaneados do Poly Haven (CC0) para versões de jogo do mundo de Elyndra.
# Entrada: ~/EcosAssets/polyhaven/<id>/<id>_<res>.gltf (Tools/texgen/fetch_ph_models.py).
# Saída:   Assets/World/Models/PolyHaven/<id>/<id>.fbx (LOD0 + LOD1) e as texturas no formato do shader
#          Standard do Unity: <id>_albedo.jpg, <id>_normal.jpg (OpenGL), <id>_ms.png (R metálico, A suavidade
#          = 1 - rugosidade), <id>_ao.jpg. As UVs e o normal map originais ficam (a malha só perde faces).
# Uso (Blender 5.2, sem interface):
#   blender -b --factory-startup --python ArtSource/WorldKit/scripts/wk_ph_reduce.py -- [id1,id2,...]
import bpy, bmesh, json, os, sys
from mathutils import Vector

ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
SRC = os.path.expanduser('~/EcosAssets/polyhaven')
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', '..'))
DST = os.path.join(ROOT, 'Assets', 'World', 'Models', 'PolyHaven')
# faces de LOD0 por modelo (LOD1 = 30%). Construções e objetos pequenos quase não perdem (arestas duras).
TARGET = {
    'boulder_01': 5000, 'rock_07': 3000, 'moon_rock_01': 2500, 'mountainside': 9000,
    'coastal_cliff_02': 12000, 'coastal_cliff_04': 14000, 'coast_rocks_05': 10000,
    'modular_fort_01': 16000, 'large_castle_door': 9000, 'large_iron_gate': 12000,
    'cannon_01': 6000, 'Lantern_01': 3000, 'Barrel_01': 2700, 'barrel_03': 1500,
    'wooden_bowl_01': 1500, 'ceramic_pot': 2400, 'jug_01': 2400,
    'dead_tree_trunk': 6000, 'dead_tree_trunk_02': 6000,
}


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def faces(o):
    return len(o.data.polygons)


def decimate_to(o, target):
    # o colapso para antes nas costuras de UV: repete até chegar perto do alvo
    bpy.context.view_layer.objects.active = o
    for _ in range(5):
        n = faces(o)
        if n <= target * 1.1:
            return
        m = o.modifiers.new('dec', 'DECIMATE')
        m.decimate_type = 'COLLAPSE'
        m.ratio = max(0.002, target / n)
        m.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=m.name)
        if faces(o) > n * 0.97:   # não anda mais: solta as costuras (pequeno custo nas UVs)
            m = o.modifiers.new('dec', 'DECIMATE')
            m.decimate_type = 'COLLAPSE'; m.ratio = max(0.002, target / faces(o)); m.delimit = set()
            bpy.ops.object.modifier_apply(modifier=m.name)


def save_tex(img_path, out_path, kind):
    """kind: 'albedo'/'normal' copia; 'ms' = R metálico, A = 1 - rugosidade; 'ao' = canal R do ARM."""
    import numpy as np, shutil
    if kind not in ('ms', 'ao'):
        shutil.copyfile(img_path, out_path)   # já é JPG (albedo sRGB, normal OpenGL)
        return
    img = bpy.data.images.load(img_path)
    if kind in ('ms', 'ao'):
        w, h = img.size
        px = np.empty(w * h * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        px = px.reshape(-1, 4)
        out = np.ones_like(px)
        if kind == 'ms':
            out[:, 0] = out[:, 1] = out[:, 2] = px[:, 2]
            out[:, 3] = 1.0 - px[:, 1]
        else:
            out[:, 0] = out[:, 1] = out[:, 2] = px[:, 0]
        dst = bpy.data.images.new(os.path.basename(out_path), w, h, alpha=True)
        dst.pixels.foreach_set(out.ravel())
        if w > 1024:   # suavidade e oclusão aguentam meia resolução (o repositório agradece)
            dst.scale(w // 2, h // 2)
        dst.filepath_raw = out_path
        dst.file_format = 'PNG' if out_path.endswith('.png') else 'JPEG'
        dst.save()
        bpy.data.images.remove(dst)
    else:
        img.filepath_raw = out_path
        img.file_format = 'JPEG'
        img.save()
    bpy.data.images.remove(img)


def process(mid):
    d = os.path.join(SRC, mid)
    info = json.load(open(os.path.join(d, 'info.json')))
    gl = [f for f in os.listdir(d) if f.endswith('.gltf')][0]
    clear()
    bpy.ops.import_scene.gltf(filepath=os.path.join(d, gl))
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    # junta tudo numa malha (aplica as transformações do glTF)
    for o in bpy.context.scene.objects:
        o.select_set(o.type == 'MESH')
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if len(meshes) > 1:
        bpy.ops.object.join()
    lo = bpy.context.view_layer.objects.active
    lo.name = mid
    if lo.data.shape_keys is not None:   # (o canhão vem com rig e shape keys)
        lo.shape_key_clear()
    # pivô no centro da base (assenta no chão), sem mexer na escala real
    bb = [lo.matrix_world @ Vector(c) for c in lo.bound_box]
    cx = sum(v.x for v in bb) / 8; cy = sum(v.y for v in bb) / 8; zmin = min(v.z for v in bb)
    lo.data.transform(__import__('mathutils').Matrix.Translation((-cx, -cy, -zmin)))
    lo.location = (0, 0, 0)
    src_faces = faces(lo)
    # o glTF duplica os vértices nas costuras de UV: solda antes (as UVs ficam nos cantos das faces)
    bm = bmesh.new(); bm.from_mesh(lo.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0002)
    bm.to_mesh(lo.data); bm.free()
    decimate_to(lo, TARGET.get(mid, 8000))
    for p in lo.data.polygons:
        p.use_smooth = True
    # LOD1
    lod1 = lo.copy(); lod1.data = lo.data.copy(); lod1.name = mid + '_LOD1'
    bpy.context.collection.objects.link(lod1)
    decimate_to(lod1, max(300, int(faces(lo) * 0.3)))
    # texturas
    out = os.path.join(DST, mid)
    os.makedirs(out, exist_ok=True)
    tex = os.path.join(d, 'textures')
    res = info.get('res', '1k')
    for f in os.listdir(tex):
        p = os.path.join(tex, f)
        if '_diff_' in f:
            save_tex(p, os.path.join(out, mid + '_albedo.jpg'), 'albedo')
        elif '_nor_gl_' in f:
            save_tex(p, os.path.join(out, mid + '_normal.jpg'), 'normal')
        elif '_arm_' in f:
            save_tex(p, os.path.join(out, mid + '_ms.png'), 'ms')
            save_tex(p, os.path.join(out, mid + '_ao.jpg'), 'ao')
    # sem materiais no FBX (o Unity monta o Standard com as texturas acima)
    for o in (lo, lod1):
        o.data.materials.clear()
    bpy.ops.object.select_all(action='DESELECT')
    lo.select_set(True); lod1.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, mid + '.fbx'), use_selection=True, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, object_types={'MESH'},
                             mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False, path_mode='STRIP')
    dims = lo.dimensions
    meta = {'id': mid, 'name': info.get('name'), 'license': 'CC0 (Poly Haven)', 'url': info.get('url'),
            'authors': info.get('authors'), 'faces_original': src_faces, 'faces_lod0': faces(lo), 'faces_lod1': faces(lod1),
            'size_m': [round(dims.x, 2), round(dims.y, 2), round(dims.z, 2)]}
    json.dump(meta, open(os.path.join(out, mid + '.json'), 'w'), indent=1)
    print(f"WK {mid}: {src_faces} -> {faces(lo)} / {faces(lod1)} faces, {meta['size_m']} m", flush=True)


def main():
    ids = ARGS[0].split(',') if ARGS else [m for m in TARGET if os.path.isdir(os.path.join(SRC, m))]
    for mid in ids:
        try:
            process(mid)
        except Exception as e:
            print(f"WK {mid}: ERRO {e}", flush=True)


main()
