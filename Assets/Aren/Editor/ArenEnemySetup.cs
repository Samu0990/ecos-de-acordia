using Aren.Enemies;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace Aren.EditorTools
{
    /// <summary>Gera o prefab do Eco Possuído (manequim UAL2 CC0 + material corrompido).</summary>
    public static class ArenEnemySetup
    {
        const string Dir = "Assets/Aren/Enemies";
        const string UAL2 = "Assets/Aren/ThirdParty/Quaternius_UAL2/UAL2_Standard.fbx";

        [MenuItem("Aren/Setup/Inimigos - Eco Possuído")]
        public static string SetupEco()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Aren", "Enemies");
            var ctrl = BuildEcoController();
            var mat = CorruptedMaterial("Eco_Corrupted", null, new Color(0.35f, 0.3f, 0.38f), 0.82f);

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(UAL2);
            var root = new GameObject("Eco");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            var anim = model.GetComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var ms = new Material[smr.sharedMaterials.Length];
                for (int i = 0; i < ms.Length; i++) ms[i] = mat;
                smr.sharedMaterials = ms;
                smr.updateWhenOffscreen = false;
                smr.quality = SkinQuality.Bone2;   // 2 ossos/vértice: mais barato, imperceptível aqui
            }

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.95f, 0); col.height = 1.9f; col.radius = 0.38f;
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.42f; agent.height = 1.9f; agent.speed = 3.4f; agent.acceleration = 14f; agent.angularSpeed = 0f;
            agent.stoppingDistance = 0.2f; agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            var eco = root.AddComponent<EnemyEco>();
            eco.displayName = "Eco Possuído";
            eco.headHeight = 1.95f;

            string path = Dir + "/Eco.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return "Eco.prefab ok\n";
        }

        public static Material CorruptedMaterial(string name, Texture albedo, Color tint, float darkness)
        {
            string p = Dir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            var sh = Shader.Find("Aren/Enemy/Corrupted");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
            m.shader = sh;
            m.SetTexture("_MainTex", albedo);
            m.SetColor("_Color", tint);
            m.SetFloat("_Darkness", darkness);
            m.SetTexture("_Cracks", AssetDatabase.LoadAssetAtPath<Texture>("Assets/Aren/Resources/VFX/noise_cracks.png"));
            m.SetTexture("_Noise", AssetDatabase.LoadAssetAtPath<Texture>("Assets/Aren/Resources/VFX/noise_perlin.png"));
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static AnimatorController BuildEcoController()
        {
            string p = Dir + "/Eco.controller";
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(p);
            if (ctrl != null) AssetDatabase.DeleteAsset(p);
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(p);
            ctrl.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "StateSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            ctrl.AddParameter(new AnimatorControllerParameter { name = "LocoSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            var sm = ctrl.layers[0].stateMachine;

            var bt = new BlendTree { name = "Locomotion", blendParameter = "MoveSpeed", blendType = BlendTreeType.Simple1D, useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(bt, ctrl);
            bt.AddChild(ArenAnimatorSetup.Clip("Zombie_Idle_Loop"), 0f);
            bt.AddChild(ArenAnimatorSetup.Clip("Zombie_Walk_Fwd_Loop"), 1.4f);
            var loco = sm.AddState("Locomotion");
            loco.motion = bt; loco.speedParameter = "LocoSpeed"; loco.speedParameterActive = true;
            sm.defaultState = loco;

            void S(string n, string clip)
            {
                var st = sm.AddState(n);
                st.motion = ArenAnimatorSetup.Clip(clip);
                st.speedParameter = "StateSpeed"; st.speedParameterActive = true;
            }
            S("Attack", "Zombie_Scratch");
            S("Hurt", "Idle_Shield_Break");
            S("Knock", "Hit_Knockback");
            S("GetUp", "LayToIdle");
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }
    }
}
