using System.Collections.Generic;
using UnityEngine;

namespace Aren.World
{
    /// <summary>
    /// O corpo do aldeão cedendo à Fenda: a coluna, o pescoço, os braços e as pernas se alongam, os dedos viram
    /// garras, os ombros caem e a cabeça pende — o primeiro passo para os 2,85 m do Sussurrante. Funciona em
    /// cima de qualquer animação (aplica depois do Animator, em LateUpdate): afasta cada osso filho do pai ao
    /// longo do próprio osso (sem escalar — escala não-uniforme torceria a malha). <see cref="amount"/> 0 = a
    /// pessoa, 1 = o corpo esticado; <see cref="hover"/> levanta o corpo do chão (a Fenda o ergue).
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class CorruptionMorph : MonoBehaviour
    {
        [Range(0, 1)] public float amount;
        public float hover;
        public float stretch = 1.32f;     // ossos longos (coluna, membros)
        public float claws = 1.7f;        // dedos
        public float hunch = 22f;         // graus: ombros e cabeça caem para a frente

        struct B { public Transform t; public Vector3 basePos; public float k; }
        readonly List<B> bones = new List<B>();
        Transform hips, chest, neck, head, lSh, rSh;
        float legLen;

        void Awake() { Init(); }

        bool inited;

        /// <summary>Acha os ossos (Awake; o editor chama direto para as prévias sem Play).</summary>
        public void Init()
        {
            if (inited) return;
            inited = true;
            var a = GetComponentInChildren<Animator>();
            if (a == null || !a.isHuman) { enabled = false; return; }
            void Add(HumanBodyBones hb, float k) { var t = a.GetBoneTransform(hb); if (t != null) bones.Add(new B { t = t, basePos = t.localPosition, k = k }); }
            // tronco (a coluna cresce mais em cima), pescoço e cabeça
            Add(HumanBodyBones.Chest, 0.7f); Add(HumanBodyBones.UpperChest, 0.8f); Add(HumanBodyBones.Neck, 1.0f); Add(HumanBodyBones.Head, 1.15f);
            // braços longos até abaixo dos joelhos
            foreach (var hb in new[] { HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm }) Add(hb, 1.25f);
            foreach (var hb in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand }) Add(hb, 1.35f);
            // pernas
            foreach (var hb in new[] { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot }) Add(hb, 1f);
            // garras: falanges do meio e das pontas
            for (int i = (int)HumanBodyBones.LeftThumbIntermediate; i <= (int)HumanBodyBones.RightLittleDistal; i++)
            {
                var name = ((HumanBodyBones)i).ToString();
                if (name.EndsWith("Intermediate") || name.EndsWith("Distal")) Add((HumanBodyBones)i, -1f);
            }
            hips = a.GetBoneTransform(HumanBodyBones.Hips);
            chest = a.GetBoneTransform(HumanBodyBones.UpperChest) ?? a.GetBoneTransform(HumanBodyBones.Chest);
            neck = a.GetBoneTransform(HumanBodyBones.Neck); head = a.GetBoneTransform(HumanBodyBones.Head);
            lSh = a.GetBoneTransform(HumanBodyBones.LeftUpperArm); rSh = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var ll = a.GetBoneTransform(HumanBodyBones.LeftLowerLeg); var lf = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (ll != null && lf != null) legLen = ll.localPosition.magnitude + lf.localPosition.magnitude;
        }

        void LateUpdate() { Pose(); }

        /// <summary>Aplica a deformação por cima da pose atual (depois do Animator).</summary>
        public void Pose()
        {
            float a = Mathf.Clamp01(amount);
            float s = Mathf.Lerp(1f, stretch, a), c = Mathf.Lerp(1f, claws, a);
            foreach (var b in bones)
            {
                if (b.t == null) continue;
                float f = b.k < 0f ? c : 1f + (s - 1f) * b.k;
                b.t.localPosition = b.basePos * f;
            }
            // as pernas mais longas empurram o corpo para cima (os pés continuam no chão) + a Fenda erguendo
            if (hips != null) hips.position += Vector3.up * (legLen * (s - 1f) + hover);
            if (a > 0.001f && lSh != null && rSh != null)
            {
                // frente do CORPO (pelos ombros) — a raiz do modelo nem sempre olha para +Z
                var right = rSh.position - lSh.position; right.y = 0f;
                if (right.sqrMagnitude < 1e-6f) return;
                right.Normalize();
                var fwd = Vector3.Cross(right, Vector3.up);
                if (chest != null) chest.rotation = Quaternion.AngleAxis(hunch * 0.5f * a, right) * chest.rotation;
                if (neck != null) neck.rotation = Quaternion.AngleAxis(hunch * 0.6f * a, right) * neck.rotation;
                if (head != null) head.rotation = Quaternion.AngleAxis(-hunch * 0.4f * a, right) * Quaternion.AngleAxis(9f * a * Mathf.Sin(Time.time * 1.7f), fwd) * head.rotation;
            }
        }

        void OnDisable()
        {
            foreach (var b in bones) if (b.t != null) b.t.localPosition = b.basePos;
        }
    }
}
