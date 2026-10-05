using Aren.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace Aren.EditorTools
{
    /// <summary>
    /// Aldeões de Campanula (Quaternius Modular Outfits Fantasy + Universal Base Characters, CC0;
    /// montados por ArtSource/Villagers/scripts/villager_export.py). Gera, de forma idempotente:
    ///   - regras de importação (Humanoid com avatar próprio, sem materiais embutidos, normal maps);
    ///   - materiais Aren/Villager por variante (roupa A/B, pele, olhos, cabelo);
    ///   - Villager.controller (clipes CC0 da UAL2 + a corrida do DPS) — o código troca os estados;
    ///   - prefabs Resources/Villagers/Villager_*.prefab (vivos) e Resources/Enemies/Eco_*.prefab
    ///     (os mesmos aldeões já corrompidos, com o Eco.controller e o EnemyEco: os Ecos da demo são
    ///     as pessoas da vila).
    /// </summary>
    public class VillagerSetup : AssetPostprocessor
    {
        const string Dir = "Assets/Aren/Character/Villagers";
        const string Tex = Dir + "/Textures/";
        const string MatDir = Dir + "/Materials";
        static readonly string[] Variants = { "Villager_M1", "Villager_M2", "Villager_F1", "Villager_F2" };

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Dir + "/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            // os nomes dos materiais do FBX dizem qual peça é qual (roupa, pele, olhos, cabelo); o prefab
            // troca todos pelos materiais Aren/Villager
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.importAnimation = false;
            mi.importBlendShapes = false;
            mi.importCameras = false; mi.importLights = false;
            mi.skinWeights = ModelImporterSkinWeights.Standard;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Tex)) return;
            var ti = (TextureImporter)assetImporter;
            bool normal = assetPath.Contains("_Normal");
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal && !assetPath.Contains("_ORM");
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 1024;
            ti.textureCompression = TextureImporterCompression.Compressed;
        }

        [MenuItem("Aren/Setup/Aldeões - tudo")]
        public static string All()
        {
            var log = new System.Text.StringBuilder();
            foreach (var v in Variants) AssetDatabase.ImportAsset(Dir + "/" + v + ".fbx", ImportAssetOptions.ForceUpdate);
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(Dir, "Materials");
            foreach (var f in new[] { "Assets/Aren/Resources/Villagers", "Assets/Aren/Resources/Enemies" })
                if (!AssetDatabase.IsValidFolder(f)) AssetDatabase.CreateFolder("Assets/Aren/Resources", System.IO.Path.GetFileName(f));
            var ctrl = BuildController();
            var ecoCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Aren/Enemies/Eco.controller");
            for (int i = 0; i < Variants.Length; i++)
            {
                string v = Variants[i];
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "/" + v + ".fbx");
                if (fbx == null) { log.Append("FALTA " + v + "\n"); continue; }
                var avatar = System.Linq.Enumerable.FirstOrDefault(System.Linq.Enumerable.OfType<Avatar>(AssetDatabase.LoadAllAssetsAtPath(Dir + "/" + v + ".fbx")));
                log.Append(v + " avatar=" + (avatar != null && avatar.isValid && avatar.isHuman) + "\n");
                bool alt = i % 2 == 1;
                // vivo
                var live = Build(fbx, v, avatar, ctrl, alt, false);
                PrefabUtility.SaveAsPrefabAsset(live, "Assets/Aren/Resources/Villagers/" + v + ".prefab");
                Object.DestroyImmediate(live);
                // Eco (o mesmo aldeão, tomado)
                var eco = Build(fbx, v, avatar, ecoCtrl, alt, true);
                var root = new GameObject("Eco_" + v.Substring(9));
                eco.name = "Model";
                eco.transform.SetParent(root.transform, false);
                var col = root.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 0.9f, 0); col.height = 1.8f; col.radius = 0.36f;
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.42f; agent.height = 1.9f; agent.speed = 3.4f; agent.acceleration = 14f; agent.angularSpeed = 0f;
                agent.stoppingDistance = 0.2f; agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                var e = root.AddComponent<EnemyEco>();
                e.displayName = "Eco Possuído";
                e.headHeight = 1.9f;
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Aren/Resources/Enemies/Eco_" + v.Substring(9) + ".prefab");
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            return log.Append("ok").ToString();
        }

        static GameObject Build(GameObject fbx, string v, Avatar avatar, RuntimeAnimatorController ctrl, bool alt, bool corrupted)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            go.name = v;
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = corrupted ? AnimatorCullingMode.CullUpdateTransforms : AnimatorCullingMode.AlwaysAnimate;
            bool female = v.Contains("_F");
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var ms = smr.sharedMaterials;
                for (int k = 0; k < ms.Length; k++) ms[k] = Mat(ms[k] != null ? ms[k].name : "MI_Peasant", alt, female, corrupted);
                smr.sharedMaterials = ms;
                smr.quality = SkinQuality.Bone2;
                smr.updateWhenOffscreen = false;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return go;
        }

        /// <summary>Material da peça (pelo nome do slot do FBX) — vivo ou corrompido, variante A/B.</summary>
        static Material Mat(string slot, bool alt, bool female, bool corrupted)
        {
            slot = slot.Split(' ')[0];
            string albedo, normal;
            string key;
            switch (slot)
            {
                case "MI_Regular_Male": albedo = "T_Regular_Male_Dark_BaseColor"; normal = "T_Regular_Male_Normal"; key = "SkinArms"; break;
                case "MI_Superhero_Male": albedo = "T_Superhero_Male_Dark"; normal = "T_Superhero_Male_Normal"; key = "HeadM"; break;
                case "MI_Superhero_Female": albedo = alt ? "T_Superhero_Female_Dark" : "T_Superhero_Female_Light"; normal = "T_Superhero_Female_Normal"; key = alt ? "HeadF_Dark" : "HeadF_Light"; break;
                case "MI_Eyes": albedo = "T_Eye_Brown"; normal = null; key = "Eyes"; break;
                case "MI_Hair_1": albedo = "T_Hair_1_BaseColor"; normal = "T_Hair_1_Normal"; key = alt ? "Hair1_B" : "Hair1_A"; break;
                case "MI_Hair_2": albedo = "T_Hair_2_BaseColor"; normal = "T_Hair_2_Normal"; key = alt ? "Hair2_B" : "Hair2_A"; break;
                default: albedo = alt ? "T_Peasant_2_BaseColor" : "T_Peasant_BaseColor"; normal = "T_Peasant_Normal"; key = alt ? "PeasantB" : "PeasantA"; break;
            }
            string p = MatDir + "/V_" + key + (corrupted ? "_Corrupt" : "") + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            var sh = Shader.Find("Aren/Villager");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
            m.shader = sh;
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture>(Tex + albedo + ".png"));
            m.SetTexture("_BumpMap", normal != null ? AssetDatabase.LoadAssetAtPath<Texture>(Tex + normal + ".png") : null);
            m.SetTexture("_Cracks", AssetDatabase.LoadAssetAtPath<Texture>("Assets/Aren/Resources/VFX/noise_cracks.png"));
            m.SetTexture("_Noise", AssetDatabase.LoadAssetAtPath<Texture>("Assets/Aren/Resources/VFX/noise_perlin.png"));
            m.SetTexture("_Void", AssetDatabase.LoadAssetAtPath<Texture>("Assets/Aren/Resources/VFX/Space/space_purple_veins.png"));
            m.SetFloat("_Corrupt", corrupted ? 1f : 0f);
            m.SetFloat("_Darkness", corrupted ? 0.72f : 0.8f);
            // o cabelo da Quaternius vem neutro (cinza claro) para ser tingido
            Color tint = Color.white;
            if (key == "Hair1_A") tint = new Color(0.36f, 0.25f, 0.17f);
            else if (key == "Hair1_B") tint = new Color(0.16f, 0.13f, 0.11f);
            else if (key == "Hair2_A") tint = new Color(0.55f, 0.29f, 0.16f);
            else if (key == "Hair2_B") tint = new Color(0.27f, 0.18f, 0.12f);
            else if (key == "Eyes" && corrupted) tint = new Color(2f, 0.4f, 2.4f);
            m.SetColor("_Color", tint);
            bool hair = key.StartsWith("Hair");
            m.SetFloat("_AlphaClip", hair ? 1f : 0f);
            m.SetFloat("_Cull", hair ? 0f : 2f);
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            return m;
        }

        static AnimatorController BuildController()
        {
            string p = Dir + "/Villager.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(p) != null) AssetDatabase.DeleteAsset(p);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(p);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            var sm = ctrl.layers[0].stateMachine;
            AnimatorState S(string n, Motion clip)
            {
                var st = sm.AddState(n);
                st.motion = clip;
                st.speedParameter = "Speed"; st.speedParameterActive = true;
                st.writeDefaultValues = true;
                return st;
            }
            var idle = S("Idle_FoldArms", ArenAnimatorSetup.Clip("Idle_FoldArms_Loop"));
            sm.defaultState = idle;
            S("Idle_Lantern", ArenAnimatorSetup.Clip("Idle_Lantern_Loop"));
            S("Idle_No", ArenAnimatorSetup.Clip("Idle_No_Loop"));
            S("Call", ArenAnimatorSetup.Clip("Idle_Rail_Call"));
            S("Yes", ArenAnimatorSetup.Clip("Yes"));
            S("Stagger", ArenAnimatorSetup.Clip("Idle_Shield_Break"));
            S("Convulse", ArenAnimatorSetup.Clip("Zombie_Scratch"));
            S("ZombieIdle", ArenAnimatorSetup.Clip("Zombie_Idle_Loop"));
            S("ZombieWalk", ArenAnimatorSetup.Clip("Zombie_Walk_Fwd_Loop"));
            S("Fall", ArenAnimatorSetup.Clip("Hit_Knockback"));
            S("GetUp", ArenAnimatorSetup.Clip("LayToIdle"));
            // corrida (DPS — a mesma do jogador)
            var run = System.Linq.Enumerable.FirstOrDefault(System.Linq.Enumerable.Where(System.Linq.Enumerable.OfType<AnimationClip>(
                AssetDatabase.LoadAllAssetsAtPath("Assets/Dynamic Parkour System/Model/Animations/Run.fbx")), c => !c.name.StartsWith("__preview")));
            S("Run", run);
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }
    }
}
