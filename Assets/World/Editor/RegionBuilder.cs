using System.Collections.Generic;
using System.Linq;
using Elyndra.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Elyndra.WorldEditor
{
    /// <summary>
    /// Constrói UMA cena de reino a partir de um RegionLayout (receita em RegionRecipes): terreno do bioma
    /// (estradas e praças recortadas no relevo), chão com as fotos do reino, rios/mar, cidades com bairros
    /// (praça, mercado, templo, oficina, taverna, casas — kit gótico de Campânula tingido pelo reino + peças do
    /// estilo), marcos, vegetação em células de streaming, portões para os reinos vizinhos (trancados pelo
    /// WorldState), pontos de retorno, zonas de inimigos por nível, Vórtice, arenas de chefe e miniboss,
    /// pontos de interesse (relíquia, artefatos, segredos, NPCs), áreas futuras bloqueadas, horizonte com os
    /// reinos vizinhos e a Fenda no céu, partículas do ar, câmeras de cinema, NavMesh nas áreas de luta.
    /// </summary>
    public static class RegionBuilder
    {
        const string Models = "Assets/Campanula/Models/";
        const string DPS = "Assets/Dynamic Parkour System/Prefabs/";
        public const string SceneDir = "Assets/World/Scenes/Regions";
        public const int LayerDetail = 11, LayerVegetation = 12;

        static Transform root, gTerrain, gPaths, gWater, gTowns, gMarks, gVeg, gGame, gHorizon, gAtmos, gCams;
        static Terrain terrain;
        static RegionLayout L;
        static RegionDef D;
        static RegionProfile P;
        static System.Random rng;
        static readonly List<(Vector2 c, float r)> blockers = new List<(Vector2, float)>();
        static readonly List<(Vector2 a, Vector2 b, float w)> roads = new List<(Vector2, Vector2, float)>();
        static readonly List<(Vector3 c, Vector3 size)> navVolumes = new List<(Vector3, Vector3)>();
        static System.Text.StringBuilder log;

        public static float CurrentSize => L != null ? L.size : 1600f;
        static float terrainMin;
        static float R01() => (float)rng.NextDouble();
        static float RR(float a, float b) => a + (b - a) * R01();

        // ================================================================== entrada

        public static string Build(RegionLayout layout, RegionProfile profile)
        {
            L = layout; D = WorldCanon.Region(layout.id); P = profile;
            rng = new System.Random(1000 + (int)layout.id * 37);
            log = new System.Text.StringBuilder();
            blockers.Clear(); roads.Clear(); navVolumes.Clear();
            terrain = null;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            root = new GameObject("Reino — " + D.name).transform;
            gTerrain = Group("Terreno"); gPaths = Group("Caminhos e pontes"); gWater = Group("Água"); gTowns = Group("Cidades e vilas");
            gMarks = Group("Marcos"); gVeg = Group("Vegetação e rochas"); gGame = Group("Jogo"); gHorizon = Group("Horizonte e céu");
            gAtmos = Group("Atmosfera"); gCams = Group("Câmeras de cinema");

            AddSettlementStreets();
            foreach (var s in L.settlements) blockers.Add((s.c, s.radius + 8f));
            foreach (var a in L.arenas) blockers.Add((a.pos, a.radius + 4f));
            foreach (var lm in L.landmarks) blockers.Add((lm.pos, 18f * lm.scale));
            foreach (var p in L.paths) for (int i = 0; i + 1 < p.pts.Count; i++) roads.Add((p.pts[i], p.pts[i + 1], p.width));

            BuildTerrain();
            BuildLighting();
            BuildWater();
            BuildPathsDressing();
            foreach (var s in L.settlements) BuildSettlement(s);
            foreach (var lm in L.landmarks) Landmarks.Build(lm, gMarks);
            BuildVegetation();
            BuildGameplay();
            BuildHorizon();
            BuildAtmosphere();
            BuildBounds();
            BakeNav();

            System.IO.Directory.CreateDirectory(SceneDir);
            string path = $"{SceneDir}/{D.scene}.unity";
            EditorSceneManager.SaveScene(scene, path);
            log.Append("cena salva: " + path + "\n");
            return log.ToString();
        }

        static Transform Group(string n) { var t = new GameObject(n).transform; t.SetParent(root, false); return t; }

        // ================================================================== terreno

        public static float Y(float x, float z)
        {
            if (terrain != null) return terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
            return L.height(x, z);
        }
        public static Vector3 G(Vector2 p, float lift = 0f) => new Vector3(p.x, Y(p.x, p.y) + lift, p.y);

        static void AddSettlementStreets()
        {
            // ruas de cada cidade: da praça até depois da borda, nas direções das saídas (viram calçamento recortado)
            foreach (var s in L.settlements)
                foreach (var ang in s.exits)
                {
                    var d = Dir(ang);
                    var style = s.style == SettlementStyle.Acampamento || s.style == SettlementStyle.AldeiaArvore ? PathStyle.Trilha : PathStyle.Calcada;
                    L.paths.Add(new PathSpec(s.name + " — rua " + ang, style, 5f, s.c + d * (s.radius * 0.22f), s.c + d * (s.radius + 14f)));
                }
        }

        public static Vector2 Dir(float deg) { float a = deg * Mathf.Deg2Rad; return new Vector2(Mathf.Sin(a), Mathf.Cos(a)); }

        static List<Vector2> Densify(List<Vector2> pts, float step)
        {
            var o = new List<Vector2>();
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                float len = Vector2.Distance(pts[i], pts[i + 1]);
                int n = Mathf.Max(1, Mathf.CeilToInt(len / step));
                for (int k = 0; k < n; k++) o.Add(Vector2.Lerp(pts[i], pts[i + 1], k / (float)n));
            }
            if (pts.Count > 0) o.Add(pts[pts.Count - 1]);
            // suaviza os cantos (Chaikin, 2 passes)
            for (int pass = 0; pass < 2 && o.Count > 3; pass++)
            {
                var s = new List<Vector2> { o[0] };
                for (int i = 0; i + 1 < o.Count; i++) { s.Add(Vector2.Lerp(o[i], o[i + 1], 0.25f)); s.Add(Vector2.Lerp(o[i], o[i + 1], 0.75f)); }
                s.Add(o[o.Count - 1]);
                o = s;
            }
            return o;
        }

        static void BuildTerrain()
        {
            int res = L.heightRes;
            float size = L.size, x0 = L.center.x - size / 2, z0 = L.center.y - size / 2;
            var H = new float[res, res];
            float step = size / (res - 1);
            for (int iz = 0; iz < res; iz++)
                for (int ix = 0; ix < res; ix++)
                    H[iz, ix] = L.height(x0 + ix * step, z0 + iz * step);

            // recortes: estradas (perfil suavizado, rampa máx. ~13%), rios (leito), praças e arenas (plano)
            var W = new float[res, res]; var T = new float[res, res];
            void Stamp(Vector2 p, float h, float inner, float outer, float bias = 1f)
            {
                int cx = Mathf.RoundToInt((p.x - x0) / step), cz = Mathf.RoundToInt((p.y - z0) / step);
                int rr = Mathf.CeilToInt(outer / step) + 1;
                for (int iz = Mathf.Max(0, cz - rr); iz <= Mathf.Min(res - 1, cz + rr); iz++)
                    for (int ix = Mathf.Max(0, cx - rr); ix <= Mathf.Min(res - 1, cx + rr); ix++)
                    {
                        float d = Vector2.Distance(p, new Vector2(x0 + ix * step, z0 + iz * step));
                        float w = d <= inner ? 1f : Mathf.SmoothStep(1f, 0f, (d - inner) / Mathf.Max(0.01f, outer - inner));
                        w *= bias;
                        if (w > W[iz, ix]) { W[iz, ix] = w; T[iz, ix] = h; }
                    }
            }
            float Raw(Vector2 p) => L.height(p.x, p.y);
            foreach (var s in L.settlements) Stamp(s.c, Raw(s.c), s.radius * 0.9f, s.radius + 30f);
            foreach (var a in L.arenas) if (a.dressing != LandmarkKind.Cratera) Stamp(a.pos, Raw(a.pos), a.radius, a.radius + 18f);
            foreach (var p in L.paths)
            {
                if (p.style == PathStyle.Ponte) continue;
                var d = Densify(p.pts, 2f);
                var h = new float[d.Count];
                for (int i = 0; i < d.Count; i++) h[i] = Raw(d[i]);
                // praças/arenas puxam o perfil da estrada para a altura delas (encaixe limpo)
                for (int i = 0; i < d.Count; i++)
                    foreach (var s in L.settlements) { float dd = Vector2.Distance(d[i], s.c); if (dd < s.radius + 20f) h[i] = Mathf.Lerp(h[i], Raw(s.c), Mathf.Clamp01(1f - (dd - s.radius) / 20f)); }
                for (int pass = 0; pass < 4; pass++)
                {
                    var c = (float[])h.Clone();
                    for (int i = 0; i < h.Length; i++) { float acc = 0; int n = 0; for (int k = -7; k <= 7; k++) { int j = Mathf.Clamp(i + k, 0, h.Length - 1); acc += c[j]; n++; } h[i] = acc / n; }
                }
                float maxStep = 0.13f * 2f;
                for (int i = 1; i < h.Length; i++) h[i] = Mathf.Clamp(h[i], h[i - 1] - maxStep, h[i - 1] + maxStep);
                for (int i = h.Length - 2; i >= 0; i--) h[i] = Mathf.Clamp(h[i], h[i + 1] - maxStep, h[i + 1] + maxStep);
                for (int i = 0; i < d.Count; i++) Stamp(d[i], h[i] - 0.05f, p.width * 0.5f + 1f, p.width * 0.5f + 9f);
            }
            foreach (var r in L.rivers)
            {
                var d = Densify(r, 3f);
                var h = new float[d.Count];
                for (int i = 0; i < d.Count; i++) h[i] = Raw(d[i]);
                for (int pass = 0; pass < 6; pass++) { var c = (float[])h.Clone(); for (int i = 0; i < h.Length; i++) { float acc = 0; int n = 0; for (int k = -6; k <= 6; k++) { int j = Mathf.Clamp(i + k, 0, h.Length - 1); acc += c[j]; n++; } h[i] = acc / n; } }
                for (int i = 1; i < h.Length; i++) h[i] = Mathf.Min(h[i], h[i - 1] + 0.05f);   // o rio só desce
                for (int i = 0; i < d.Count; i++) Stamp(d[i], h[i] - 2.4f, L.riverWidth * 0.5f, L.riverWidth * 0.5f + 7f);
            }
            float mn = float.MaxValue, mx = float.MinValue;
            for (int iz = 0; iz < res; iz++)
                for (int ix = 0; ix < res; ix++)
                {
                    float v = Mathf.Lerp(H[iz, ix], T[iz, ix], W[iz, ix]);
                    H[iz, ix] = v; mn = Mathf.Min(mn, v); mx = Mathf.Max(mx, v);
                }
            mn -= 2f; float range = Mathf.Max(20f, mx - mn + 4f);
            terrainMin = mn;
            var hn = new float[res, res];
            for (int iz = 0; iz < res; iz++) for (int ix = 0; ix < res; ix++) hn[iz, ix] = (H[iz, ix] - mn) / range;

            var td = new TerrainData { heightmapResolution = res };
            td.alphamapResolution = 512; td.baseMapResolution = 256;
            td.size = new Vector3(size, range, size);
            td.SetHeights(0, 0, hn);
            td.terrainLayers = new[] { "TL_grass", "TL_dirt", "TL_cobble", "TL_field" }.Select(n => AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Campanula/Materials/" + n + ".terrainlayer")).ToArray();

            // chão: R base (capim/folhas/neve…), G terra/estrada, B calçamento, A camada especial do reino
            int ar = 512; float ast = size / ar;
            var dirt = new float[ar, ar]; var cob = new float[ar, ar];
            for (int i = 0; i < ar; i++) for (int j = 0; j < ar; j++) { dirt[i, j] = 999f; cob[i, j] = 999f; }
            void Paint(float[,] dist, Vector2 p, float half)
            {
                int cx = Mathf.RoundToInt((p.x - x0) / ast), cz = Mathf.RoundToInt((p.y - z0) / ast);
                int rr = Mathf.CeilToInt((half + 4f) / ast);
                for (int iz = Mathf.Max(0, cz - rr); iz <= Mathf.Min(ar - 1, cz + rr); iz++)
                    for (int ix = Mathf.Max(0, cx - rr); ix <= Mathf.Min(ar - 1, cx + rr); ix++)
                    {
                        float d = Vector2.Distance(p, new Vector2(x0 + (ix + 0.5f) * ast, z0 + (iz + 0.5f) * ast)) - half;
                        if (d < dist[iz, ix]) dist[iz, ix] = d;
                    }
            }
            foreach (var p in L.paths) { if (p.style == PathStyle.Ponte) continue; foreach (var q in Densify(p.pts, 1.5f)) Paint(p.style == PathStyle.Calcada ? cob : dirt, q, p.width * 0.5f); }
            foreach (var r in L.rivers) foreach (var q in Densify(r, 2f)) Paint(dirt, q, L.riverWidth * 0.5f + 2.5f);
            foreach (var s in L.settlements)
            {
                bool rustic = s.style == SettlementStyle.Acampamento || s.style == SettlementStyle.AldeiaArvore || s.style == SettlementStyle.Caravana;
                Paint(rustic ? dirt : cob, s.c, s.radius * 0.3f);
            }
            foreach (var a in L.arenas) Paint(dirt, a.pos, a.radius * 0.8f);
            var alpha = new float[ar, ar, 4];
            for (int iz = 0; iz < ar; iz++)
                for (int ix = 0; ix < ar; ix++)
                {
                    float x = x0 + (ix + 0.5f) * ast, z = z0 + (iz + 0.5f) * ast;
                    float n = Mathf.PerlinNoise(x * 0.13f + 3f, z * 0.13f + 7f);
                    int hi = Mathf.Clamp(Mathf.RoundToInt(iz * (res - 1f) / (ar - 1f)), 1, res - 2), hj = Mathf.Clamp(Mathf.RoundToInt(ix * (res - 1f) / (ar - 1f)), 1, res - 2);
                    float y = H[hi, hj];
                    float slope = Mathf.Atan(new Vector2(H[hi, hj + 1] - H[hi, hj - 1], H[hi + 1, hj] - H[hi - 1, hj]).magnitude / (2f * step)) * Mathf.Rad2Deg;
                    float g = Mathf.Clamp01(1f - (dirt[iz, ix] + (n - 0.5f) * 1.6f) / 1.4f);
                    float c = Mathf.Clamp01(1f - (cob[iz, ix] + (n - 0.5f) * 0.8f) / 0.9f);
                    float sp = L.special != null ? Mathf.Clamp01(L.special(x, z, y, slope)) : 0f;
                    sp *= 1f - Mathf.Max(g, c);
                    float sum = g + c + sp;
                    if (sum > 1f) { g /= sum; c /= sum; sp /= sum; sum = 1f; }
                    alpha[iz, ix, 0] = 1f - sum; alpha[iz, ix, 1] = g; alpha[iz, ix, 2] = c; alpha[iz, ix, 3] = sp;
                }
            td.SetAlphamaps(0, 0, alpha);
            BuildDetails(td, alpha, x0, z0, size);
            System.IO.Directory.CreateDirectory("Assets/World/Data/Terrain");
            string tp = $"Assets/World/Data/Terrain/{D.scene}_Terreno.asset";
            AssetDatabase.DeleteAsset(tp);
            AssetDatabase.CreateAsset(td, tp);
            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terreno — " + D.name;
            go.transform.SetParent(gTerrain, false);
            go.transform.position = new Vector3(x0, mn, z0);
            terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = WorldMats.Terrain(D.scene, L.tex);
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 5000f;
            terrain.drawInstanced = true;
            terrain.detailObjectDistance = 45f;
            terrain.detailObjectDensity = 0.8f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            terrain.drawTreesAndFoliage = false;
            var gf = go.AddComponent<Campanula.GrassField>();
            gf.terrain = terrain;
            gf.macro = WorldMats.Macro;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            log.Append($"terreno {size} m, altura {mn:0}..{mn + range:0} m\n");
        }

        static void BuildDetails(TerrainData td, float[,,] alpha, float x0, float z0, float size)
        {
            int dr = 512;
            td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            td.SetDetailResolution(dr, 16);
            td.detailPrototypes = new[]
            {
                new DetailPrototype { prototypeTexture = WorldMats.Tex("Assets/Campanula/Textures/detail_grass.png"), renderMode = DetailRenderMode.GrassBillboard, minWidth = 0.55f, maxWidth = 0.95f, minHeight = 0.32f, maxHeight = 0.62f, noiseSpread = 0.35f },
                new DetailPrototype { prototypeTexture = WorldMats.Tex("Assets/Campanula/Textures/detail_wheat.png"), renderMode = DetailRenderMode.Grass, minWidth = 0.7f, maxWidth = 1.05f, minHeight = 0.8f, maxHeight = 1.15f, noiseSpread = 0.25f },
            };
            int ar = alpha.GetLength(0);
            var g = new int[dr, dr]; var w = new int[dr, dr];
            if (L.veg.grass > 0f || L.veg.wheat > 0f)
                for (int iz = 0; iz < dr; iz++)
                    for (int ix = 0; ix < dr; ix++)
                    {
                        float x = x0 + size * (ix + 0.5f) / dr, z = z0 + size * (iz + 0.5f) / dr;
                        int ax = Mathf.Clamp(ix * ar / dr, 0, ar - 1), az = Mathf.Clamp(iz * ar / dr, 0, ar - 1);
                        float n = Mathf.PerlinNoise(x * 0.07f + 5f, z * 0.07f + 9f), n2 = Mathf.PerlinNoise(x * 0.5f, z * 0.5f);
                        float extra = L.veg.density != null ? L.veg.density(x, z) : 1f;
                        if (alpha[az, ax, 0] > 0.8f && L.veg.grass > 0f) g[iz, ix] = Mathf.RoundToInt(Mathf.Clamp01((n - 0.3f) * 1.6f) * (2f + n2 * 3f) * L.veg.grass * Mathf.Lerp(0.6f, 1.2f, extra));
                        if (alpha[az, ax, 3] > 0.8f && L.veg.wheat > 0f) w[iz, ix] = Mathf.RoundToInt((3 + n2 * 3f) * L.veg.wheat);
                    }
            td.SetDetailLayer(0, 0, 0, g);
            td.SetDetailLayer(0, 0, 1, w);
        }

        // ================================================================== luz, céu, água

        static void BuildLighting()
        {
            var sun = new GameObject("Sol / luz principal").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.color = P.sunColor; sun.intensity = P.sunIntensity;
            sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.8f; sun.shadowBias = 0.05f; sun.shadowNormalBias = 0.4f;
            sun.transform.rotation = Quaternion.Euler(P.sunAngles.x, P.sunAngles.y, 0f);
            sun.lightmapBakeType = LightmapBakeType.Realtime;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = P.ambientSky; RenderSettings.ambientEquatorColor = P.ambientEquator; RenderSettings.ambientGroundColor = P.ambientGround;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = P.fogColor; RenderSettings.fogDensity = P.fogDensity;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.45f;
            var fd = WorldCanon.FendaDirection(L.id);
            float fk = Mathf.Clamp01(1f - WorldCanon.FendaDistanceKm(L.id) / 70f);
            RenderSettings.skybox = WorldMats.Sky(P, fd, fk);
            var ls = new LightingSettings { name = D.scene + "_Luz", bakedGI = false, realtimeGI = false };
            System.IO.Directory.CreateDirectory("Assets/World/Data/Lighting");
            string lp = $"Assets/World/Data/Lighting/{D.scene}.lighting";
            AssetDatabase.DeleteAsset(lp);
            AssetDatabase.CreateAsset(ls, lp);
            Lightmapping.lightingSettings = ls;
            var rr = root.gameObject.AddComponent<RegionRoot>();
            rr.region = L.id; rr.profile = P; rr.sun = sun;
        }

        static void BuildWater()
        {
            var wm = WorldMats.Water(D.scene, L.waterDeep, L.waterSky);
            foreach (var r in L.rivers)
            {
                var d = Densify(r, 3f);
                var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
                float v = 0;
                for (int i = 0; i < d.Count; i++)
                {
                    var a = d[Mathf.Max(0, i - 1)]; var b = d[Mathf.Min(d.Count - 1, i + 1)];
                    var t = (b - a).normalized; var nrm = new Vector2(-t.y, t.x);
                    float y = Y(d[i].x, d[i].y) + 1.5f;
                    var l = d[i] + nrm * (L.riverWidth * 0.5f + 1.5f); var rgt = d[i] - nrm * (L.riverWidth * 0.5f + 1.5f);
                    verts.Add(new Vector3(l.x, y, l.y)); verts.Add(new Vector3(rgt.x, y, rgt.y));
                    if (i > 0) v += Vector2.Distance(d[i], d[i - 1]);
                    uvs.Add(new Vector2(0, v)); uvs.Add(new Vector2(1, v));
                    if (i > 0) { int k = verts.Count - 4; tris.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
                }
                var m = new Mesh { name = "Rio" };
                m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateBounds();
                System.IO.Directory.CreateDirectory(ProcMesh.Dir);
                string mp = $"{ProcMesh.Dir}/{D.scene}_rio_{L.rivers.IndexOf(r)}.asset";
                AssetDatabase.DeleteAsset(mp); AssetDatabase.CreateAsset(m, mp);
                var go = new GameObject("Rio");
                go.transform.SetParent(gWater, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = wm; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (!float.IsNegativeInfinity(L.seaLevel))
            {
                var sea = Object(L.glassSea ? "Mar de Vidro (cristalizado — caminhável)" : "Mar", ProcMesh.Box(L.size * 3f, 0.2f, L.size * 3f), L.glassSea ? WorldMats.GlassSea() : wm,
                    new Vector3(L.center.x, L.seaLevel - 0.2f, L.center.y), Quaternion.identity, Vector3.one, gWater, L.glassSea);
                sea.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // ================================================================== peças

        /// <summary>Objeto com malha procedural + material, estático; colisor de malha opcional.</summary>
        public static GameObject Object(string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3 scale, Transform parent, bool collider = true, int layer = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            if (mat != null && mat.shader != null && (mat.shader.name == "Elyndra/Glow" || mat.shader.name == "Elyndra/Crystal")) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (collider) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; }
            if (layer != 0) go.layer = layer;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        public static PlaceholderTag Tag(GameObject go, string replaceWith, string category = "Ambiente")
        {
            var t = go.GetComponent<PlaceholderTag>() ?? go.AddComponent<PlaceholderTag>();
            t.replaceWith = replaceWith; t.category = category;
            return t;
        }

        static readonly string[] StoneMats = { "CMP_ashlar", "CMP_stone_wall", "CMP_stone_dark", "CMP_rock", "CMP_trim", "CMP_cobble" };
        static readonly string[] RoofMats = { "CMP_roof_slate", "CMP_roof_tiles", "CMP_straw" };
        static readonly string[] WoodMats = { "CMP_timber", "CMP_planks", "CMP_plaster", "CMP_bark" };

        /// <summary>Modelo do kit de Campânula (FBX do Blender) com LOD, colisor e as cores do reino.</summary>
        public static GameObject Kit(string model, Vector3 pos, float yaw, Transform parent, float scale = 1f, bool collider = true, bool snap = true, int layer = 0)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Models + model + ".fbx");
            if (src == null) { Debug.LogWarning("[Elyndra] modelo do kit não encontrado: " + model); return null; }
            var holder = new GameObject(model);
            holder.transform.SetParent(parent, false);
            if (snap && terrain != null) pos.y = Y(pos.x, pos.z) + pos.y;
            holder.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            holder.transform.localScale = Vector3.one * scale;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, holder.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0, 180f, 0) * src.transform.localRotation;
            GameObjectUtility.SetStaticEditorFlags(holder, StaticEditorFlags.BatchingStatic);
            if (L != null && D != null) Recolor(go);
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                GameObjectUtility.SetStaticEditorFlags(mf.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr != null) mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                if (collider) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; }
                if (layer != 0) mf.gameObject.layer = layer;
            }
            if (L != null && L.windowGlow)
                foreach (var t in go.GetComponentsInChildren<Transform>())
                    if (t.name.StartsWith("WIN_") && R01() < 0.55f)
                    {
                        var q = Object("Janela acesa", ProcMesh.Box(0.9f, 1.3f, 0.02f), WorldMats.Glow(new Color(1.6f, 0.9f, 0.4f), 0f, 0.6f), t.position + t.forward * 0.03f - Vector3.up * 0.6f, t.rotation, Vector3.one, holder.transform, false);
                        q.layer = LayerDetail;
                    }
            var lodSrc = AssetDatabase.LoadAssetAtPath<GameObject>(Models + model + "_LOD1.fbx");
            if (lodSrc != null)
            {
                var lod1 = (GameObject)PrefabUtility.InstantiatePrefab(lodSrc, holder.transform);
                PrefabUtility.UnpackPrefabInstance(lod1, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                lod1.transform.localPosition = Vector3.zero;
                lod1.transform.localRotation = go.transform.localRotation;
                var drop = new List<GameObject>();
                foreach (var t in lod1.GetComponentsInChildren<Transform>(true))
                    if (t != lod1.transform && (t.name.StartsWith("WIN_") || t.name.StartsWith("LAMP_") || t.name.StartsWith("LEDGE_") || t.name.StartsWith("TOP_") || t.name.StartsWith("FALL_"))) drop.Add(t.gameObject);
                foreach (var g in drop) UnityEngine.Object.DestroyImmediate(g);
                if (L != null && D != null) Recolor(lod1);
                foreach (var mf in lod1.GetComponentsInChildren<MeshFilter>())
                    GameObjectUtility.SetStaticEditorFlags(mf.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                var lg = holder.AddComponent<LODGroup>();
                lg.SetLODs(new[] { new LOD(0.25f, go.GetComponentsInChildren<Renderer>()), new LOD(0.01f, lod1.GetComponentsInChildren<Renderer>()) });
                lg.RecalculateBounds();
            }
            return holder;
        }

        static void Recolor(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string n = mats[i].name;
                    Color tint = System.Array.IndexOf(StoneMats, n) >= 0 ? L.stoneTint : System.Array.IndexOf(RoofMats, n) >= 0 ? L.roofTint : System.Array.IndexOf(WoodMats, n) >= 0 ? L.woodTint : Color.white;
                    if (tint == Color.white) continue;
                    mats[i] = WorldMats.KitVariant(mats[i], D.scene, tint);
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        // ================================================================== caminhos

        static void BuildPathsDressing()
        {
            var lamp = WorldMats.Glow(new Color(1.8f, 1.05f, 0.5f), 0.12f, 1.2f);
            foreach (var p in L.paths)
            {
                if (p.style == PathStyle.Ponte) { BuildBridge(p); continue; }
                if (!p.lamps) continue;
                var d = Densify(p.pts, 1f);
                float acc = 0; int side = 1;
                for (int i = 1; i < d.Count; i++)
                {
                    acc += Vector2.Distance(d[i], d[i - 1]);
                    if (acc < 34f) continue;
                    acc = 0; side = -side;
                    var t = (d[i] - d[i - 1]).normalized; var nrm = new Vector2(-t.y, t.x);
                    var q = d[i] + nrm * side * (p.width * 0.5f + 1.2f);
                    if (InsideSettlement(q, 0.95f)) continue;
                    var lp = Kit("LampPost", new Vector3(q.x, 0, q.y), Mathf.Atan2(t.x, t.y) * Mathf.Rad2Deg, gPaths, 1f, true, true, LayerDetail);
                    if (lp != null) Object("Chama", ProcMesh.Sphere(8), lamp, lp.transform.position + Vector3.up * 2.75f, Quaternion.identity, Vector3.one * 0.35f, lp.transform, false, LayerDetail);
                }
            }
        }

        static void BuildBridge(PathSpec p)
        {
            var a = p.pts[0]; var b = p.pts[p.pts.Count - 1];
            float ya = Y(a.x, a.y), yb = Y(b.x, b.y);
            var dir = b - a; float len = dir.magnitude; dir /= len;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            var mat = WorldMats.Stone("camp:planks", new Color(0.8f, 0.7f, 0.6f) * L.woodTint, 2.5f);
            var stone = WorldMats.Stone("camp:stone_wall", L.stoneTint, 3f);
            var root = new GameObject(p.name).transform; root.SetParent(gPaths, false);
            int seg = Mathf.Max(1, Mathf.CeilToInt(len / 6f));
            for (int i = 0; i < seg; i++)
            {
                float t0 = i / (float)seg, t1 = (i + 1f) / seg;
                var c = Vector2.Lerp(a, b, (t0 + t1) / 2);
                float y = Mathf.Lerp(ya, yb, (t0 + t1) / 2) + Mathf.Sin(((t0 + t1) / 2) * Mathf.PI) * Mathf.Min(6f, len * 0.04f);
                var deck = Object("Tabuleiro", ProcMesh.Box(p.width, 0.5f, len / seg + 0.05f), mat, new Vector3(c.x, y - 0.4f, c.y), Quaternion.Euler(0, yaw, 0), Vector3.one, root);
                if (i % 2 == 0)
                {
                    float ground = Y(c.x, c.y);
                    float h = y - ground;
                    if (h > 3f)
                        foreach (int sgn in new[] { -1, 1 })
                        {
                            var off = new Vector2(-dir.y, dir.x) * sgn * (p.width * 0.5f - 0.4f);
                            Object("Pilar", ProcMesh.Prism(6, 0.7f, 0.55f, h + 2f), stone, new Vector3(c.x + off.x, ground - 2f, c.y + off.y), Quaternion.identity, Vector3.one, root);
                        }
                }
                foreach (int sgn in new[] { -1, 1 })
                {
                    var off = new Vector2(-dir.y, dir.x) * sgn * (p.width * 0.5f - 0.15f);
                    Object("Guarda-corpo", ProcMesh.Box(0.2f, 1.0f, len / seg), mat, new Vector3(c.x + off.x, y - 0.15f, c.y + off.y), Quaternion.Euler(0, yaw, 0), Vector3.one, root);
                }
            }
            Tag(root.gameObject, "Ponte provisória (tabuleiro e pilares procedurais) — trocar por ponte modelada no estilo do reino", "Construção");
        }

        public static bool InsideSettlement(Vector2 q, float k = 1f)
        {
            foreach (var s in L.settlements) if (Vector2.Distance(q, s.c) < s.radius * k) return true;
            return false;
        }

        public static bool NearRoad(Vector2 q, float extra)
        {
            foreach (var (a, b, w) in roads)
            {
                var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                if (Vector2.Distance(q, a + ab * t) < w * 0.5f + extra) return true;
            }
            return false;
        }

        static bool Blocked(Vector2 q, float extra = 0f)
        {
            foreach (var (c, r) in blockers) if (Vector2.Distance(q, c) < r + extra) return true;
            return false;
        }

        // ================================================================== cidades

        static void BuildSettlement(SettlementSpec s)
        {
            var g = new GameObject(s.name + " (" + s.style + ")").transform; g.SetParent(gTowns, false);
            g.position = G(s.c);
            var content = new GameObject("Conteúdo").transform; content.SetParent(g, false);
            var cell = g.gameObject.AddComponent<StreamingCell>(); cell.content = content.gameObject; cell.radius = Mathf.Max(380f, s.radius * 5f);
            Settlements.Build(s, content, rng);
            log.Append($"cidade {s.name}: estilo {s.style}\n");
        }

        // ================================================================== vegetação

        static void BuildVegetation()
        {
            var cells = new Dictionary<Vector2Int, Transform>();
            Transform Cell(Vector2 p)
            {
                var k = new Vector2Int(Mathf.FloorToInt(p.x / 250f), Mathf.FloorToInt(p.y / 250f));
                if (cells.TryGetValue(k, out var t)) return t;
                var c = new GameObject($"Célula {k.x},{k.y}").transform; c.SetParent(gVeg, false);
                c.position = G(new Vector2((k.x + 0.5f) * 250f, (k.y + 0.5f) * 250f));
                var content = new GameObject("Conteúdo").transform; content.SetParent(c, false);
                var sc = c.gameObject.AddComponent<StreamingCell>(); sc.content = content.gameObject; sc.radius = 340f;
                cells[k] = content; return content;
            }
            float half = L.size / 2 - 30f;
            int trees = 0, rocks = 0, crystals = 0;
            // bosques de verdade: grade fina (9 m) com densidade por manchas (ou pela função do reino) — árvores
            // juntas onde há floresta, clareiras e campos abertos onde não há (antes: árvores soltas e uniformes)
            float spacing = 9f;
            int cap = Mathf.RoundToInt(3800 * Mathf.Clamp01(L.veg.trees * 1.4f));
            var crystalMat = WorldMats.Crystal(new Color(L.veg.crystalColor.r * 0.4f, L.veg.crystalColor.g * 0.4f, L.veg.crystalColor.b * 0.4f, 0.6f), L.veg.crystalColor * 1.6f, L.veg.crystalColor * 0.8f, 0.7f);
            var deadMat = WorldMats.Stone("camp:timber", new Color(0.35f, 0.32f, 0.3f), 1.5f);
            // ordem embaralhada: se bater o limite, as árvores que faltam somem por igual (não some o norte inteiro)
            var cellsList = new List<Vector2>();
            for (float z = -half; z < half; z += spacing) for (float x = -half; x < half; x += spacing) cellsList.Add(new Vector2(x, z));
            for (int i = cellsList.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var tmp = cellsList[i]; cellsList[i] = cellsList[j]; cellsList[j] = tmp; }
            foreach (var cxz in cellsList)
                {
                    if (trees >= cap) break;
                    float x = cxz.x, z = cxz.y;
                    var q = L.center + new Vector2(x + RR(-0.45f, 0.45f) * spacing, z + RR(-0.45f, 0.45f) * spacing);
                    float clump = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.66f, Mathf.PerlinNoise(q.x / 170f + 11f, q.y / 170f + 3f)));
                    float dens = L.veg.density != null ? Mathf.Clamp01(L.veg.density(q.x, q.y)) * (0.4f + 0.6f * clump) : clump;
                    float chance = L.veg.trees * (0.03f + 0.4f * dens);
                    if (R01() > chance) continue;
                    if (NearRoad(q, 4f) || Blocked(q) || InsideSettlement(q, 1.15f)) continue;
                    float y = Y(q.x, q.y);
                    if (!float.IsNegativeInfinity(L.seaLevel) && y < L.seaLevel + 1.5f) continue;
                    var nrm = terrain.terrainData.GetInterpolatedNormal((q.x - terrain.transform.position.x) / L.size, (q.y - terrain.transform.position.z) / L.size);
                    if (nrm.y < 0.8f) continue;
                    var parent = Cell(q);
                    if (L.veg.deadTrees && R01() < 0.7f)
                    {
                        float h = RR(5f, 11f);
                        Object("Árvore morta", ProcMesh.Prism(5, 0.35f, 0.06f, h, true, RR(-40f, 40f)), deadMat, new Vector3(q.x, y - 0.3f, q.y), Quaternion.Euler(RR(-8, 8), RR(0, 360), RR(-8, 8)), Vector3.one, parent, false, LayerVegetation);
                        trees++; continue;
                    }
                    string kind = L.veg.treeKinds[rng.Next(L.veg.treeKinds.Length)];
                    var tr = Kit(kind, new Vector3(q.x, -0.2f, q.y), RR(0, 360), parent, RR(0.85f, 1.45f) * (L.veg.giantTrees ? 1.6f : 1f), false, true, LayerVegetation);
                    if (tr != null)
                    {
                        var col = tr.AddComponent<CapsuleCollider>(); col.radius = 0.45f; col.height = 6f; col.center = new Vector3(0, 3f, 0);
                        trees++;
                    }
                }
            // rochas e cristais: em grupos (encostas, beiras de estrada), não salpicados
            float rs = Mathf.Lerp(120f, 45f, Mathf.Clamp01(L.veg.rocks));
            string[] rockModels = { "Rock_A", "Rock_B", "Cliff_Rock_A", "Cliff_Rock_B", "Cliff_Rock_C" };
            for (float z = -half; z < half; z += rs)
                for (float x = -half; x < half; x += rs)
                {
                    var q = L.center + new Vector2(x + RR(-0.4f, 0.4f) * rs, z + RR(-0.4f, 0.4f) * rs);
                    if (NearRoad(q, 3f) || Blocked(q, -4f) || InsideSettlement(q, 1.05f)) continue;
                    float y = Y(q.x, q.y);
                    if (!float.IsNegativeInfinity(L.seaLevel) && y < L.seaLevel - 2f) continue;
                    var parent = Cell(q);
                    if (L.veg.crystals > 0f && R01() < L.veg.crystals)
                    {
                        int n = rng.Next(2, 6);
                        var cl = new GameObject("Cristais").transform; cl.SetParent(parent, false); cl.position = new Vector3(q.x, y, q.y);
                        for (int k = 0; k < n; k++)
                        {
                            float h = RR(1.2f, 4.5f);
                            Object("Cristal", ProcMesh.Crystal(rng.Next(0, 12)), crystalMat, new Vector3(q.x + RR(-1.5f, 1.5f), y - 0.3f, q.y + RR(-1.5f, 1.5f)), Quaternion.Euler(RR(-25, 25), RR(0, 360), RR(-25, 25)), new Vector3(h, h, h), cl, k == 0, LayerDetail);
                        }
                        crystals++;
                        continue;
                    }
                    var nrm = terrain.terrainData.GetInterpolatedNormal((q.x - terrain.transform.position.x) / L.size, (q.y - terrain.transform.position.z) / L.size);
                    float slopeK = Mathf.InverseLerp(0.98f, 0.8f, nrm.y);   // encostas ganham mais pedra
                    if (R01() > L.veg.rocks * (0.35f + 1.3f * slopeK)) continue;
                    int m = rng.Next(2, 6);
                    for (int k = 0; k < m && rocks < 600; k++)
                    {
                        var qq = q + new Vector2(RR(-6f, 6f), RR(-6f, 6f));
                        var r = Kit(rockModels[rng.Next(rockModels.Length)], new Vector3(qq.x, -0.5f, qq.y), RR(0, 360), parent, RR(0.5f, 2f) * (k == 0 ? 1.4f : 1f), k < 2, true, 0);
                        if (r != null) rocks++;
                    }
                }
            log.Append($"vegetação: {trees} árvores, {rocks} rochas, {crystals} grupos de cristal, {cells.Count} células de streaming\n");
        }

        // ================================================================== jogo

        static void BuildGameplay()
        {
            // jogador (o mesmo Player.prefab de Campânula) + fluxo do reino
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DPS + "Player.prefab"));
            var model = player.GetComponentInChildren<Climbing.ThirdPersonController>().transform;
            player.transform.position = Vector3.zero;
            model.position = G(L.spawn, 0.05f);
            model.rotation = Quaternion.Euler(0, L.spawnYaw, 0);
            var cam = player.GetComponentInChildren<Camera>();
            if (cam != null) { cam.farClipPlane = Mathf.Min(4000f, L.size * 0.75f + 1700f); cam.clearFlags = CameraClearFlags.Skybox; }
            var flowGo = new GameObject("Fluxo do reino (RegionFlow)");
            flowGo.transform.SetParent(gGame, false);
            var flow = flowGo.AddComponent<RegionFlow>();
            var rr = root.GetComponent<RegionRoot>();
            flow.root = rr;
            var spawn = new GameObject("Chegada padrão").transform; spawn.SetParent(gGame, false);
            spawn.SetPositionAndRotation(G(L.spawn, 0.1f), Quaternion.Euler(0, L.spawnYaw, 0));
            rr.defaultSpawn = spawn;

            foreach (var gs in L.gates) Gameplay.Gate(gs, gGame);
            int ci = 0;
            foreach (var c in L.checkpoints) Gameplay.CheckpointAt(c, D.scene + "_cp" + (ci++), gGame);
            foreach (var z in L.zones) { Gameplay.Zone(z, gGame); navVolumes.Add((G(z.pos), new Vector3(z.radius * 2 + 70f, 40f, z.radius * 2 + 70f))); }
            foreach (var a in L.arenas) { Gameplay.Arena(a, gGame, gCams); navVolumes.Add((G(a.pos), new Vector3(a.radius * 2 + 30f, 40f, a.radius * 2 + 30f))); }
            foreach (var v in L.vortices) Gameplay.Vortex(v, gGame);
            foreach (var p in L.pois) Gameplay.Poi(p, gGame);
            foreach (var lk in L.locked) Gameplay.Locked(lk, gGame);
            var dd = WorldCanon.Dungeon(D.dungeonId);
            if (dd != null) Gameplay.DungeonEntrance(dd, L.dungeonPos, L.dungeonYaw, gGame);
            // câmeras de cinema nos marcos
            foreach (var lm in L.landmarks)
            {
                var cp = new GameObject("Câmera — " + lm.name).AddComponent<CinematicCameraPoint>();
                cp.transform.SetParent(gCams, false);
                var at = G(lm.pos, 6f);
                var from = at + new Vector3(Dir(lm.yaw + 200f).x, 0, Dir(lm.yaw + 200f).y) * (60f * lm.scale) + Vector3.up * (25f * lm.scale);
                cp.transform.position = from; cp.transform.rotation = Quaternion.LookRotation(at - from);
                cp.label = lm.name; cp.fov = 42f;
            }
            // rota principal (teste de travessia)
            var route = new GameObject("Rota principal").AddComponent<RegionRoute>();
            route.transform.SetParent(gGame, false);
            var pts = new List<Vector3>(); foreach (var q in L.route) pts.Add(G(q, 0.2f));
            route.points = pts.ToArray();
            log.Append($"jogo: {L.gates.Count} portões, {L.checkpoints.Count} pontos de retorno, {L.zones.Count} zonas, {L.arenas.Count} arenas, {L.vortices.Count} vórtices, {L.pois.Count} pontos de interesse\n");
        }

        // ================================================================== horizonte, atmosfera, limites, navmesh

        static void BuildHorizon()
        {
            float radius = L.size * 0.75f + 700f;
            // picos nas direções dos reinos (montanhas de Granith, vulcão da Coroa, mar baixo...)
            var bumps = new List<(float az, float h, float w)>();
            foreach (var r in WorldCanon.Regions)
            {
                if (r.id == L.id) continue;
                var d = WorldCanon.Direction(L.id, r.id);
                float az = Mathf.Atan2(d.x, d.z);
                float dist = (r.km - D.km).magnitude;
                float near = Mathf.Clamp01(1f - dist / 60f);
                float h = r.id == RegionId.Granith ? 520f : r.id == RegionId.CoroaDeCinza ? 430f : r.id == RegionId.MarDeVidro || r.id == RegionId.Caliria ? 40f : 180f;
                bumps.Add((az, h * (0.35f + 0.65f * near), 0.12f + 0.1f * near));
            }
            float baseY = terrainMin - 2f;
            float H(float a)
            {
                float h = L.horizonHeight * (0.35f + 0.65f * Mathf.PerlinNoise(a * 3.1f + (int)L.id, 1.7f)) + 40f * Mathf.PerlinNoise(a * 17f, 4.2f);
                foreach (var (az, bh, w) in bumps) { float da = Mathf.DeltaAngle(a * Mathf.Rad2Deg, az * Mathf.Rad2Deg) * Mathf.Deg2Rad; h += bh * Mathf.Exp(-(da * da) / (w * w)); }
                return h;
            }
            // chão além da borda do terreno até as serras (sem "vazio" no horizonte)
            if (!L.underground)
            {
                var skirtMat = WorldMats.Stone(L.tex.baseFar.StartsWith("camp:") ? L.tex.baseFar : "camp:gnd_meadow", L.tex.farTint * 0.8f, 40f);
                var skirt = Object("Chão distante (além do limite)", ProcMesh.Box(radius * 2.2f, 1f, radius * 2.2f), skirtMat, new Vector3(L.center.x, terrainMin - 1.5f, L.center.y), Quaternion.identity, Vector3.one, gHorizon, false);
                skirt.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var ring = ProcMesh.HorizonRing(D.scene, radius, H, baseY);
            var mat = WorldMats.Stone("darkrock", Color.Lerp(P.fogColor, new Color(0.25f, 0.24f, 0.26f), 0.5f), 60f);
            var go = Object("Serras distantes (silhueta)", ring, mat, new Vector3(L.center.x, 0, L.center.y), Quaternion.identity, Vector3.one, gHorizon, false);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Tag(go, "Silhueta de serras do horizonte — pode virar FarLands com relevo real (como em Campânula)");
            // vulcão da Coroa visível de longe: brasa no topo
            // a FENDA no céu: sempre na direção certa e do tamanho certo (enorme na Fronteira Muda, pequena em Valtéria)
            var fd = WorldCanon.FendaDirection(L.id);
            float km = WorldCanon.FendaDistanceKm(L.id);
            float hgt = Mathf.Clamp(250f * 63f / Mathf.Max(km, 4f), 220f, 2600f);
            var fpos = new Vector3(L.center.x, 0, L.center.y) + fd * (radius + 600f) + Vector3.up * (baseY + 200f + hgt * 0.45f);
            var rift = GameObject.CreatePrimitive(PrimitiveType.Quad);
            rift.name = $"A Fenda do Contracanto ({km:0} km)";
            UnityEngine.Object.DestroyImmediate(rift.GetComponent<Collider>());
            rift.transform.SetParent(gHorizon, false);
            rift.transform.position = fpos;
            rift.transform.rotation = Quaternion.LookRotation(fd);
            rift.transform.localScale = new Vector3(hgt * 0.6f, hgt, 1f);
            var mr = rift.GetComponent<MeshRenderer>(); mr.sharedMaterial = WorldMats.Rift(); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rift.AddComponent<Campanula.RiftPulse>();
        }

        static void BuildAtmosphere()
        {
            var go = new GameObject("Ar do reino (partículas)");
            go.transform.SetParent(gAtmos, false);
            go.AddComponent<AirMotes>();
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.startLifetime = 9f; main.startSpeed = 0.2f; main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.startColor = P.motesColor; main.maxParticles = 600; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = P.motesFall ? 0.02f : -0.004f;
            var em = ps.emission; em.rateOverTime = P.motesRate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(40f, 14f, 40f);
            var nz = ps.noise; nz.enabled = true; nz.strength = 0.25f; nz.frequency = 0.2f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient(); grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sharedMaterial = WorldMats.Motes();
        }

        static void BuildBounds()
        {
            var b = new GameObject("Limites do reino").transform; b.SetParent(gGame, false);
            float s = L.size, half = s / 2 - 8f;
            void Wall(Vector3 c, Vector3 size) { var w = new GameObject("Limite").AddComponent<BoxCollider>(); w.transform.SetParent(b, false); w.transform.position = c; w.size = size; w.gameObject.layer = 2; }
            float y = 200f;
            Wall(new Vector3(L.center.x - half, y, L.center.y), new Vector3(4, 1200, s));
            Wall(new Vector3(L.center.x + half, y, L.center.y), new Vector3(4, 1200, s));
            Wall(new Vector3(L.center.x, y, L.center.y - half), new Vector3(s, 1200, 4));
            Wall(new Vector3(L.center.x, y, L.center.y + half), new Vector3(s, 1200, 4));
        }

        static void BakeNav()
        {
            var g = new GameObject("NavMesh (áreas de luta)").transform; g.SetParent(root, false);
            int k = 0;
            System.IO.Directory.CreateDirectory("Assets/World/Data/NavMesh");
            foreach (var (c, size) in navVolumes)
            {
                var go = new GameObject("NavMesh " + k); go.transform.SetParent(g, false); go.transform.position = c;
                var surf = go.AddComponent<NavMeshSurface>();
                surf.collectObjects = CollectObjects.Volume;
                surf.size = size; surf.center = Vector3.zero;
                surf.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surf.layerMask = ~((1 << 8) | (1 << 10) | (1 << 2));
                surf.BuildNavMesh();
                if (surf.navMeshData != null)
                {
                    string p = $"Assets/World/Data/NavMesh/{D.scene}_{k}.asset";
                    AssetDatabase.DeleteAsset(p);
                    AssetDatabase.CreateAsset(surf.navMeshData, p);
                }
                k++;
            }
            log.Append($"navmesh: {k} áreas\n");
        }
    }
}
