using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Campanula.EditorTools
{
    /// <summary>
    /// Monta a cena "Campanula, Vila dos Doze Sinos" (Bíblia de Lore) a partir do kit
    /// modelado no Blender (ArtSource/Campanula). Idempotente: recria a cena do zero.
    ///
    /// Eixos: X leste, Z norte. Frente das construções = +Z local (yaw 0 olha para o norte).
    /// Layout: estrada sul com campos → muralha com portão (z −42) → rua do mercado →
    /// praça (0, 12) com a Torre dos Sinos ao norte → riacho e ponte a leste → Campo da
    /// Fenda (onde o cervo espera). A Fenda fica no céu a leste-nordeste, oposta ao sol.
    /// </summary>
    public static class CampanulaBuilder
    {
        public const string ScenePath = "Assets/Campanula/Scenes/Campanula.unity";
        const string Models = "Assets/Campanula/Models/";
        const string DPS = "Assets/Dynamic Parkour System/Prefabs/";
        const int LayerLedge = 8, LayerWall = 9;

        static Transform root, statics, parkour, gameplay;
        static readonly List<GameObject> navStatics = new List<GameObject>();

        // ------------------------------------------------------------ terreno
        public const float TerrainSize = 320f;
        public const float TerrainHeight = 45f;
        public const float BaseHeight = 2f;   // altura do chão da vila dentro do terreno (y mundo = 0)

        public static float StreamCenter(float z) => 42f + Mathf.Sin(z * 0.045f) * 2.2f;

        /// <summary>Altura do chão (y mundo) em qualquer ponto — usada também para assentar objetos.</summary>
        public static float GroundY(float x, float z)
        {
            float h = 0f;
            // morros fora da área jogável (anel), com ruído
            float r = Mathf.Max(Mathf.Abs(x - 15f) / 1.15f, Mathf.Abs(z));
            float hill = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(105f, 150f, r));
            float n = Mathf.PerlinNoise(x * 0.018f + 3.1f, z * 0.018f + 7.7f);
            h += hill * (12f + 22f * n);
            // ondulação suave nos campos (fora da muralha), plano na vila
            bool village = x > -50f && x < 38f && z > -44f && z < 52f;
            if (!village) h += (Mathf.PerlinNoise(x * 0.05f, z * 0.05f) - 0.5f) * 0.8f * (1f - hill);
            // leito do riacho
            float dx = Mathf.Abs(x - StreamCenter(z));
            float bed = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.2f, 5.5f, dx));
            h -= bed * 1.7f;
            return h;
        }

        // ------------------------------------------------------------ entrada

        [MenuItem("Campanula/Construir cena")]
        public static string Build()
        {
            var log = new System.Text.StringBuilder();
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            navStatics.Clear();
            root = new GameObject("Campanula").transform;
            statics = new GameObject("Static").transform; statics.SetParent(root);
            parkour = new GameObject("Parkour").transform; parkour.SetParent(root);
            gameplay = new GameObject("Gameplay").transform; gameplay.SetParent(root);

            var terrain = BuildTerrain(log);
            BuildLighting(log);
            BuildWalls(log);
            BuildMarket(log);
            BuildPlaza(log);
            BuildEast(log);
            BuildSouthRoad(log);
            BuildNature(log);
            BuildWater(log);
            BuildSky(log);
            BuildGameplay(log);
            BuildNavMesh(log);

            AssignCullLayers(log);
            StaticBatchingUtility.Combine(statics.gameObject);
            EditorSceneManager.SaveScene(scene, ScenePath);
            // occlusion culling: as casas da rua escondem o resto da vila (Intel UHD agradece)
            StaticOcclusionCulling.smallestOccluder = 4f;
            StaticOcclusionCulling.smallestHole = 0.35f;
            StaticOcclusionCulling.backfaceThreshold = 100f;
            if (StaticOcclusionCulling.Compute()) log.Append("occlusion culling ok\n");
            EditorSceneManager.SaveScene(scene, ScenePath);
            log.Append("cena salva: " + ScenePath + "\n");
            return log.ToString();
        }

        // ------------------------------------------------------------ camadas de distância
        public const int LayerDetail = 11, LayerVegetation = 12;
        static readonly HashSet<string> DetailModels = new HashSet<string> {
            "Crate", "Barrel", "HayBale", "Fence", "LowWall", "LampPost", "Bench", "BannerPole", "SlideBeam", "Cart", "Stall_Red", "Stall_Blue", "Well", "Bush", "Rock_A" };
        static readonly HashSet<string> VegetationModels = new HashSet<string> { "Tree_Oak", "Tree_Oak2", "Tree_Pine" };

        /// <summary>Props pequenos e árvores em camadas próprias: a câmera deixa de desenhá-los de longe.</summary>
        static void AssignCullLayers(System.Text.StringBuilder log)
        {
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            layers.GetArrayElementAtIndex(LayerDetail).stringValue = "Detail";
            layers.GetArrayElementAtIndex(LayerVegetation).stringValue = "Vegetation";
            tags.ApplyModifiedProperties();
            int n = 0;
            foreach (Transform holder in statics)
            {
                int layer = DetailModels.Contains(holder.name) ? LayerDetail : VegetationModels.Contains(holder.name) ? LayerVegetation : -1;
                if (layer < 0) continue;
                foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                n++;
            }
            log.Append("camadas de distância: " + n + " objetos\n");
        }

        // ------------------------------------------------------------ utilitários

        static GameObject Place(string model, Vector3 pos, float yaw, Transform parent = null, bool snap = true, bool collider = true, int layer = 0, float scale = 1f)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Models + model + ".fbx");
            if (src == null) { Debug.LogError("modelo não encontrado: " + model); return null; }
            // contêiner com rotação só em Y (frente = +Z); o modelo dentro guarda a correção
            // de eixo do FBX (geometria Z-up do Blender) e a meia-volta (a frente exportada é −Z)
            var holder = new GameObject(model);
            holder.transform.SetParent(parent ?? statics, false);
            if (snap) pos.y += GroundY(pos.x, pos.z);
            holder.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            holder.transform.localScale = Vector3.one * scale;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, holder.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0, 180f, 0) * src.transform.localRotation;
            GameObjectUtility.SetStaticEditorFlags(holder, StaticEditorFlags.BatchingStatic);
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var g = mf.gameObject;
                GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic
                    | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
                var mr = g.GetComponent<MeshRenderer>();
                if (mr != null) { mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; }
                if (collider) { var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; }
                if (layer != 0) g.layer = layer;
                navStatics.Add(g);
            }
            // marcadores de parkour exportados do Blender
            Physics.SyncTransforms();
            foreach (var t in go.GetComponentsInChildren<Transform>())
            {
                if (t.name.StartsWith("LEDGE_"))
                {
                    // LEDGE_<comprimento em dm>_<n>
                    float len = 1f;
                    var parts = t.name.Split('_');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int dm)) len = dm / 10f;
                    MakeLedgeAuto(t.position, holder.transform, Mathf.Max(0.5f, len * scale));
                }
            }
            return holder;
        }

        /// <summary>
        /// Orienta a borda testando as 4 direções horizontais: a "de fora" é aquela em que um
        /// raio vindo de fora, logo abaixo da borda, bate na parede a menos de 0.7 m.
        /// </summary>
        static void MakeLedgeAuto(Vector3 pos, Transform owner, float length)
        {
            Vector3 best = owner.forward; float bestDist = 99f;
            foreach (var d in new[] { owner.forward, -owner.forward, owner.right, -owner.right })
            {
                Vector3 o = pos + d * 0.8f - Vector3.up * 0.35f;
                if (Physics.Raycast(o, -d, out var hit, 1.2f) && hit.distance < bestDist) { bestDist = hit.distance; best = d; }
            }
            MakeLedge(pos, Quaternion.LookRotation(best, Vector3.up), length);
        }

        /// <summary>Borda agarrável do DPS: prefab Ledge (layer Ledge) invisível, eixo X ao longo da borda.</summary>
        static GameObject MakeLedge(Vector3 pos, Quaternion rot, float length)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(DPS + "Environment/Climb/Ledge.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parkour);
            go.transform.SetPositionAndRotation(pos, rot);
            var s = go.transform.localScale; s.x = length; go.transform.localScale = s;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.enabled = false;
            return go;
        }

        /// <summary>Volume de parkour invisível com a tag do DPS (Vault, Deep Jump, Slide, Reach).</summary>
        static GameObject Helper(string tag, Vector3 center, Vector3 size, float yaw, Transform parent = null)
        {
            var go = new GameObject(tag + "_helper");
            go.transform.SetParent(parent ?? parkour, false);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0, yaw, 0));
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            go.tag = tag;
            return go;
        }

        static void Box(GameObject go, Vector3 center, Vector3 size, string tag = null)
        {
            var bc = go.AddComponent<BoxCollider>(); bc.center = center; bc.size = size;
        }

        /// <summary>Prop com colisor de caixa (não usa MeshCollider) e opcionalmente a tag de parkour.</summary>
        static GameObject Prop(string model, Vector3 pos, float yaw, Vector3 boxCenter, Vector3 boxSize, string tag = null, float scale = 1f)
        {
            var go = Place(model, pos, yaw, null, true, false, 0, scale);
            if (go == null) return null;
            var bc = go.AddComponent<BoxCollider>(); bc.center = boxCenter; bc.size = boxSize;
            if (tag != null) go.tag = tag;
            return go;
        }

        // ------------------------------------------------------------ terreno

        static Terrain BuildTerrain(System.Text.StringBuilder log)
        {
            var td = new TerrainData();
            td.heightmapResolution = 257;
            td.alphamapResolution = 256;
            td.baseMapResolution = 256;
            td.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
            // cria o asset ANTES de pintar: criar depois descartava os splatmaps (tudo virava grama)
            AssetDatabase.CreateAsset(td, "Assets/Campanula/Scenes/Campanula_Terrain.asset");
            int res = td.heightmapResolution;
            var h = new float[res, res];
            for (int iz = 0; iz < res; iz++)
                for (int ix = 0; ix < res; ix++)
                {
                    float x = -TerrainSize / 2 + TerrainSize * ix / (res - 1);
                    float z = -TerrainSize / 2 + TerrainSize * iz / (res - 1);
                    h[iz, ix] = (GroundY(x, z) + BaseHeight) / TerrainHeight;
                }
            td.SetHeights(0, 0, h);

            string[] layers = { "grass", "dirt", "cobble", "field" };
            float[] tiles = { 6f, 5f, 3.2f, 5f };
            var tls = new TerrainLayer[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                string p = "Assets/Campanula/Materials/TL_" + layers[i] + ".terrainlayer";
                var tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(p);
                if (tl == null) { tl = new TerrainLayer(); AssetDatabase.CreateAsset(tl, p); }
                tl.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Campanula/Textures/" + layers[i] + "_albedo.png");
                // sem normal map no terreno: ele cobre metade da tela e 4 camadas × normal pesam no Intel UHD
                tl.normalMapTexture = null;
                tl.tileSize = new Vector2(tiles[i], tiles[i]);
                tl.smoothness = 0.05f;
                EditorUtility.SetDirty(tl);
                tls[i] = tl;
            }
            td.terrainLayers = tls;
            int ar = td.alphamapResolution;
            var a = new float[ar, ar, layers.Length];
            for (int iz = 0; iz < ar; iz++)
                for (int ix = 0; ix < ar; ix++)
                {
                    float x = -TerrainSize / 2 + TerrainSize * (ix + 0.5f) / ar;
                    float z = -TerrainSize / 2 + TerrainSize * (iz + 0.5f) / ar;
                    float n = Mathf.PerlinNoise(x * 0.15f, z * 0.15f);
                    float cobble = 0, dirt = 0, field = 0;
                    // praça e rua do mercado: calçamento
                    if (x > -19 && x < 19 && z > -5 && z < 30) cobble = 1;
                    if (x > -4.8f && x < 4.8f && z > -44 && z < -3) cobble = 1;
                    // estrada sul e caminhos de terra
                    if (Mathf.Abs(x - Mathf.Sin(z * 0.04f) * 1.5f) < 3.2f + n && z < -44) dirt = 1;
                    if (z > 6 && z < 13 && x > 18 && x < 120) dirt = Mathf.Max(dirt, 1f - Mathf.Abs(z - 9.5f) / 3.5f + n * 0.3f);
                    if (Mathf.Abs(x - StreamCenter(z)) < 4.5f) dirt = Mathf.Max(dirt, 0.7f);
                    // campos de trigo ao sul (dos dois lados da estrada) e a leste
                    if (z < -50 && z > -110 && Mathf.Abs(x) > 8 && Mathf.Abs(x) < 70) field = 1;
                    if (x > 50 && x < 112 && z > 18 && z < 60) field = 1;
                    float sum = cobble + dirt + field;
                    float grass = Mathf.Max(0f, 1f - sum);
                    if (sum > 1) { cobble /= sum; dirt /= sum; field /= sum; }
                    a[iz, ix, 0] = grass; a[iz, ix, 1] = dirt; a[iz, ix, 2] = cobble; a[iz, ix, 3] = field;
                }
            td.SetAlphamaps(0, 0, a);
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();
            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terrain";
            go.transform.SetParent(root);
            go.transform.position = new Vector3(-TerrainSize / 2, -BaseHeight, -TerrainSize / 2);
            var t = go.GetComponent<Terrain>();
            t.heightmapPixelError = 10f;
            t.basemapDistance = 70f;
            t.drawInstanced = true;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // morros não precisam projetar sombra (barato)
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            log.Append("terreno ok\n");
            return t;
        }

        // ------------------------------------------------------------ luz e atmosfera

        static void BuildLighting(System.Text.StringBuilder log)
        {
            var sun = new GameObject("Sol (pôr do sol)").AddComponent<Light>();
            sun.transform.SetParent(root);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.68f, 0.43f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.04f; sun.shadowNormalBias = 0.35f;
            // vem do oeste-sudoeste, baixo: luz rasante dourada; a Fenda fica do lado oposto
            sun.transform.rotation = Quaternion.Euler(16f, 72f, 0f);
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.50f, 0.42f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.40f, 0.33f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.16f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0042f;
            RenderSettings.fogColor = new Color(0.80f, 0.56f, 0.46f);
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.5f;
            var ls = new LightingSettings { name = "Campanula_Lighting", bakedGI = false, realtimeGI = false };
            AssetDatabase.CreateAsset(ls, "Assets/Campanula/Scenes/Campanula_Lighting.lighting");
            Lightmapping.lightingSettings = ls;
            log.Append("luz ok\n");
        }

        static void BuildSky(System.Text.StringBuilder log)
        {
            var mat = new Material(Shader.Find("Campanula/SunsetSky"));
            mat.SetTexture("_Clouds", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Aren/Resources/VFX/noise_perlin.png"));
            Vector3 riftDir = new Vector3(0.78f, 0.36f, 0.5f).normalized;
            mat.SetVector("_RiftDir", riftDir);
            AssetDatabase.CreateAsset(mat, "Assets/Campanula/Materials/Sky_Sunset.mat");
            RenderSettings.skybox = mat;

            // a Fenda: quad gigante longe, no céu a leste-nordeste
            var rift = GameObject.CreatePrimitive(PrimitiveType.Quad);
            rift.name = "A Fenda (Ruptura)";
            Object.DestroyImmediate(rift.GetComponent<Collider>());
            rift.transform.SetParent(root);
            Vector3 center = new Vector3(15f, 0f, 12f) + riftDir * 370f;
            rift.transform.position = center;
            rift.transform.rotation = Quaternion.LookRotation(riftDir);
            rift.transform.localScale = new Vector3(150f, 240f, 1f);
            var rm = new Material(Shader.Find("Campanula/Rift"));
            rm.SetTexture("_Noise", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Aren/Resources/VFX/noise_perlin.png"));
            AssetDatabase.CreateAsset(rm, "Assets/Campanula/Materials/Rift.mat");
            var mr = rift.GetComponent<MeshRenderer>();
            mr.sharedMaterial = rm;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            rift.AddComponent<Campanula.RiftPulse>();
            log.Append("céu + Fenda ok\n");
        }

        static void BuildWater(System.Text.StringBuilder log)
        {
            var mat = new Material(Shader.Find("Campanula/Water"));
            mat.SetTexture("_Noise", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Aren/Resources/VFX/noise_perlin.png"));
            AssetDatabase.CreateAsset(mat, "Assets/Campanula/Materials/Water.mat");
            // faixa de água seguindo o riacho (malha gerada)
            var verts = new List<Vector3>(); var tris = new List<int>();
            int seg = 80; float z0 = -150f, z1 = 150f, w = 3.4f, y = -0.75f;
            for (int i = 0; i <= seg; i++)
            {
                float z = Mathf.Lerp(z0, z1, i / (float)seg);
                float cx = StreamCenter(z);
                verts.Add(new Vector3(cx - w, y, z)); verts.Add(new Vector3(cx + w, y, z));
                if (i < seg) { int k = i * 2; tris.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
            }
            var mesh = new Mesh { name = "StreamWater" };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, "Assets/Campanula/Scenes/StreamWater.asset");
            var go = new GameObject("Riacho");
            go.transform.SetParent(root);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            log.Append("riacho ok\n");
        }

        // ------------------------------------------------------------ muralha

        static void BuildWalls(System.Text.StringBuilder log)
        {
            // muralha sul (z = −42): a frente dos modelos (+Z local) é o lado de fora → yaw 180
            Place("Gatehouse", new Vector3(0, 0, -42f), 180f, null, true, true, LayerWall);
            float[] xs = { -9.5f, -19.5f, -29.5f, 9.5f, 19.5f, 29.5f };
            foreach (var x in xs)
                Place(x == -19.5f ? "Wall_Climb" : "Wall_Segment", new Vector3(x, 0, -42f), 180f, null, true, true, LayerWall);
            foreach (var x in new[] { -37.5f, 37.5f })
                Place("Wall_Tower", new Vector3(x, 0, -42f), 0f, null, true, true, LayerWall);
            // muralha oeste (x = −40), olhando para fora (oeste) → yaw −90
            for (int i = 0; i < 9; i++)
                Place("Wall_Segment", new Vector3(-40f, 0, -32.5f + i * 10f), -90f, null, true, true, LayerWall);
            Place("Wall_Tower", new Vector3(-40f, 0, 58f), 0f, null, true, true, LayerWall);
            log.Append("muralha ok\n");
        }

        // ------------------------------------------------------------ rua do mercado

        static void BuildMarket(System.Text.StringBuilder log)
        {
            // casas do lado oeste viradas para leste (yaw 90) e do lado leste viradas para oeste (yaw −90)
            Place("House_A", new Vector3(-10.5f, 0, -33f), 90f);
            Place("House_D", new Vector3(-11f, 0, -24f), 90f);
            Place("House_B", new Vector3(-10f, 0, -15.5f), 90f);
            Place("House_C", new Vector3(-10f, 0, -7.5f), 90f);
            Place("House_B", new Vector3(10f, 0, -34f), -90f);
            Place("Tavern", new Vector3(12f, 0, -23f), -90f);
            Place("House_D", new Vector3(11f, 0, -11.5f), -90f);

            // barracas encostadas nas casas, viradas para a rua
            Prop("Stall_Red", new Vector3(-5.4f, 0, -29f), 90f, new Vector3(0, 0.5f, -0.2f), new Vector3(3.2f, 1f, 1.6f));
            Prop("Stall_Blue", new Vector3(5.4f, 0, -16f), -90f, new Vector3(0, 0.5f, -0.2f), new Vector3(3.2f, 1f, 1.6f));
            Prop("Stall_Red", new Vector3(5.4f, 0, -7f), -90f, new Vector3(0, 0.5f, -0.2f), new Vector3(3.2f, 1f, 1.6f));
            // caixotes e barris
            foreach (var p in new[] { new Vector3(-5.6f, 0, -21f), new Vector3(-5.8f, 0, -19.6f), new Vector3(5.8f, 0, -29.5f) })
                Prop("Crate", p, Random.Range(-10f, 10f), new Vector3(0, 0.5f, 0), Vector3.one, "Deep Jump");
            foreach (var p in new[] { new Vector3(-6f, 0, -12f), new Vector3(-6.3f, 0, -11.2f), new Vector3(6.2f, 0, -26.5f), new Vector3(6.4f, 0, -25.7f) })
            {
                var b = Place("Barrel", p, Random.Range(0f, 360f), null, true, false);
                var cc = b.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0, 0.5f, 0); cc.radius = 0.36f; cc.height = 1f;
            }
            // viga baixa com estandarte cruzando a rua: deslizar por baixo
            var beam = Prop("SlideBeam", new Vector3(0, 0, -20f), 0f, new Vector3(0, 1.72f, 0), new Vector3(3.9f, 0.3f, 0.3f));
            Helper("Slide", new Vector3(0, 1.19f, -20f), new Vector3(3.6f, 0.82f, 0.9f), 0f);
            // as laterais da viga fechadas por caixotes (o caminho é por baixo)
            Prop("Crate", new Vector3(-3.1f, 0, -20f), 0f, new Vector3(0, 0.5f, 0), Vector3.one, "Deep Jump");
            Prop("Crate", new Vector3(3.1f, 0, -20f), 0f, new Vector3(0, 0.5f, 0), Vector3.one, "Deep Jump");
            // postes de lanterna alternados
            for (int i = 0; i < 4; i++)
            {
                float z = -36f + i * 9f;
                float x = i % 2 == 0 ? -4.6f : 4.6f;
                var lp = Place("LampPost", new Vector3(x, 0, z), x < 0 ? 90f : -90f, null, true, false);
                var cc = lp.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0, 2f, 0); cc.radius = 0.15f; cc.height = 4f;
            }
            log.Append("mercado ok\n");
        }

        // ------------------------------------------------------------ praça e torre

        static void BuildPlaza(System.Text.StringBuilder log)
        {
            // Torre dos Doze Sinos ao norte da praça; andaime (face −Z local do Blender = +Z Unity) virado para a praça → yaw 180
            var tower = Place("BellTower", new Vector3(0, 0, 36f), 180f, null, true, true, LayerWall);
            // sinos não são estáticos (balançam): tira do lote estático
            foreach (var t in tower.GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("Bell_"))
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                    var mc = t.GetComponent<MeshCollider>(); if (mc) Object.DestroyImmediate(mc);
                }

            Place("Well", new Vector3(0, 0, 10f), 0f);
            // casas em volta da praça
            Place("House_D", new Vector3(-25f, 0, 3f), 90f);
            Place("House_A", new Vector3(-25.5f, 0, 14f), 90f);
            Place("House_B", new Vector3(-24.5f, 0, 24f), 90f);
            Place("Tavern", new Vector3(26f, 0, 5f), -90f);
            Place("House_B", new Vector3(24.5f, 0, 17f), -90f);
            Place("House_C", new Vector3(25f, 0, 26f), -90f);
            Place("House_C", new Vector3(-13f, 0, 38f), 180f);
            Place("House_A", new Vector3(13.5f, 0, 38.5f), 180f);
            Place("House_D", new Vector3(-30f, 0, -8f), 90f);
            // bancos, barris, estandartes, barracas na borda
            foreach (var p in new[] { new Vector3(-6f, 0, 16f), new Vector3(6f, 0, 16f), new Vector3(-6f, 0, 4f), new Vector3(6f, 0, 4f) })
                Prop("Bench", p, p.x < 0 ? 90f : -90f, new Vector3(0, 0.25f, 0), new Vector3(1.8f, 0.5f, 0.45f));
            foreach (var p in new[] { new Vector3(-14f, 0, 27f), new Vector3(14f, 0, 27f), new Vector3(-16f, 0, -2f), new Vector3(16f, 0, -2f) })
            {
                var bp = Place("BannerPole", p, p.x < 0 ? 45f : -45f, null, true, false);
                var cc = bp.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0, 3f, 0); cc.radius = 0.12f; cc.height = 6f;
            }
            Prop("Stall_Blue", new Vector3(-15.5f, 0, 10f), 90f, new Vector3(0, 0.5f, -0.2f), new Vector3(3.2f, 1f, 1.6f));
            Prop("Stall_Red", new Vector3(15.5f, 0, 12f), -90f, new Vector3(0, 0.5f, -0.2f), new Vector3(3.2f, 1f, 1.6f));
            Prop("Cart", new Vector3(-13f, 0, 21f), 30f, new Vector3(0, 0.8f, 0), new Vector3(2.7f, 1.0f, 1.6f));
            // pequeno circuito de parkour na praça: muro baixo e fardos
            Prop("LowWall", new Vector3(-9f, 0, 22f), 0f, new Vector3(0, 0.42f, 0), new Vector3(4.1f, 0.84f, 0.4f), "Vault");
            Prop("LowWall", new Vector3(9f, 0, 0f), 0f, new Vector3(0, 0.42f, 0), new Vector3(4.1f, 0.84f, 0.4f), "Vault");
            Prop("HayBale", new Vector3(12f, 0, 22f), 0f, new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.9f, 0.9f), "Deep Jump");
            log.Append("praça + torre ok\n");
        }

        // ------------------------------------------------------------ leste: riacho, ponte, Campo da Fenda

        static void BuildEast(System.Text.StringBuilder log)
        {
            float bz = 9.5f;
            var br = Place("Bridge", new Vector3(StreamCenter(bz), 0, bz), 0f, null, false, true);
            br.transform.position = new Vector3(StreamCenter(bz), -0.55f, bz);
            // casas e celeiros perto do riacho
            Place("House_C", new Vector3(30f, 0, -16f), -90f);
            Place("House_A", new Vector3(31f, 0, 36f), -90f);
            // campo: cercas quebradas, fardos, pedras
            for (int i = 0; i < 10; i++)
            {
                if (i == 4 || i == 7) continue;   // brechas
                var f = Place("Fence", new Vector3(52f + i * 3f, 0, 17f), 0f, null, true, false);
                f.AddComponent<BoxCollider>().center = new Vector3(0, 0.5f, 0);
                f.GetComponent<BoxCollider>().size = new Vector3(3f, 1.05f, 0.15f);
            }
            foreach (var p in new[] { new Vector3(62f, 0, 2f), new Vector3(70f, 0, 28f), new Vector3(88f, 0, -4f), new Vector3(58f, 0, 34f) })
                Prop("HayBale", p, Random.Range(0f, 90f), new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.9f, 0.9f), "Deep Jump");
            foreach (var p in new[] { new Vector3(95f, 0, 20f), new Vector3(80f, 0, -18f), new Vector3(100f, 0, 40f), new Vector3(66f, 0, -26f) })
            {
                var r = Place("Rock_B", p, Random.Range(0f, 360f), null, true, false);
                var mf2 = r.GetComponentInChildren<MeshFilter>();
                var mc = mf2.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf2.sharedMesh; mc.convex = true;
            }
            log.Append("leste ok\n");
        }

        // ------------------------------------------------------------ estrada sul (tutorial)

        static void BuildSouthRoad(System.Text.StringBuilder log)
        {
            // moinho e fazenda
            var mill = Place("Windmill", new Vector3(-48f, 0, -78f), 30f);
            foreach (var t in mill.GetComponentsInChildren<Transform>())
                if (t.name.Contains("Sails"))
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                    var mc = t.GetComponent<MeshCollider>(); if (mc) Object.DestroyImmediate(mc);
                    t.gameObject.AddComponent<Campanula.Spin>().axis = Vector3.up;   // eixo Y do Blender (pás no plano XZ)
                }
            Place("House_C", new Vector3(-30f, 0, -70f), 60f);
            Prop("Cart", new Vector3(-6f, 0, -58f), 80f, new Vector3(0, 0.8f, 0), new Vector3(2.7f, 1.0f, 1.6f));
            // obstáculos do tutorial na estrada: muro baixo (vault), fardos (salto), viga (slide)
            Prop("LowWall", new Vector3(0, 0, -80f), 0f, new Vector3(0, 0.42f, 0), new Vector3(4.1f, 0.84f, 0.4f), "Vault");
            Prop("LowWall", new Vector3(-4.2f, 0, -80f), 0f, new Vector3(0, 0.42f, 0), new Vector3(4.1f, 0.84f, 0.4f), "Vault");
            Prop("LowWall", new Vector3(4.2f, 0, -80f), 0f, new Vector3(0, 0.42f, 0), new Vector3(4.1f, 0.84f, 0.4f), "Vault");
            Prop("HayBale", new Vector3(-0.8f, 0, -71f), 0f, new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.9f, 0.9f), "Deep Jump");
            Prop("HayBale", new Vector3(0.8f, 0, -71f), 0f, new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.9f, 0.9f), "Deep Jump");
            Prop("SlideBeam", new Vector3(0, 0, -62f), 0f, new Vector3(0, 1.72f, 0), new Vector3(3.9f, 0.3f, 0.3f));
            Helper("Slide", new Vector3(0, GroundY(0, -62f) + 1.19f, -62f), new Vector3(3.6f, 0.82f, 0.9f), 0f);
            for (int i = 0; i < 2; i++)
            {
                Prop("HayBale", new Vector3(-2.7f - i * 1.4f, 0, -62f), 90f, new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.9f, 0.9f));
                Prop("HayBale", new Vector3(2.7f + i * 1.4f, 0, -62f), 90f, new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.9f, 0.9f));
            }
            // cercas ao longo da estrada
            for (int i = 0; i < 12; i++)
            {
                foreach (float x in new[] { -6.5f, 6.5f })
                {
                    if (i == 5) continue;
                    var f = Place("Fence", new Vector3(x, 0, -100f + i * 4.6f), 90f, null, true, false);
                    var bc = f.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.5f, 0); bc.size = new Vector3(3f, 1.05f, 0.15f);
                }
            }
            foreach (var p in new[] { new Vector3(-14f, 0, -88f), new Vector3(-20f, 0, -60f), new Vector3(16f, 0, -84f), new Vector3(22f, 0, -66f), new Vector3(12f, 0, -54f) })
                Prop("HayBale", p, Random.Range(0f, 180f), new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.9f, 0.9f), "Deep Jump");
            log.Append("estrada sul ok\n");
        }

        // ------------------------------------------------------------ natureza

        static void BuildNature(System.Text.StringBuilder log)
        {
            var rng = new System.Random(1234);
            int count = 0;
            string[] kinds = { "Tree_Oak", "Tree_Oak2", "Tree_Pine" };
            for (int i = 0; i < 900 && count < 110; i++)
            {
                float x = (float)(rng.NextDouble() * 280 - 140);
                float z = (float)(rng.NextDouble() * 280 - 140);
                bool village = x > -46 && x < 38 && z > -46 && z < 50;
                bool road = Mathf.Abs(x) < 9 && z < -40;
                bool field = (z < -48 && z > -112 && Mathf.Abs(x) > 6 && Mathf.Abs(x) < 72) || (x > 48 && x < 114 && z > -30 && z < 62);
                bool stream = Mathf.Abs(x - StreamCenter(z)) < 6;
                if (village || road || stream) continue;
                if (field && rng.NextDouble() < 0.9) continue;
                float gy = GroundY(x, z);
                string k = gy > 8 ? "Tree_Pine" : kinds[rng.Next(kinds.Length)];
                var t = Place(k, new Vector3(x, 0, z), (float)rng.NextDouble() * 360f, null, true, false, 0, 0.8f + (float)rng.NextDouble() * 0.6f);
                var cc = t.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0, 2f, 0); cc.radius = 0.35f; cc.height = 4f;
                count++;
            }
            // arbustos e pedras perto das muralhas e do riacho
            for (int i = 0; i < 60; i++)
            {
                float z = (float)(rng.NextDouble() * 200 - 100);
                if (Mathf.Abs(z - 9.5f) < 9f) continue;   // livre perto da ponte (a câmera passa por ali)
                float x = StreamCenter(z) + (rng.NextDouble() < 0.5 ? -1 : 1) * (5.5f + (float)rng.NextDouble() * 3f);
                Place(rng.NextDouble() < 0.7 ? "Bush" : "Rock_A", new Vector3(x, 0, z), (float)rng.NextDouble() * 360f, null, true, false);
            }
            log.Append("natureza ok (" + count + " árvores)\n");
        }

        // ------------------------------------------------------------ jogo

        static void BuildGameplay(System.Text.StringBuilder log)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DPS + "Player.prefab"));
            player.transform.SetParent(null);
            var model = player.GetComponentInChildren<Climbing.ThirdPersonController>().transform;
            Vector3 spawn = new Vector3(0, GroundY(0, -92f) + 0.05f, -92f);
            player.transform.position = Vector3.zero;
            model.position = spawn;
            model.rotation = Quaternion.identity;
            var cam = player.GetComponentInChildren<Camera>();
            if (cam != null) { cam.farClipPlane = 460f; cam.clearFlags = CameraClearFlags.Skybox; }

            var flow = new GameObject("GameFlow");
            flow.transform.SetParent(gameplay);
            var gf = flow.AddComponent<Aren.World.GameFlow>();
            gf.ecoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Aren/Enemies/Eco.prefab");
            gf.deerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Aren/Enemies/Deer.prefab");
            var towerHolder = GameObject.Find("BellTower");
            gf.bellTower = towerHolder != null ? towerHolder.transform : null;
            log.Append("jogador em " + spawn + "\n");
        }

        static void BuildNavMesh(System.Text.StringBuilder log)
        {
            var go = new GameObject("NavMesh");
            go.transform.SetParent(root);
            var surf = go.AddComponent<NavMeshSurface>();
            surf.collectObjects = CollectObjects.All;
            surf.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surf.layerMask = ~((1 << LayerLedge) | (1 << 10));   // sem bordas e sem o jogador
            surf.BuildNavMesh();
            if (surf.navMeshData != null)
            {
                AssetDatabase.CreateAsset(surf.navMeshData, "Assets/Campanula/Scenes/Campanula_NavMesh.asset");
                log.Append("navmesh ok\n");
            }
            else log.Append("ATENÇÃO: navmesh vazio\n");
        }
    }
}
