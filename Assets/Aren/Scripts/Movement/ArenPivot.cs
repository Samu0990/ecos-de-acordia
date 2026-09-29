using UnityEngine;
using Climbing;

namespace Aren
{
    /// <summary>
    /// Pivot/derrapada ao inverter a direção correndo.
    ///
    /// Antes: o DPS copiava a direção nova do input na velocidade imediatamente — invertendo
    /// o stick a 4.5 m/s a velocidade virava −4.5 m/s no mesmo passo de física enquanto o
    /// corpo ainda girava (~0.2 s "andando de costas"). Agora: micro-estado curto que freia
    /// na direção antiga, deixa o corpo virar e reacelera a partir de ~0 na direção nova.
    /// Sem clipe de pivot no material disponível → procedural (freio + inclinação do
    /// ArenLean + evento para poeira/VFX).
    /// </summary>
    [DefaultExecutionOrder(110)]
    public class ArenPivot : MonoBehaviour
    {
        [Tooltip("Ângulo mínimo entre velocidade atual e input para disparar (graus).")]
        public float triggerAngle = 130f;
        [Tooltip("Velocidade horizontal mínima para derrapar (m/s).")]
        public float minSpeed = 3.0f;
        [Tooltip("Duração da derrapada (s). Docs: curta para não prejudicar responsividade.")]
        public float skidTime = 0.14f;
        public float cooldown = 0.25f;

        public bool IsSkidding => skidLeft > 0f;
        public Vector3 SkidVelocity { get; private set; }
        public event System.Action<Vector3> OnSkidStart;   // direção antiga (para poeira/VFX)

        ThirdPersonController tpc;
        MovementCharacterController move;
        ClimbController climb;
        Rigidbody rb;
        float skidLeft, lastSkid = -10f, decel;
        Vector3 prevHv;

        void Awake()
        {
            tpc = GetComponent<ThirdPersonController>();
            move = GetComponent<MovementCharacterController>();
            climb = GetComponent<ClimbController>();
            rb = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            // Roda DEPOIS do MovementCharacterController (order 110), que já copiou a direção
            // nova do input para a velocidade neste passo; a direção "antiga" de verdade é a
            // velocidade que saiu do passo anterior.
            Step(prevHv);
            prevHv = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        }

        void Step(Vector3 oldHv)
        {
            if (skidLeft > 0f)
            {
                if (!Eligible(false)) { skidLeft = 0f; return; }
                skidLeft -= Time.fixedDeltaTime;
                SkidVelocity = Vector3.MoveTowards(SkidVelocity, Vector3.zero, decel * Time.fixedDeltaTime);
                SetHorizontal(SkidVelocity);
                return;
            }

            if (Time.time - lastSkid < cooldown || oldHv.magnitude < minSpeed || !Eligible(true)) return;

            Vector3 want = Intent();
            if (want == Vector3.zero || Vector3.Angle(oldHv, want) < triggerAngle) return;

            skidLeft = skidTime;
            lastSkid = Time.time;
            SkidVelocity = oldHv;
            decel = oldHv.magnitude / skidTime;
            SetHorizontal(SkidVelocity);
            OnSkidStart?.Invoke(oldHv.normalized);
        }

        void SetHorizontal(Vector3 v)
        {
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
            tpc.characterAnimation.SetAnimVelocity(v);
            // Mantém a velocidade suavizada do DPS em zero: quando a derrapada acabar,
            // ApplyInputMovement reacelera a partir do repouso na direção nova.
            move.ResetSpeed();
        }

        bool Eligible(bool needInput)
        {
            if (tpc.dummy || !tpc.allowMovement || tpc.isVaulting || tpc.isJumping || !tpc.isGrounded) return false;
            if (climb != null && climb.CurrentClimbState != ClimbController.ClimbState.None) return false;
            if (needInput && tpc.characterInput.movement.magnitude < 0.5f) return false;
            return true;
        }

        Vector3 Intent()
        {
            Vector2 m = tpc.characterInput.movement;
            if (m.magnitude < 0.5f || tpc.mainCamera == null) return Vector3.zero;
            Vector3 f = tpc.mainCamera.forward; f.y = 0; f.Normalize();
            Vector3 r = tpc.mainCamera.right; r.y = 0; r.Normalize();
            return (f * m.y + r * m.x).normalized;
        }
    }
}
