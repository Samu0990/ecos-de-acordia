using Climbing;
using UnityEngine;

namespace Aren.Combat
{
    /// <summary>
    /// Troca a flauta entre a bainha nas costas (FluteHolster) e a mão direita
    /// (FluteSocket). Em combate vai para a mão; parkour ou alguns segundos sem lutar
    /// guardam de volta (mãos livres para escalar). A troca é uma interpolação curta no
    /// espaço do mundo, não um "pop".
    /// </summary>
    [DefaultExecutionOrder(210)]
    public class ArenFlute : MonoBehaviour
    {
        [Tooltip("Pose da flauta no osso FluteSocket (ajustada vendo os golpes de espada da UAL2).")]
        public Vector3 handLocalPosition = new Vector3(0f, 0f, 0f);
        public Vector3 handLocalEuler = new Vector3(0f, 0f, 0f);
        public float blendTime = 0.1f;
        public float holsterAfter = 4f;

        public Transform Flute { get; private set; }
        public Transform Tip { get; private set; }
        public Transform Mouth { get; private set; }
        public Transform Center { get; private set; }
        public bool InHand { get; private set; }

        Transform holster, socket;
        Vector3 holsterLocalPos; Quaternion holsterLocalRot;
        float blendT = 1f;
        Vector3 fromPos; Quaternion fromRot;
        ThirdPersonController tpc;
        ClimbController climb;
        ArenCombat combat;

        void Start()
        {
            tpc = GetComponent<ThirdPersonController>();
            climb = GetComponent<ClimbController>();
            combat = GetComponent<ArenCombat>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Aren_Flute": Flute = t; break;
                    case "FluteHolster": holster = t; break;
                    case "FluteSocket": socket = t; break;
                    case "Flute_Tip": Tip = t; break;
                    case "Flute_Mouth": Mouth = t; break;
                    case "Flute_Center": Center = t; break;
                }
            }
            if (Flute == null || holster == null || socket == null)
            {
                Debug.LogWarning("[ArenFlute] flauta/ossos não encontrados no modelo");
                enabled = false;
                return;
            }
            if (Flute.parent != holster) Flute.SetParent(holster, true);
            holsterLocalPos = Flute.localPosition;
            holsterLocalRot = Flute.localRotation;
        }

        public void Draw()
        {
            if (!enabled || InHand) return;
            InHand = true;
            BeginBlend(socket);
            ArenAudio.Play(Sfx.Equip, transform.position + Vector3.up * 1.2f, 0.6f);
        }

        public void Holster()
        {
            if (!enabled || !InHand) return;
            InHand = false;
            BeginBlend(holster);
            ArenAudio.Play(Sfx.Unequip, transform.position + Vector3.up * 1.2f, 0.5f);
        }

        void BeginBlend(Transform newParent)
        {
            fromPos = Flute.position; fromRot = Flute.rotation;
            Flute.SetParent(newParent, false);
            blendT = 0f;
        }

        void LateUpdate()
        {
            if (Flute == null) return;
            if (InHand)
            {
                bool parkour = tpc.isVaulting || tpc.dummy
                    || (climb != null && climb.CurrentClimbState != ClimbController.ClimbState.None);
                if (parkour || (combat != null && combat.State == CombatState.Free && Time.time - combat.LastCombatTime > holsterAfter))
                    Holster();
            }

            Vector3 lp = InHand ? handLocalPosition : holsterLocalPos;
            Quaternion lr = InHand ? Quaternion.Euler(handLocalEuler) : holsterLocalRot;
            if (blendT < 1f)
            {
                blendT = Mathf.Min(1f, blendT + Time.deltaTime / Mathf.Max(0.01f, blendTime));
                var parent = Flute.parent;
                Vector3 tp = parent.TransformPoint(lp);
                Quaternion tr = parent.rotation * lr;
                float e = 1f - (1f - blendT) * (1f - blendT);
                Flute.position = Vector3.Lerp(fromPos, tp, e);
                Flute.rotation = Quaternion.Slerp(fromRot, tr, e);
            }
            else
            {
                Flute.localPosition = lp;
                Flute.localRotation = lr;
            }
        }

        /// <summary>Ponta da flauta no mundo (VFX nascem daqui).</summary>
        public Vector3 TipPosition => Tip != null ? Tip.position : transform.position + Vector3.up * 1.2f + transform.forward * 0.6f;
        public Vector3 MouthPosition => Mouth != null ? Mouth.position : transform.position + Vector3.up * 1.6f;
    }
}
