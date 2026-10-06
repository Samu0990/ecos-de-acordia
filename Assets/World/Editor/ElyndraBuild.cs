using System.Collections.Generic;
using System.Linq;
using Elyndra.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Elyndra.WorldEditor
{
    /// <summary>
    /// Entrada do construtor do mundo de Elyndra (menu "Elyndra"): perfis visuais dos 13 reinos, bestiário,
    /// materiais de runtime, as cenas dos reinos (Assets/World/Scenes/Regions), as masmorras
    /// (Assets/World/Scenes/Dungeons), o mapa do continente e as Build Settings (Campânula continua a cena 0).
    /// Tudo é GERADO: para mudar um reino, edite RegionRecipes.cs e reconstrua aquele reino.
    /// </summary>
    public static class ElyndraBuild
    {
        public const string DataDir = "Assets/World/Data";

        [MenuItem("Elyndra/Construir mundo inteiro")]
        public static string BuildAll()
        {
            var log = new System.Text.StringBuilder();
            log.Append(Prepare());
            foreach (RegionId r in System.Enum.GetValues(typeof(RegionId))) log.Append(BuildRegion(r));
            foreach (var d in WorldCanon.Dungeons) log.Append(BuildDungeon(d.id));
            log.Append(WorldMapBuilder.Build());
            log.Append(UpdateBuildSettings());
            return log.ToString();
        }

        [MenuItem("Elyndra/Preparar (perfis, bestiário, materiais)")]
        public static string Prepare()
        {
            ProcMesh.ClearCache(); WorldMats.ClearCache();
            foreach (var d in new[] { DataDir, DataDir + "/Regions", "Assets/World/Resources/Elyndra", "Assets/World/Scenes", "Assets/World/Prefabs" }) System.IO.Directory.CreateDirectory(d);
            foreach (RegionId r in System.Enum.GetValues(typeof(RegionId))) { Profile(r); DungeonProfile(r); }
            Catalog();
            RuntimeMaterials();
            AssetDatabase.SaveAssets();
            return "perfis, bestiário e materiais prontos\n";
        }

        public static string BuildRegion(RegionId id)
        {
            try
            {
                ProcMesh.ClearCache(); WorldMats.ClearCache();
                var L = RegionRecipes.For(id);
                var s = RegionBuilder.Build(L, Profile(id));
                AssetDatabase.SaveAssets();
                return $"== {WorldCanon.Region(id).name} ==\n{s}";
            }
            catch (System.Exception e) { Debug.LogException(e); return $"ERRO ao construir {id}: {e.Message}\n{e.StackTrace}\n"; }
        }

        public static string BuildDungeon(string dungeonId)
        {
            try
            {
                var dd = WorldCanon.Dungeon(dungeonId);
                var s = DungeonBuilder.Build(dd, Profile(dd.region), DungeonProfile(dd.region));
                AssetDatabase.SaveAssets();
                return s;
            }
            catch (System.Exception e) { Debug.LogException(e); return $"ERRO na masmorra {dungeonId}: {e.Message}\n{e.StackTrace}\n"; }
        }

        [MenuItem("Elyndra/Reino: Valtéria")] static void MV() => Debug.Log(BuildRegion(RegionId.Valteria));
        [MenuItem("Elyndra/Mapa do continente")] static void MM() => Debug.Log(WorldMapBuilder.Build());
        [MenuItem("Elyndra/Atualizar Build Settings")] static void MB() => Debug.Log(UpdateBuildSettings());

        public static string UpdateBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>();
            var camp = EditorBuildSettings.scenes.FirstOrDefault(s => s.path.EndsWith("Campanula.unity"));
            list.Add(camp ?? new EditorBuildSettingsScene("Assets/Campanula/Scenes/Campanula.unity", true));
            foreach (var r in WorldCanon.Regions)
            {
                string p = $"{RegionBuilder.SceneDir}/{r.scene}.unity";
                if (System.IO.File.Exists(p)) list.Add(new EditorBuildSettingsScene(p, true));
            }
            foreach (var d in WorldCanon.Dungeons)
            {
                string p = $"{DungeonBuilder.SceneDir}/{WorldCanon.DungeonScene(d.id)}.unity";
                if (System.IO.File.Exists(p)) list.Add(new EditorBuildSettingsScene(p, true));
            }
            if (System.IO.File.Exists(WorldMapBuilder.ScenePath)) list.Add(new EditorBuildSettingsScene(WorldMapBuilder.ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();   // grava ProjectSettings/EditorBuildSettings.asset já (senão só ao fechar o editor)
            return $"build settings: {list.Count} cenas\n";
        }

        // ---------------------------------------------------------------- perfis visuais

        static void C(RegionProfile p, Color sun, float si, float elev, float az, Color sky, Color eq, Color gr, Color fog, float fd, Color top, Color hor, Color motes, float rate, bool fall, Color corr, Color corrFog)
        {
            p.sunColor = sun; p.sunIntensity = si; p.sunAngles = new Vector2(elev, az);
            p.ambientSky = sky; p.ambientEquator = eq; p.ambientGround = gr;
            p.fogColor = fog; p.fogDensity = fd; p.skyTop = top; p.skyHorizon = hor;
            p.motesColor = motes; p.motesRate = rate; p.motesFall = fall;
            p.corruptionTint = corr; p.corruptionFog = corrFog;
        }

        // céu (nuvens, disco do sol, estrelas), cor das serras distantes e a imagem do reino (pós)
        static void K(RegionProfile p, float overcast, float sunDisk, float stars, Color far, float contrast, float sat, float vignette, float exposure, float purkinje, Color shadow, Color high, bool bloom = true)
        {
            p.overcast = overcast; p.sunDisk = sunDisk; p.starAmount = stars; p.farRange = far;
            p.gradeContrast = contrast; p.gradeSaturation = sat; p.gradeVignette = vignette; p.gradeExposure = exposure; p.purkinje = purkinje;
            p.shadowTint = shadow; p.highTint = high; p.bloom = bloom;
        }

        public static RegionProfile Profile(RegionId id)
        {
            string path = $"{DataDir}/Regions/{id}.asset";
            var p = AssetDatabase.LoadAssetAtPath<RegionProfile>(path);
            bool fresh = p == null;
            if (fresh) { p = ScriptableObject.CreateInstance<RegionProfile>(); AssetDatabase.CreateAsset(p, path); }
            p.region = id;
            Color c(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);
            // Mundo SOMBRIO (pedido do autor): céus pesados, sol baixo ou velado, ambiente escuro, cor contida.
            // Neblina mais fina que antes (dá para ver os marcos ao longe) — o peso vem do céu, da luz e da imagem.
            switch (id)
            {
                // crepúsculo pesado depois da queda do brilho; a Fenda violeta a nor-nordeste
                case RegionId.Valteria:
                    C(p, c(1f, 0.58f, 0.36f), 0.95f, 9f, 80f, c(0.3f, 0.27f, 0.4f), c(0.38f, 0.28f, 0.27f), c(0.1f, 0.085f, 0.085f), c(0.42f, 0.31f, 0.33f), 0.0011f, c(0.07f, 0.06f, 0.14f), c(0.85f, 0.42f, 0.3f), c(1f, 0.85f, 0.6f, 0.45f), 35f, false, c(0.55f, 0.35f, 0.7f), c(0.3f, 0.22f, 0.34f));
                    K(p, 0.55f, 0.6f, 0.15f, c(0.2f, 0.17f, 0.21f), 0.32f, 0.9f, 0.36f, 1.05f, 0.1f, c(0.9f, 0.93f, 1.08f), c(1.07f, 1f, 0.9f)); break;
                // planícies de vento e poeira dourada sob tempestade de fim de tarde
                case RegionId.Velaria:
                    C(p, c(1f, 0.72f, 0.45f), 1.05f, 13f, 60f, c(0.42f, 0.38f, 0.36f), c(0.48f, 0.38f, 0.29f), c(0.14f, 0.11f, 0.08f), c(0.62f, 0.5f, 0.38f), 0.001f, c(0.16f, 0.2f, 0.32f), c(0.95f, 0.66f, 0.4f), c(1f, 0.9f, 0.7f, 0.4f), 30f, false, c(0.5f, 0.6f, 0.9f), c(0.35f, 0.35f, 0.45f));
                    K(p, 0.5f, 0.8f, 0.05f, c(0.3f, 0.25f, 0.22f), 0.3f, 0.88f, 0.34f, 1f, 0f, c(0.92f, 0.95f, 1.06f), c(1.08f, 1f, 0.88f)); break;
                // torres de vidro na bruma azul do anoitecer
                case RegionId.Miralume:
                    C(p, c(0.62f, 0.7f, 0.92f), 0.5f, 22f, 200f, c(0.24f, 0.28f, 0.38f), c(0.2f, 0.24f, 0.3f), c(0.06f, 0.07f, 0.09f), c(0.33f, 0.38f, 0.47f), 0.002f, c(0.03f, 0.05f, 0.11f), c(0.3f, 0.36f, 0.48f), c(0.8f, 0.9f, 1f, 0.35f), 40f, false, c(0.6f, 0.6f, 0.7f), c(0.25f, 0.27f, 0.32f));
                    K(p, 0.45f, 0.3f, 0.7f, c(0.17f, 0.19f, 0.25f), 0.28f, 0.85f, 0.38f, 1.12f, 0.45f, c(0.9f, 0.95f, 1.1f), c(1f, 1.02f, 1.05f)); break;
                // floresta gigante: verde fundo, névoa e fachos de sol entre as copas
                case RegionId.Orvalume:
                    C(p, c(1f, 0.86f, 0.6f), 0.85f, 24f, 120f, c(0.3f, 0.38f, 0.3f), c(0.26f, 0.32f, 0.22f), c(0.08f, 0.1f, 0.06f), c(0.33f, 0.4f, 0.32f), 0.0024f, c(0.2f, 0.3f, 0.36f), c(0.62f, 0.66f, 0.5f), c(1f, 0.95f, 0.6f, 0.5f), 60f, false, c(0.45f, 0.8f, 0.3f), c(0.25f, 0.33f, 0.2f));
                    K(p, 0.6f, 0.5f, 0f, c(0.16f, 0.2f, 0.15f), 0.33f, 0.82f, 0.4f, 1.05f, 0f, c(0.88f, 0.98f, 1.02f), c(1.06f, 1.03f, 0.9f)); break;
                // Sol Oco — o meio-dia sem luz: branco lavado, cor que não esquenta, luz chapada e estranha
                case RegionId.Helion:
                    C(p, c(1f, 0.97f, 0.9f), 1.05f, 62f, 30f, c(0.5f, 0.5f, 0.52f), c(0.5f, 0.47f, 0.42f), c(0.2f, 0.18f, 0.15f), c(0.78f, 0.76f, 0.72f), 0.0014f, c(0.45f, 0.5f, 0.6f), c(0.92f, 0.88f, 0.8f), c(1f, 1f, 0.9f, 0.25f), 20f, false, c(0.9f, 0.85f, 0.5f), c(0.5f, 0.47f, 0.4f));
                    K(p, 0.3f, 1f, 0f, c(0.45f, 0.42f, 0.38f), 0.22f, 0.6f, 0.38f, 1f, 0f, c(0.95f, 0.96f, 1.02f), c(1.04f, 1.02f, 0.97f), false); break;
                // metrópole da noite: índigo, violeta e lanternas
                case RegionId.Sefra:
                    C(p, c(0.5f, 0.45f, 0.75f), 0.38f, 35f, 300f, c(0.16f, 0.13f, 0.27f), c(0.2f, 0.12f, 0.22f), c(0.04f, 0.03f, 0.06f), c(0.15f, 0.1f, 0.22f), 0.0016f, c(0.015f, 0.015f, 0.06f), c(0.22f, 0.1f, 0.28f), c(0.9f, 0.6f, 1f, 0.45f), 50f, false, c(1f, 0.4f, 0.8f), c(0.2f, 0.1f, 0.22f));
                    K(p, 0.25f, 0.9f, 1.2f, c(0.09f, 0.07f, 0.13f), 0.25f, 1f, 0.4f, 1.25f, 0.55f, c(0.92f, 0.9f, 1.12f), c(1.05f, 0.98f, 1f)); break;
                // penhascos, mosteiros e cemitérios no cinza do fim de tarde
                case RegionId.Nereth:
                    C(p, c(0.72f, 0.76f, 0.86f), 0.55f, 7f, 250f, c(0.24f, 0.27f, 0.33f), c(0.22f, 0.24f, 0.28f), c(0.06f, 0.07f, 0.08f), c(0.33f, 0.36f, 0.42f), 0.0022f, c(0.06f, 0.08f, 0.12f), c(0.4f, 0.43f, 0.5f), c(0.8f, 0.8f, 0.85f, 0.4f), 45f, true, c(0.6f, 0.65f, 0.75f), c(0.24f, 0.26f, 0.3f));
                    K(p, 0.75f, 0.2f, 0.3f, c(0.15f, 0.16f, 0.19f), 0.3f, 0.7f, 0.42f, 1.08f, 0.3f, c(0.92f, 0.95f, 1.08f), c(1f, 1f, 1.02f)); break;
                // fortalezas da montanha sob céu frio e coberto
                case RegionId.Granith:
                    C(p, c(0.85f, 0.88f, 0.95f), 0.75f, 26f, 150f, c(0.38f, 0.42f, 0.5f), c(0.32f, 0.35f, 0.4f), c(0.11f, 0.11f, 0.13f), c(0.56f, 0.6f, 0.66f), 0.0013f, c(0.26f, 0.32f, 0.42f), c(0.66f, 0.7f, 0.75f), c(1f, 1f, 1f, 0.75f), 120f, true, c(0.55f, 0.55f, 0.7f), c(0.4f, 0.4f, 0.45f));
                    K(p, 0.7f, 0.3f, 0f, c(0.24f, 0.25f, 0.28f), 0.3f, 0.8f, 0.36f, 1.02f, 0f, c(0.92f, 0.95f, 1.06f), c(1f, 1f, 1.02f)); break;
                // cinza, brasa e céu vermelho das forjas
                case RegionId.CoroaDeCinza:
                    C(p, c(1f, 0.48f, 0.28f), 0.75f, 18f, 40f, c(0.32f, 0.18f, 0.15f), c(0.42f, 0.22f, 0.15f), c(0.12f, 0.06f, 0.04f), c(0.38f, 0.19f, 0.13f), 0.002f, c(0.1f, 0.04f, 0.04f), c(0.62f, 0.25f, 0.13f), c(1f, 0.5f, 0.2f, 0.8f), 80f, false, c(1f, 0.45f, 0.2f), c(0.35f, 0.15f, 0.1f));
                    K(p, 0.7f, 0.5f, 0.1f, c(0.14f, 0.09f, 0.08f), 0.34f, 0.9f, 0.4f, 1.05f, 0f, c(0.95f, 0.92f, 1f), c(1.08f, 0.98f, 0.88f)); break;
                // o mar cristalizado no crepúsculo frio
                case RegionId.MarDeVidro:
                    C(p, c(0.8f, 0.9f, 1f), 0.7f, 10f, 210f, c(0.22f, 0.35f, 0.4f), c(0.22f, 0.33f, 0.36f), c(0.07f, 0.1f, 0.11f), c(0.3f, 0.46f, 0.5f), 0.0012f, c(0.04f, 0.1f, 0.17f), c(0.42f, 0.65f, 0.7f), c(0.7f, 1f, 1f, 0.4f), 40f, false, c(0.4f, 0.9f, 0.9f), c(0.2f, 0.32f, 0.34f));
                    K(p, 0.35f, 0.8f, 0.45f, c(0.14f, 0.2f, 0.23f), 0.28f, 0.85f, 0.36f, 1.08f, 0.2f, c(0.9f, 0.97f, 1.08f), c(1.02f, 1.02f, 1f)); break;
                // clínicas de sal: branco estéril sob céu velado
                case RegionId.Caliria:
                    C(p, c(1f, 0.97f, 0.93f), 0.95f, 32f, 100f, c(0.52f, 0.57f, 0.63f), c(0.5f, 0.52f, 0.52f), c(0.2f, 0.2f, 0.19f), c(0.72f, 0.78f, 0.82f), 0.0013f, c(0.32f, 0.46f, 0.62f), c(0.82f, 0.86f, 0.9f), c(1f, 1f, 1f, 0.3f), 25f, false, c(0.95f, 0.75f, 0.75f), c(0.55f, 0.5f, 0.5f));
                    K(p, 0.6f, 0.4f, 0f, c(0.4f, 0.44f, 0.48f), 0.24f, 0.7f, 0.34f, 1f, 0f, c(0.95f, 0.98f, 1.04f), c(1.02f, 1.02f, 1f), false); break;
                // cidades nas cavernas: só o brilho dos cristais
                case RegionId.Sombrafonte:
                    C(p, c(0.4f, 0.62f, 0.72f), 0.22f, 70f, 0f, c(0.11f, 0.17f, 0.21f), c(0.09f, 0.13f, 0.15f), c(0.03f, 0.04f, 0.05f), c(0.05f, 0.1f, 0.12f), 0.006f, c(0.01f, 0.02f, 0.03f), c(0.04f, 0.08f, 0.1f), c(0.5f, 1f, 1f, 0.5f), 60f, false, c(0.4f, 0.8f, 1f), c(0.05f, 0.08f, 0.1f));
                    K(p, 0f, 0f, 0f, c(0.05f, 0.07f, 0.08f), 0.3f, 0.9f, 0.45f, 1.3f, 0.5f, c(0.9f, 0.97f, 1.1f), c(1f, 1.02f, 1.04f)); break;
                // Fronteira Muda: o Vazio Mudo tira som, cor e profundidade; a Fenda domina o céu
                default:
                    C(p, c(0.7f, 0.68f, 0.78f), 0.55f, 15f, 200f, c(0.24f, 0.23f, 0.28f), c(0.22f, 0.21f, 0.23f), c(0.07f, 0.07f, 0.08f), c(0.3f, 0.29f, 0.33f), 0.0016f, c(0.05f, 0.04f, 0.09f), c(0.3f, 0.26f, 0.34f), c(0.75f, 0.75f, 0.78f, 0.5f), 50f, true, c(0.6f, 0.4f, 0.8f), c(0.25f, 0.22f, 0.28f));
                    K(p, 0.5f, 0.15f, 0.6f, c(0.12f, 0.11f, 0.14f), 0.3f, 0.35f, 0.45f, 1f, 0.2f, c(0.95f, 0.95f, 1.02f), c(1f, 1f, 1f)); break;
            }
            EditorUtility.SetDirty(p);
            return p;
        }

        public static RegionProfile DungeonProfile(RegionId id)
        {
            var src = Profile(id);
            string path = $"{DataDir}/Regions/{id}_Masmorra.asset";
            var p = AssetDatabase.LoadAssetAtPath<RegionProfile>(path);
            if (p == null) { p = ScriptableObject.CreateInstance<RegionProfile>(); AssetDatabase.CreateAsset(p, path); }
            EditorUtility.CopySerialized(src, p);
            p.name = id + "_Masmorra";
            p.sunIntensity = 0.15f;
            p.ambientSky = src.ambientSky * 0.45f; p.ambientEquator = src.ambientEquator * 0.4f; p.ambientGround = src.ambientGround * 0.4f;
            p.fogColor = src.fogColor * 0.25f; p.fogDensity = 0.03f;
            p.motesRate = 15f;
            p.gradeExposure = src.gradeExposure * 1.15f; p.purkinje = Mathf.Max(src.purkinje, 0.35f); p.gradeVignette = src.gradeVignette + 0.06f;
            EditorUtility.SetDirty(p);
            return p;
        }

        // ---------------------------------------------------------------- bestiário

        static void Catalog()
        {
            string path = "Assets/World/Resources/Elyndra/EnemyCatalog.asset";
            var cat = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(path);
            if (cat == null) { cat = ScriptableObject.CreateInstance<EnemyCatalog>(); AssetDatabase.CreateAsset(cat, path); }
            var eco = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Aren/Enemies/Eco.prefab");
            var deer = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Aren/Enemies/Deer.prefab");
            // corpos humanos provisórios: os aldeões corrompidos de Campânula (Quaternius CC0, VillagerSetup)
            var bodies = new List<GameObject>();
            foreach (var v in new[] { "M1", "F1", "M2", "F2" })
            {
                var b = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Aren/Resources/Enemies/Eco_" + v + ".prefab");
                if (b != null) bodies.Add(b);
            }
            if (bodies.Count == 0 && eco != null) bodies.Add(eco);
            int nb = 0;
            GameObject Body() => bodies.Count > 0 ? bodies[nb++ % bodies.Count] : null;
            // o Sussurrante novo (outra sessão está criando): usa assim que existir
            var suss = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Aren/Resources/Sussurrante/Sussurrante.prefab");
            var old = new Dictionary<string, GameObject>();
            foreach (var e in cat.entries) if (e.prefab != null) old[e.id] = e.prefab;
            cat.entries.Clear();
            bool IsProvisional(GameObject g) => g == null || g == eco || g == deer || bodies.Contains(g);
            // H = humano (os únicos no mundo por enquanto): sem corpo próprio, ganha um corpo de aldeão provisório
            void H(string id, string name, string host, Distortion d, string reading, GameObject prefab = null, float hp = 1f, int tier = 1)
            {
                if (prefab == null && old.TryGetValue(id, out var kept) && !IsProvisional(kept)) prefab = kept;   // não apaga um prefab arrastado à mão
                bool provisional = prefab == null;
                if (provisional) prefab = Body();
                cat.entries.Add(new EnemyCatalog.Entry { id = id, displayName = name, host = host, human = true, distortion = d, reading = reading, prefab = prefab, provisionalBody = provisional, healthScale = hp, tier = tier });
            }
            // A = animal/planta do cânone: guardado para depois (não aparece enquanto EnemyCatalog.HumansOnly)
            void A(string id, string name, string host, Distortion d, string reading, GameObject prefab = null, float hp = 1f, int tier = 1)
            {
                if (prefab == null && old.TryGetValue(id, out var kept) && !IsProvisional(kept)) prefab = kept;
                cat.entries.Add(new EnemyCatalog.Entry { id = id, displayName = name, host = host, human = false, distortion = d, reading = reading, prefab = prefab, provisionalBody = false, healthScale = hp, tier = tier });
            }
            // --- humanos corrompidos (moradores, cantores, peregrinos, servos de Vharos)
            H("sussurrante", "Sussurrante", "morador", Distortion.Roubo, "voz fragmentada; ataques curtos, leitura pelo sussurro antes do golpe", suss);
            H("sussurrante_loop", "Sussurrante de Loop", "morador", Distortion.Loop, "repete o último ataque depois de um atraso", null, 1.3f, 2);
            H("sussurrante_oco", "Sussurrante Oco", "morador", Distortion.Ausencia, "campo sem som: telegraphs só visuais", null, 1.4f, 3);
            H("corista_suspenso", "Corista Suspenso", "cantor do coro", Distortion.Saturacao, "voa sem asas — a voz corrompida o sustenta", null, 1.2f, 2);
            H("passante_invertido", "Passante Invertido", "morador", Distortion.Inversao, "o corpo reage antes do passo; finta direção", null, 1.2f, 2);
            H("partido_em_dois", "Partido em Dois", "morador", Distortion.Fenda, "imagem atrasada faz ação paralela; vida compartilhada", null, 1.5f, 3);
            H("morador_sem_palavra", "Morador Sem Palavra", "morador", Distortion.Roubo, "ataca ao tentar lembrar nomes", null, 1.1f, 2);
            H("peregrino_estouro", "Peregrino de Estouro", "peregrino", Distortion.Estouro, "acumula carga a cada golpe recebido", null, 1.6f, 3);
            H("regente_desfeito", "Regente Desfeito", "regente de coro (servo)", Distortion.Saturacao, "sincroniza os outros corrompidos", null, 4f, 2);
            H("afinador_profano", "Afinador Profano", "servo voluntário", Distortion.Saturacao, "marca alvos com frequências", null, 1.3f, 3);
            H("cantor_corrente", "Cantor de Corrente", "servo voluntário", Distortion.Saturacao, "Laço corrompido prende e puxa", null, 1.4f, 3);
            H("portador_estouro", "Portador de Estouro", "servo voluntário", Distortion.Estouro, "carrega Ressonância e descarrega perto", null, 1.5f, 3);
            H("confessor_sem_eco", "Confessor Sem Eco", "servo", Distortion.Roubo, "rouba a técnica usada e devolve deformada", null, 1.5f, 3);
            H("voz_vharos", "Voz de Vharos", "corpo preparado (elite)", Distortion.Fenda, "o Maestro fala e age por segundos", null, 2.2f, 4);
            H("copista_fenda", "Copista da Fenda", "copista", Distortion.Fenda, "replica a postura do Aren e cria uma solução falsa", null, 1.4f, 3);
            // --- animais e plantas do cânone (Bíblia v2, §38–41): entram quando tiverem corpo
            A("lobo_desafinado", "Lobo Desafinado", "lobo", Distortion.Nenhuma, "caça em dupla; mordida, pressão e recuo");
            A("lobo_refrao", "Lobo de Refrão", "lobo", Distortion.Loop, "a investida deixa um Eco que repete a trajetória");
            A("lobo_contratempo", "Lobo de Contratempo", "lobo", Distortion.Inversao, "rosnado e patas soam antes do movimento");
            A("lobo_saturado", "Lobo Saturado", "lobo", Distortion.Saturacao, "instinto de caça domina; persegue mais longe");
            A("lobo_partido", "Lobo Partido", "lobo", Distortion.Fenda, "duas posições; só uma causa dano");
            A("lobo_sem_faro", "Lobo Sem Faro", "lobo", Distortion.Roubo, "some do indicador de ameaça");
            A("lobo_carga", "Lobo de Carga", "lobo", Distortion.Estouro, "a terceira mordida explode");
            A("cervo_contratempo", "Cervo de Contratempo", "cervo", Distortion.Inversao, "o som do casco chega antes da pata — leia o corpo, não o som", deer, 0.4f, 1);
            A("cervo_contratempo_alfa", "Cervo de Contratempo (alfa)", "cervo", Distortion.Inversao, "investida só esquiva; garra e pisão contra-atacáveis", deer, 1f, 2);
            A("cervo_erguido", "Cervo Erguido", "cervo", Distortion.Saturacao, "postura humanoide, galhada e alcance");
            A("cervo_oco", "Cervo Oco", "cervo", Distortion.Ausencia, "corredor silencioso; corta técnicas sustentadas");
            A("cervo_bifurcado", "Cervo Bifurcado", "cervo", Distortion.Fenda, "duas trajetórias até o último instante");
            A("corvo_repetidor", "Corvo Repetidor", "corvo", Distortion.Loop, "grava um som e o reproduz deformado");
            A("corvo_fenda", "Corvo de Fenda", "corvo", Distortion.Fenda, "divide a rota em duas; uma é falsa");
            A("coruja_velada", "Coruja Velada", "coruja", Distortion.Roubo, "apaga telegraphs luminosos; ataca da sombra");
            A("coruja_ausencia", "Coruja de Ausência", "coruja", Distortion.Ausencia, "bolsões sem profundidade sonora");
            A("abutre_estouro", "Abutre de Estouro", "abutre", Distortion.Estouro, "acumula carga circulando e mergulha");
            A("abutre_saturado", "Abutre Saturado", "abutre", Distortion.Saturacao, "consome Ecos liberados para evoluir");
            A("javali_impacto", "Javali de Impacto", "javali", Distortion.Estouro, "carrega vibração nas presas e descarrega ao colidir");
            A("raiz_cantante", "Raiz Cantante", "planta", Distortion.Saturacao, "cresce em direção a sons e fecha rotas");
            // --- minibosses e chefe final: arenas prontas, corpos ainda não implementados
            foreach (var mb in new[] { ("bibliotecario", "Bibliotecário Sem Nome"), ("cavaleiro_passo", "Cavaleiro do Passo Repetido"), ("idolo_vidro", "Ídolo de Vidro"), ("colecionador", "Colecionador de Promessas"), ("monge", "Monge que Não Termina"), ("colosso", "Colosso de Pedra Oca"), ("mestre_muralha", "Mestre de Muralha emudecido"), ("ferreiro", "Ferreiro de Estouro"), ("portador_braseiro", "Portador do Braseiro"), ("naufrago", "Náufrago Prismático"), ("coro_carne", "Coro da Carne Perfeita"), ("guardiao_calice", "Guardião do Cálice"), ("oraculo", "Oráculo Repetido"), ("voz_vharos_elite", "Voz de Vharos (elite)"), ("lavadeira_loop", "Lavadeira de Loop"), ("mestra_caravana", "Mestra de Caravana"), ("curadora_coro", "Primeira Curadora do Coro"), ("vharos", "Elyan Vharos, Regente do Contracanto") })
                cat.entries.Add(new EnemyCatalog.Entry { id = mb.Item1, displayName = mb.Item2, host = "miniboss/chefe (humano)", human = true, distortion = Distortion.Nenhuma, reading = "ainda não implementado — arena pronta", prefab = old.TryGetValue(mb.Item1, out var k) ? k : null, healthScale = 1f, tier = 3 });
            cat.entries.Add(new EnemyCatalog.Entry { id = "cervo_raiz", displayName = "Cervo-Raiz", host = "cervo (animal — entra depois)", human = false, distortion = Distortion.Saturacao, reading = "ainda não implementado — arena pronta", healthScale = 1f, tier = 3 });
            EditorUtility.SetDirty(cat);
        }

        static void RuntimeMaterials()
        {
            // usados em runtime por Shader.Find/Resources (portão de Campânula): ficam em Resources para irem para o build
            void Save(string name, Material m) { string p = "Assets/World/Resources/Elyndra/" + name + ".mat"; var old = AssetDatabase.LoadAssetAtPath<Material>(p); if (old != null) { old.CopyPropertiesFromMaterial(m); old.shader = m.shader; EditorUtility.SetDirty(old); } else AssetDatabase.CreateAsset(new Material(m), p); }
            Save("GateVeil", WorldMats.Crystal(new Color(0.25f, 0.08f, 0.35f, 0.35f), new Color(1.1f, 0.4f, 1.6f), new Color(0.4f, 0.1f, 0.6f), 0.55f));
            Save("GateStone", WorldMats.Stone("camp:ashlar", new Color(0.78f, 0.74f, 0.7f), 2.2f));
            Save("GateGlow", WorldMats.Glow(new Color(1.9f, 1.1f, 0.5f), 0.15f, 1.2f));
        }

        [MenuItem("Elyndra/Relatório de placeholders (cena aberta)")]
        public static string PlaceholderReport()
        {
            var sb = new System.Text.StringBuilder();
            var groups = new Dictionary<string, int>();
            foreach (var t in Object.FindObjectsByType<PlaceholderTag>(FindObjectsSortMode.None))
            {
                groups.TryGetValue(t.category, out int n); groups[t.category] = n + 1;
                sb.Append($"[{t.category}] {t.name}: {t.replaceWith}\n");
            }
            var head = string.Join(", ", groups.Select(kv => kv.Key + " " + kv.Value));
            Debug.Log("PLACEHOLDERS: " + head + "\n" + sb);
            return head;
        }
    }
}
