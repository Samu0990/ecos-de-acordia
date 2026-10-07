using Aren.Combat;
using UnityEngine;

namespace Aren.Enemies
{
    /// <summary>
    /// "Fragmento de Eco" (drop inicial da prancha do Sussurrante: material comum usado em
    /// melhorias iniciais). Um cristal violeta nasce do que sobrou do corpo, sobe girando e,
    /// quando o Aren chega perto (ou depois de um tempo), voa até ele. Conta em
    /// <see cref="Collected"/> (salvo em PlayerPrefs "eda_echo_fragments") e avisa no HUD.
    /// </summary>
    public class EchoFragment : MonoBehaviour
    {
        public static int Collected
        {
            get => PlayerPrefs.GetInt("eda_echo_fragments", 0);
            private set => PlayerPrefs.SetInt("eda_echo_fragments", value);
        }
        public static event System.Action<int> OnCollected;

        static Mesh crystal;
        static Material crystalMat;
        Transform gem;
        MeshRenderer halo;
        ParticleSystem sparkle;
        float born, seed;
        bool flying, done;
        Vector3 vel;
        MaterialPropertyBlock mpb;

        public static EchoFragment Spawn(Vector3 pos)
        {
            var go = new GameObject("Fragmento de Eco");
            go.transform.position = pos;
            var f = go.AddComponent<EchoFragment>();
            f.Build();
            return f;
        }

        void Build()
        {
            born = Time.time; seed = Random.value * 10f;
            mpb = new MaterialPropertyBlock();
            if (crystal == null) crystal = MakeCrystal();
            if (crystalMat == null)
            {
                var sh = Shader.Find("Aren/SussurranteBlade");
                crystalMat = new Material(sh != null ? sh : Shader.Find("Standard")) { name = "FragmentoDeEco" };
                crystalMat.SetTexture("_Noise", Resources.Load<Texture2D>("VFX/noise_perlin"));
                crystalMat.SetColor("_GlowColor", new Color(2.2f, 1.0f, 4.4f));
            }
            gem = new GameObject("cristal").transform;
            gem.SetParent(transform, false);
            gem.localScale = Vector3.one * 0.16f;
            gem.gameObject.AddComponent<MeshFilter>().sharedMesh = crystal;
            var mr = gem.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = crystalMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var hgo = new GameObject("halo");
            hgo.transform.SetParent(transform, false);
            hgo.transform.localScale = Vector3.one * 0.55f;
            hgo.AddComponent<MeshFilter>().sharedMesh = Quad();
            halo = hgo.AddComponent<MeshRenderer>();
            var gs = Shader.Find("Hidden/Aren/SussGlow");
            if (gs != null) halo.sharedMaterial = new Material(gs);
            halo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            halo.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", new Color(1.2f, 0.55f, 2.6f));
            mpb.SetFloat("_Falloff", 5f);
            mpb.SetFloat("_Flicker", 0.2f);
            halo.SetPropertyBlock(mpb);

            sparkle = new GameObject("brilhos").AddComponent<ParticleSystem>();
            sparkle.transform.SetParent(transform, false);
            sparkle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparkle.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.45f, 1f), new Color(1f, 0.85f, 1f));
            main.maxParticles = 40;
            var em = sparkle.emission; em.rateOverTime = 14f;
            var shp = sparkle.shape; shp.shapeType = ParticleSystemShapeType.Sphere; shp.radius = 0.18f;
            SussurranteFX.SetVelocity(sparkle, ParticleSystemSimulationSpace.World, Vector2.zero, new Vector2(0.15f, 0.45f), Vector2.zero, Vector2.zero);
            var r = sparkle.GetComponent<ParticleSystemRenderer>();
            var wp = Shader.Find("Hidden/Aren/WorldParticle");
            if (wp != null) r.sharedMaterial = new Material(wp);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sparkle.Play();
            ArenVFX.Sparks(transform.position, Vector3.up, new Color(0.7f, 0.4f, 1f), 10, 2.5f, 80f);
        }

        static Mesh quadMesh;
        static Mesh Quad()
        {
            if (quadMesh != null) return quadMesh;
            quadMesh = new Mesh { name = "quad" };
            quadMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            quadMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quadMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return quadMesh;
        }

        /// <summary>Cristal de 6 faces alongado (bipirâmide irregular); cores de vértice = "cristal"
        /// no shader da lâmina (R 0.6), brilho (G) mais forte nas pontas.</summary>
        static Mesh MakeCrystal()
        {
            var rnd = new System.Random(5);
            const int sides = 6;
            var v = new System.Collections.Generic.List<Vector3>();
            var c = new System.Collections.Generic.List<Color>();
            var t = new System.Collections.Generic.List<int>();
            Vector3 top = new Vector3(0.05f, 1.25f, -0.03f), bot = new Vector3(-0.04f, -0.85f, 0.02f);
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.3f;
                float r = 0.32f + (float)rnd.NextDouble() * 0.12f;
                ring[i] = new Vector3(Mathf.Cos(a) * r, (float)rnd.NextDouble() * 0.2f - 0.1f, Mathf.Sin(a) * r);
            }
            void Tri(Vector3 a, Vector3 b, Vector3 cc, float ga, float gb, float gc)
            {
                int k = v.Count;
                v.Add(a); v.Add(b); v.Add(cc);
                c.Add(new Color(0.6f, ga, 0.5f)); c.Add(new Color(0.6f, gb, 0.5f)); c.Add(new Color(0.6f, gc, 0.5f));
                t.Add(k); t.Add(k + 1); t.Add(k + 2);
            }
            for (int i = 0; i < sides; i++)
            {
                var a = ring[i]; var b = ring[(i + 1) % sides];
                Tri(a, top, b, 0.3f, 1f, 0.3f);
                Tri(b, bot, a, 0.3f, 0.9f, 0.3f);
            }
            var m = new Mesh { name = "FragmentoDeEco" };
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        void Update()
        {
            if (done) return;
            float age = Time.time - born;
            var p = CombatRegistry.Player;
            Vector3 target = p != null ? p.AimPoint : transform.position;
            if (!flying)
            {
                // sobe e flutua
                float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.9f));
                transform.position += Vector3.up * (Mathf.Sin(age * 2.2f + seed) * 0.12f + (1f - rise) * 0.9f) * Time.deltaTime;
                if (p != null && CombatRegistry.IsValid(p) && age > 0.8f)
                {
                    float d = (target - transform.position).magnitude;
                    if (d < 3.2f || age > 18f) { flying = true; vel = Vector3.up * 2.5f; }
                }
            }
            else
            {
                Vector3 to = target - transform.position;
                float d = to.magnitude;
                vel = Vector3.Lerp(vel, to.normalized * Mathf.Lerp(6f, 14f, Mathf.Clamp01((age - 0.8f) / 2f)), Time.deltaTime * 6f);
                transform.position += vel * Time.deltaTime;
                if (d < 0.45f) Collect();
            }
            gem.localRotation = Quaternion.Euler(12f, age * 140f, 8f * Mathf.Sin(age * 3f));
            if (halo != null)
            {
                halo.GetPropertyBlock(mpb);
                mpb.SetFloat("_Intensity", 0.7f + 0.3f * Mathf.Sin(age * 5f + seed));
                halo.SetPropertyBlock(mpb);
            }
            if (age > 40f) Destroy(gameObject);
        }

        void Collect()
        {
            done = true;
            Collected = Collected + 1;
            OnCollected?.Invoke(Collected);
            ArenVFX.Sparks(transform.position, Vector3.up, new Color(0.8f, 0.55f, 1f), 14, 3f, 90f);
            ArenVFX.Ring(transform.position, 0.05f, 0.7f, 0.3f, new Color(0.75f, 0.45f, 1f), 0.05f, false);
            ArenAudio.PlayUI(Sfx.AbilityReady, 0.55f, 1.25f);
            Aren.UI.ArenHUD.Instance?.Toast("Fragmento de Eco  ·  " + Collected, new Color(0.78f, 0.6f, 1f), 1.6f);
            var em = sparkle.emission; em.rateOverTime = 0f;
            sparkle.transform.SetParent(null, true);
            Destroy(sparkle.gameObject, 1.5f);
            Destroy(gameObject);
        }
    }
}
