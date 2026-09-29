using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Aren.EditorTools
{
    /// <summary>
    /// Adiciona ao Animator Controller do DPS os estados do remake do Aren, de forma
    /// idempotente (rodar de novo não duplica nada). Tudo que é alterado em estados
    /// existentes do DPS está listado no AREN_REMAKE_EXECUTION_LOG.md.
    /// </summary>
    public static class ArenAnimatorSetup
    {
        public const string ControllerPath = "Assets/Dynamic Parkour System/Model/Animator Controller.controller";
        public const string UAL2Path = "Assets/Aren/ThirdParty/Quaternius_UAL2/UAL2_Standard.fbx";

        [MenuItem("Aren/Setup/Animator - Movimento (pulo livre + pousos)")]
        public static string SetupMovement()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var root = ctrl.layers[0].stateMachine;
            var log = new System.Text.StringBuilder();

            AddParam(ctrl, "LandType", AnimatorControllerParameterType.Int, log);
            AddParam(ctrl, "AirVelY", AnimatorControllerParameterType.Float, log);

            var jumpSM = FindSM(root, "Jump");
            var walk = FindState(root, "Walk");
            var fallIdle = FindState(jumpSM, "Fall Idle");
            var fallLand = FindState(jumpSM, "Fall");                       // Falling To Landing (médio, parado)
            var landRun = FindState(jumpSM, "Fall A Land To Run Forward");  // médio, em movimento

            // --- Estado de pulo livre ---
            var jump = FindState(jumpSM, "Aren Jump");
            if (jump == null)
            {
                jump = jumpSM.AddState("Aren Jump", new Vector3(600, 300, 0));
                log.Append("state Aren Jump criado\n");
            }
            jump.motion = Clip("NinjaJump_Start");
            jump.speed = 1f;
            jump.writeDefaultValues = true;

            // --- Pouso pesado ---
            var heavy = FindState(jumpSM, "Aren Land Heavy");
            if (heavy == null)
            {
                heavy = jumpSM.AddState("Aren Land Heavy", new Vector3(600, 420, 0));
                log.Append("state Aren Land Heavy criado\n");
            }
            heavy.motion = Clip("NinjaJump_Land");
            heavy.speed = 1.35f;
            heavy.cycleOffset = 0.04f;
            heavy.tag = "HeavyLand";

            // --- Queda própria do pulo livre ---
            // Não reaproveita o "Fall Idle" do DPS porque ele tem tag "Root" (root motion
            // ligado): no ar isso briga com a física/controle aéreo. Mesmo clipe, sem tag.
            var fall = FindState(jumpSM, "Aren Fall");
            if (fall == null)
            {
                fall = jumpSM.AddState("Aren Fall", new Vector3(800, 300, 0));
                log.Append("state Aren Fall criado\n");
            }
            fall.motion = fallIdle.motion;
            fall.tag = "";

            // Aren Jump -> Aren Fall quando começa a descer. Interrompível pelo destino:
            // se tocar o chão no meio da transição, o pouso entra na hora (antes esperava
            // a transição de 0.3 s terminar -> pés "flutuando" ~0.12 s no chão).
            ReplaceTransitions(jump);
            var t = jump.AddTransition(fall);
            Configure(t, 0.25f, null);
            t.interruptionSource = TransitionInterruptionSource.Destination;
            t.AddCondition(AnimatorConditionMode.Less, -0.5f, "AirVelY");
            AddLandTransitions(jump, walk, fallLand, landRun, heavy);

            ReplaceTransitions(fall);
            AddLandTransitions(fall, walk, fallLand, landRun, heavy);

            // Fall Idle: os dois pousos originais do DPS passam a exigir LandType==1 (médio),
            // e ganham pouso leve (0) e pesado (2).
            foreach (var tr in fallIdle.transitions)
            {
                if ((tr.destinationState == fallLand || tr.destinationState == landRun)
                    && !tr.conditions.Any(c => c.parameter == "LandType"))
                {
                    tr.AddCondition(AnimatorConditionMode.Equals, 1, "LandType");
                    log.Append("Fall Idle -> " + tr.destinationState.name + ": +LandType==1\n");
                }
            }
            RemoveTransitionsTo(fallIdle, walk);
            RemoveTransitionsTo(fallIdle, heavy);
            var soft = fallIdle.AddTransition(walk);
            Configure(soft, 0.12f, null);
            soft.AddCondition(AnimatorConditionMode.If, 0, "Land");
            soft.AddCondition(AnimatorConditionMode.Equals, 0, "LandType");
            var hv = fallIdle.AddTransition(heavy);
            Configure(hv, 0.06f, null);
            hv.AddCondition(AnimatorConditionMode.If, 0, "Land");
            hv.AddCondition(AnimatorConditionMode.Equals, 2, "LandType");

            // Aren Land Heavy -> Walk no fim da recuperação; se o jogador voltar a andar
            // depois da trava (ArenJump.heavyRecoveryTime), sai mais cedo — senão os pés
            // deslizariam com o corpo se movendo dentro da pose de agachamento.
            ReplaceTransitions(heavy);
            var back = heavy.AddTransition(walk);
            Configure(back, 0.2f, 0.82f);
            var early = heavy.AddTransition(walk);
            Configure(early, 0.18f, 0.42f);
            early.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Velocity");

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return log.Length == 0 ? "sem mudanças estruturais (já configurado)" : log.ToString();
        }

        static void AddLandTransitions(AnimatorState from, AnimatorState walk, AnimatorState fallLand, AnimatorState landRun, AnimatorState heavy)
        {
            // Pouso leve correndo: direto para o Run. Via Walk, o Animator passava por
            // "Start Running" (clipe Idle To Sprint = arrancada do parado) no meio da corrida.
            var run = FindState(AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath).layers[0].stateMachine, "Run");
            if (run != null)
            {
                var sr = from.AddTransition(run); Configure(sr, 0.15f, null);
                sr.AddCondition(AnimatorConditionMode.If, 0, "Land");
                sr.AddCondition(AnimatorConditionMode.Equals, 0, "LandType");
                sr.AddCondition(AnimatorConditionMode.If, 0, "Run");
                sr.AddCondition(AnimatorConditionMode.Greater, 3f, "Velocity");
            }

            var s = from.AddTransition(walk); Configure(s, 0.15f, null);
            s.AddCondition(AnimatorConditionMode.If, 0, "Land");
            s.AddCondition(AnimatorConditionMode.Equals, 0, "LandType");

            var m1 = from.AddTransition(fallLand); Configure(m1, 0.1f, null);
            m1.AddCondition(AnimatorConditionMode.If, 0, "Land");
            m1.AddCondition(AnimatorConditionMode.Equals, 1, "LandType");
            m1.AddCondition(AnimatorConditionMode.Less, 1, "Velocity");

            var m2 = from.AddTransition(landRun); Configure(m2, 0.12f, null);
            m2.AddCondition(AnimatorConditionMode.If, 0, "Land");
            m2.AddCondition(AnimatorConditionMode.Equals, 1, "LandType");
            m2.AddCondition(AnimatorConditionMode.Greater, 1, "Velocity");

            var h = from.AddTransition(heavy); Configure(h, 0.06f, null);
            h.AddCondition(AnimatorConditionMode.If, 0, "Land");
            h.AddCondition(AnimatorConditionMode.Equals, 2, "LandType");
        }

        // ---------- helpers ----------
        public static AnimationClip Clip(string name, string path = UAL2Path)
        {
            var c = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(x => x.name == name);
            if (c == null) Debug.LogError("[ArenAnimatorSetup] clipe não encontrado: " + name);
            return c;
        }

        public static void AddParam(AnimatorController ctrl, string name, AnimatorControllerParameterType type, System.Text.StringBuilder log)
        {
            if (ctrl.parameters.Any(p => p.name == name)) return;
            ctrl.AddParameter(name, type);
            log?.Append("param " + name + " criado\n");
        }

        public static AnimatorStateMachine FindSM(AnimatorStateMachine sm, string name)
        {
            if (sm.name == name) return sm;
            foreach (var c in sm.stateMachines)
            {
                var r = FindSM(c.stateMachine, name);
                if (r != null) return r;
            }
            return null;
        }

        public static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (var s in sm.states) if (s.state.name == name) return s.state;
            foreach (var c in sm.stateMachines)
            {
                var r = FindState(c.stateMachine, name);
                if (r != null) return r;
            }
            return null;
        }

        public static void Configure(AnimatorStateTransition t, float duration, float? exitTime)
        {
            t.duration = duration;
            t.hasFixedDuration = true;
            t.hasExitTime = exitTime.HasValue;
            if (exitTime.HasValue) t.exitTime = exitTime.Value;
            t.interruptionSource = TransitionInterruptionSource.None;
        }

        public static void ReplaceTransitions(AnimatorState s)
        {
            foreach (var tr in s.transitions.ToArray()) s.RemoveTransition(tr);
        }

        public static void RemoveTransitionsTo(AnimatorState s, AnimatorState dst)
        {
            foreach (var tr in s.transitions.Where(x => x.destinationState == dst).ToArray()) s.RemoveTransition(tr);
        }
    }
}
