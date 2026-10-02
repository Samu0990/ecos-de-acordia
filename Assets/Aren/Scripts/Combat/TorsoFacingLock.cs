using UnityEngine;

namespace Aren.Combat
{
    /// <summary>
    /// Mantém o tronco de frente para o "frente" do personagem durante o combate.
    /// Os golpes de espada da UAL2 são encadeados com giros (A termina "enrolado" de costas,
    /// B desenrola; C é um giro de 360°): com o personagem virado para o alvo, isso mostrava
    /// as costas para o inimigo no meio do golpe. Aqui, depois da animação, o esqueleto inteiro
    /// gira em torno do eixo vertical do personagem para cortar o excesso de torção — torções
    /// naturais até <see cref="deadZone"/> graus passam intactas (o golpe continua com peso).
    /// Puramente visual: não mexe no transform, na física nem no Animator.
    /// </summary>
    [DefaultExecutionOrder(150)]   // depois da animação/IK, antes do ArenLean (200) e da flauta (210)
    public class TorsoFacingLock : MonoBehaviour
    {
        [Tooltip("Torção do tronco (graus) que passa sem correção.")]
        public float deadZone = 30f;
        [Tooltip("Fração do excesso que é corrigida (1 = nunca passa da zona morta).")]
        [Range(0f, 1f)] public float strength = 1f;
        [Tooltip("Velocidade de entrada/saída da trava (1/s).")]
        public float blendSpeed = 10f;

        /// <summary>Quando a trava vale (ex.: estados de ataque). Nulo = sempre.</summary>
        public System.Func<bool> active;

        Animator anim;
        Transform hips, lArm, rArm, lLeg, rLeg, head;
        float weight;

        public float LastYaw { get; private set; }

        void Start()
        {
            anim = GetComponentInChildren<Animator>();
            if (anim == null || !anim.isHuman) { enabled = false; return; }
            hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            lArm = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rArm = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            lLeg = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rLeg = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (hips == null || lArm == null || rArm == null || lLeg == null || rLeg == null) enabled = false;
        }

        /// <summary>Yaw do tronco (ombros 60% + quadril 40%) em relação ao "frente" do personagem.</summary>
        public float TorsoYaw()
        {
            Vector3 up = Vector3.up;
            Vector3 chest = Vector3.Cross(rArm.position - lArm.position, up);
            Vector3 pelvis = Vector3.Cross(rLeg.position - lLeg.position, up);
            Vector3 f = chest.normalized * 0.6f + pelvis.normalized * 0.4f;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.SignedAngle(transform.forward, f, up);
        }

        void LateUpdate()
        {
            if (anim == null || !anim.isActiveAndEnabled) return;
            bool on = active == null || active();
            // deitado (queda, levantar do chão): ombros não dizem para onde ele "olha"
            if (on && head != null && Vector3.Angle(head.position - hips.position, Vector3.up) > 50f) on = false;
            weight = Mathf.MoveTowards(weight, on ? 1f : 0f, blendSpeed * Time.deltaTime);
            if (weight <= 0.001f) return;

            float yaw = TorsoYaw();
            LastYaw = yaw;
            float excess = Mathf.Sign(yaw) * Mathf.Max(0f, Mathf.Abs(yaw) - deadZone);
            float corr = -excess * strength * weight;
            if (Mathf.Abs(corr) < 0.05f) return;
            Vector3 pivot = transform.position;
            pivot.y = hips.position.y;
            hips.RotateAround(pivot, Vector3.up, corr);
        }
    }
}
