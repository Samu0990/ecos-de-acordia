using UnityEngine;
using Climbing;

namespace Aren
{
    /// <summary>
    /// Inclinação procedural do tronco (pendência B.2 + prompt §7 "pequeno lean do corpo"):
    /// - lateral: inclina para dentro da curva, proporcional à aceleração centrípeta (v·ω);
    /// - longitudinal: leve para frente acelerando, para trás freando/derrapando.
    /// Aplicado em LateUpdate por cima da pose animada (aditivo), distribuído em
    /// Spine/Chest/UpperChest. Só em locomoção no chão; some suavemente no resto.
    /// Mola crítica-amortecida → independente de FPS.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class ArenLean : MonoBehaviour
    {
        [Header("Lateral (curvas)")]
        public float lateralGain = 1.4f;            // graus por m/s²
        public float maxLateral = 12f;
        [Header("Longitudinal")]
        public float forwardGain = 0.9f;            // graus por m/s² (acelerando)
        public float maxForward = 7f;
        public float maxBack = 12f;
        [Header("Resposta")]
        [Tooltip("Tempo de resposta da mola (s).")]
        public float responseTime = 0.12f;
        [Range(0, 1)] public float weight = 1f;

        public Vector2 CurrentLean => new Vector2(roll, pitch);

        ThirdPersonController tpc;
        ArenPivot pivot;
        Animator anim;
        Rigidbody rb;
        Transform spine, chest, upperChest;
        float roll, rollVel, pitch, pitchVel, activeW, activeWVel;
        float lastYaw;
        Vector3 lastHv;
        float accLong;

        void Start()
        {
            tpc = GetComponent<ThirdPersonController>();
            pivot = GetComponent<ArenPivot>();
            anim = GetComponent<Animator>();
            rb = GetComponent<Rigidbody>();
            CacheBones();
            lastYaw = transform.eulerAngles.y;
        }

        public void CacheBones()
        {
            if (anim == null || !anim.isHuman) return;
            spine = anim.GetBoneTransform(HumanBodyBones.Spine);
            chest = anim.GetBoneTransform(HumanBodyBones.Chest);
            upperChest = anim.GetBoneTransform(HumanBodyBones.UpperChest);
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 hv = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            float speed = hv.magnitude;

            float yaw = transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(lastYaw, yaw) * Mathf.Deg2Rad / dt;
            lastYaw = yaw;

            // aceleração longitudinal suavizada (evita ruído do passo de física)
            float aLongRaw = Vector3.Dot(hv - lastHv, transform.forward) / dt;
            lastHv = hv;
            accLong = Mathf.Lerp(accLong, aLongRaw, 1f - Mathf.Exp(-dt / 0.06f));

            bool active = tpc.isGrounded && !tpc.isVaulting && !tpc.dummy && !tpc.isJumping;
            float targetRoll = 0f, targetPitch = 0f;
            if (active)
            {
                float aLat = speed * yawRate;   // positivo virando à direita
                targetRoll = Mathf.Clamp(-aLat * lateralGain, -maxLateral, maxLateral);
                targetPitch = Mathf.Clamp(accLong * forwardGain, -maxBack, maxForward);
                if (pivot != null && pivot.IsSkidding) targetPitch = -maxBack;
            }

            Spring(ref roll, ref rollVel, targetRoll, dt);
            Spring(ref pitch, ref pitchVel, targetPitch, dt);
            Spring(ref activeW, ref activeWVel, active ? 1f : 0f, dt);

            float w = weight * activeW;
            if (w < 0.001f || spine == null) return;

            // roll em torno do "para frente" do personagem, pitch em torno da "direita"
            Quaternion q = Quaternion.AngleAxis(roll * w, transform.forward) * Quaternion.AngleAxis(pitch * w, transform.right);
            Apply(spine, q, 0.35f);
            Apply(chest, q, 0.4f);
            Apply(upperChest != null ? upperChest : chest, q, upperChest != null ? 0.25f : 0.25f);
        }

        static void Apply(Transform bone, Quaternion full, float fraction)
        {
            if (bone == null) return;
            bone.rotation = Quaternion.Slerp(Quaternion.identity, full, fraction) * bone.rotation;
        }

        void Spring(ref float x, ref float v, float target, float dt)
        {
            float omega = 2f / Mathf.Max(responseTime, 0.01f);
            float f = 1f + 2f * dt * omega;
            float oo = omega * omega, hoo = dt * oo, hhoo = dt * hoo;
            float detInv = 1f / (f + hhoo);
            float detX = f * x + dt * v + hhoo * target;
            float detV = v + hoo * (target - x);
            x = detX * detInv;
            v = detV * detInv;
        }
    }
}
