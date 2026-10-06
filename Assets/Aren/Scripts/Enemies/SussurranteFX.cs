using System.Collections.Generic;
using Aren.Combat;
using UnityEngine;

namespace Aren.Enemies
{
    /// <summary>
    /// Efeitos do Sussurrante (prancha do autor): tudo que faz ele ler como "Eco Primordial · Rasgos".
    ///  - olhos acesos no fundo das órbitas e a fenda do peito pulsando no mesmo "coração" do shader;
    ///  - LASCAS: pixels escuros de borda violeta se soltando da cabeça, ombros e do braço esquerdo
    ///    e subindo (o desenho da prancha), e fiapos de energia saindo do crânio;
    ///  - lâmina: carga (_Charge) e o rastro do corte (meia-lua violeta);
    ///  - grito: sucção para a boca, anéis no chão, onda que distorce, lascas em leque;
    ///  - dano, morte (o corpo vira lasca de cima para baixo) e materialização (sobe dos pés).
    /// Os pontos de ancoragem (olhos, boca, peito, topo da cabeça, ponta/base da lâmina) vêm do
    /// esqueleto exportado pelo ArtSource/Sussurrante (ossos-marcadores) e do prefab.
    /// </summary>
    public class SussurranteFX : MonoBehaviour
    {
        public static readonly Color Violet = new Color(0.62f, 0.32f, 1f);
        static readonly Color ShardDark = new Color(0.28f, 0.14f, 0.5f, 0.95f);
        static readonly Color ShardGlint = new Color(0.9f, 0.65f, 1f, 0.22f);

        static Material shardMat, glowMat, slashMat, sparkMat;
        static Mesh quad;
        static readonly int IdVeins = Shader.PropertyToID("_Veins");
        static readonly int IdSpawn = Shader.PropertyToID("_Spawn");
        static readonly int IdCharge = Shader.PropertyToID("_Charge");
        static readonly int IdSeed = Shader.PropertyToID("_Seed");
        static readonly int IdIntensity = Shader.PropertyToID("_Intensity");

        Transform head, eyeL, eyeR, mouth, chest, headTop, shoulders, armL, handL, armR, bladeBase, bladeTip, footL, footR;
        Renderer[] body;      // corpo (LOD0/LOD1)
        Renderer blade;
        MaterialPropertyBlock mpb;
        MeshRenderer eyeGlowL, eyeGlowR, chestGlow;
        readonly List<ParticleSystem> shards = new List<ParticleSystem>();
        ParticleSystem wisps, burst, suck;
        BladeTrail trail;
        AudioSource voice;
        readonly AudioSource[] oneShots = new AudioSource[3];
        int nextShot;
        float seed;

        // estado animado
        float veins, veinsTarget, charge, chargeTarget, spawn = 1f, eye = 1f, eyeTarget = 1f, alive = 1f;
        float lastVeins = -1, lastCharge = -1, lastSpawn = -1;
        float footLY, footRY;
        bool footLDown, footRDown;

        public Transform Mouth => mouth != null ? mouth : head;
        public Transform Chest => chest;

        // ------------------------------------------------------------ montagem

        public void Init()
        {
            seed = Random.value * 10f;
            mpb = new MaterialPropertyBlock();
            Transform F(string n) => FindDeep(transform, n);
            head = F("Head"); eyeL = F("eye_l"); eyeR = F("eye_r"); mouth = F("mouth"); chest = F("chest_fx");
            headTop = F("FX_HeadTop"); shoulders = F("spine_03"); armL = F("lowerarm_l"); handL = F("hand_l"); armR = F("upperarm_r");
            bladeBase = F("Blade_Base"); bladeTip = F("Blade_Tip");
            footL = F("foot_l"); footR = F("foot_r");
            var list = new List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is SkinnedMeshRenderer) list.Add(r);
                else if (r.name.StartsWith("SussurranteBlade")) blade = r;
            }
            body = list.ToArray();
            EnsureMaterials();

            if (eyeL != null) eyeGlowL = Glow("Olho E", eyeL, 0.085f, new Color(1.5f, 0.6f, 3.2f), 0.35f);
            if (eyeR != null) eyeGlowR = Glow("Olho D", eyeR, 0.085f, new Color(1.5f, 0.6f, 3.2f), 0.35f);
            if (chest != null) chestGlow = Glow("Fenda do peito", chest, 0.32f, new Color(0.8f, 0.35f, 1.9f), 0.1f, 5f);

            // lascas que se soltam (pontos da prancha: cabeça, ombros, braço esquerdo com os raios)
            if (head != null) shards.Add(Shards("Lascas cabeça", head, Vector3.zero, 0.17f, 7f));
            if (shoulders != null) shards.Add(Shards("Lascas ombros", shoulders, Vector3.zero, 0.3f, 9f));
            if (armL != null) shards.Add(Shards("Lascas braço E", armL, Vector3.zero, 0.12f, 6f));
            if (armR != null) shards.Add(Shards("Lascas braço D", armR, Vector3.zero, 0.1f, 3f));
            if (headTop != null || head != null) wisps = Wisps(headTop != null ? headTop : head);
            burst = Burst();
            suck = Suck();
            if (bladeBase != null && bladeTip != null)
            {
                trail = new GameObject("Rastro da lâmina").AddComponent<BladeTrail>();
                trail.Init(bladeBase, bladeTip, slashMat);
            }
            BuildAudio();
            ApplyBody(true);
        }

        public static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        static void EnsureMaterials()
        {
            if (shardMat != null) return;
            var noise = Resources.Load<Texture2D>("VFX/noise_perlin");
            shardMat = new Material(Shader.Find("Hidden/Aren/SussShard")) { name = "SussShard" };
            glowMat = new Material(Shader.Find("Hidden/Aren/SussGlow")) { name = "SussGlow" };
            slashMat = new Material(Shader.Find("Hidden/Aren/SussSlash")) { name = "SussSlash" };
            slashMat.SetTexture("_Noise", noise);
            var wp = Shader.Find("Hidden/Aren/WorldParticle");
            sparkMat = wp != null ? new Material(wp) { name = "SussSpark" } : glowMat;
            if (wp != null) sparkMat.SetFloat("_Boost", 2.6f);
            quad = new Mesh { name = "SussQuad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quad.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
        }

        MeshRenderer Glow(string name, Transform parent, float size, Color color, float flicker, float falloff = 6f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            // escala no mundo independente da escala do osso
            Vector3 ls = parent.lossyScale;
            float s = Mathf.Max(1e-4f, (Mathf.Abs(ls.x) + Mathf.Abs(ls.y) + Mathf.Abs(ls.z)) / 3f);
            go.transform.localScale = Vector3.one * (size / s);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = glowMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var b = new MaterialPropertyBlock();
            b.SetColor("_Color", color);
            b.SetFloat("_Flicker", flicker);
            b.SetFloat("_Falloff", falloff);
            b.SetFloat("_Seed", Random.value * 10f);
            b.SetFloat(IdIntensity, 1f);
            mr.SetPropertyBlock(b);
            return mr;
        }

        static ParticleSystem NewPS(string name, Transform parent, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            return ps;
        }

        static Gradient Fade(Color c0, Color c1)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c0, 0f), new GradientColorKey(c1, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        ParticleSystem Shards(string name, Transform parent, Vector3 offset, float radius, float rate)
        {
            var ps = NewPS(name, parent, shardMat);
            ps.transform.localPosition = offset;
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.016f, 0.048f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(ShardDark, ShardGlint);
            main.gravityModifier = -0.04f;
            main.maxParticles = 80;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius; sh.radiusThickness = 0.35f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.12f); vel.y = new ParticleSystem.MinMaxCurve(0.22f, 0.62f); vel.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.12f);
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 1.1f; noise.scrollSpeed = 0.4f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, new Color(0.7f, 0.6f, 0.9f));
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.25f));
            ps.Play();
            return ps;
        }

        ParticleSystem Wisps(Transform parent)
        {
            var ps = NewPS("Fiapos do crânio", parent, sparkMat);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.4f, 1f, 0.9f), new Color(1f, 0.8f, 1f, 0.7f));
            main.maxParticles = 60;
            var em = ps.emission; em.rateOverTime = 16f;
            // esfera pequena + subida no MUNDO (o eixo local do osso da cabeça não é o "para cima")
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.085f; sh.radiusThickness = 0.5f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f); vel.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            vel.y = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.5f; noise.frequency = 2.2f; noise.scrollSpeed = 1.2f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(new Color(1f, 0.85f, 1f), new Color(0.45f, 0.2f, 0.9f));
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 2.6f; r.velocityScale = 0.12f;
            ps.Play();
            return ps;
        }

        ParticleSystem Burst()
        {
            var ps = NewPS("Lascas (rajada)", transform, shardMat);
            var main = ps.main;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(ShardDark, ShardGlint);
            main.gravityModifier = 0.25f;
            main.maxParticles = 400;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.enabled = false;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.9f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, new Color(0.6f, 0.5f, 0.85f));
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            ps.Play();
            return ps;
        }

        ParticleSystem Suck()
        {
            var ps = NewPS("Sucção do grito", Mouth != null ? Mouth : transform, sparkMat);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.04f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.45f, 1f, 0.9f), new Color(1f, 0.85f, 1f, 0.8f));
            main.maxParticles = 120;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 1.5f; sh.radiusThickness = 0.15f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.radial = new ParticleSystem.MinMaxCurve(-3.2f, -2.2f);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(new Color(0.6f, 0.35f, 1f), new Color(1f, 0.9f, 1f));
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 2.5f; r.velocityScale = 0.12f;
            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------ som

        void BuildAudio()
        {
            var loop = Resources.Load<AudioClip>("Sussurrante/Audio/suss_whisper_loop");
            if (loop != null)
            {
                voice = (head != null ? head.gameObject : gameObject).AddComponent<AudioSource>();
                voice.clip = loop; voice.loop = true; voice.playOnAwake = false;
                voice.spatialBlend = 1f; voice.rolloffMode = AudioRolloffMode.Logarithmic; voice.minDistance = 2.2f; voice.maxDistance = 24f;
                voice.dopplerLevel = 0f; voice.priority = 150;
                voice.time = Random.Range(0f, loop.length * 0.9f);
                voice.pitch = Random.Range(0.94f, 1.04f);
                voice.volume = 0f;
                voice.Play();
            }
            for (int i = 0; i < oneShots.Length; i++)
            {
                var a = gameObject.AddComponent<AudioSource>();
                a.playOnAwake = false; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Logarithmic;
                a.minDistance = 3f; a.maxDistance = 45f; a.dopplerLevel = 0f; a.priority = 90;
                oneShots[i] = a;
            }
        }

        static readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

        public static AudioClip Clip(string path)
        {
            if (!clipCache.TryGetValue(path, out var c)) { c = Resources.Load<AudioClip>(path); clipCache[path] = c; }
            return c;
        }

        /// <summary>Som 3D preso ao Sussurrante (as fontes andam com ele).</summary>
        public void Say(string path, float vol, float pitch = 1f)
        {
            var c = Clip(path);
            if (c == null) return;
            var a = oneShots[nextShot]; nextShot = (nextShot + 1) % oneShots.Length;
            a.Stop();
            a.clip = c;
            a.pitch = pitch * Mathf.Lerp(1f, Time.timeScale, 0.25f);
            a.volume = Mathf.Clamp01(vol * ArenAudio.Effects);
            a.Play();
        }

        // ------------------------------------------------------------ comandos (EnemySussurrante)

        public void SetVeins(float v) { veinsTarget = Mathf.Clamp01(v); }
        public void PulseVeins(float v) { veins = Mathf.Max(veins, v); }
        public void SetCharge(float v) { chargeTarget = Mathf.Clamp01(v); }
        public void SetEyes(float v) { eyeTarget = v; }
        public void Trail(bool on) { if (trail != null) trail.Active = on; }

        public void HitBurst(Vector3 point, Vector3 dir, bool heavy)
        {
            ArenVFX.Sparks(point, dir, Violet, heavy ? 14 : 8, heavy ? 7f : 5f, 70f);
            Emit(burst, point, dir.normalized * 2.5f + Vector3.up * 1.2f, heavy ? 18 : 9, 0.9f);
            PulseVeins(heavy ? 0.9f : 0.5f);
            eye = Mathf.Max(eye, 2.2f);
        }

        /// <summary>Rajada de lascas a partir de um ponto (velocidade base + espalhamento).</summary>
        public void Emit(ParticleSystem ps, Vector3 p, Vector3 v, int n, float spread)
        {
            if (ps == null) return;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                ep.position = p + Random.insideUnitSphere * 0.12f;
                ep.velocity = v + Random.insideUnitSphere * (v.magnitude * spread + 0.5f);
                ps.Emit(ep, 1);
            }
        }

        public void ScreamWindup(float k)
        {
            veinsTarget = Mathf.Max(veinsTarget, k * 0.65f);
            eyeTarget = 1f + k * 2.2f;
            if (suck != null) { var em = suck.emission; em.rateOverTime = 70f * k; }
        }

        public void ScreamRelease(float radius)
        {
            if (suck != null) { var em = suck.emission; em.rateOverTime = 0f; }
            Vector3 o = Mouth != null ? Mouth.position : transform.position + Vector3.up * 2.5f;
            Vector3 ground = new Vector3(o.x, transform.position.y + 0.08f, o.z);
            ArenVFX.Ring(ground, 0.3f, radius, 0.55f, Violet, 0.22f, true);
            StartCoroutine(Later(0.14f, () => ArenVFX.Ring(ground, 0.2f, radius * 0.75f, 0.5f, new Color(0.85f, 0.6f, 1f), 0.12f, true)));
            StartCoroutine(Later(0.3f, () => ArenVFX.Ring(ground, 0.2f, radius * 0.5f, 0.45f, Violet, 0.08f, true)));
            ArenVFX.Ring(o, 0.15f, radius * 0.55f, 0.35f, new Color(0.9f, 0.7f, 1f), 0.06f, false);
            ArenVFX.Distortion(o, radius * 0.8f, 0.6f, 0.07f);
            // leque de lascas saindo da boca, em todas as direções horizontais
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < 70; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), Random.Range(-0.15f, 0.35f), Mathf.Sin(a));
                ep.position = o + d * 0.2f;
                ep.velocity = d * Random.Range(5f, 9f);
                burst.Emit(ep, 1);
            }
            veins = 1f; veinsTarget = 0.85f;
            eye = 4f; eyeTarget = 3f;
            GameFeel.Shake(0.35f);
            GameFeel.Ripple(o, 0.06f);
        }

        /// <summary>Pulsos menores enquanto o grito dura (o som "bate" em ondas).</summary>
        public void ScreamPulse(float radius)
        {
            Vector3 o = Mouth != null ? Mouth.position : transform.position + Vector3.up * 2.5f;
            Vector3 ground = new Vector3(o.x, transform.position.y + 0.08f, o.z);
            ArenVFX.Ring(ground, 0.4f, radius * Random.Range(0.55f, 0.8f), 0.45f, Violet * 0.8f, 0.07f, true);
        }

        public void ScreamEnd() { veinsTarget = 0f; eyeTarget = 1f; if (suck != null) { var em = suck.emission; em.rateOverTime = 0f; } }

        public void DeathFX()
        {
            alive = 0f;
            veins = 1f; veinsTarget = 0f; chargeTarget = 0f;
            Trail(false);
            if (suck != null) { var em = suck.emission; em.rateOverTime = 0f; }
            foreach (var s in shards) { var em = s.emission; em.rateOverTime = 0f; }
            if (wisps != null) { var em = wisps.emission; em.rateOverTime = 0f; }
            StartCoroutine(DeathShards());
        }

        System.Collections.IEnumerator DeathShards()
        {
            // o corpo "desfaz" de cima para baixo: as lascas saem acompanhando a altura do dissolve
            var smr = body.Length > 0 ? body[0] as SkinnedMeshRenderer : null;
            float t = 0f, dur = 2.2f;
            var ep = new ParticleSystem.EmitParams();
            float top = transform.position.y + 2.7f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01((t - 0.55f) / (dur - 0.55f));
                if (k > 0f && smr != null)
                {
                    Bounds b = smr.bounds;
                    float y = Mathf.Lerp(Mathf.Min(top, b.max.y), b.min.y, k);
                    int n = Mathf.RoundToInt(Mathf.Lerp(3f, 6f, Random.value));
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 p = new Vector3(Random.Range(b.min.x, b.max.x) * 0.6f + b.center.x * 0.4f, y + Random.Range(-0.08f, 0.08f),
                                                Random.Range(b.min.z, b.max.z) * 0.6f + b.center.z * 0.4f);
                        ep.position = p;
                        ep.velocity = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(0.4f, 1.6f), Random.Range(-0.6f, 0.6f));
                        burst.Emit(ep, 1);
                    }
                }
                eye = Mathf.Lerp(3f, 0f, t / 1.2f);
                yield return null;
            }
        }

        public void Materialize(float dur)
        {
            spawn = 0f;
            StartCoroutine(MaterializeCo(dur));
        }

        System.Collections.IEnumerator MaterializeCo(float dur)
        {
            float t = 0f;
            var ep = new ParticleSystem.EmitParams();
            Vector3 c = transform.position;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                spawn = Mathf.SmoothStep(0f, 1f, k);
                // lascas convergindo para o corpo que se forma
                int n = Random.value < 0.7f ? 3 : 2;
                for (int i = 0; i < n; i++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    float y = Random.Range(0.1f, 2.8f * spawn + 0.3f);
                    Vector3 from = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(1.0f, 1.8f) + Vector3.up * y;
                    Vector3 to = c + Vector3.up * Mathf.Min(y, 2.6f);
                    ep.position = from;
                    ep.velocity = (to - from) * Random.Range(1.4f, 2.2f) + Vector3.up * 0.3f;
                    ep.startLifetime = 0.55f;
                    burst.Emit(ep, 1);
                }
                eye = Mathf.Lerp(0f, 2.5f, Mathf.InverseLerp(0.7f, 1f, k));
                yield return null;
            }
            spawn = 1f;
        }

        System.Collections.IEnumerator Later(float t, System.Action a) { yield return new WaitForSeconds(t); a(); }

        // ------------------------------------------------------------ quadro a quadro

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            veins = Mathf.MoveTowards(veins, veinsTarget, dt * (veins > veinsTarget ? 0.9f : 3f));
            charge = Mathf.MoveTowards(charge, chargeTarget, dt * (charge > chargeTarget ? 2.5f : 5f));
            eye = Mathf.MoveTowards(eye, eyeTarget * alive, dt * 3f);
            ApplyBody(false);

            float heart = Heart(Time.time * 0.8f + seed * 0.31f);
            SetGlow(eyeGlowL, eye * (0.9f + 0.1f * heart));
            SetGlow(eyeGlowR, eye * (0.9f + 0.1f * heart));
            SetGlow(chestGlow, alive * (0.08f + 0.18f * heart + veins * 0.45f) * spawn);

            if (voice != null)
            {
                float target = alive * (0.32f + veins * 0.45f) * ArenAudio.Effects * spawn;
                voice.volume = Mathf.MoveTowards(voice.volume, target, dt * 0.8f);
            }
            Steps();
        }

        static float Heart(float t)
        {
            float ph = t - Mathf.Floor(t);
            float b1 = Mathf.Exp(-Mathf.Pow((ph - 0.08f) / 0.045f, 2f));
            float b2 = Mathf.Exp(-Mathf.Pow((ph - 0.27f) / 0.06f, 2f)) * 0.65f;
            return 0.42f + 0.58f * Mathf.Clamp01(b1 + b2);
        }

        void SetGlow(MeshRenderer r, float v)
        {
            if (r == null) return;
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(IdIntensity, v);
            r.SetPropertyBlock(mpb);
            r.enabled = v > 0.01f;
        }

        void ApplyBody(bool force)
        {
            if (!force && Mathf.Abs(veins - lastVeins) < 0.01f && Mathf.Abs(spawn - lastSpawn) < 0.005f && Mathf.Abs(charge - lastCharge) < 0.01f) return;
            lastVeins = veins; lastSpawn = spawn; lastCharge = charge;
            foreach (var r in body)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(IdVeins, veins);
                mpb.SetFloat(IdSpawn, spawn);
                if (force) mpb.SetFloat(IdSeed, seed);
                r.SetPropertyBlock(mpb);
            }
            if (blade != null)
            {
                blade.GetPropertyBlock(mpb);
                mpb.SetFloat(IdCharge, charge);
                if (force) mpb.SetFloat(IdSeed, seed);
                blade.SetPropertyBlock(mpb);
                blade.enabled = spawn > 0.6f;
            }
        }

        /// <summary>Passos pesados: poeira e baque quando o pé assenta (pé desce e para).</summary>
        void Steps()
        {
            if (alive < 0.5f) return;
            Step(footL, ref footLY, ref footLDown);
            Step(footR, ref footRY, ref footRDown);
        }

        void Step(Transform f, ref float lastY, ref bool down)
        {
            if (f == null) return;
            float y = f.position.y - transform.position.y;
            bool low = y < 0.24f;
            if (low && !down && lastY > y)
            {
                Vector3 p = f.position; p.y = transform.position.y + 0.02f;
                ArenVFX.Dust(p, Vector3.up * 0.25f, new Color(0.24f, 0.22f, 0.26f, 0.35f), 2, 0.55f);
                ArenAudio.Play(Sfx.Footstep, p, 0.55f, Random.Range(0.62f, 0.72f));
            }
            down = low;
            lastY = y;
        }

        // o corpo é desativado no fim da morte (não destruído): o rastro, que vive fora dele, acompanha
        void OnDisable() { if (trail != null) trail.gameObject.SetActive(false); }
        void OnEnable() { if (trail != null) trail.gameObject.SetActive(true); }
        void OnDestroy() { if (trail != null) Destroy(trail.gameObject); }
    }

    /// <summary>
    /// Rastro do corte: fita entre a base e a ponta da lâmina, amostrada a cada quadro do golpe e
    /// suavizada (Catmull-Rom) para não ficar "quebrada" a 30 FPS. Malha no mundo, some com a idade.
    /// </summary>
    public class BladeTrail : MonoBehaviour
    {
        const int MaxSamples = 14;
        const float Life = 0.2f;
        const int Sub = 3;
        Transform a, b;
        Mesh mesh;
        MeshRenderer mr;
        readonly List<(Vector3 a, Vector3 b, float t)> samples = new List<(Vector3, Vector3, float)>();
        Vector3[] verts; Vector2[] uvs; Color[] cols; int[] tris;
        public bool Active;

        public void Init(Transform baseT, Transform tipT, Material mat)
        {
            a = baseT; b = tipT;
            mesh = new Mesh { name = "rastro" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            int n = (MaxSamples - 1) * Sub + 1;
            verts = new Vector3[n * 2]; uvs = new Vector2[n * 2]; cols = new Color[n * 2]; tris = new int[(n - 1) * 6];
        }

        void LateUpdate()
        {
            if (a == null || b == null) { Destroy(gameObject); return; }
            float now = Time.time;
            if (Active)
            {
                samples.Insert(0, (a.position, b.position, now));
                if (samples.Count > MaxSamples) samples.RemoveAt(samples.Count - 1);
            }
            samples.RemoveAll(s => now - s.t > Life);
            if (samples.Count < 2) { mr.enabled = false; return; }
            mr.enabled = true;
            int count = (samples.Count - 1) * Sub + 1;
            int v = 0;
            for (int i = 0; i < samples.Count - 1; i++)
            {
                var p0 = samples[Mathf.Max(0, i - 1)]; var p1 = samples[i]; var p2 = samples[i + 1]; var p3 = samples[Mathf.Min(samples.Count - 1, i + 2)];
                for (int s = 0; s < Sub; s++)
                {
                    float u = s / (float)Sub;
                    Put(ref v, CR(p0.a, p1.a, p2.a, p3.a, u), CR(p0.b, p1.b, p2.b, p3.b, u), Mathf.Lerp(now - p1.t, now - p2.t, u) / Life);
                }
            }
            var last = samples[samples.Count - 1];
            Put(ref v, last.a, last.b, (now - last.t) / Life);
            int ti = 0;
            for (int i = 0; i < count - 1; i++)
            {
                int i0 = i * 2;
                tris[ti++] = i0; tris[ti++] = i0 + 1; tris[ti++] = i0 + 2;
                tris[ti++] = i0 + 1; tris[ti++] = i0 + 3; tris[ti++] = i0 + 2;
            }
            mesh.Clear();
            mesh.SetVertices(verts, 0, count * 2);
            mesh.SetUVs(0, uvs, 0, count * 2);
            mesh.SetColors(cols, 0, count * 2);
            mesh.SetTriangles(tris, 0, (count - 1) * 6, 0);
            mesh.RecalculateBounds();
        }

        void Put(ref int v, Vector3 pa, Vector3 pb, float age)
        {
            age = Mathf.Clamp01(age);
            verts[v] = pa; uvs[v] = new Vector2(age, 0f); cols[v] = Color.white; v++;
            verts[v] = pb; uvs[v] = new Vector2(age, 1f); cols[v] = Color.white; v++;
        }

        static Vector3 CR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
