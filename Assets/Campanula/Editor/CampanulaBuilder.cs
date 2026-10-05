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
        public const float TerrainHeight = 62f;
        public const float BaseHeight = 18f;  // altura do chão da vila dentro do terreno (y mundo = 0); 18 m de folga para o desfiladeiro

        public static float StreamCenter(float z) => StreamMath.Center(z);
        public const float BridgeZ = 9.5f;   // a ponte-aqueduto sobre o desfiladeiro

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
            // leito do riacho; no trecho do desfiladeiro, o cânion (StreamMath.Gorge)
            float dx = Mathf.Abs(x - StreamCenter(z));
            float bed = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.2f, 5.5f, dx));
            float g = StreamMath.Gorge(x, z);
            // bordas do cânion planas (os muros de arrimo e as cabeceiras da ponte assentam em y = 0)
            if (StreamMath.GorgeAlong(z) > 0.01f) h *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(9.5f, 13f, dx));
            // cabeceiras da ponte: chão plano no nível da cidade (o tabuleiro fica em y = 0)
            float bz = Mathf.Abs(z - BridgeZ);
            if (bz < 7f && g < 0.5f && Mathf.Abs(x - StreamCenter(BridgeZ)) < 17f) h *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.5f, 7f, bz));
            h -= Mathf.Max(bed * 1.7f, g);
            return h;
        }

        // ------------------------------------------------------------ entrada

        /// <summary>Assa as sombras do sol (assíncrono). Rodar depois do Build; salvar a cena ao fim.</summary>
        [MenuItem("Campanula/Assar luz (lightmap)")]
        public static string BakeLighting()
        {
            var t = Object.FindAnyObjectByType<Terrain>();
            if (t != null)
            {
                var so = new SerializedObject(t);
                var p = so.FindProperty("m_ScaleInLightmap");
                if (p != null) { p.floatValue = 0.15f; so.ApplyModifiedProperties(); }
            }
            Lightmapping.bakeCompleted -= OnBakeDone;
            Lightmapping.bakeCompleted += OnBakeDone;
            return Lightmapping.BakeAsync() ? "bake iniciado" : "bake não iniciou";
        }

        static void OnBakeDone()
        {
            Lightmapping.bakeCompleted -= OnBakeDone;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("CAMPANULA BAKE OK: " + LightmapSettings.lightmaps.Length + " lightmaps");
        }

        [MenuItem("Campanula/Construir cena")]
        public static string Build()
        {
            var log = new System.Text.StringBuilder();
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Lightmapping.Clear();
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
            BuildGorge(log);
            BuildSkyline(log);
            BuildGreatAqueduct(log);
            BuildUpperTown(log);
            BuildGroundMist(log);
            BuildSouthRoad(log);
            BuildKitDressing(log);
            BuildNature(log);
            BuildWater(log);
            BuildSky(log);
            BuildGameplay(log);
            ConnectLedges(log);
            BuildNavMesh(log);

            AssignCullLayers(log);
            // (sem StaticBatchingUtility.Combine aqui: as malhas combinadas iam para dentro do .unity — com a
            // cidade gótica a cena passava de 100 MB, o limite do GitHub. O Unity combina na hora do build:
            // os objetos já têm a flag BatchingStatic e o static batching está ligado no Player.)
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
                foreach (var t in holder.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = layer;
                    // props e árvores fora do lightmap (menos texels; ficam com a luz ambiente)
                    var f = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, f & ~StaticEditorFlags.ContributeGI);
                }
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
            // a parede "de fora" fica logo atrás da borda: o raio que vem de 0.8 m bate a
            // ~0.65–0.8 m (saliência de 0–15 cm). Parapeito fino tem parede dos dois lados —
            // o lado de dentro bate cedo demais e perde.
            Vector3 best = owner.forward; float bestScore = 99f;
            foreach (var d in new[] { owner.forward, -owner.forward, owner.right, -owner.right })
            {
                Vector3 o = pos + d * 0.8f - Vector3.up * 0.35f;
                if (!Physics.Raycast(o, -d, out var hit, 1.2f)) continue;
                float score = Mathf.Abs(hit.distance - 0.7f);
                if (score < bestScore) { bestScore = score; best = d; }
            }
            // convenção do DPS: a frente da borda aponta PARA DENTRO da parede (para onde o
            // jogador olha pendurado); o salto entre bordas posiciona o corpo por essa rotação
            MakeLedge(pos, Quaternion.LookRotation(-best, Vector3.up), length);
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

        /// <summary>
        /// Capim nas áreas de grama e trigo nos campos (detalhes do terreno, billboards com
        /// vento). Fora da estrada, do calçamento, do leito do riacho e da arena do chefe (o trigo
        /// de 1 m esconderia os pés na luta). Distância/densidade vêm da qualidade (GameSettings).
        /// </summary>
        public static void BuildTerrainDetails(TerrainData td, float[,,] splat)
        {
            const int dr = 512;   // 320 m / 512 = 0.63 m por célula
            // modo de contagem: o valor da camada é o número de tufos por célula (o padrão da
            // Unity 6 é cobertura 0–255, em que 0–6 quase não desenhava nada)
            td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            td.SetDetailResolution(dr, 16);
            var grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Campanula/Textures/detail_grass.png");
            var wheatTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Campanula/Textures/detail_wheat.png");
            td.detailPrototypes = new[]
            {
                new DetailPrototype
                {
                    prototypeTexture = grassTex, renderMode = DetailRenderMode.GrassBillboard, usePrototypeMesh = false,
                    minWidth = 0.55f, maxWidth = 0.95f, minHeight = 0.32f, maxHeight = 0.62f, noiseSpread = 0.35f,
                    healthyColor = new Color(0.86f, 0.95f, 0.72f), dryColor = new Color(0.95f, 0.82f, 0.55f),
                },
                new DetailPrototype
                {
                    prototypeTexture = wheatTex, renderMode = DetailRenderMode.Grass, usePrototypeMesh = false,
                    minWidth = 0.7f, maxWidth = 1.05f, minHeight = 0.8f, maxHeight = 1.15f, noiseSpread = 0.25f,
                    healthyColor = new Color(1f, 0.93f, 0.75f), dryColor = new Color(0.9f, 0.74f, 0.5f),
                },
            };
            int ar = splat.GetLength(0);
            var g = new int[dr, dr]; var w = new int[dr, dr];
            for (int iz = 0; iz < dr; iz++)
                for (int ix = 0; ix < dr; ix++)
                {
                    float x = -TerrainSize / 2 + TerrainSize * (ix + 0.5f) / dr;
                    float z = -TerrainSize / 2 + TerrainSize * (iz + 0.5f) / dr;
                    int ax = Mathf.Clamp(ix * ar / dr, 0, ar - 1), az = Mathf.Clamp(iz * ar / dr, 0, ar - 1);
                    float grass = splat[az, ax, 0], field = splat[az, ax, 3];
                    float n = Mathf.PerlinNoise(x * 0.11f + 5f, z * 0.11f + 9f);
                    float n2 = Mathf.PerlinNoise(x * 0.6f, z * 0.6f);
                    bool arena = (new Vector2(x - 70f, z - 14f)).magnitude < 22f;
                    bool stream = Mathf.Abs(x - StreamCenter(z)) < 4f;
                    bool inWalls = x > -40f && x < 38f && z > -42f && z < 50f;
                    if (grass > 0.85f && !stream)
                    {
                        // dentro da muralha só nas bordas, em tufos (a praça e as ruas ficam limpas)
                        float k = inWalls ? Mathf.Clamp01((n - 0.55f) * 3f) : Mathf.Clamp01((n - 0.25f) * 1.6f);
                        g[iz, ix] = Mathf.RoundToInt(k * (2f + n2 * 3f));
                    }
                    if (field > 0.85f && !arena && !stream)
                        w[iz, ix] = 3 + Mathf.RoundToInt(n2 * 3f);
                }
            td.SetDetailLayer(0, 0, 0, g);
            td.SetDetailLayer(0, 0, 1, w);
            td.wavingGrassStrength = 0.3f;
            td.wavingGrassAmount = 0.35f;
            td.wavingGrassSpeed = 0.45f;
            td.wavingGrassTint = new Color(0.95f, 0.85f, 0.65f);
        }

        // ------------------------------------------------------------ props do kit (Quaternius)

        const string KitModels = "Assets/Campanula/ThirdParty/FantasyProps/Models/";
        static Material flameMat;
        static Mesh flameQuad;

        /// <summary>
        /// Prop do Fantasy Props MegaKit (CC0). Mesma convenção do Place(): o contêiner gira só em
        /// Y (frente = +Z) e o modelo dentro leva a meia-volta (a frente exportada é −Z). Camada
        /// Detail (some a 70 m), static batching e, se pedido, caixa de colisão ajustada aos
        /// limites (o NavMesh usa os colisores, então os props viram obstáculo para os Ecos).
        /// </summary>
        static GameObject Kit(string model, Vector3 pos, float yaw, bool collider = true, bool snap = true, float scale = 1f)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(KitModels + model + ".fbx");
            if (src == null) { Debug.LogError("prop do kit não encontrado: " + model); return null; }
            var holder = new GameObject("Kit_" + model);
            holder.transform.SetParent(statics, false);
            if (snap) pos.y += GroundY(pos.x, pos.z);
            holder.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            holder.transform.localScale = Vector3.one * scale;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, holder.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0, 180f, 0) * src.transform.localRotation;
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            GameObjectUtility.SetStaticEditorFlags(holder, flags);
            holder.layer = LayerDetail;
            Bounds b = default; bool first = true;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = LayerDetail;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
                var mr = t.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                // limites no espaço do contêiner (8 cantos)
                var wb = mr.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) == 0 ? wb.min.x : wb.max.x, (i & 2) == 0 ? wb.min.y : wb.max.y, (i & 4) == 0 ? wb.min.z : wb.max.z);
                    var lc = holder.transform.InverseTransformPoint(c);
                    if (first) { b = new Bounds(lc, Vector3.zero); first = false; } else b.Encapsulate(lc);
                }
            }
            if (collider && !first)
            {
                var bc = holder.AddComponent<BoxCollider>();
                bc.center = b.center; bc.size = Vector3.Max(b.size, new Vector3(0.1f, 0.1f, 0.1f));
            }
            return holder;
        }

        /// <summary>Raio horizontal até a parede mais próxima (ignora bordas, props e árvores).</summary>
        static bool WallHit(Vector3 from, Vector3 dir, float maxDist, out RaycastHit hit)
        {
            Physics.SyncTransforms();
            int mask = ~((1 << LayerLedge) | (1 << LayerDetail) | (1 << LayerVegetation) | (1 << 10));
            if (Physics.Raycast(from, dir.normalized, out hit, maxDist, mask, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.normal.y) < 0.3f)
                return true;
            return false;
        }

        /// <summary>Tocha de parede com chama (billboard aditivo, fora do static batching).</summary>
        static bool WallTorch(Vector3 from, Vector3 dir, float maxDist = 14f)
        {
            if (!WallHit(from, dir, maxDist, out var hit)) return false;
            Vector3 n = new Vector3(hit.normal.x, 0, hit.normal.z).normalized;
            var t = Kit("Torch_Metal", hit.point + n * 0.02f, Quaternion.LookRotation(n).eulerAngles.y, false, false);
            if (t == null) return false;
            if (flameMat == null)
            {
                flameMat = new Material(Shader.Find("Campanula/Flame"));
                flameMat.SetTexture("_Noise", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Aren/Resources/VFX/noise_perlin.png"));
                flameMat.enableInstancing = true;
                AssetDatabase.CreateAsset(flameMat, "Assets/Campanula/Materials/Flame.mat");
                flameQuad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            }
            var f = new GameObject("TorchFlame");
            f.transform.SetParent(gameplay, false);
            f.transform.position = t.transform.TransformPoint(new Vector3(0f, 0.36f, 0.31f));
            f.layer = LayerDetail;
            f.AddComponent<MeshFilter>().sharedMesh = flameQuad;
            var mr = f.AddComponent<MeshRenderer>();
            mr.sharedMaterial = flameMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return true;
        }

        /// <summary>Estandarte de pano preso na fachada (pivô no topo do pano).</summary>
        static bool WallBanner(Vector3 from, Vector3 dir, string model, float topY)
        {
            if (!WallHit(from, dir, 14f, out var hit)) return false;
            Vector3 n = new Vector3(hit.normal.x, 0, hit.normal.z).normalized;
            Vector3 p = new Vector3(hit.point.x, topY, hit.point.z) + n * 0.05f;
            return Kit(model, p, Quaternion.LookRotation(n).eulerAngles.y, false, false) != null;
        }

        static void BuildKitDressing(System.Text.StringBuilder log)
        {
            int torches = 0, banners = 0;
            // --- rua do mercado: mercadorias nas barracas (balcão a ~1 m) e caixas nas bordas
            Kit("FarmCrate_Apple", new Vector3(-5.02f, 1.0f, -30.0f), 90f, false, false);   // borda da frente: as caixas do modelo da barraca escondem o fundo
            Kit("FarmCrate_Carrot", new Vector3(-5.02f, 1.0f, -28.7f), 90f, false, false);
            Kit("Bag", new Vector3(-5.4f, 0, -31.4f), 30f);
            foreach (var (n, z) in new[] { ("Potion_2", -17.0f), ("Potion_4", -16.6f), ("Potion_1", -16.2f), ("SmallBottles_1", -15.7f), ("Bottle_1", -15.2f), ("Vase_4", -14.8f) })
                Kit(n, new Vector3(4.98f, 1.0f, z), -90f + Random.Range(-25f, 25f), false, false);
            foreach (var (n, z) in new[] { ("Coin_Pile", -8.0f), ("Chalice", -7.4f), ("Mug", -6.9f), ("Coin_Pile_2", -6.3f), ("Book_Stack_1", -5.8f) })
                Kit(n, new Vector3(4.98f, 1.0f, z), -90f + Random.Range(-25f, 25f), false, false);
            Kit("Barrel_Apples", new Vector3(-5.8f, 0, -37.8f), 0f);
            Kit("Crate_Wooden", new Vector3(-6.0f, 0, -36.7f), 10f);
            Kit("Barrel", new Vector3(-5.9f, 0, -24.6f), 0f);
            Kit("Bucket_Wooden_1", new Vector3(-5.4f, 0, -23.8f), 40f);
            Kit("Crate_Wooden", new Vector3(-5.8f, 0, -16.8f), 20f);
            Kit("Barrel_Apples", new Vector3(-5.6f, 0, -8.5f), 0f);
            Kit("Bag", new Vector3(-5.7f, 0, -5.4f), -20f);
            Kit("Crate_Wooden", new Vector3(6.0f, 0, -38.0f), -5f);
            Kit("Barrel", new Vector3(6.1f, 0, -36.9f), 0f);
            Kit("Bag", new Vector3(5.8f, 0, -33.0f), 160f);
            // ferreiro na porta da taverna
            Kit("WeaponStand", new Vector3(6.0f, 0, -22.0f), -90f);
            Kit("Anvil", new Vector3(5.7f, 0, -20.3f), -90f);
            Kit("Barrel", new Vector3(6.1f, 0, -19.2f), 0f);
            foreach (float z in new[] { -35f, -24f, -14f })
                if (WallBanner(new Vector3(0, 3.6f, z), Vector3.left, z == -24f ? "Banner_2_Cloth" : "Banner_1_Cloth", 3.7f)) banners++;
            foreach (float z in new[] { -31f, -18f, -10f })
                if (WallBanner(new Vector3(0, 3.6f, z), Vector3.right, z == -18f ? "Banner_1_Cloth" : "Banner_2_Cloth", 3.7f)) banners++;
            foreach (float z in new[] { -38.5f, -27.5f, -12.5f })
            {
                if (WallTorch(new Vector3(0, 2.4f, z), Vector3.left)) torches++;
                if (WallTorch(new Vector3(0, 2.4f, z + 1.5f), Vector3.right)) torches++;
            }
            // portão: face de dentro e de fora
            foreach (float x in new[] { -3.1f, 3.1f })
            {
                if (WallTorch(new Vector3(x, 2.7f, -36f), Vector3.back, 10f)) torches++;
                if (WallTorch(new Vector3(x, 2.7f, -50f), Vector3.forward, 10f)) torches++;
            }

            // --- praça: barracas e carroça na borda oeste, mesa da taverna e barraca a leste,
            // baú/ferraria ao norte (a base da torre fica livre para a escalada), balde no poço
            Kit("Stall_Empty", new Vector3(-18.2f, 0, 20f), 90f);
            Kit("FarmCrate_Apple", new Vector3(-17.2f, 0, 18.7f), 80f);
            Kit("FarmCrate_Apple", new Vector3(-17.2f, 0.24f, 18.7f), 95f, false);
            Kit("Barrel_Apples", new Vector3(-17.2f, 0, 21.5f), 0f);
            Kit("Stall_Cart_Empty", new Vector3(-17.8f, 0, 3.5f), 90f);
            Kit("Bag", new Vector3(-16.8f, 0, 5.6f), 20f);
            Kit("Crate_Wooden", new Vector3(-17.2f, 0, 1.4f), -15f);
            Kit("Table_Large", new Vector3(18.0f, 0, 5.5f), 90f);
            Kit("Bench", new Vector3(16.9f, 0, 5.5f), 90f);
            Kit("Bench", new Vector3(19.1f, 0, 5.5f), 90f);
            Kit("Mug", new Vector3(17.85f, 0.81f, 4.7f), 30f, false);
            Kit("Mug", new Vector3(18.1f, 0.81f, 6.3f), -60f, false);
            Kit("Table_Plate", new Vector3(18.0f, 0.81f, 5.5f), 0f, false);
            Kit("Stall_Empty", new Vector3(18.3f, 0, 24f), -90f);
            Kit("FarmCrate_Carrot", new Vector3(17.4f, 0, 22.6f), -80f);
            Kit("Barrel", new Vector3(17.5f, 0, 25.6f), 0f);
            Kit("Chest_Wood", new Vector3(-9f, 0, 29f), 180f);
            Kit("Barrel", new Vector3(-10.5f, 0, 29.3f), 0f);
            Kit("Crate_Wooden", new Vector3(-11.4f, 0, 28.7f), 12f);
            Kit("WeaponStand", new Vector3(7.5f, 0, 29f), 180f);
            Kit("Anvil", new Vector3(9.3f, 0, 29.1f), 180f);
            Kit("Whetstone", new Vector3(10.9f, 0, 29.3f), 200f);
            Kit("Dummy", new Vector3(-12.5f, 0, -2.5f), 60f);
            Kit("Bucket_Wooden_1", new Vector3(1.7f, 0, 11.0f), 15f);
            foreach (float x in new[] { -3.0f, 3.0f })
                if (WallTorch(new Vector3(x, 2.6f, 25f), Vector3.forward, 12f)) torches++;
            foreach (float z in new[] { 6f, 20f })
            {
                if (WallBanner(new Vector3(0, 3.9f, z), Vector3.left, "Banner_1_Cloth", 4.0f)) banners++;
                if (WallBanner(new Vector3(0, 3.9f, z + 2f), Vector3.right, "Banner_2_Cloth", 4.0f)) banners++;
            }
            foreach (float z in new[] { 1f, 13f, 23.5f })   // fora da linha dos postes de estandarte (z −2 e 27)
            {
                if (WallTorch(new Vector3(0, 2.5f, z), Vector3.left, 26f)) torches++;
                if (WallTorch(new Vector3(0, 2.5f, z + 1f), Vector3.right, 26f)) torches++;
            }

            // --- estrada sul: pátio de treino na entrada, fazenda, moinho, carroça do mercador
            Kit("Dummy", new Vector3(8.6f, 0, -95f), -90f);
            Kit("WeaponStand", new Vector3(9.8f, 0, -96.6f), -90f);
            Kit("Barrel", new Vector3(8.3f, 0, -97.6f), 0f);
            Kit("FarmCrate_Carrot", new Vector3(-26.5f, 0, -66.5f), 60f);
            Kit("FarmCrate_Apple", new Vector3(-25.8f, 0, -67.7f), 50f);
            Kit("Bag", new Vector3(-27.2f, 0, -65.3f), 10f);
            Kit("Barrel", new Vector3(-25.0f, 0, -65.8f), 0f);
            Kit("Bag", new Vector3(-44.0f, 0, -74.5f), 0f);
            Kit("Bag", new Vector3(-44.6f, 0, -75.3f), 70f);
            Kit("Crate_Wooden", new Vector3(-43.5f, 0, -76.2f), 25f);
            Kit("Barrel_Holder", new Vector3(-45.2f, 0, -72.6f), 30f);
            Kit("Barrel_Apples", new Vector3(-7.7f, 0, -56.9f), 0f);
            Kit("FarmCrate_Apple", new Vector3(-7.4f, 0, -59.4f), 75f);
            Kit("Barrel", new Vector3(-4.6f, 0, -46.6f), 0f);
            Kit("Crate_Wooden", new Vector3(4.6f, 0, -46.5f), -10f);

            // --- leste: margem do riacho e o acampamento abandonado na borda do Campo da Fenda
            Kit("Barrel", new Vector3(37.4f, 0, 6.5f), 0f);
            Kit("Rope_2", new Vector3(37.0f, 0.02f, 5.6f), 40f, false);
            Kit("Crate_Wooden", new Vector3(36.8f, 0, 12.7f), 30f);
            Kit("Cage_Small", new Vector3(52f, 0, 26f), 30f);
            Kit("Chain_Coil", new Vector3(53.6f, 0.02f, 27.4f), 0f, false);
            Kit("Vase_Rubble_Medium", new Vector3(88f, 0, 4f), 20f, false);
            Kit("Crate_Wooden", new Vector3(87f, 0, 26f), 40f);
            Kit("Bucket_Metal", new Vector3(88.2f, 0, 27.1f), 75f);
            Kit("Barrel", new Vector3(72f, 0, -4f), 0f);
            Kit("Vase_Rubble_Medium", new Vector3(70.4f, 0, -4.7f), 120f, false);
            Kit("Chest_Wood", new Vector3(90f, 0, 15f), -90f);

            log.Append("props do kit ok (tochas " + torches + ", estandartes " + banners + ")\n");
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
                // fotos do Poly Haven quando existem (Tools/texgen/fetch_polyhaven.py)
                var ph = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Campanula/Textures/PH/terrain_" + layers[i] + "_albedo.png");
                if (ph != null && CampanulaMaterials.TerrainPhTile.TryGetValue(layers[i], out float phTile)) { tl.diffuseTexture = ph; tiles[i] = phTile; }
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
                    // paredes e fundo do cânion: pedra e terra (nada de grama na rocha)
                    float gd = StreamMath.Gorge(x, z);
                    if (gd > 0.25f) { float k = Mathf.Clamp01(gd / 2.5f); cobble = Mathf.Max(cobble, 0.6f * k); dirt = Mathf.Max(dirt, 0.4f + 0.6f * (1 - k)); }
                    // campos de trigo ao sul (dos dois lados da estrada) e a leste
                    if (z < -50 && z > -110 && Mathf.Abs(x) > 8 && Mathf.Abs(x) < 70) field = 1;
                    if (x > 50 && x < 112 && z > 18 && z < 60) field = 1;
                    float sum = cobble + dirt + field;
                    float grass = Mathf.Max(0f, 1f - sum);
                    if (sum > 1) { cobble /= sum; dirt /= sum; field /= sum; }
                    a[iz, ix, 0] = grass; a[iz, ix, 1] = dirt; a[iz, ix, 2] = cobble; a[iz, ix, 3] = field;
                }
            td.SetAlphamaps(0, 0, a);
            BuildTerrainDetails(td, a);
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
            t.detailObjectDistance = 40f;    // o GameSettings ajusta por qualidade
            t.detailObjectDensity = 0.8f;
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
            // Testei sombras assadas (Mixed + Subtractive, 1.0 e 2.5 texels/m): paredes
            // manchadas e escuras com o lightmapper de CPU — pior que a luz em tempo real.
            // Fica em tempo real; sem sombra na Média (blob nos personagens), sombra completa na Alta.
            sun.lightmapBakeType = LightmapBakeType.Realtime;
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
            var ls = new LightingSettings
            {
                name = "Campanula_Lighting", bakedGI = false, realtimeGI = false,
                mixedBakeMode = MixedLightingMode.Subtractive,
                lightmapper = LightingSettings.Lightmapper.ProgressiveCPU,
                lightmapResolution = 2.5f, lightmapMaxSize = 2048, lightmapPadding = 2,
                directSampleCount = 32, indirectSampleCount = 64, environmentSampleCount = 96,
                maxBounces = 1, ao = false,
                // sem denoiser (o automático derrubava o LightBaker no Linux, código 11): Gaussiano
                filteringMode = LightingSettings.FilterMode.Advanced,
                denoiserTypeDirect = LightingSettings.DenoiserType.None,
                denoiserTypeIndirect = LightingSettings.DenoiserType.None,
                denoiserTypeAO = LightingSettings.DenoiserType.None,
                filterTypeDirect = LightingSettings.FilterType.Gaussian,
                filterTypeIndirect = LightingSettings.FilterType.Gaussian,
                filterTypeAO = LightingSettings.FilterType.Gaussian,
                filteringGaussianRadiusDirect = 1, filteringGaussianRadiusIndirect = 5, filteringGaussianRadiusAO = 2,
                lightmapCompression = LightmapCompression.NormalQuality,
            };
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
            mat.SetTexture("_Stars", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Aren/Resources/VFX/Space/space_stars_dense.png"));
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
            rm.SetTexture("_Space", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Aren/Resources/VFX/Space/space_purple_stars.png"));
            rm.SetTexture("_Stars", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Aren/Resources/VFX/Space/space_starfield.png"));
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
            int seg = 300; float z0 = -150f, z1 = 150f, w = 3.4f;
            for (int i = 0; i <= seg; i++)
            {
                float z = Mathf.Lerp(z0, z1, i / (float)seg);
                float cx = StreamCenter(z);
                // a superfície segue o leito (0,95 m acima do fundo): no cânion ela corre 16 m abaixo
                float y = GroundY(cx, z) + 0.95f;
                float ww = w + 2.2f * StreamMath.GorgeAlong(z);   // no cânion o rio ocupa o fundo
                verts.Add(new Vector3(cx - ww, y, z)); verts.Add(new Vector3(cx + ww, y, z));
                // na queda da cabeceira não há "superfície": a cortina da cachoeira cobre o degrau
                float zn = Mathf.Lerp(z0, z1, (i + 1) / (float)seg);
                bool steep = Mathf.Abs(GroundY(StreamCenter(zn), zn) - (y - 0.95f)) > 0.8f;
                if (i < seg && !steep) { int k = i * 2; tris.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 }); }
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
            Place("GHouse_A", new Vector3(-10.5f, 0, -33f), 90f);
            Place("GHouse_D", new Vector3(-11f, 0, -24f), 90f);
            Place("GHouse_B", new Vector3(-10f, 0, -15.5f), 90f);
            Place("GHouse_C", new Vector3(-10f, 0, -7.5f), 90f);
            Place("GHouse_B", new Vector3(10f, 0, -34f), -90f);
            Place("GTavern", new Vector3(12f, 0, -23f), -90f);
            Place("GHouse_D", new Vector3(11f, 0, -11.5f), -90f);

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
            Place("GHouse_D", new Vector3(-25f, 0, 3f), 90f);
            Place("GHouse_A", new Vector3(-25.5f, 0, 14f), 90f);
            Place("GHouse_B", new Vector3(-24.5f, 0, 24f), 90f);
            Place("GTavern", new Vector3(26f, 0, 5f), -90f);
            Place("GHouse_B", new Vector3(24.5f, 0, 17f), -90f);
            Place("GHouse_C", new Vector3(25f, 0, 26f), -90f);
            Place("GHouse_C", new Vector3(-13f, 0, 38f), 180f);
            Place("GHouse_A", new Vector3(13.5f, 0, 38.5f), 180f);
            Place("GHouse_D", new Vector3(-30f, 0, -8f), 90f);
            // bancos, barris, estandartes, barracas na borda
            foreach (var p in new[] { new Vector3(-6f, 0, 16f), new Vector3(6f, 0, 16f), new Vector3(-6f, 0, 4f), new Vector3(6f, 0, 4f) })
                Prop("Bench", p, p.x < 0 ? 90f : -90f, new Vector3(0, 0.25f, 0), new Vector3(1.8f, 0.5f, 0.45f));
            foreach (var p in new[] { new Vector3(-14f, 0, 27f), new Vector3(14f, 0, 27f), new Vector3(-16f, 0, -2f), new Vector3(16f, 0, -2f) })
            {
                var bp = Place("Banner_Clef", p, p.x < 0 ? 45f : -45f, null, true, false);
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
            // a ponte-aqueduto de dois andares de arcos sobre o desfiladeiro (tabuleiro em y = 0)
            Place("Aqueduct", new Vector3(StreamCenter(BridgeZ), 0f, BridgeZ), 0f, null, false, true);
            // casas e celeiros perto do riacho
            Place("GHouse_C", new Vector3(30f, 0, -16f), -90f);
            Place("GHouse_A", new Vector3(31f, 0, 36f), -90f);
            // campo: cercas quebradas, fardos, pedras
            for (int i = 0; i < 10; i++)
            {
                if (i == 4 || i == 7) continue;   // brechas
                var f = Place("Fence", new Vector3(63f + i * 3f, 0, 17f), 0f, null, true, false);
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

        // ------------------------------------------------------------ desfiladeiro: muros, cachoeiras, névoa

        static Material mistMat, fallMat;

        static void BuildGorge(System.Text.StringBuilder log)
        {
            fallMat = new Material(Shader.Find("Campanula/Waterfall"));
            fallMat.SetTexture("_Noise", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Aren/Resources/VFX/noise_night.png"));
            AssetDatabase.CreateAsset(fallMat, "Assets/Campanula/Materials/Waterfall.mat");
            mistMat = new Material(Shader.Find("Campanula/Mist"));
            mistMat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Aren/Resources/VFX/fx_smoke.png"));
            mistMat.enableInstancing = true;
            AssetDatabase.CreateAsset(mistMat, "Assets/Campanula/Materials/Mist.mat");
            var froot = new GameObject("Desfiladeiro").transform;
            froot.SetParent(root);
            int falls = 0;
            // muros de arrimo na borda da cidade (oeste), virados para o cânion, dos dois lados da ponte;
            // e um par na margem leste, emoldurando a ponte
            var walls = new List<(float z, bool west)> { (-2f, true), (21f, true), (-1f, false), (20.5f, false) };
            foreach (var (wz, west) in walls)
            {
                float cx = StreamCenter(wz);
                // o muro fica no pé da encosta do terreno (a 5,4 m do eixo): a face cai no vazio do cânion e o
                // tabuleiro de 5 m cobre a encosta até o chão da cidade
                float rim = west ? cx - 5.4f : cx + 5.4f;
                // tangente do riacho para o muro acompanhar a curva
                float dxdz = (StreamCenter(wz + 1f) - StreamCenter(wz - 1f)) / 2f;
                float yaw = (west ? 90f : -90f) + Mathf.Atan(dxdz) * Mathf.Rad2Deg;
                var w = Place("Gorge_Wall", new Vector3(rim, 0.04f, wz), yaw, null, false, true);   // 4 cm acima do chão: sem briga de profundidade com o terreno
                foreach (var t in w.GetComponentsInChildren<Transform>())
                    if (t.name.StartsWith("FALL_")) { Waterfall(froot, t.position, w.transform.forward, 2.0f); falls++; }
            }
            // lanternas no fundo do cânion (pés dos muros e da ponte, beira do rio): o fundo não fica preto
            int lampN = 0;
            foreach (float lz in new[] { -40f, -26f, -12f, 2f, 16f, 30f, 44f, 56f })
                foreach (int side in new[] { -1, 1 })
                {
                    if (side > 0 && (lz == -40f || lz == 56f)) continue;
                    float cx = StreamCenter(lz);
                    float k = StreamMath.GorgeAlong(lz);
                    if (k < 0.5f) continue;
                    var l = new GameObject("LAMP_canion_" + (lampN++));
                    l.transform.SetParent(froot, false);
                    l.transform.position = new Vector3(cx + side * 4.6f, -StreamMath.GorgeDepth * k + 1.8f, lz);
                }
            // a cachoeira grande da cabeceira: o riacho despenca inteiro no cânion
            {
                float z = StreamMath.GorgeNorth + 1.2f;
                Waterfall(froot, new Vector3(StreamCenter(z), -0.7f, z), Vector3.back, 7.5f, 0.8f);
                falls++;
            }
            // penedos revestindo as encostas onde não há muro (o terreno esticado vira penhasco de pedra)
            var rng = new System.Random(77);
            int rocks = 0;
            string[] rk = { "Cliff_Rock_A", "Cliff_Rock_B", "Cliff_Rock_C" };
            for (float z = StreamMath.GorgeSouth - 30f; z <= StreamMath.GorgeNorth - 1f; z += 4.2f)
            {
                float k = StreamMath.GorgeAlong(z);
                if (k < 0.25f) continue;
                foreach (int side in new[] { -1, 1 })
                {
                    bool walled = (side < 0 && z > -11.5f && z < 30.5f) || (side > 0 && z > -10.5f && z < 30f) || Mathf.Abs(z - BridgeZ) < 3.5f;
                    if (walled) continue;
                    float cx = StreamCenter(z);
                    float jz = (float)(rng.NextDouble() - 0.5) * 2.5f;
                    float dx = 6.6f + (float)rng.NextDouble() * 0.9f;
                    var p = new Vector3(cx + side * dx, -StreamMath.GorgeDepth * k * (0.36f + 0.12f * (float)rng.NextDouble()), z + jz);
                    float sc = (0.9f + 0.5f * (float)rng.NextDouble()) * Mathf.Lerp(0.5f, 1f, k);
                    // laje de pé na encosta (eixo longo na vertical), a face larga virada para o cânion
                    float yaw = (side < 0 ? 90f : -90f) + (float)(rng.NextDouble() - 0.5) * 40f;
                    var go = Place(rk[rng.Next(rk.Length)], p, yaw, froot, false, true, 0, sc);
                    go.transform.rotation = Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 12f, yaw, (float)(rng.NextDouble() - 0.5) * 12f);
                    rocks++;
                }
            }
            // névoa no fundo do cânion (esconde o pé dos pilares e o fundo do terreno, como na arte)
            for (float z = -56f; z <= 58f; z += 13f)
            {
                float k = StreamMath.GorgeAlong(z);
                if (k < 0.3f) continue;
                var p = new Vector3(StreamCenter(z), -StreamMath.GorgeDepth * k + 1.2f, z);
                Mist(froot, p, new Vector3(9f, 1.5f, 13f), 3.2f, 6f, 10f, 0.16f, 0.25f);
            }
            log.Append("desfiladeiro ok (" + falls + " cachoeiras, " + rocks + " penedos)\n");
        }

        /// <summary>Cortina de água em arco (sai do bueiro para fora e cai) + borrifo no pé.</summary>
        static void Waterfall(Transform parent, Vector3 top, Vector3 outward, float width, float outSpeed = 1.6f)
        {
            outward.y = 0f; outward.Normalize();
            float bottom = GroundY(top.x + outward.x * 2.5f, top.z + outward.z * 2.5f);
            float drop = Mathf.Max(2f, top.y - bottom);
            const int segs = 20;
            float T = Mathf.Sqrt(2f * drop / 9.81f);
            var right = Vector3.Cross(Vector3.up, outward);
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var cols = new List<Color>(); var tris = new List<int>();
            for (int i = 0; i <= segs; i++)
            {
                float k = i / (float)segs, tt = k * T;
                float fwd = outSpeed * tt, down = 0.5f * 9.81f * tt * tt;
                float w = width * (1f + 0.45f * k);
                var c = outward * fwd + Vector3.down * down;
                verts.Add(c - right * w / 2f); verts.Add(c + right * w / 2f);
                uvs.Add(new Vector2(0f, k)); uvs.Add(new Vector2(1f, k));
                var col = new Color(drop / 30f, 0, 0, 1); cols.Add(col); cols.Add(col);
                if (i < segs) { int b = i * 2; tris.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 }); }
            }
            var mesh = new Mesh { name = "Cachoeira" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.AddObjectToAsset(mesh, fallMat);
            var go = new GameObject("Cachoeira");
            go.transform.SetParent(parent, false);
            go.transform.position = top;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = fallMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            // borrifo no pé
            var foot = top + outward * (outSpeed * T) + Vector3.down * drop;
            Mist(go.transform, foot + Vector3.up * 0.6f, new Vector3(width * 1.4f, 0.5f, 1.5f), 1.2f + width * 0.25f, 2.5f, 4.5f, 0.3f, 1f + width * 0.2f);
        }

        /// <summary>Partículas de névoa (Campanula/Mist): caixa, tamanho, vida, transparência e taxa.</summary>
        static void Mist(Transform parent, Vector3 pos, Vector3 box, float speed, float sizeMin, float sizeMax, float alpha, float rate)
        {
            var go = new GameObject("Nevoa");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.1f, speed * 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = new Color(0.72f, 0.78f, 0.9f, alpha);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = box;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.3f));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mistMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.sortingFudge = 2f;
            ps.Play();
        }

        // ------------------------------------------------------------ silhueta da cidade (torres)

        static void BuildSkyline(System.Text.StringBuilder log)
        {
            // torres góticas em volta e atrás da vila (fora das ruas jogáveis): a cidade de muitas torres
            // da referência. Norte: duas emolduram o Campanário; oeste: além da muralha; leste: na outra
            // margem do cânion, nas cabeceiras da ponte.
            var towers = new (string m, Vector3 p, float yaw)[]
            {
                ("GTower_A", new Vector3(-23f, 0, 50f), 180f), ("GTower_B", new Vector3(23f, 0, 51f), 180f),
                ("GTower_C", new Vector3(-36f, 0, 44f), 135f), ("GTower_A", new Vector3(-50f, 0, 12f), 90f),
                ("GTower_B", new Vector3(-50f, 0, -24f), 90f), ("GTower_C", new Vector3(-46f, 0, -50f), 45f),
                ("GTower_C", new Vector3(57.5f, 0, 1.5f), -90f), ("GTower_B", new Vector3(58f, 0, 17.5f), -90f),
                ("GTower_C", new Vector3(29f, 0, 47f), 200f),
            };
            foreach (var t in towers) Place(t.m, t.p, t.yaw, null, true, true);
            // estandartes da clave nas cabeceiras da ponte e no portão
            foreach (var p in new[] { new Vector3(StreamCenter(BridgeZ) - 12.4f, 0, BridgeZ - 3.6f), new Vector3(StreamCenter(BridgeZ) - 12.4f, 0, BridgeZ + 3.6f),
                                      new Vector3(StreamCenter(BridgeZ) + 12.4f, 0, BridgeZ - 3.6f), new Vector3(StreamCenter(BridgeZ) + 12.4f, 0, BridgeZ + 3.6f),
                                      new Vector3(-6.2f, 0, -45.5f), new Vector3(6.2f, 0, -45.5f) })
            {
                var bp = Place("Banner_Clef", p, p.z < -40f ? 180f : (p.x < StreamCenter(BridgeZ) ? 90f : -90f), null, true, false);
                var cc = bp.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0, 3f, 0); cc.radius = 0.12f; cc.height = 6f;
            }
            log.Append("silhueta ok (" + towers.Length + " torres)\n");
        }

        // ------------------------------------------------------------ o Grande Aqueduto e a cidade alta

        static void BuildGreatAqueduct(System.Text.StringBuilder log)
        {
            // atravessa o fundo da vila de oeste a leste (z = 78), por cima do riacho: a ponte de arcos
            // monumental da referência, vista por cima dos telhados desde a estrada
            const float z = 78f, seg = 30f;
            float xStart = -128f;
            int n = 6, falls = 0;
            var aroot = new GameObject("Grande Aqueduto").transform;
            aroot.SetParent(root);
            for (int i = 0; i < n; i++)
            {
                float x = xStart + seg * (i + 0.5f);
                var a = Place("Great_Aqueduct", new Vector3(x, 0f, z), 180f, null, false, true);
                foreach (var t in a.GetComponentsInChildren<Transform>())
                    if (t.name.StartsWith("FALL_") && (i % 2 == 1 || i == n - 1) && t.position.x > xStart + 20f)
                    {
                        Waterfall(aroot, t.position, a.transform.forward, 1.6f, 1.2f);
                        falls++;
                    }
            }
            // a água correndo no canal do alto
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Campanula/Materials/Water.mat");
                var verts = new List<Vector3>(); var tris = new List<int>();
                float y = 23.86f, x0 = xStart, x1 = xStart + seg * n;
                verts.Add(new Vector3(x0, y, z - 1.0f)); verts.Add(new Vector3(x0, y, z + 1.0f));
                verts.Add(new Vector3(x1, y, z - 1.0f)); verts.Add(new Vector3(x1, y, z + 1.0f));
                tris.AddRange(new[] { 0, 1, 2, 2, 1, 3 });
                var mesh = new Mesh { name = "AguaDoAqueduto" };
                mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                AssetDatabase.AddObjectToAsset(mesh, fallMat);
                var go = new GameObject("Agua do aqueduto");
                go.transform.SetParent(aroot, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // luzes no pé dos pilares (na referência os arcos são acesos por baixo): marcadores LAMP_ que o
            // VillageLights transforma em lanterna + luz da cidade
            for (int i = 0; i <= n * 2; i++)
            {
                float px = xStart + i * seg / 2f;
                if (px < xStart + 15f) continue;
                var l = new GameObject("LAMP_pilar_" + i);
                l.transform.SetParent(aroot, false);
                l.transform.position = new Vector3(px, GroundY(px, z) + 2.4f, z - 3.7f);
            }
            // névoa no pé dos arcos (o vale enevoado da referência)
            for (int i = 0; i < n; i++)
                Mist(aroot, new Vector3(xStart + seg * (i + 0.5f), GroundY(xStart + seg * (i + 0.5f), z) + 1.5f, z), new Vector3(26f, 2f, 10f), 2.5f, 8f, 14f, 0.12f, 1.4f);
            log.Append("grande aqueduto ok (" + n + " segmentos, " + falls + " cachoeiras)\n");
        }

        static void BuildUpperTown(System.Text.StringBuilder log)
        {
            // a cidade continua além dos muros (a referência é uma cidade, não uma vila): fileiras de sobrados
            // e torres ao norte (entre a praça e o aqueduto) e a oeste (além da muralha), fora das ruas jogáveis
            string[] hs = { "GHouse_A", "GHouse_B", "GHouse_C", "GHouse_D", "GTavern" };
            var rng = new System.Random(4242);
            int n = 0;
            // norte: frentes para o sul (yaw 180)
            foreach (var (x, zz) in new[] { (-45f, 64f), (-31f, 62.5f), (-14f, 61.5f), (-6f, 62f), (6f, 62.5f), (14f, 61.5f), (27f, 62f),
                                            (-36f, 70.5f), (-20f, 70f), (-2f, 70.5f), (12f, 70f), (26f, 70.5f) })
            { Place(hs[rng.Next(hs.Length)], new Vector3(x, 0, zz), 180f + (float)(rng.NextDouble() - 0.5) * 6f); n++; }
            // oeste: frentes para leste (yaw 90), além da muralha (x = −40)
            foreach (var (x, zz) in new[] { (-55f, -38f), (-56f, -4f), (-55f, 4.5f), (-56f, 24f), (-55f, 33f), (-56f, 44f),
                                            (-65f, -32f), (-66f, -12f), (-65f, 2f), (-66f, 18f), (-65f, 32f) })
            { Place(hs[rng.Next(hs.Length)], new Vector3(x, 0, zz), 90f + (float)(rng.NextDouble() - 0.5) * 6f); n++; }
            foreach (var t in new (string m, Vector3 p, float yaw)[] { ("GTower_A", new Vector3(-8f, 0, 79f), 180f), ("GTower_C", new Vector3(-70f, 0, -50f), 90f), ("GTower_B", new Vector3(-72f, 0, 46f), 90f) })
            { Place(t.m, t.p, t.yaw); n++; }
            log.Append("cidade alta ok (" + n + " construções)\n");
        }

        static void BuildGroundMist(System.Text.StringBuilder log)
        {
            // faixas de névoa baixa entre a estrada, os campos e a muralha: camadas de profundidade como na arte
            var mroot = new GameObject("Névoa baixa").transform;
            mroot.SetParent(root);
            // (nenhuma em cima da estrada: partícula grande cobrindo a tela pesa no Intel UHD)
            var pts = new[] { new Vector3(-42, 0, -64), new Vector3(44, 0, -60),
                              new Vector3(-30, 0, -50), new Vector3(30, 0, -50), new Vector3(-50, 0, -15), new Vector3(-50, 0, 20),
                              new Vector3(-30, 0, 88), new Vector3(10, 0, 90), new Vector3(60, 0, 40), new Vector3(70, 0, -10) };
            foreach (var p in pts)
                Mist(mroot, new Vector3(p.x, GroundY(p.x, p.z) + 0.8f, p.z), new Vector3(18f, 1f, 10f), 1.2f, 9f, 16f, 0.08f, 0.7f);
            log.Append("névoa baixa ok\n");
        }

        // ------------------------------------------------------------ estrada sul (tutorial)

        static void BuildSouthRoad(System.Text.StringBuilder log)
        {
            // pórtico gótico do sino da estrada (o BellShrine pendura o sino e a lanterna nele)
            Place("Bell_Pavilion", Aren.World.GameFlow.SpawnPos + new Vector3(2.6f, 0f, 0.9f), 90f, null, true, true);
            // moinho e fazenda
            var mill = Place("Windmill", new Vector3(-48f, 0, -78f), 30f);
            foreach (var t in mill.GetComponentsInChildren<Transform>())
                if (t.name.Contains("Sails"))
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                    var mc = t.GetComponent<MeshCollider>(); if (mc) Object.DestroyImmediate(mc);
                    t.gameObject.AddComponent<Campanula.Spin>().axis = Vector3.up;   // eixo Y do Blender (pás no plano XZ)
                }
            Place("GHouse_C", new Vector3(-30f, 0, -70f), 60f);
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
                bool stream = Mathf.Abs(x - StreamCenter(z)) < 6 + 5.5f * StreamMath.GorgeAlong(z);
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
                float ga = StreamMath.GorgeAlong(z);
                // no cânion: arbustos e pedras no fundo, junto do rio (nas encostas ficariam flutuando)
                float off = ga > 0.2f ? 3.6f + (float)rng.NextDouble() * 1.6f : 5.5f + (float)rng.NextDouble() * 3f;
                float x = StreamCenter(z) + (rng.NextDouble() < 0.5 ? -1 : 1) * off;
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

        /// <summary>
        /// Grafo de salto entre bordas do DPS: o ClimbController só salta para pontos
        /// "vizinhos" calculados pelo HandlePointConnection (no editor, normalmente à mão).
        /// Sem isto o Aren agarra a primeira pedra e não sobe mais.
        /// </summary>
        static void ConnectLedges(System.Text.StringBuilder log)
        {
            var mgr = parkour.gameObject.AddComponent<Climbing.HandlePointConnection>();
            mgr.maxDistance = 1.55f;
            mgr.minDistance = 0.4f;
            mgr.updateConnections = true;
            typeof(Climbing.HandlePointConnection).GetMethod("Update", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(mgr, null);
            int links = 0;
            foreach (var pt in parkour.GetComponentsInChildren<Climbing.Point>()) links += pt.neighbours.Count;
            log.Append("bordas conectadas: " + mgr.allPoints.Count + " pontos, " + links + " ligações\n");
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
