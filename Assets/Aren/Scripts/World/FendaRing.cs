using UnityEngine;
using Aren.World.Night;

namespace Aren.World
{
    /// <summary>
    /// O círculo da Fenda: um anel de luz que se solta do céu rasgado, voa até a pessoa, se fecha em volta
    /// dela e a consome. Quem coreografa é o VillageCorruption (escreve <see cref="center"/>, <see cref="radius"/>,
    /// <see cref="normal"/> e <see cref="intensity"/> a cada quadro); aqui só o visual: dois anéis (o de luz e
    /// um de runas tracejadas, girando ao contrário), a luz que ele joga em volta, faíscas que correm pelo anel
    /// e um rastro enquanto voa. <see cref="Collapse"/> fecha o anel no peito com um clarão.
    /// </summary>
    public class FendaRing : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 0.6f;
        public Vector3 normal = Vector3.up;
        public float intensity = 1f;
        public float width = 0.11f;
        public bool faceCamera;          // no voo o círculo encara a câmera (lê como um círculo, não um traço)
        public float spin = 1f;

        Mesh ringMesh, runeMesh;
        Transform ringT, runeT;
        Light glow;
        ParticleSystem sparks;
        TrailRenderer trail;
        float angle;
        static Material ringMat, runeMat, trailMat;
        const int Seg = 72;

        public static FendaRing Create(Vector3 at, Transform parent)
        {
            var go = new GameObject("Círculo da Fenda");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<FendaRing>();
            r.center = at;
            r.Build();
            return r;
        }

        static Material Mat(ref Material m, Color core, Color halo, float dash, float spin, float coreW)
        {
            if (m != null) return m;
            m = new Material(Shader.Find("Aren/FX/FendaRing")) { hideFlags = HideFlags.DontSave };
            m.SetColor("_Color", core); m.SetColor("_Glow", halo);
            m.SetTexture("_Noise", Resources.Load<Texture2D>("VFX/noise_perlin"));
            m.SetFloat("_Dash", dash); m.SetFloat("_Spin", spin); m.SetFloat("_Core", coreW);
            return m;
        }

        void Build()
        {
            transform.position = center;
            ringT = Part("Anel de luz", Mat(ref ringMat, new Color(2.6f, 1.7f, 4.2f), new Color(1.1f, 0.35f, 2.3f), 0f, 0.6f, 0.16f), out ringMesh);
            runeT = Part("Anel de runas", Mat(ref runeMat, new Color(1.6f, 0.9f, 3.2f), new Color(0.5f, 0.2f, 1.2f), 1f, -0.9f, 0.3f), out runeMesh);
            var lg = new GameObject("Luz do círculo"); lg.transform.SetParent(transform, false);
            glow = lg.AddComponent<Light>(); glow.type = LightType.Point; glow.color = new Color(0.72f, 0.42f, 1f);
            glow.range = 6f; glow.intensity = 0f; glow.shadows = LightShadows.None;
            // faíscas que correm pelo anel (espaço do mundo: ficam para trás no voo e espiralam em volta)
            var sg = new GameObject("Faíscas"); sg.transform.SetParent(transform, false);
            sparks = sg.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.04f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 1f), new Color(0.65f, 0.35f, 1f));
            main.maxParticles = 400;
            var sh = sparks.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radiusThickness = 0f; sh.arcMode = ParticleSystemShapeMultiModeValue.Random;
            var em = sparks.emission; em.rateOverTime = 0f;
            var col = sparks.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.7f, 0.35f, 1f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var pr = sg.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = SevenGlows.SparkMat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
            sparks.Play();
            // rastro do voo
            trail = gameObject.AddComponent<TrailRenderer>();
            if (trailMat == null)
            {
                trailMat = new Material(Shader.Find("Aren/FX/Additive")) { hideFlags = HideFlags.DontSave };
                trailMat.mainTexture = Resources.Load<Texture2D>("VFX/fx_streak");
                trailMat.SetColor("_Color", new Color(1.4f, 0.6f, 2.6f, 1f));
            }
            trail.sharedMaterial = trailMat;
            trail.time = 0.55f; trail.minVertexDistance = 0.15f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(1f, 0f));
            trail.colorGradient = g;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
            Refresh();
        }

        Transform Part(string n, Material m, out Mesh mesh)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            mesh = new Mesh { name = n };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return go.transform;
        }

        static readonly Vector3[] vb = new Vector3[(Seg + 1) * 2];
        static readonly Vector2[] ub = new Vector2[(Seg + 1) * 2];
        static int[] tris;

        static void Annulus(Mesh m, float r, float w)
        {
            if (tris == null)
            {
                tris = new int[Seg * 6];
                for (int i = 0; i < Seg; i++) { int a = i * 2; tris[i * 6] = a; tris[i * 6 + 1] = a + 2; tris[i * 6 + 2] = a + 1; tris[i * 6 + 3] = a + 1; tris[i * 6 + 4] = a + 2; tris[i * 6 + 5] = a + 3; }
            }
            float r0 = Mathf.Max(0.005f, r - w * 0.5f), r1 = r + w * 0.5f;
            for (int i = 0; i <= Seg; i++)
            {
                float a = i * Mathf.PI * 2f / Seg, c = Mathf.Cos(a), s = Mathf.Sin(a);
                vb[i * 2] = new Vector3(c * r0, 0f, s * r0); vb[i * 2 + 1] = new Vector3(c * r1, 0f, s * r1);
                ub[i * 2] = new Vector2(i / (float)Seg, 0f); ub[i * 2 + 1] = new Vector2(i / (float)Seg, 1f);
            }
            m.vertices = vb; m.uv = ub;
            if (m.GetIndexCount(0) == 0) m.triangles = tris;
            m.bounds = new Bounds(Vector3.zero, new Vector3(r1 * 2f + 0.5f, 0.5f, r1 * 2f + 0.5f));
        }

        void LateUpdate() { Refresh(); }

        MaterialPropertyBlock mpb;

        void Refresh()
        {
            if (sparks == null) return;   // já parou (Stop) — o objeto some no fim do quadro
            transform.position = center;
            Vector3 nrm = normal;
            var cam = Camera.main;
            if (faceCamera && cam != null) nrm = cam.transform.position - center;
            if (nrm.sqrMagnitude < 1e-4f) nrm = Vector3.up;
            angle += Time.deltaTime * 90f * spin;
            var q = Quaternion.FromToRotation(Vector3.up, nrm.normalized);
            ringT.rotation = q * Quaternion.Euler(0f, angle, 0f);
            runeT.rotation = q * Quaternion.Euler(0f, -angle * 0.7f, 0f);
            float k = Mathf.Max(0f, intensity);
            Annulus(ringMesh, radius, width * Mathf.Lerp(0.6f, 1.2f, Mathf.Clamp01(k)));
            Annulus(runeMesh, radius * 1.22f + 0.04f, width * 1.6f);
            if (mpb == null) mpb = new MaterialPropertyBlock();
            mpb.SetFloat("_Intensity", k);
            ringT.GetComponent<MeshRenderer>().SetPropertyBlock(mpb);
            mpb.SetFloat("_Intensity", k * 0.8f);
            runeT.GetComponent<MeshRenderer>().SetPropertyBlock(mpb);
            glow.intensity = 1.9f * Mathf.Min(k, 1.3f);
            glow.range = 2.5f + radius * 3.5f;
            var sh = sparks.shape; sh.radius = radius; sh.rotation = new Vector3(90f, 0f, 0f);   // o círculo do Unity fica no plano XY
            var em = sparks.emission; em.rateOverTime = 140f * k * Mathf.Clamp01(radius * 1.5f + 0.2f);
            var vel = sparks.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            var zero = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.x = zero; vel.y = zero; vel.z = zero;
            vel.orbitalX = zero; vel.orbitalY = new ParticleSystem.MinMaxCurve(2.4f * spin, 3.6f * spin); vel.orbitalZ = zero;
            vel.orbitalOffsetX = zero; vel.orbitalOffsetY = zero; vel.orbitalOffsetZ = zero;
            vel.radial = new ParticleSystem.MinMaxCurve(-0.35f, -0.1f);
            vel.speedModifier = new ParticleSystem.MinMaxCurve(1f, 1f);
            sparks.transform.rotation = q;
            trail.emitting = faceCamera;
        }

        /// <summary>O anel se fecha no ponto (o peito) e estoura num clarão: a Fenda entrou.</summary>
        public System.Collections.IEnumerator Collapse(System.Func<Vector3> into, float dur)
        {
            float r0 = radius, k0 = intensity, t = 0f;
            Vector3 c0 = center;
            while (t < dur)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / dur), e = u * u * (3f - 2f * u);
                center = Vector3.Lerp(c0, into(), e);
                radius = Mathf.Lerp(r0, 0.02f, e * e);
                intensity = Mathf.Lerp(k0, 2.2f, e);
                spin = Mathf.Lerp(1f, 4f, e);
                yield return null;
            }
            var p = into();
            ArenVFX.Flash(p, new Color(0.75f, 0.4f, 1f), 3.5f, 5f, 0.45f);
            ArenVFX.Ring(p, 0.1f, 2.6f, 0.6f, new Color(0.8f, 0.45f, 1f, 1f), 0.09f, false);
            ArenVFX.Glyphs(p, new Color(0.8f, 0.45f, 1f), 8, 1.4f);
            Stop();
        }

        /// <summary>Some (as faíscas no ar terminam sozinhas).</summary>
        public void Stop()
        {
            if (sparks != null) { var em = sparks.emission; em.rateOverTime = 0f; sparks.transform.SetParent(null, true); Destroy(sparks.gameObject, 1.2f); sparks = null; }
            Destroy(gameObject);
        }
    }
}
