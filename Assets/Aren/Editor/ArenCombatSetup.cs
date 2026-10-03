using Aren.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Aren.EditorTools
{
    /// <summary>
    /// Monta o combate no projeto, de forma idempotente:
    /// estados de combate no Animator do DPS, AttackData dos golpes e componentes no
    /// prefab do Player. Tempos de contato dos clipes UAL2 medidos pela velocidade da
    /// mão direita (amostragem 60 Hz no AnimationMode) — ver AREN_REMAKE_EXECUTION_LOG.md.
    /// </summary>
    public static class ArenCombatSetup
    {
        const string AttackDir = "Assets/Aren/Combat";
        const string PlayerPrefab = "Assets/Dynamic Parkour System/Prefabs/Player.prefab";

        struct StateDef
        {
            public string name, clip; public bool loop;
            public StateDef(string n, string c, bool l = false) { name = n; clip = c; loop = l; }
        }

        static readonly StateDef[] States =
        {
            new StateDef("Aren Atk1", "Sword_Regular_A"),
            new StateDef("Aren Atk2", "Sword_Regular_B"),
            new StateDef("Aren Atk3", "Sword_Regular_Combo"),
            new StateDef("Aren Atk4", "Sword_Regular_C"),
            new StateDef("Aren Counter", "Melee_Hook"),
            new StateDef("Aren Dodge", "Shield_Dash"),
            new StateDef("Aren Parry", "Sword_Block"),
            new StateDef("Aren Hurt", "Idle_Shield_Break"),
            new StateDef("Aren Death", "Hit_Knockback"),
            new StateDef("Aren Revive", "LayToIdle"),
            new StateDef("Aren Cast Pulse", "Sword_Block"),
            new StateDef("Aren Cast Blade", "OverhandThrow"),
            new StateDef("Aren Cast Echo", "Shield_OneShot"),
            new StateDef("Aren Charge", "Idle_Shield_Loop", true),
            new StateDef("Aren Release", "Sword_Heavy_Combo"),
        };

        [MenuItem("Aren/Setup/Combate - tudo")]
        public static string SetupAll()
        {
            var log = new System.Text.StringBuilder();
            log.Append(SetupAnimator());
            log.Append(CreateAttacks());
            log.Append(SetupPlayer());
            return log.ToString();
        }

        [MenuItem("Aren/Setup/Combate - Animator")]
        public static string SetupAnimator()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ArenAnimatorSetup.ControllerPath);
            var root = ctrl.layers[0].stateMachine;
            var log = new System.Text.StringBuilder();
            ArenAnimatorSetup.AddParam(ctrl, "CombatSpeed", AnimatorControllerParameterType.Float, log);
            foreach (var p in ctrl.parameters)
                if (p.name == "CombatSpeed" && p.defaultFloat != 1f)
                {
                    var ps = ctrl.parameters;
                    for (int i = 0; i < ps.Length; i++) if (ps[i].name == "CombatSpeed") ps[i].defaultFloat = 1f;
                    ctrl.parameters = ps;
                }

            var sm = ArenAnimatorSetup.FindSM(root, "Combat");
            if (sm == null) { sm = root.AddStateMachine("Combat", new Vector3(500, -200, 0)); log.Append("SM Combat criado\n"); }

            int k = 0;
            foreach (var d in States)
            {
                var st = ArenAnimatorSetup.FindState(sm, d.name);
                if (st == null) { st = sm.AddState(d.name, new Vector3(300 + (k % 3) * 220, 100 + (k / 3) * 70, 0)); log.Append("estado " + d.name + "\n"); }
                st.motion = ArenAnimatorSetup.Clip(d.clip);
                st.speed = 1f;
                st.speedParameter = "CombatSpeed";
                st.speedParameterActive = true;
                st.writeDefaultValues = true;
                st.tag = "Combat";
                st.iKOnFeet = true;
                ArenAnimatorSetup.ReplaceTransitions(st);   // saídas são do código (CrossFade)
                if (st.motion == null) log.Append("ATENÇÃO: clipe não encontrado " + d.clip + "\n");
                k++;
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            log.Append(ArenAnimationEventSetup.Apply(ctrl));
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        static AttackData Atk(string file, System.Action<AttackData> set)
        {
            if (!AssetDatabase.IsValidFolder(AttackDir)) AssetDatabase.CreateFolder("Assets/Aren", "Combat");
            string path = AttackDir + "/" + file + ".asset";
            var a = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<AttackData>(); AssetDatabase.CreateAsset(a, path); }
            set(a);
            EditorUtility.SetDirty(a);
            return a;
        }

        /// <summary>animSpeed = (contato − offset) / startup: o contato do clipe cai no fim do startup.</summary>
        static float Speed(float contact, float offset, float startup) => (contact - offset) / startup;

        [MenuItem("Aren/Setup/Combate - AttackData")]
        public static string CreateAttacks()
        {
            var flute = new Color(0.55f, 0.95f, 1f);
            Atk("Attack_M1_1", a =>
            {
                a.stateName = "Aren Atk1"; a.startOffset = 0.04f; a.startup = 0.14f; a.animSpeed = Speed(0.23f, 0.04f, 0.14f);
                a.active = 0.06f; a.recovery = 0.26f; a.cancelFrom = 0.22f; a.comboKeep = 0.6f;
                a.damage = 9f; a.stagger = 12f; a.knockback = 2.5f; a.kind = HitKind.Light; a.reach = 1.7f; a.arcHalfAngle = 75f; a.hitstop = 0.045f;
                a.noteIndex = 0; a.slashRoll = 20f; a.slashRadius = 1.5f; a.slashColor = flute; a.shake = 0.12f;
            });
            Atk("Attack_M1_2", a =>
            {
                a.stateName = "Aren Atk2"; a.startOffset = 0.05f; a.startup = 0.15f; a.animSpeed = Speed(0.25f, 0.05f, 0.15f);
                a.active = 0.06f; a.recovery = 0.3f; a.cancelFrom = 0.24f; a.comboKeep = 0.6f;
                a.damage = 10f; a.stagger = 14f; a.knockback = 2.8f; a.kind = HitKind.Light; a.reach = 1.75f; a.arcHalfAngle = 75f; a.hitstop = 0.05f;
                a.noteIndex = 1; a.slashRoll = 195f; a.slashRadius = 1.55f; a.slashColor = flute; a.shake = 0.14f;
            });
            Atk("Attack_M1_3", a =>
            {
                a.stateName = "Aren Atk3"; a.startOffset = 0.5f; a.startup = 0.16f; a.animSpeed = Speed(0.72f, 0.5f, 0.16f);
                a.active = 0.06f; a.recovery = 0.3f; a.cancelFrom = 0.25f; a.comboKeep = 0.6f;
                a.damage = 11f; a.stagger = 16f; a.knockback = 3f; a.kind = HitKind.Light; a.reach = 1.8f; a.arcHalfAngle = 80f; a.hitstop = 0.055f;
                a.noteIndex = 2; a.slashRoll = 35f; a.slashRadius = 1.6f; a.slashColor = flute; a.shake = 0.16f;
            });
            Atk("Attack_M1_4", a =>
            {
                a.stateName = "Aren Atk4"; a.startOffset = 0.25f; a.startup = 0.26f; a.animSpeed = Speed(0.63f, 0.25f, 0.26f);
                a.active = 0.08f; a.recovery = 0.45f; a.cancelFrom = 0.42f; a.comboKeep = 0.4f;
                a.damage = 20f; a.stagger = 40f; a.knockback = 7f; a.kind = HitKind.Heavy; a.reach = 2.0f; a.arcHalfAngle = 180f; a.hitstop = 0.09f;
                a.noteIndex = 4; a.slashRoll = 0f; a.slashRadius = 2.1f; a.slashColor = new Color(0.75f, 1f, 1f); a.shake = 0.4f;
                a.magnetRange = 6.5f;
            });
            var counter = Atk("Attack_Counter", a =>
            {
                a.stateName = "Aren Counter"; a.startOffset = 0.03f; a.startup = 0.12f; a.animSpeed = Speed(0.23f, 0.03f, 0.12f);
                a.active = 0.06f; a.recovery = 0.3f; a.cancelFrom = 0.25f; a.comboKeep = 0.6f;
                a.damage = 16f; a.stagger = 999f; a.knockback = 6f; a.kind = HitKind.Heavy; a.reach = 1.9f; a.arcHalfAngle = 70f; a.hitstop = 0.1f;
                a.noteIndex = 5; a.slashRoll = 160f; a.slashRadius = 1.6f; a.slashColor = new Color(1f, 0.82f, 0.4f); a.shake = 0.35f;
            });
            AssetDatabase.SaveAssets();
            return "AttackData ok\n";
        }

        [MenuItem("Aren/Setup/Combate - Player")]
        public static string SetupPlayer()
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                // o prefab "Player" é um contêiner (câmeras + PlayerModel); o personagem é o
                // objeto com ThirdPersonController. Limpa o que tenha ido para o contêiner.
                foreach (var t in new System.Type[] { typeof(ArenAbilities), typeof(ArenHealth), typeof(ArenCombat), typeof(ArenFlute), typeof(ArenAnimationEvents), typeof(ArenFootsteps) })
                {
                    var wrong = prefabRoot.GetComponent(t);
                    if (wrong != null) Object.DestroyImmediate(wrong, true);
                }
                var root = prefabRoot.GetComponentInChildren<Climbing.ThirdPersonController>(true).gameObject;
                T Ensure<T>() where T : Component { var c = root.GetComponent<T>(); return c != null ? c : root.AddComponent<T>(); }
                Ensure<ArenFlute>();
                var combat = Ensure<ArenCombat>();
                Ensure<ArenHealth>();
                Ensure<ArenAbilities>();
                Ensure<ArenFootsteps>();
                Ensure<ArenAnimationEvents>();
                combat.combo = new[]
                {
                    AssetDatabase.LoadAssetAtPath<AttackData>(AttackDir + "/Attack_M1_1.asset"),
                    AssetDatabase.LoadAssetAtPath<AttackData>(AttackDir + "/Attack_M1_2.asset"),
                    AssetDatabase.LoadAssetAtPath<AttackData>(AttackDir + "/Attack_M1_3.asset"),
                    AssetDatabase.LoadAssetAtPath<AttackData>(AttackDir + "/Attack_M1_4.asset"),
                };
                combat.counterAttack = AssetDatabase.LoadAssetAtPath<AttackData>(AttackDir + "/Attack_Counter.asset");
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefabRoot); }
            return "Player ok\n";
        }
    }
}
