using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Aren.EditorTools
{
    /// <summary>
    /// Cria copias locais dos clipes usados pelo Aren e grava Animation Events nos frames
    /// medidos. Os FBX de terceiros permanecem intactos e inimigos que compartilham aqueles
    /// clipes nao recebem eventos destinados ao jogador.
    /// </summary>
    public static class ArenAnimationEventSetup
    {
        const string OutputDir = "Assets/Aren/Animations/EventClips";
        const string DpsAnimations = "Assets/Dynamic Parkour System/Model/Animations/";

        struct Cue
        {
            public string function;
            public float time;
            public Cue(string function, float time) { this.function = function; this.time = time; }
        }

        struct StateCue
        {
            public string state, source;
            public Cue[] cues;
            public StateCue(string state, string source, params Cue[] cues)
            { this.state = state; this.source = source; this.cues = cues; }
        }

        static readonly StateCue[] CombatCues =
        {
            // Contatos medidos nos proprios clipes. O evento de preparacao antecede o contato.
            new StateCue("Aren Atk1", "Sword_Regular_A", new Cue("AttackSwing", .162f), new Cue("AttackContact", .230f)),
            new StateCue("Aren Atk2", "Sword_Regular_B", new Cue("AttackSwing", .183f), new Cue("AttackContact", .250f)),
            new StateCue("Aren Atk3", "Sword_Regular_Combo", new Cue("AttackSwing", .651f), new Cue("AttackContact", .720f)),
            new StateCue("Aren Atk4", "Sword_Regular_C", new Cue("AttackSwing", .557f), new Cue("AttackContact", .630f)),
            new StateCue("Aren Cast Pulse", "Sword_Block", new Cue("PulseRelease", .220f)),
            new StateCue("Aren Cast Blade", "OverhandThrow", new Cue("BladePrepare", .215f), new Cue("BladeRelease", .377f)),
            new StateCue("Aren Release", "Sword_Heavy_Combo", new Cue("ContracantoRelease", .380f)),
        };

        [MenuItem("Aren/Setup/Animation Events - passos, golpes e habilidades")]
        public static string SetupAll()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ArenAnimatorSetup.ControllerPath);
            if (ctrl == null) return "Animator Controller nao encontrado";
            string result = Apply(ctrl);
            AssetDatabase.SaveAssets();
            return result;
        }

        public static string Apply(AnimatorController ctrl)
        {
            EnsureFolders();
            var root = ctrl.layers[0].stateMachine;
            int clips = 0, events = 0;

            // Os tempos de contato sao os pontos onde as curvas de IK LeftFootCurve e
            // RightFootCurve passam a apoiar cada pe no chao.
            var locomotion = new Dictionary<string, Cue[]>
            {
                { "Walk", new[] { new Cue("FootstepLeft", .313f), new Cue("FootstepRight", .909f) } },
                { "Jog Forward", new[] { new Cue("FootstepLeft", .261f), new Cue("FootstepRight", .682f) } },
            };
            var walkState = ArenAnimatorSetup.FindState(root, "Walk");
            var tree = walkState != null ? walkState.motion as BlendTree : null;
            if (tree != null)
            {
                var children = tree.children;
                for (int i = 0; i < children.Length; i++)
                {
                    foreach (var pair in locomotion)
                    {
                        if (children[i].motion == null ||
                            (children[i].motion.name != pair.Key && !children[i].motion.name.StartsWith(pair.Key + "__ArenEvents"))) continue;
                        var source = LoadDpsClip(pair.Key);
                        children[i].motion = Clone(source, "Move_" + pair.Key, pair.Value);
                        clips++; events += pair.Value.Length;
                    }
                }
                tree.children = children;
                EditorUtility.SetDirty(tree);
            }

            var run = ArenAnimatorSetup.FindState(root, "Run");
            var runCues = new[] { new Cue("FootstepLeft", .198f), new Cue("FootstepRight", .510f) };
            if (run != null)
            {
                run.motion = Clone(LoadDpsClip("Run"), "Move_Run", runCues);
                clips++; events += runCues.Length;
            }

            foreach (var d in CombatCues)
            {
                var state = ArenAnimatorSetup.FindState(root, d.state);
                if (state == null) continue;
                state.motion = Clone(ArenAnimatorSetup.Clip(d.source), d.state, d.cues);
                clips++; events += d.cues.Length;
            }

            EditorUtility.SetDirty(ctrl);
            return $"Animation Events: {events} marcadores em {clips} clipes locais\n";
        }

        static AnimationClip LoadDpsClip(string name)
        {
            string path = DpsAnimations + name + ".fbx";
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == name && !c.name.StartsWith("__preview__"));
        }

        static AnimationClip Clone(AnimationClip source, string id, Cue[] cues)
        {
            if (source == null) return null;
            string safe = new string(id.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
            string path = OutputDir + "/" + safe + ".anim";
            var clone = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clone == null)
            {
                clone = Object.Instantiate(source);
                AssetDatabase.CreateAsset(clone, path);
            }
            else EditorUtility.CopySerialized(source, clone);

            clone.name = source.name + "__ArenEvents";
            var animationEvents = cues.Select(c => new AnimationEvent
            {
                functionName = c.function,
                time = Mathf.Clamp(c.time, 0f, source.length),
            }).ToArray();
            AnimationUtility.SetAnimationEvents(clone, animationEvents);
            EditorUtility.SetDirty(clone);
            return clone;
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Aren/Animations"))
                AssetDatabase.CreateFolder("Assets/Aren", "Animations");
            if (!AssetDatabase.IsValidFolder(OutputDir))
                AssetDatabase.CreateFolder("Assets/Aren/Animations", "EventClips");
        }
    }
}
