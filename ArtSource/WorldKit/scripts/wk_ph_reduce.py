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
    'boulder_01': 5000, 'rock_07': 3000, 'moon_rock_01': 2500, 'mountainside': 28000,
    'coastal_cliff_02': 36000, 'coastal_cliff_04': 44000, 'coast_rocks_05': 10000,
    'modular_fort_01': 16000, 'large_castle_door': 9000, 'large_iron_gate': 12000,
    'cannon_01': 6000,
    # (fora: Barrel_01/barrel_03 são tambores de metal modernos e Lantern_01 um lampião a querosene)
    'wooden_bowl_01': 1500, 'ceramic_pot': 2400, 'jug_01': 2400,
    'dead_tree_trunk': 6000, 'dead_tree_trunk_02': 6000,
    # ruas da Campânula
    'wooden_barrels_01': 4000, 'wine_barrel_01': 3000, 'wooden_crate_01': 1500, 'wooden_crate_02': 1500,
    'wooden_bucket_01': 1500, 'wicker_basket_01': 3000, 'wooden_lantern_01': 2500, 'round_wooden_table_01': 2500,
    'wooden_stool_01': 1500, 'painted_wooden_bench': 2500, 'gothic_statue': 12000, 'spinning_wheel_01': 4000,
    'wooden_ladder': 1500, 'stone_fire_pit': 3000,
    # Campânula v3 ("nível Dark Souls"): mais objetos de rua, igreja e cemitério
    'wooden_bucket_02': 1500, 'wooden_ladder_02': 1500, 'wooden_broom': 1200, 'brass_candleholders': 3000,
    'wooden_candlestick': 1500, 'lantern_chandelier_01': 4000, 'horse_statue_01': 8000, 'lion_head': 6000,
    'modular_wooden_pier': 12000, 'ceramic_vase_01': 1500, 'ceramic_vase_03': 1500, 'brass_pot_01': 1500,
    'antique_ceramic_vase_01': 2000, 'wooden_display_shelves_01': 3000, 'treasure_chest': 4000, 'street_rat': 3000,
    'rock_moss_set_01': 6000, 'rock_moss_set_02': 6000, 'tree_stump_01': 4000, 'wooden_axe': 1500,
    # plantas (folhas recortadas: sem colapso, alfa preservado, faces dos dois lados)
    'weed_plant_02': 3000, 'nettle_plant': 4000, 'fern_02': 3000, 'dandelion_01': 3000, 'shrub_02': 5000, 'periwinkle_plant': 4000,
}
# plantas com folhas recortadas (albedo PNG com alfa → material Cutout no Unity; faces duplicadas do avesso)
PLANTS = {'weed_plant_02', 'nettle_plant', 'fern_02', 'dandelion_01', 'shrub_02', 'periwinkle_plant'}


# scans abertos (uma face só, bordas soltas): o colapso sem as costuras abre buracos
SURFACES = {'mountainside', 'coastal_cliff_02', 'coastal_cliff_04'}
# kits numa malha só: separa por peças soltas e exporta cada peça (pivô no centro da base)
SPLIT = {'modular_fort_01'}


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
        if faces(o) > n * 0.97 and o.name not in SURFACES:   # não anda mais: solta as costuras (pequeno custo nas UVs)
            m = o.modifiers.new('dec', 'DECIMATE')
            m.decimate_type = 'COLLAPSE'; m.ratio = max(0.002, target / faces(o)); m.delimit = set()
            bpy.ops.object.modifier_apply(modifier=m.name)


def save_tex(img_path, out_path, kind):
    """kind: 'albedo'/'normal' copia; 'ms' = R metálico, A = 1 - rugosidade; 'ao' = canal R do ARM."""
    import numpy as np, shutil
    if kind not in ('ms', 'ao'):
        if kind == 'albedo' and out_path.endswith('.png') and not img_path.lower().endswith('.png'):
            img = bpy.data.images.load(img_path); img.filepath_raw = out_path; img.file_format = 'PNG'; img.save(); bpy.data.images.remove(img)
            return
        if os.path.splitext(img_path)[1].lower() != os.path.splitext(out_path)[1].lower():
            img = bpy.data.images.load(img_path); img.filepath_raw = out_path
            img.file_format = 'PNG' if out_path.endswith('.png') else 'JPEG'; img.save(); bpy.data.images.remove(img)
            return
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
    if mid in PLANTS:
        # folhas de uma face só: duplica do avesso (o Standard não desenha as costas)
        bm = bmesh.new(); bm.from_mesh(lo.data)
        dup = bmesh.ops.duplicate(bm, geom=list(bm.faces))
        back = [g for g in dup['geom'] if isinstance(g, bmesh.types.BMFace)]
        bmesh.ops.reverse_faces(bm, faces=back)
        bm.to_mesh(lo.data); bm.free()
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
        if '_diff_' in f and mid in PLANTS:
            import numpy as np
            ap = [os.path.join(tex, g) for g in os.listdir(tex) if '_alpha_' in g]
            img = bpy.data.images.load(p); w, h = img.size
            px = np.empty(w * h * 4, dtype=np.float32); img.pixels.foreach_get(px); px = px.reshape(-1, 4)
            if ap:
                am = bpy.data.images.load(ap[0])
                if tuple(am.size) != (w, h): am.scale(w, h)
                apx = np.empty(w * h * 4, dtype=np.float32); am.pixels.foreach_get(apx)
                px[:, 3] = apx.reshape(-1, 4)[:, 0]
                bpy.data.images.remove(am)
            dst = bpy.data.images.new(mid + '_albedo', w, h, alpha=True)
            dst.pixels.foreach_set(px.ravel())
            dst.filepath_raw = os.path.join(out, mid + '_albedo.png'); dst.file_format = 'PNG'; dst.save()
            bpy.data.images.remove(dst); bpy.data.images.remove(img)
        elif '_diff_' in f:
            save_tex(p, os.path.join(out, mid + '_albedo.jpg'), 'albedo')
        elif '_nor_gl_' in f:
            save_tex(p, os.path.join(out, mid + '_normal.jpg'), 'normal')
        elif '_arm_' in f:
            save_tex(p, os.path.join(out, mid + '_ms.png'), 'ms')
            save_tex(p, os.path.join(out, mid + '_ao.jpg'), 'ao')
    if mid in SPLIT:
        bpy.data.objects.remove(lod1)
        old_fbx = os.path.join(out, mid + '.fbx')
        if os.path.exists(old_fbx): os.remove(old_fbx)
        parts = split_export(lo, mid, out)
        json.dump({'id': mid, 'name': info.get('name'), 'license': 'CC0 (Poly Haven)', 'url': info.get('url'), 'authors': info.get('authors'), 'parts': parts},
                  open(os.path.join(out, mid + '.json'), 'w'), indent=1)
        print(f"WK {mid}: kit separado em {len(parts)} peças: " + ', '.join(f"{p['name'][-3:]} {p['size_m']}" for p in parts[:40]), flush=True)
        return
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


def split_export(lo, mid, out):
    """Kit numa malha só (o forte): separa por peças soltas, junta as que se tocam (ameias, degraus) e exporta
    cada grupo como <id>_pNN.fbx com o pivô no centro da base."""
    import mathutils
    bpy.ops.object.select_all(action='DESELECT')
    lo.select_set(True); bpy.context.view_layer.objects.active = lo
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='LOOSE'); bpy.ops.object.mode_set(mode='OBJECT')
    parts = [o for o in bpy.context.scene.objects if o.type == 'MESH' and not o.name.endswith('_LOD1')]
    def box(o):
        bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
        return (min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb), max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb))
    boxes = [box(o) for o in parts]
    parent = list(range(len(parts)))
    def f(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]; i = parent[i]
        return i
    pad = 0.15
    for i in range(len(parts)):
        a = boxes[i]
        for j in range(i + 1, len(parts)):
            b = boxes[j]
            if a[0] - pad <= b[3] and b[0] - pad <= a[3] and a[1] - pad <= b[4] and b[1] - pad <= a[4] and a[2] - pad <= b[5] and b[2] - pad <= a[5]:
                parent[f(i)] = f(j)
    groups = {}
    for i, o in enumerate(parts):
        groups.setdefault(f(i), []).append(o)
    pieces = []
    for g in groups.values():
        bpy.ops.object.select_all(action='DESELECT')
        for o in g: o.select_set(True)
        bpy.context.view_layer.objects.active = g[0]
        if len(g) > 1: bpy.ops.object.join()
        pieces.append(bpy.context.view_layer.objects.active)
    pieces = [p for p in pieces if max(p.dimensions) > 0.6]   # sobras minúsculas ficam de fora
    pieces.sort(key=lambda p: -p.dimensions.x * p.dimensions.y * p.dimensions.z)
    info = []
    for k, p in enumerate(pieces):
        name = f"{mid}_p{k:02d}"
        p.name = name
        bb = [p.matrix_world @ Vector(c) for c in p.bound_box]
        cx = sum(v.x for v in bb) / 8; cy = sum(v.y for v in bb) / 8; zmin = min(v.z for v in bb)
        p.data.transform(mathutils.Matrix.Translation((-cx, -cy, -zmin)))
        p.location = (0, 0, 0)
        p.data.materials.clear()
        for poly in p.data.polygons: poly.use_smooth = True
        lod1 = p.copy(); lod1.data = p.data.copy(); lod1.name = name + '_LOD1'
        bpy.context.collection.objects.link(lod1)
        decimate_to(lod1, max(200, int(faces(p) * 0.35)))
        bpy.ops.object.select_all(action='DESELECT'); p.select_set(True); lod1.select_set(True)
        bpy.ops.export_scene.fbx(filepath=os.path.join(out, name + '.fbx'), use_selection=True, apply_unit_scale=True,
                                 apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, object_types={'MESH'},
                                 mesh_smooth_type='FACE', add_leaf_bones=False, path_mode='STRIP')
        info.append({'name': name, 'faces': faces(p), 'size_m': [round(p.dimensions.x, 2), round(p.dimensions.y, 2), round(p.dimensions.z, 2)], 'at': [round(cx, 2), round(cy, 2)]})
    return info


def main():
    ids = ARGS[0].split(',') if ARGS else [m for m in TARGET if os.path.isdir(os.path.join(SRC, m))]
    for mid in ids:
        try:
            process(mid)
        except Exception as e:
            print(f"WK {mid}: ERRO {e}", flush=True)


main()
