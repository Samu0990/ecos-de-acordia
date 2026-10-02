using Climbing;
using UnityEngine;

namespace Aren
{
    /// <summary>
    /// Passos do Aren com amostras por tipo de chão (pedra da vila, grama, madeira da ponte e
    /// dos andaimes, terra da estrada). O passo sai quando o pé desce de volta ao chão na
    /// animação (altura do osso do pé), então acompanha qualquer clipe de locomoção.
    /// </summary>
    public class ArenFootsteps : MonoBehaviour
    {
        public float upHeight = 0.13f, downHeight = 0.09f;
        public float minSpeed = 0.5f;
        Animator anim;
        ThirdPersonController tpc;
        Rigidbody rb;
        Transform lFoot, rFoot;
        bool lUp, rUp;
        float lastStep;
        static Terrain terrain;
        static float[,,] alpha;
        static int alphaRes;
        int mask;

        void Start()
        {
            anim = GetComponent<Animator>();
            tpc = GetComponent<ThirdPersonController>();
            rb = GetComponent<Rigidbody>();
            if (anim == null || !anim.isHuman) { enabled = false; return; }
            lFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            rFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            mask = ~((1 << gameObject.layer) | (1 << 2) | (1 << 8));
            var jump = GetComponent<ArenJump>();
            if (jump != null) jump.OnLand += (tier, speed) => Land(tier);
        }

        void LateUpdate()
        {
            if (tpc == null || !tpc.isGrounded || tpc.isVaulting || tpc.dummy) { lUp = rUp = false; return; }
            Vector3 hv = rb != null ? new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z) : Vector3.zero;
            float speed = hv.magnitude;
            float y0 = transform.position.y;
            Check(lFoot, ref lUp, y0, speed);
            Check(rFoot, ref rUp, y0, speed);
        }

        void Check(Transform foot, ref bool up, float y0, float speed)
        {
            if (foot == null) return;
            float h = foot.position.y - y0;
            if (h > upHeight) up = true;
            else if (up && h < downHeight)
            {
                up = false;
                if (speed > minSpeed && Time.time - lastStep > 0.16f)
                {
                    lastStep = Time.time;
                    ArenAudio.Footstep(foot.position, SurfaceAt(transform.position), Mathf.Lerp(0.22f, 0.55f, Mathf.InverseLerp(1f, 7f, speed)));
                }
            }
        }

        void Land(int tier)
        {
            var surf = SurfaceAt(transform.position);
            float v = tier >= 2 ? 0.8f : 0.55f;
            ArenAudio.Footstep(transform.position, surf, v);
            if (tier >= 1) ArenAudio.Footstep(transform.position + transform.right * 0.2f, surf, v * 0.8f);
            if (tier >= 2) ArenAudio.Play(Sfx.BodyFall, transform.position, 0.35f);
        }

        Surface SurfaceAt(Vector3 p)
        {
            if (!Physics.Raycast(p + Vector3.up * 0.4f, Vector3.down, out var hit, 1.2f, mask, QueryTriggerInteraction.Ignore))
                return Surface.Stone;
            if (hit.collider is TerrainCollider tc) return TerrainSurface(tc.GetComponent<Terrain>(), hit.point);
            string n = hit.collider.name;
            var t = hit.collider.transform;
            if (t.parent != null) n += t.parent.name;
            if (n.Contains("Bridge") || n.Contains("Scaffold") || n.Contains("Plank") || n.Contains("Stall") || n.Contains("Cart")
                || n.Contains("Crate") || n.Contains("Wood") || n.Contains("Table") || n.Contains("Bench") || n.Contains("Barrel") || n.Contains("Floor"))
                return Surface.Wood;
            if (n.Contains("Hay") || n.Contains("Grass")) return Surface.Grass;
            return Surface.Stone;
        }

        /// <summary>Camadas do terreno de Campanula: grama, terra, calçamento, campo arado.</summary>
        static Surface TerrainSurface(Terrain t, Vector3 p)
        {
            if (t == null) return Surface.Grass;
            if (terrain != t || alpha == null)
            {
                terrain = t;
                var td = t.terrainData;
                alphaRes = td.alphamapResolution;
                alpha = td.GetAlphamaps(0, 0, alphaRes, alphaRes);
            }
            Vector3 local = p - t.transform.position;
            var size = t.terrainData.size;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / size.x * alphaRes), 0, alphaRes - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / size.z * alphaRes), 0, alphaRes - 1);
            int layers = alpha.GetLength(2);
            int best = 0; float bw = -1f;
            for (int i = 0; i < layers; i++) if (alpha[z, x, i] > bw) { bw = alpha[z, x, i]; best = i; }
            switch (best)
            {
                case 1: return Surface.Dirt;
                case 2: return Surface.Stone;
                case 3: return Surface.Dirt;
                default: return Surface.Grass;
            }
        }
    }
}
