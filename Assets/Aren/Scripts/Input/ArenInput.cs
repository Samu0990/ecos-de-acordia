using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Climbing;

namespace Aren
{
    public enum ArenAction { None, Attack, Counter, Dodge, Ability, Ability2, Ability3 }

    public struct BufferedAction
    {
        public ArenAction action;
        public float time;          // Time.time (escalado: hitstop/câmera lenta congelam o buffer junto)
        public Vector3 direction;   // intenção em world-space no momento do press (zero = sem direção)
    }

    /// <summary>
    /// Fila de 1 ação futura (Freeflow §16) + histórico curto só para debug.
    /// Regra de substituição previsível (Freeflow §15): uma ação nova só substitui a que
    /// está na fila se tiver prioridade maior ou igual, ou se a da fila já passou da
    /// metade da janela. Prioridade: Counter > Dodge > Attack > Ability — defesa nunca é
    /// "engolida" por um ataque apertado logo depois.
    /// </summary>
    public class ActionBuffer
    {
        public float window = 0.2f;
        private BufferedAction queued;
        private bool hasQueued;
        public readonly List<BufferedAction> history = new List<BufferedAction>(8);

        static int Priority(ArenAction a)
        {
            switch (a)
            {
                case ArenAction.Counter: return 3;
                case ArenAction.Dodge: return 2;
                case ArenAction.Attack: return 1;
                default: return 0;
            }
        }

        public void Push(ArenAction action, Vector3 direction)
        {
            var entry = new BufferedAction { action = action, time = Time.time, direction = direction };
            if (history.Count == 8) history.RemoveAt(0);
            history.Add(entry);

            if (hasQueued && IsValid(queued) && Priority(action) < Priority(queued.action)
                && Time.time - queued.time < window * 0.5f)
                return;

            queued = entry;
            hasQueued = true;
        }

        bool IsValid(BufferedAction a) => Time.time - a.time <= window;

        public bool TryPeek(out BufferedAction action)
        {
            action = queued;
            if (hasQueued && !IsValid(queued)) hasQueued = false;
            return hasQueued;
        }

        public bool TryConsume(ArenAction action, out BufferedAction consumed)
        {
            consumed = default;
            if (!TryPeek(out var q) || q.action != action) return false;
            consumed = q;
            hasQueued = false;
            return true;
        }

        public void Clear() => hasQueued = false;
    }

    /// <summary>
    /// Ações de combate do Aren (não mexe no PlayerControls.cs gerado do DPS).
    /// Callbacks "performed" (e não WasPressedThisFrame) para não perder presses que
    /// acontecem fora do Update do frame — igual ao InputCharacterController do DPS.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ArenInput : MonoBehaviour
    {
        [Tooltip("Janela do buffer de ações (s). Faixa pedida nos docs: 0.10–0.18 para pulo; combate um pouco maior.")]
        public float bufferWindow = 0.2f;
        [Tooltip("Deadzone do vetor de intenção (teclado e stick).")]
        public float intentDeadzone = 0.2f;

        public readonly ActionBuffer buffer = new ActionBuffer();

        public bool AttackHeld => attack != null && attack.IsPressed();
        public float AttackHoldTime { get; private set; }
        public bool CounterHeld => counter != null && counter.IsPressed();

        /// <summary>Direção de intenção no mundo (relativa à câmera). Zero se sem input.</summary>
        public Vector3 IntentDirection { get; private set; }
        /// <summary>Último vetor de intenção não-nulo (teclado solta a tecla antes do golpe sair).</summary>
        public Vector3 LastIntentDirection { get; private set; }
        public float LastIntentTime { get; private set; } = -10f;

        public event System.Action<ArenAction> OnActionPressed;
        public event System.Action OnAttackReleased;

        private InputAction attack, counter, dodge, ability, ability2, ability3;
        private ThirdPersonController controller;

        void Awake()
        {
            controller = GetComponent<ThirdPersonController>();
            buffer.window = bufferWindow;

            attack = new InputAction("Attack", InputActionType.Button);
            attack.AddBinding("<Mouse>/leftButton");
            attack.AddBinding("<Gamepad>/buttonWest");

            counter = new InputAction("Counter", InputActionType.Button);
            counter.AddBinding("<Mouse>/rightButton");
            counter.AddBinding("<Gamepad>/buttonNorth");

            dodge = new InputAction("Dodge", InputActionType.Button);
            dodge.AddBinding("<Keyboard>/leftCtrl");
            dodge.AddBinding("<Gamepad>/rightShoulder");

            // Q Pulso de Ressonância · E Lâmina de Frequência · R Eco Fantasma
            // (Contracanto = segurar o ataque)
            ability = new InputAction("Ability", InputActionType.Button);
            ability.AddBinding("<Keyboard>/q");
            ability.AddBinding("<Gamepad>/leftShoulder");

            ability2 = new InputAction("Ability2", InputActionType.Button);
            ability2.AddBinding("<Keyboard>/e");
            ability2.AddBinding("<Gamepad>/leftTrigger");   // RT é a corrida do DPS

            ability3 = new InputAction("Ability3", InputActionType.Button);
            ability3.AddBinding("<Keyboard>/r");
            ability3.AddBinding("<Gamepad>/dpad/up");

            attack.performed += _ => Press(ArenAction.Attack);
            attack.canceled += _ => OnAttackReleased?.Invoke();
            counter.performed += _ => Press(ArenAction.Counter);
            dodge.performed += _ => Press(ArenAction.Dodge);
            ability.performed += _ => Press(ArenAction.Ability);
            ability2.performed += _ => Press(ArenAction.Ability2);
            ability3.performed += _ => Press(ArenAction.Ability3);
        }

        InputAction[] All => new[] { attack, counter, dodge, ability, ability2, ability3 };
        void OnEnable() { foreach (var a in All) a.Enable(); }
        void OnDisable() { foreach (var a in All) a.Disable(); }
        void OnDestroy() { foreach (var a in All) a.Dispose(); }

        /// <summary>Menus/pausa desligam o input de combate sem desligar o componente.</summary>
        public void SetCombatInputEnabled(bool on)
        {
            foreach (var a in All) { if (on) a.Enable(); else a.Disable(); }
            if (!on) { buffer.Clear(); AttackHoldTime = 0f; }
        }

        void Press(ArenAction a)
        {
            UpdateIntent();
            buffer.Push(a, IntentDirection.sqrMagnitude > 0 ? IntentDirection : Vector3.zero);
            OnActionPressed?.Invoke(a);
        }

        void Update()
        {
            buffer.window = bufferWindow;
            UpdateIntent();
            AttackHoldTime = AttackHeld ? AttackHoldTime + Time.deltaTime : 0f;
        }

        void UpdateIntent()
        {
            if (controller == null || controller.characterInput == null || controller.mainCamera == null)
            {
                IntentDirection = Vector3.zero;
                return;
            }
            Vector2 m = controller.characterInput.movement;
            if (m.magnitude < intentDeadzone)
            {
                IntentDirection = Vector3.zero;
                return;
            }
            Vector3 fwd = controller.mainCamera.forward; fwd.y = 0; fwd.Normalize();
            Vector3 right = controller.mainCamera.right; right.y = 0; right.Normalize();
            IntentDirection = (fwd * m.y + right * m.x).normalized;
            LastIntentDirection = IntentDirection;
            LastIntentTime = Time.time;
        }

        /// <summary>
        /// Intenção para resolver alvo: input atual; se o jogador acabou de soltar a
        /// tecla (teclado), usa o último vetor por uma janela curta.
        /// </summary>
        public Vector3 GetIntentForTargeting(float graceTime = 0.12f)
        {
            if (IntentDirection.sqrMagnitude > 0) return IntentDirection;
            if (Time.time - LastIntentTime <= graceTime) return LastIntentDirection;
            return Vector3.zero;
        }
    }
}
