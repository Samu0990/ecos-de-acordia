using UnityEngine;

namespace Aren.World.Night
{
    /// <summary>
    /// Olhar procedural do Aren na abertura: gira coluna, peito, pescoço e cabeça (por cima da
    /// animação de parado) para um alvo no mundo, com inclinação de "escutando". Os pesos somam
    /// 1, então a cabeça chega exatamente no alvo; o tronco acompanha um pouco. Some sozinho
    /// (Weight → 0) quando a cinemática termina.
    /// </summary>
    public class CinematicLook : MonoBehaviour
    {
        public Vector3 target;          // ponto no mundo
        public bool hasTarget;
        public float weight;            // 0..1 (alvo); o real é suavizado
        public float listenTilt;        // graus de inclinação da cabeça (escutando)
        public float speed = 2.2f;
        Animator anim;
        Transform spine, chest, neck, head;
        float w, yaw, pitch, tilt, yawVel, pitchVel;

        void Awake()
        {
            anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                spine = anim.GetBoneTransform(HumanBodyBones.Spine);
                chest = anim.GetBoneTransform(HumanBodyBones.UpperChest) ?? anim.GetBoneTransform(HumanBodyBones.Chest);
                neck = anim.GetBoneTransform(HumanBodyBones.Neck);
                head = anim.GetBoneTransform(HumanBodyBones.Head);
            }
        }

        public void LookAt(Vector3 p, float wgt = 1f) { target = p; hasTarget = true; weight = wgt; }
        public void Release() { weight = 0f; listenTilt = 0f; }

        void LateUpdate()
        {
            if (head == null) return;
            float dt = Time.deltaTime;
            w = Mathf.MoveTowards(w, weight, dt * 1.5f);
            if (w <= 0.001f && Mathf.Abs(yaw) < 0.1f) return;
            Transform root = anim.transform;
            // ângulos desejados relativos ao corpo
            float ty = 0f, tp = 0f;
            if (hasTarget)
            {
                Vector3 d = target - head.position;
                Vector3 local = root.InverseTransformDirection(d.normalized);
                ty = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -115f, 115f);
                tp = Mathf.Clamp(Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -35f, 70f);
            }
            yaw = Mathf.SmoothDampAngle(yaw, ty, ref yawVel, 1f / speed);
            pitch = Mathf.SmoothDampAngle(pitch, tp, ref pitchVel, 1f / speed);
            tilt = Mathf.MoveTowards(tilt, listenTilt, dt * 25f);
            float Y = yaw * w, P = pitch * w;
            // distribui do quadril para a cabeça (cada osso gira sua parte, em espaço do mundo)
            Rotate(spine, Y * 0.2f, P * 0.1f, 0f, root);
            Rotate(chest, Y * 0.25f, P * 0.2f, 0f, root);
            Rotate(neck, Y * 0.25f, P * 0.3f, 0f, root);
            Rotate(head, Y * 0.3f, P * 0.4f, tilt * w, root);
        }

        static void Rotate(Transform b, float yawDeg, float pitchDeg, float rollDeg, Transform root)
        {
            if (b == null) return;
            var q = Quaternion.AngleAxis(yawDeg, root.up);
            Vector3 right = q * root.right;
            q = Quaternion.AngleAxis(-pitchDeg, right) * q;
            if (Mathf.Abs(rollDeg) > 0.01f) q = Quaternion.AngleAxis(rollDeg, q * root.forward) * q;
            b.rotation = q * b.rotation;
        }
    }
}
