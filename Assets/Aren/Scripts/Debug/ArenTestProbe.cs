using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Climbing;
// Disponível também no executável: o modo -eda-test (DemoAutoTest) roda as rotas de parkour
// com teclado/mouse virtuais e grava o resultado no disco.

namespace Aren.DebugTools
{
    /// <summary>
    /// Robô de teste em Play mode: executa uma linha do tempo de input (teclado/mouse)
    /// dentro do próprio loop do jogo e grava o estado do jogador a cada frame.
    /// Existe porque cada chamada da CLI tem centenas de ms de latência — teste de
    /// timing (pulo, buffer, combo) precisa rodar aqui dentro.
    ///
    /// Timeline: "0:W+LeftShift;0.8:W+LeftShift+Space;0.9:W+LeftShift;2.0:"
    /// (tempo em segundos : teclas separadas por '+'; "LMB"/"RMB" = botões do mouse;
    /// vazio = soltar tudo).
    /// </summary>
    public class ArenTestProbe : MonoBehaviour
    {
        struct Step { public float t; public List<Key> keys; public bool lmb, rmb; }

        readonly List<Step> steps = new List<Step>();
        readonly Dictionary<int, string> stateNames = new Dictionary<int, string>();
        public readonly StringBuilder log = new StringBuilder();
        public System.Func<string> extra;
        public float duration = 2f;
        public float sampleEvery = 0f;
        public bool Done { get; private set; }

        float t0, lastSample = -1f;
        int next;
        ThirdPersonController tpc;
        Animator anim;
        Rigidbody rb;

        /// <summary>
        /// Põe o jogador num estado limpo no chão em 'pos' (só para testes): sai de
        /// poste/vault/queda, religa o controller, zera velocidade e volta ao Idle.
        /// Sem isso um teleporte no meio de um estado do DPS (ex.: agachado no poste)
        /// deixa o personagem travado e contamina o teste seguinte.
        /// </summary>
        public static void ResetPlayer(GameObject player, Vector3 pos, float yaw = 0f)
        {
            var tpc = player.GetComponent<ThirdPersonController>();
            var rb = player.GetComponent<Rigidbody>();
            var anim = player.GetComponent<Animator>();
            var jp = player.GetComponent<JumpPredictionController>();
            if (jp != null) jp.curPoint = null;
            tpc.isVaulting = false;
            tpc.isJumping = false;
            tpc.onAir = false;
            tpc.EnableController();
            var rot = Quaternion.Euler(0, yaw, 0);
            rb.position = pos; rb.rotation = rot;
            player.transform.SetPositionAndRotation(pos, rot);
            rb.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            anim.SetBool("Land", false);
            anim.SetBool("onAir", false);
            anim.SetBool("Crouch", false);
            anim.SetBool("Hanging", false);
            anim.SetInteger("Climb State", 0);
            anim.Play("Idle", 0, 0f);
            var hp = player.GetComponent<Aren.Combat.ArenHealth>();
            if (hp != null && hp.Health < hp.maxHealth) hp.Revive();

            // câmera atrás do jogador: o input é relativo à câmera, então "W" precisa
            // significar "para frente do yaw pedido" em todo teste
            var fl = Object.FindAnyObjectByType<Cinemachine.CinemachineFreeLook>();
            if (fl != null)
            {
                fl.m_XAxis.Value = fl.m_BindingMode == Cinemachine.CinemachineTransposer.BindingMode.WorldSpace ? yaw : 0f;
                fl.m_YAxis.Value = 0.5f;
                fl.PreviousStateIsValid = false;
            }
        }

        public static ArenTestProbe Run(GameObject player, string timeline, float duration, IEnumerable<string> states = null)
        {
            var old = player.GetComponent<ArenTestProbe>();
            if (old) DestroyImmediate(old);
            var p = player.AddComponent<ArenTestProbe>();
            p.duration = duration;
            p.Parse(timeline);
            if (states != null) foreach (var s in states) p.stateNames[Animator.StringToHash(s)] = s;
            return p;
        }

        void Parse(string timeline)
        {
            foreach (var part in timeline.Split(';'))
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                var kv = part.Split(':');
                var st = new Step { t = float.Parse(kv[0], CultureInfo.InvariantCulture), keys = new List<Key>() };
                if (kv.Length > 1)
                    foreach (var k in kv[1].Split('+'))
                    {
                        var name = k.Trim();
                        if (name.Length == 0) continue;
                        if (name == "LMB") st.lmb = true;
                        else if (name == "RMB") st.rmb = true;
                        else st.keys.Add((Key)System.Enum.Parse(typeof(Key), name));
                    }
                steps.Add(st);
            }
            steps.Sort((a, b) => a.t.CompareTo(b.t));
        }

        void Start()
        {
            tpc = GetComponent<ThirdPersonController>();
            anim = GetComponent<Animator>();
            rb = GetComponent<Rigidbody>();
            t0 = Time.time;
            // o editor fora de foco desliga o teclado (Input System); o robô precisa
            // do input mesmo com a janela do Unity em segundo plano (só em memória).
            // Teclado/mouse VIRTUAIS: o que a pessoa digita em outro programa não entra no
            // teste, e o teste não depende do foco. Os reais ficam desligados até o fim.
            BeginIsolatedInput();
            log.Append("t | pos | vel | g j air v | state | extra\n");
        }

        void Update()
        {
            if (Done) return;
            float t = Time.time - t0;
            // no máximo um passo por frame: com o editor lento, apertar e soltar no mesmo
            // frame fazia o DPS nunca ver a tecla (performed e canceled juntos)
            if (next < steps.Count && steps[next].t <= t)
            {
                Apply(steps[next]);
                next++;
            }
            if (t - lastSample >= sampleEvery)
            {
                lastSample = t;
                Sample(t);
            }
            if (t >= duration)
            {
                Apply(new Step { keys = new List<Key>() });
                Done = true;
                EndIsolatedInput();
            }
        }

        static Keyboard vKeyboard; static Mouse vMouse;
        static readonly List<InputDevice> disabledReal = new List<InputDevice>();
        static InputSettings.BackgroundBehavior savedBg;
        static bool isolated;

        static void BeginIsolatedInput()
        {
            if (isolated) return;
            isolated = true;
            savedBg = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            disabledReal.Clear();
            foreach (var d in InputSystem.devices)
                if ((d is Keyboard || d is Mouse) && d.enabled && d.name != "ProbeKeyboard" && d.name != "ProbeMouse")
                { InputSystem.DisableDevice(d); disabledReal.Add(d); }
            if (vKeyboard == null || !vKeyboard.added) vKeyboard = InputSystem.AddDevice<Keyboard>("ProbeKeyboard");
            if (vMouse == null || !vMouse.added) vMouse = InputSystem.AddDevice<Mouse>("ProbeMouse");
        }

        public static void EndIsolatedInput()
        {
            if (!isolated) return;
            isolated = false;
            if (vKeyboard != null && vKeyboard.added) InputSystem.RemoveDevice(vKeyboard);
            if (vMouse != null && vMouse.added) InputSystem.RemoveDevice(vMouse);
            vKeyboard = null; vMouse = null;
            foreach (var d in disabledReal) if (d.added) InputSystem.EnableDevice(d);
            disabledReal.Clear();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;
#endif
        }

        void OnDestroy() => EndIsolatedInput();

        void Apply(Step s)
        {
            if (vKeyboard != null && vKeyboard.added)
                InputSystem.QueueStateEvent(vKeyboard, new KeyboardState(s.keys.ToArray()));
            if (vMouse != null && vMouse.added)
            {
                var ms = new MouseState();
                if (s.lmb) ms = ms.WithButton(MouseButton.Left, true);
                if (s.rmb) ms = ms.WithButton(MouseButton.Right, true);
                InputSystem.QueueStateEvent(vMouse, ms);
            }
            log.Append("   >> input @" + (Time.time - t0).ToString("F2", CultureInfo.InvariantCulture) + ": " + string.Join("+", s.keys) + (s.lmb ? "+LMB" : "") + (s.rmb ? "+RMB" : "") + "\n");
        }

        void Sample(float t)
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            string name = stateNames.TryGetValue(st.shortNameHash, out var n) ? n : st.shortNameHash.ToString();
            if (anim.IsInTransition(0))
            {
                var nx = anim.GetNextAnimatorStateInfo(0);
                name += "->" + (stateNames.TryGetValue(nx.shortNameHash, out var n2) ? n2 : nx.shortNameHash.ToString());
            }
            var p = transform.position; var v = rb.linearVelocity;
            log.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} | {1:F2},{2:F2},{3:F2} | h{4:F2} y{5:F2} | {6}{7}{8}{9} | {10} {11:F2} | {12}\n",
                t, p.x, p.y, p.z, new Vector3(v.x, 0, v.z).magnitude, v.y,
                tpc.isGrounded ? "G" : "-", tpc.isJumping ? "J" : "-", tpc.onAir ? "A" : "-", tpc.isVaulting ? "V" : "-",
                name, st.normalizedTime, extra != null ? extra() : "");
        }
    }
}
