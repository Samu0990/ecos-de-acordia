using UnityEngine;

namespace Aren.Combat
{
    /// <summary>
    /// Corrige os clipes de combate herdados: durante os quatro ataques o Aren leva a
    /// flauta à boca e mantém as duas mãos sobre o instrumento. O movimento de pernas e
    /// o avanço freeflow continuam vindo do clipe, mas os braços deixam de brandir a
    /// flauta como espada.
    /// </summary>
    [DefaultExecutionOrder(205)]
    [RequireComponent(typeof(Animator))]
    public sealed class ArenFlutePerformancePose : MonoBehaviour
    {
        public float blendSpeed = 12f;
        /// <summary>Abertura: o Aren toca a flauta parado (1) e a abaixa devagar (→ 0). O diretor anima.</summary>
        public float cinematic;

        Animator anim;
        ArenCombat combat;
        ArenFlute flute;
        Transform head;
        float weight;

        public float Weight => weight;

        void Awake()
        {
            anim = GetComponent<Animator>();
            combat = GetComponent<ArenCombat>();
            flute = GetComponent<ArenFlute>();
            if (anim != null && anim.isHuman) head = anim.GetBoneTransform(HumanBodyBones.Head);
        }

        void Update()
        {
            bool playing = combat != null && combat.State == CombatState.Attack
                && flute != null && flute.InHand && head != null;
            if (cinematic > 0.001f && flute != null && flute.InHand && head != null) { weight = Mathf.Clamp01(cinematic); return; }
            weight = Mathf.MoveTowards(weight, playing ? 1f : 0f, blendSpeed * Time.deltaTime);
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || anim == null || head == null || flute == null || weight <= 0.001f) return;

            Vector3 up = transform.up;
            Vector3 right = transform.right;
            Vector3 forward = transform.forward;
            float breath = Mathf.Sin(Time.time * 14f) * 0.008f * weight;
            Vector3 mouth = head.position + forward * 0.115f - up * (0.105f - breath) + right * 0.018f;

            // Flauta transversal: a embocadura fica nos labios e o corpo segue para a
            // direita. As posicoes sao resolvidas pelo IK Humanoid, independentemente do rig.
            Vector3 leftHand = mouth + right * 0.19f - up * 0.055f + forward * 0.012f;
            Vector3 rightHand = mouth + right * 0.43f - up * 0.075f + forward * 0.018f;
            float handWeight = weight * 0.96f;
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, handWeight);
            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, handWeight);
            anim.SetIKPosition(AvatarIKGoal.LeftHand, leftHand);
            anim.SetIKPosition(AvatarIKGoal.RightHand, rightHand);

            anim.SetLookAtWeight(weight * 0.34f, 0.12f, 0.65f, 0.15f, 0.7f);
            anim.SetLookAtPosition(mouth + forward * 7f);

            Quaternion fluteRotation = Quaternion.LookRotation(forward, -right);
            flute.SetPerformancePose(mouth, fluteRotation, weight);
        }
    }
}
