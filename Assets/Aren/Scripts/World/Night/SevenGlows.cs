using System.Collections.Generic;
using UnityEngine;

namespace Aren.World.Night
{
    /// <summary>
    /// Os sete brilhos que saem da Fenda (o jogo não explica o que são). Voam no "domo" do céu a
    /// ~240 m do ponto de vista do Aren (dentro do far clip da câmera da abertura), cada um com
    /// cabeça luminosa, halo e rastro; somem atrás das serras do céu no lugar certo (mesma função
    /// Ridge do shader). Um deles — o dourado — passa sobre Campanula e cai MUITO longe, atrás da
    /// serra do leste, e dispara o clarão no céu.
    /// </summary>
    public class SevenGlows : MonoBehaviour
    {
        public class Glow
        {
            public Transform head; public Renderer headR, haloR; public TrailRenderer trail;
            public Color color; public float az0, el0, az1, el1, az2, el2, r0, r1, r2, dur, delay;
            public bool falls, gone; public float alpha = 1f;
            public Vector3 pos;
        }

        public readonly List<Glow> glows = new List<Glow>();
        public Glow Fallen { get; private set; }
        public System.Action onFallBehindRidge;
        Vector3 eye;
        float t0 = -1f;
        bool fallReported;
        MaterialPropertyBlock mpb;

        public static readonly Color[] Colors =
        {
            new Color(1f, 0.76f, 0.36f), new Color(1f, 0.32f, 0.3f), new Color(0.38f, 0.52f, 1f), new Color(0.4f, 0.95f, 1f),
            new Color(0.45f, 1f, 0.55f), new Color(0.78f, 0.46f, 1f), new Color(0.96f, 0.96f, 1f),
        };

        /// <summary>Solta os sete a partir da Fenda. 'eye' = de onde a cena é vista (o Aren).</summary>
        public static SevenGlows Launch(Vector3 eye)
        {
            var go = new GameObject("Os sete brilhos");
            var s = go.AddComponent<SevenGlows>();
            s.eye = eye; s.t0 = Time.time;
            s.mpb = new MaterialPropertyBlock();
            float a = NightSetup.FendaAz, e = NightSetup.FendaBaseEl;
            // destinos (azimute, seno da elevação) — cada um para um lado; o dourado (0) é o que cai
            s.Add(0, a, e, a + 0.55f, 0.93f, NightSetup.ImpactAz, -0.02f, 240f, 150f, 245f, 7.6f, 0.00f, true);
            s.Add(1, a, e, a - 0.55f, 0.24f, a - 1.25f, 0.015f, 240f, 238f, 245f, 4.4f, 0.10f, false);
            s.Add(2, a, e, a + 0.38f, 0.16f, a + 0.82f, 0.02f, 240f, 238f, 245f, 4.6f, 0.05f, false);
            s.Add(3, a, e, a - 0.2f, 0.42f, a - 0.55f, 0.75f, 240f, 238f, 236f, 4.0f, 0.18f, false);
            s.Add(4, a, e, a + 0.2f, 0.45f, a + 0.45f, 0.8f, 240f, 238f, 236f, 4.1f, 0.14f, false);
            s.Add(5, a, e, a - 0.9f, 0.18f, a - 1.9f, 0.06f, 240f, 240f, 245f, 4.8f, 0.22f, false);
            s.Add(6, a, e, a + 0.03f, 0.55f, a - 0.05f, 0.97f, 240f, 238f, 236f, 3.8f, 0.08f, false);
            return s;
        }

        void Add(int i, float az0, float el0, float az1, float el1, float az2, float el2, float r0, float r1, float r2, float dur, float delay, bool falls)
        {
            var g = new Glow { color = Colors[i], az0 = az0, el0 = el0, az1 = az1, el1 = el1, az2 = az2, el2 = el2, r0 = r0, r1 = r1, r2 = r2, dur = dur, delay = delay, falls = falls };
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var root = new GameObject("Brilho_" + i).transform;
            root.SetParent(transform, false);
            g.head = root;
            var head = new GameObject("Cabeca"); head.transform.SetParent(root, false); head.transform.localScale = new Vector3(5.5f, 5.5f, 1f);
            head.AddComponent<MeshFilter>().sharedMesh = quad;
            g.headR = head.AddComponent<MeshRenderer>(); g.headR.sharedMaterial = NightSetup.GlowMat;
            var halo = new GameObject("Halo"); halo.transform.SetParent(root, false); halo.transform.localScale = new Vector3(26f, 26f, 1f);
            halo.AddComponent<MeshFilter>().sharedMesh = quad;
            g.haloR = halo.AddComponent<MeshRenderer>(); g.haloR.sharedMaterial = NightSetup.GlowMat;
            foreach (var r in new[] { g.headR, g.haloR }) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; }
            g.trail = root.gameObject.AddComponent<TrailRenderer>();
            g.trail.time = falls ? 1.6f : 1.1f;
            g.trail.widthCurve = new AnimationCurve(new Keyframe(0, 2.6f), new Keyframe(1, 0f));
            g.trail.minVertexDistance = 1.5f;
            g.trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.trail.receiveShadows = false;
            var ts = Resources.Load<Shader>("Shaders/WorldTrail");
            if (ts != null) g.trail.sharedMaterial = new Material(ts);
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.Lerp(g.color, Color.white, 0.4f), 0), new GradientColorKey(g.color, 0.4f), new GradientColorKey(g.color, 1) },
                         new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.5f, 0.35f), new GradientAlphaKey(0f, 1) });
            g.trail.colorGradient = grad;
            g.trail.emitting = false;
            if (falls) Fallen = g;
            glows.Add(g);
            Place(g, 0f);
        }

        static Vector3 Sph(float az, float sinEl, float r) => NightSetup.Dir(az, sinEl) * r;

        /// <summary>Posição no domo: duas etapas (saída → ponto alto → destino), com aceleração de projétil.</summary>
        void Place(Glow g, float u)
        {
            float k = Mathf.Clamp01(u);
            float a, e, r;
            if (k < 0.5f)
            {
                float s = k / 0.5f; s = 1f - (1f - s) * (1f - s);         // sai rápido da Fenda e desacelera
                a = Mathf.Lerp(g.az0, g.az1, s); e = Mathf.Lerp(g.el0, g.el1, s); r = Mathf.Lerp(g.r0, g.r1, s);
            }
            else
            {
                float s = (k - 0.5f) / 0.5f; s = s * s;                   // depois cai acelerando
                a = Mathf.Lerp(g.az1, g.az2, s); e = Mathf.Lerp(g.el1, g.el2, s); r = Mathf.Lerp(g.r1, g.r2, s);
            }
            g.pos = eye + Sph(a, e, r);
            g.head.position = g.pos;
        }

        void Update()
        {
            if (t0 < 0f) return;
            float t = Time.time - t0;
            bool any = false;
            foreach (var g in glows)
            {
                if (g.gone) continue;
                any = true;
                float u = (t - g.delay) / g.dur;
                if (u < 0f) { SetAlpha(g, 0f); continue; }
                Place(g, u);
                g.trail.emitting = true;
                // nasce com um lampejo; os que não caem se perdem longe; todos somem atrás da serra
                float a = Mathf.Clamp01(u * 12f);
                if (!g.falls) a *= 1f - Mathf.Clamp01((u - 0.7f) / 0.3f);
                bool behind = NightSetup.BehindRidge(eye, g.pos, out float margin);
                a *= Mathf.Clamp01((margin + 0.004f) / 0.008f);
                if (g.falls && behind && !fallReported) { fallReported = true; onFallBehindRidge?.Invoke(); }
                SetAlpha(g, a);
                if (u >= 1f || (g.falls && margin < -0.02f)) { g.gone = true; g.trail.emitting = false; SetAlpha(g, 0f); }
            }
            if (!any && t > 12f) Destroy(gameObject, 2f);
        }

        void SetAlpha(Glow g, float a)
        {
            g.alpha = a;
            var c = g.color; c.a = a;
            mpb.SetColor("_Color", Color.Lerp(c, new Color(1, 1, 1, a), 0.35f));
            g.headR.SetPropertyBlock(mpb);
            c.a = a * 0.32f;
            mpb.SetColor("_Color", c);
            g.haloR.SetPropertyBlock(mpb);
        }
    }
}
