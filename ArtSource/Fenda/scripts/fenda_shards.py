# Cacos de rocha da Fenda (a realidade estilhaçada em volta do rasgo, como no storyboard do autor).
# Procedural, sem assets de terceiros: cada caco é o fecho convexo de uma nuvem de pontos num elipsoide
# alongado, lascado por 2 a 5 planos (faces de fratura planas, como pedra/cristal partido) e com os
# vértices levemente empurrados por ruído (não fica liso demais). Sombreamento facetado.
# Cor de vértice: R = 1 nas faces de FRATURA (as que o shader faz brilhar em brasa quando o caco nasce),
#                 G = altura relativa (0 embaixo, 1 em cima: oclusão falsa), B = semente por caco.
# Tudo convexo (o caco fica a ~12 km: um caco convexo nunca briga consigo mesmo no depth buffer).
#   ~/.local/opt/blender-5.2/blender -b --factory-startup --python ArtSource/Fenda/scripts/fenda_shards.py
import bpy, bmesh, os, random, math
from mathutils import Vector, Matrix, noise
OUT = os.path.expanduser("~/Unity/ParkourLab/Assets/Aren/Resources/Fenda/FendaShards.fbx")
PREVIEW = os.path.expanduser("~/Unity/ParkourLab/ArtSource/Fenda/preview.png")
os.makedirs(os.path.dirname(OUT), exist_ok=True)
random.seed(7)
bpy.ops.wm.read_factory_settings(use_empty=True)

# (nome, proporções do elipsoide, pontos, cortes)
KINDS = [
    ("Shard_Spike_A", (1.0, 0.28, 0.22), 26, 3),
    ("Shard_Spike_B", (1.0, 0.34, 0.2), 22, 4),
    ("Shard_Spike_C", (1.0, 0.22, 0.3), 30, 2),
    ("Shard_Slab_A", (1.0, 0.75, 0.22), 30, 4),
    ("Shard_Slab_B", (0.9, 1.0, 0.3), 24, 5),
    ("Shard_Chunk_A", (1.0, 0.7, 0.6), 34, 5),
    ("Shard_Chunk_B", (0.8, 1.0, 0.7), 28, 4),
    ("Shard_Chunk_C", (1.0, 0.6, 0.5), 20, 3),
    ("Shard_Wedge_A", (1.0, 0.55, 0.18), 18, 3),
    ("Shard_Wedge_B", (1.0, 0.45, 0.35), 24, 4),
    ("Shard_Splinter", (1.0, 0.16, 0.14), 16, 2),
    ("Shard_Flake", (0.7, 1.0, 0.12), 20, 3),
]

def make(name, ax, npts, cuts, seed):
    rnd = random.Random(seed)
    bm = bmesh.new()
    pts = []
    for _ in range(npts):
        while True:
            v = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1)))
            if v.length <= 1: break
        # pontos puxados para a casca (formas mais "cheias") com pontas ocasionais
        v = v.normalized() * (0.65 + 0.35 * v.length) * (1.25 if rnd.random() < 0.12 else 1.0)
        pts.append(bm.verts.new(Vector((v.x * ax[0], v.y * ax[1], v.z * ax[2]))))
    bmesh.ops.convex_hull(bm, input=pts)
    # remove o que sobrou de vértices internos
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    for f in bm.faces: f.material_index = 0
    frac_layer = bm.faces.layers.int.new("frac")
    # lascas: planos que cortam um pedaço (a face nova é uma face de fratura)
    for c in range(cuts):
        n = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1))).normalized()
        ext = max(abs(n.x) * ax[0], abs(n.y) * ax[1], abs(n.z) * ax[2])
        d = ext * rnd.uniform(0.45, 0.8)
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=n * d, plane_no=n, clear_outer=True)
        edges = [e for e in res['geom_cut'] if isinstance(e, bmesh.types.BMEdge)]
        if edges:
            fill = bmesh.ops.holes_fill(bm, edges=edges, sides=0)
            for f in fill['faces']: f[frac_layer] = 1
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    # ruído leve nos vértices (sem perder a convexidade: só ao longo da direção do centro, pouco)
    for v in bm.verts:
        k = 1.0 + 0.06 * noise.noise(v.co * 2.3 + Vector((seed, seed * 0.3, 1.7)))
        v.co *= k
    # normaliza: centro no centróide, maior dimensão = 1 (o script do Unity escala em metros)
    c = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
    for v in bm.verts: v.co -= c
    r = max(v.co.length for v in bm.verts)
    for v in bm.verts: v.co /= r
    # fecho convexo de novo por segurança (o ruído pode ter criado concavidades mínimas): mantém a marca
    me = bpy.data.meshes.new(name)
    zmin = min(v.co.z for v in bm.verts); zmax = max(v.co.z for v in bm.verts)
    col = me.color_attributes.new("Col", 'BYTE_COLOR', 'CORNER') if False else None
    bm.to_mesh(me)
    frac = [f[frac_layer] for f in bm.faces]
    bm.free()
    ca = me.color_attributes.new("Col", 'BYTE_COLOR', 'CORNER')
    s = rnd.random()
    for poly in me.polygons:
        for li in poly.loop_indices:
            vz = me.vertices[me.loops[li].vertex_index].co.z
            ca.data[li].color = (float(frac[poly.index]), (vz - zmin) / max(1e-4, zmax - zmin), s, 1.0)
    for p in me.polygons: p.use_smooth = False
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob

objs = [make(n, ax, p, c, 100 + i * 17) for i, (n, ax, p, c) in enumerate(KINDS)]
tris = sum(len(o.data.polygons) for o in objs)
print("cacos:", len(objs), "triângulos:", tris)

# prévia: os cacos lado a lado com uma luz violeta de lado (só para conferir a forma)
for i, o in enumerate(objs):
    o.location = ((i % 6) * 2.4 - 6, 0, (i // 6) * -2.4 + 1.2)
    o.rotation_euler = (0.4 * i, 0.7 * i, 0.3 * i)
bpy.ops.object.camera_add(location=(0, -14, 0), rotation=(math.pi / 2, 0, 0))
cam = bpy.context.object; cam.data.lens = 50; bpy.context.scene.camera = cam
bpy.ops.object.light_add(type='SUN', location=(6, -3, 4)); sun = bpy.context.object
sun.data.energy = 4; sun.data.color = (0.75, 0.6, 1.0); sun.rotation_euler = (0.9, 0.6, 1.9)
bpy.ops.object.light_add(type='SUN'); fill = bpy.context.object; fill.data.energy = 0.6; fill.data.color = (0.5, 0.6, 1.0); fill.rotation_euler = (-0.6, 0, -0.8)
mat = bpy.data.materials.new("Rock"); mat.use_nodes = True
bs = mat.node_tree.nodes.get("Principled BSDF"); bs.inputs["Base Color"].default_value = (0.08, 0.075, 0.09, 1); bs.inputs["Roughness"].default_value = 0.85
for o in objs: o.data.materials.append(mat)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_WORKBENCH'
sc.render.resolution_x, sc.render.resolution_y = 1200, 520
sc.world = bpy.data.worlds.new("W"); sc.world.color = (0.02, 0.02, 0.035)
sc.render.filepath = PREVIEW
try:
    bpy.ops.render.render(write_still=True)
except Exception as e:
    print("prévia falhou:", e)

# exporta (sem material: o Unity usa Aren/FendaShard); posição/rotação zeradas
for o in objs:
    o.location = (0, 0, 0); o.rotation_euler = (0, 0, 0); o.data.materials.clear()
bpy.ops.object.select_all(action='DESELECT')
for o in objs: o.select_set(True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={'MESH'}, mesh_smooth_type='FACE',
                         use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False, colors_type='LINEAR',
                         apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y')
print("exportado:", OUT)
