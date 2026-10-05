using System.Collections;
using System.Collections.Generic;
using Aren.UI;
using Aren.World.Night;
using UnityEngine;
using UnityEngine.UI;

namespace Aren.World
{
    /// <summary>
    /// A Corrupção chega na rua do mercado, na frente do Aren (cena curta, em tempo real, ~11 s):
    ///   A — por cima do ombro dele: os aldeões no meio da rua olham a Fenda; o céu pulsa e FIOS
    ///       desafinados descem dela (a harmonia da Fenda virou corda solta) e se prendem às pessoas;
    ///   B — dois deles cambaleiam e convulsionam; a mancha sobe dos pés à cabeça (rachaduras com a
    ///       nebulosa da Fenda, frente acesa); os gritos entortam; os outros fogem, janelas batem;
    ///   C — um deles não aguenta: desintegra em cinza e brasa da cabeça para os pés, e o grito vira
    ///       um coro desafinado (a regra da lore: o som nunca some, ele entorta);
    ///   D — os dois tomados se viram para o Aren e rosnam → o jogo volta e eles SÃO os Ecos da luta.
    /// Os aldeões já estão na rua antes (o jogador os vê ao passar o portão). Testes que pulam direto
    /// para o mercado (DebugJump) chamam Cancel(): a rua fica como antes.
    /// </summary>
    public class VillageCorruption : MonoBehaviour
    {
        public static VillageCorruption Instance { get; private set; }
        public bool Running { get; private set; }
        public bool Done { get; private set; }

        enum Role { Eco, Dust, Flee }
        class V
        {
            public GameObject go; public Animator anim; public Renderer[] rends; public Transform head, chest;
            public Role role; public string prefab, ecoPrefab; public Vector3 pos; public float yaw;
            public float corrupt, dissolve; public float seed; public Light light; public ParticleSystem ash;
            public Vector3 fleeTo;
        }
        readonly List<V> vs = new List<V>();
        MaterialPropertyBlock mpb;
        Canvas canvas; RectTransform barTop, barBottom; float bars;
        static readonly int IdCorrupt = Shader.PropertyToID("_Corrupt"), IdDissolve = Shader.PropertyToID("_Dissolve"), IdSeed = Shader.PropertyToID("_Seed");

        public static VillageCorruption Create()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Corrupção na vila");
            Instance = go.AddComponent<VillageCorruption>();
            Instance.Spawn();
            return Instance;
        }

        void Spawn()
        {
            mpb = new MaterialPropertyBlock();
            // posições na rua do mercado (livre entre x -3,5 e 3,5); olham a Fenda (nor-nordeste, longe)
            Add("Villager_F1", "Eco_F1", Role.Eco, new Vector3(-1.5f, 0f, -23.2f), "Idle_No");
            Add("Villager_M1", "Eco_M1", Role.Eco, new Vector3(1.6f, 0f, -22.2f), "Idle_FoldArms");
            Add("Villager_M2", null, Role.Dust, new Vector3(0.6f, 0f, -28.5f), "Call");
            Add("Villager_F2", null, Role.Dust, new Vector3(-2.2f, 0f, -17.0f), "Idle_FoldArms");
            Add("Villager_F2", null, Role.Flee, new Vector3(2.9f, 0f, -26.0f), "Idle_No").fleeTo = new Vector3(2.6f, 0f, -6f);
            Add("Villager_M1", null, Role.Flee, new Vector3(-3.0f, 0f, -29.5f), "Yes").fleeTo = new Vector3(-2.4f, 0f, -5f);
        }

        V Add(string prefab, string eco, Role role, Vector3 p, string idle)
        {
            var src = Resources.Load<GameObject>("Villagers/" + prefab);
            var v = new V { prefab = prefab, ecoPrefab = eco, role = role, seed = Random.value * 10f };
            p.y = Ground(p.x, p.z);
            var fd = NightSetup.Dir(NightSetup.FendaAz, 0f);
            v.yaw = Mathf.Atan2(fd.x, fd.z) * Mathf.Rad2Deg + Random.Range(-25f, 25f);
            v.pos = p;
            vs.Add(v);
            if (src == null) return v;
            v.go = Instantiate(src, p, Quaternion.Euler(0f, v.yaw, 0f), transform);
            v.anim = v.go.GetComponent<Animator>();
            v.anim.cullingMode = AnimatorCullingMode.CullCompletely;   // até a cena: só anima se estiver na tela
            v.anim.Play(idle, 0, Random.value);
            v.anim.SetFloat("Speed", Random.Range(0.9f, 1.1f));
            v.rends = v.go.GetComponentsInChildren<Renderer>();
            v.head = v.anim.GetBoneTransform(HumanBodyBones.Head);
            v.chest = v.anim.GetBoneTransform(HumanBodyBones.Chest) ?? v.head;
            Apply(v);
            return v;
        }

        static float Ground(float x, float z)
        {
            float gy = FarLands.GroundY(x, z);
            int mask = ~((1 << 10) | (1 << 8));
            if (Physics.Raycast(new Vector3(x, gy + 2.2f, z), Vector3.down, out var hit, 5f, mask, QueryTriggerInteraction.Ignore)) return hit.point.y;
            return gy;
        }

        void Apply(V v)
        {
            if (v.rends == null) return;
            foreach (var r in v.rends)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(IdCorrupt, v.corrupt); mpb.SetFloat(IdDissolve, v.dissolve); mpb.SetFloat(IdSeed, v.seed);
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>Teste pulou o roteiro: tira os aldeões e não toca a cena.</summary>
        public void Cancel()
        {
            Done = true;
            foreach (var v in vs) if (v.go != null) Destroy(v.go);
            vs.Clear();
        }

        // ------------------------------------------------------------ a cena

        /// <summary>Toca a cena com a câmera 'cam' (já ativa). Devolve, no fim, onde nasceram os Ecos.</summary>
        public IEnumerator Play(Camera cam, Transform aren, List<(GameObject prefab, Vector3 pos, float yaw)> ecos)
        {
            Running = true;
            var snd = OpeningSound.Instance;
            var sky = RuptureSky.Instance;
            BuildBars();
            foreach (var v in vs) if (v.anim != null) v.anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            bool cine = RenderScaler.Cinematic;
            if (GameSettings.Quality >= 1) RenderScaler.Cinematic = true;
            Vector3 A = aren.position, up = Vector3.up;
            // luz de preenchimento presa na câmera (os rostos não somem no contraluz das lanternas)
            var fill = new GameObject("Luz de preenchimento").AddComponent<Light>();
            fill.transform.SetParent(cam.transform, false); fill.transform.localPosition = new Vector3(-0.6f, 0.4f, 0.3f);
            fill.type = LightType.Point; fill.range = 9f; fill.intensity = 0.7f; fill.color = new Color(0.75f, 0.8f, 1f); fill.shadows = LightShadows.None;
            Vector3 fendaDir = NightSetup.Dir(NightSetup.FendaAz, 0.42f);
            if (snd != null) { snd.droneLevel = 0.3f; snd.Play("x_riser", 0.42f, 0f); snd.Play("tower_detuned", 0.4f, 0.15f); }

            // A — por cima do ombro: os aldeões olham a Fenda; os fios descem
            var e0 = vs.Find(x => x.role == Role.Eco);
            var e1 = vs.FindLast(x => x.role == Role.Eco);
            var d0 = vs.Find(x => x.role == Role.Dust);
            var d1 = vs.FindLast(x => x.role == Role.Dust);
            StartCoroutine(At(1.0f, () => { sky?.Burst(1f); sky?.Wave(); snd?.Play("x_sky_tear", 0.35f, 0.25f); }));
            float[] delays = { 1.3f, 1.55f, 1.75f, 2.05f };
            var victims = new[] { e0, e1, d0, d1 };
            for (int i = 0; i < victims.Length; i++)
            {
                var vv = victims[i]; float dl = delays[i];
                if (vv?.go == null) continue;
                StartCoroutine(At(dl, () => StartCoroutine(String(vv, fendaDir))));
            }
            // a cena inteira acontece "agora": o Aren também reage (olha e para)
            var look = aren.GetComponent<CinematicLook>();
            look?.LookAt(new Vector3(0f, 1.6f, -21f), 1f);
            yield return Shot(cam, A + new Vector3(0.75f, 1.62f, -2.0f), new Vector3(0.1f, 1.7f, -22f), 46f,
                                   A + new Vector3(0.65f, 1.6f, -1.4f), new Vector3(0.1f, 2.4f, -22f), 42f, 3.0f);

            // B — dois são tomados: cambaleiam, convulsionam, a mancha sobe; gritos; os outros fogem
            foreach (var v in vs)
            {
                if (v.go == null) continue;
                if (v.role == Role.Flee) StartCoroutine(Flee(v, Random.Range(0.1f, 0.5f)));
                else StartCoroutine(Seize(v, v.role == Role.Eco ? 0f : 0.25f, v.role == Role.Eco));
            }
            if (snd != null)
            {
                snd.Play("x_crowd_panic", 0.5f, 0f);
                StartCoroutine(At(0.5f, () => snd.PlayAt("x_running_cobble", new Vector3(0f, 1f, -22f), 0.7f, 3f, 40f)));
                StartCoroutine(At(1.1f, () => snd.PlayAt("x_shutters_slam", new Vector3(5.5f, 2f, -20f), 0.8f, 3f, 40f)));
                StartCoroutine(At(2.0f, () => snd.PlayAt("x_shutters_slam", new Vector3(-5.5f, 2f, -15f), 0.6f, 3f, 40f, 0.92f)));
                snd.fireLevel = 0.35f;
            }
            Vector3 M = e0 != null && e1 != null ? (e0.pos + e1.pos) * 0.5f : new Vector3(0, 0, -20);
            yield return Shot(cam, M + new Vector3(-2.1f, 1.6f, -6.4f), M + new Vector3(0f, 1.0f, -0.6f), 44f,
                                   M + new Vector3(-1.8f, 1.55f, -5.7f), M + new Vector3(0f, 1.05f, -0.6f), 41f, 3.4f);

            // C — o que desintegra: o grito vira coro desafinado; cinza e brasa sobem
            if (d0?.go != null)
            {
                Vector3 P = d0.pos;
                yield return Shot(cam, P + new Vector3(1.7f, 1.5f, -3.0f), P + new Vector3(0f, 0.95f, -0.5f), 40f,
                                       P + new Vector3(1.45f, 1.4f, -2.55f), P + new Vector3(0f, 0.9f, -0.5f), 37f, 2.7f);
            }

            // D — os dois tomados se viram para o Aren; rosnam; olhos acesos
            foreach (var v in new[] { e0, e1 })
            {
                if (v?.go == null) continue;
                var to = A - v.pos; to.y = 0f;
                StartCoroutine(Turn(v, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 0.9f));
                v.anim.CrossFadeInFixedTime("ZombieIdle", 0.3f);
            }
            StartCoroutine(At(0.35f, () => { if (snd != null && e0?.go != null) snd.PlayAt("x_growl_a", e0.pos + up * 1.6f, 1f, 2f, 30f); }));
            StartCoroutine(At(0.8f, () => { if (snd != null && e1?.go != null) snd.PlayAt("x_growl_b", e1.pos + up * 1.6f, 1f, 2f, 30f, 0.9f); }));
            yield return Shot(cam, M + new Vector3(0.3f, 1.2f, -5.6f), M + new Vector3(0f, 1.2f, -0.7f), 46f,
                                   M + new Vector3(0.25f, 1.15f, -4.9f), M + new Vector3(0f, 1.25f, -0.7f), 43f, 2.3f);

            // fim: os Ecos são estas pessoas (mesmo modelo, já tomado) — troca no mesmo quadro
            ecos.Clear();
            foreach (var v in new[] { e0, e1 })
            {
                if (v == null) continue;
                var prefab = Resources.Load<GameObject>("Enemies/" + v.ecoPrefab);
                ecos.Add((prefab, v.go != null ? v.go.transform.position : v.pos, v.go != null ? v.go.transform.eulerAngles.y : v.yaw));
            }
            foreach (var v in vs) { if (v.light != null) Destroy(v.light.gameObject); if (v.go != null && v.role != Role.Dust) Destroy(v.go); }
            look?.Release();
            if (fill != null) Destroy(fill.gameObject);
            RenderScaler.Cinematic = cine;
            if (snd != null) { snd.droneLevel = 0.18f; snd.fireLevel = 0.22f; }
            Running = false; Done = true;
            StartCoroutine(HideBars());
        }

        // ------------------------------------------------------------ peças

        IEnumerator At(float t, System.Action a) { yield return new WaitForSeconds(t); a(); }

        IEnumerator Shot(Camera cam, Vector3 p0, Vector3 l0, float f0, Vector3 p1, Vector3 l1, float f1, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                float tt = Time.time;
                Vector3 p = Vector3.Lerp(p0, p1, k) + new Vector3(Mathf.PerlinNoise(tt * 0.4f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.1f, tt * 0.35f) - 0.5f, 0f) * 0.03f;
                cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(Vector3.Lerp(l0, l1, k) - p));
                cam.fieldOfView = Mathf.Lerp(f0, f1, k);
                yield return null;
            }
        }

        IEnumerator Turn(V v, float yaw, float dur)
        {
            float y0 = v.go.transform.eulerAngles.y, t = 0f;
            while (t < dur && v.go != null) { t += Time.deltaTime; v.go.transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(y0, yaw, Mathf.SmoothStep(0f, 1f, t / dur)), 0f); yield return null; }
        }

        static Material stringMat;
        static Material StringMat
        {
            get
            {
                if (stringMat == null)
                {
                    var sh = Shader.Find("Aren/FX/Additive");
                    stringMat = new Material(sh) { hideFlags = HideFlags.DontSave };
                    stringMat.mainTexture = Resources.Load<Texture2D>("VFX/fx_streak");
                    stringMat.SetColor("_Color", new Color(1.6f, 0.6f, 2.6f, 1f));
                }
                return stringMat;
            }
        }

        /// <summary>Um fio desafinado desce da Fenda e se prende ao peito do aldeão: vibra (onda estacionária) e some.</summary>
        IEnumerator String(V v, Vector3 fendaDir)
        {
            var go = new GameObject("Fio desafinado");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = StringMat;
            lr.positionCount = 28;
            lr.useWorldSpace = true;
            lr.widthCurve = new AnimationCurve(new Keyframe(0, 0.25f), new Keyframe(0.7f, 0.09f), new Keyframe(1, 0.07f));
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
            var snd = OpeningSound.Instance;
            snd?.PlayAt(Random.value < 0.5f ? "chime_bad2" : "chime_bad4", v.chest.position, 0.6f, 2f, 40f, Random.Range(0.55f, 0.75f));
            Vector3 top = v.chest.position + fendaDir * 70f;
            float t = 0f, life = 3.2f;
            var side = Vector3.Cross(fendaDir, Vector3.up).normalized;
            while (t < life && v.go != null)
            {
                t += Time.deltaTime;
                float reach = Mathf.Clamp01(t / 0.32f);                     // desce rápido
                float amp = 0.22f * Mathf.Exp(-t * 1.6f) * reach;           // vibra e assenta
                Vector3 end = v.chest.position;
                for (int i = 0; i < lr.positionCount; i++)
                {
                    float u = i / (lr.positionCount - 1f);
                    float uu = u * reach;
                    Vector3 p = Vector3.Lerp(top, end, uu);
                    // 3º harmônico batendo com um 3,2 (desafinado): a corda "bate"
                    float w = Mathf.Sin(uu * Mathf.PI * 3f) * Mathf.Sin(t * 37f) + 0.6f * Mathf.Sin(uu * Mathf.PI * 3.2f) * Mathf.Sin(t * 39.5f);
                    p += side * w * amp * (1f - u * 0.3f);
                    lr.SetPosition(i, p);
                }
                float a = Mathf.Clamp01(t / 0.1f) * Mathf.Clamp01((life - t) / 0.6f);
                lr.startColor = new Color(1f, 1f, 1f, a * 0.55f); lr.endColor = new Color(1f, 1f, 1f, a);
                if (t > 0.32f && t - Time.deltaTime <= 0.32f)
                {
                    ArenVFX.Flash(end, new Color(0.7f, 0.3f, 1f), 2.5f, 4f, 0.35f);
                    ArenVFX.Glyphs(end, new Color(0.75f, 0.4f, 1f), 6, 1.2f);
                }
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>Tomado: cambaleia, convulsiona, a mancha sobe; grita. Eco = fica; Pó = desintegra.</summary>
        IEnumerator Seize(V v, float delay, bool becomesEco)
        {
            yield return new WaitForSeconds(delay);
            if (v.go == null) yield break;
            var snd = OpeningSound.Instance;
            v.anim.CrossFadeInFixedTime("Stagger", 0.15f);
            v.anim.SetFloat("Speed", 0.85f);
            // luz violeta no corpo (pisca com a corrupção)
            var lg = new GameObject("Luz da corrupcao"); lg.transform.SetParent(v.go.transform, false); lg.transform.localPosition = new Vector3(0f, 1.2f, 0.4f);
            v.light = lg.AddComponent<Light>(); v.light.type = LightType.Point; v.light.color = new Color(0.65f, 0.3f, 1f); v.light.range = 4.5f; v.light.intensity = 0f; v.light.shadows = LightShadows.None;
            bool female = v.prefab.Contains("_F");
            snd?.PlayAt(female ? "x_scream_woman_a" : "x_scream_man_a", v.head.position, 0.8f, 3f, 45f, Random.Range(0.95f, 1.05f));
            if (!becomesEco) snd?.PlayAt(female ? "x_scream_woman_b" : "x_scream_man_b", v.head.position, 0.6f, 3f, 45f, 0.96f);
            snd?.PlayAt("x_corrupt_transform", v.chest.position, 0.7f, 2f, 30f);
            yield return new WaitForSeconds(0.55f);
            if (v.go == null) yield break;
            v.anim.CrossFadeInFixedTime("Convulse", 0.2f);
            v.anim.SetFloat("Speed", 0.7f);
            float t = 0f, dur = becomesEco ? 2.6f : 1.7f, target = becomesEco ? 1f : 0.55f;
            while (t < dur && v.go != null)
            {
                t += Time.deltaTime;
                float k = t / dur;
                v.corrupt = target * Mathf.SmoothStep(0f, 1f, k);
                v.light.intensity = (1.2f + 1.6f * Mathf.PerlinNoise(t * 9f, v.seed)) * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI * 0.9f + 0.2f);
                if (Random.value < Time.deltaTime * 10f) ArenVFX.Glyphs(v.chest.position + Random.insideUnitSphere * 0.3f, new Color(0.7f, 0.3f, 1f), 1, 0.8f);
                Apply(v);
                yield return null;
            }
            if (v.go == null) yield break;
            if (becomesEco) { v.light.intensity = 1.1f; v.anim.CrossFadeInFixedTime("ZombieIdle", 0.4f); v.anim.SetFloat("Speed", 1f); yield break; }
            // desintegra: o grito entorta em coro; cinza e brasa sobem da pele
            snd?.PlayAt(Random.value < 0.5f ? "x_scream_choir_a" : "x_scream_choir_b", v.head.position, 1f, 3f, 45f);
            snd?.PlayAt(Random.value < 0.5f ? "x_disintegrate_a" : "x_disintegrate_b", v.chest.position, 1f, 2f, 35f);
            v.anim.SetFloat("Speed", 0.35f);   // o corpo "trava" enquanto some
            v.ash = MakeAsh(v);
            float td = 0f, dd = 1.9f;
            while (td < dd && v.go != null)
            {
                td += Time.deltaTime;
                v.dissolve = Mathf.SmoothStep(0f, 1f, td / dd);
                v.corrupt = Mathf.Lerp(0.55f, 0.8f, td / dd);
                v.light.intensity = 2.4f * (1f - td / dd) + 0.6f;
                v.light.color = Color.Lerp(new Color(0.65f, 0.3f, 1f), new Color(1f, 0.55f, 0.25f), 0.5f);
                var em = v.ash.emission; em.rateOverTime = 260f * Mathf.Sin(Mathf.Clamp01(td / dd) * Mathf.PI) + 30f;
                Apply(v);
                yield return null;
            }
            if (v.ash != null) { var em = v.ash.emission; em.rateOverTime = 0f; v.ash.transform.SetParent(transform, true); Destroy(v.ash.gameObject, 4f); }
            if (v.go != null) { ArenVFX.Dust(v.pos + Vector3.up * 0.2f, Vector3.up * 0.6f, new Color(0.25f, 0.22f, 0.26f, 0.5f), 12, 0.7f); Destroy(v.go); }
        }

        ParticleSystem MakeAsh(V v)
        {
            var go = new GameObject("Cinza");
            go.transform.SetParent(v.go.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.25f), new Color(0.75f, 0.35f, 1f));
            main.gravityModifier = -0.12f;
            main.maxParticles = 600;
            var sh = ps.shape;
            var smr = v.go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null) { sh.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer; sh.skinnedMeshRenderer = smr; sh.meshShapeType = ParticleSystemMeshShapeType.Triangle; }
            else { sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.4f; }
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.15f, 0.45f); vel.y = new ParticleSystem.MinMaxCurve(0.2f, 0.7f); vel.z = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.8f; noise.scrollSpeed = 0.6f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.8f, 0.5f), 0f), new GradientColorKey(new Color(0.7f, 0.3f, 1f), 0.45f), new GradientColorKey(new Color(0.15f, 0.12f, 0.16f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SevenGlows.SparkMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        IEnumerator Flee(V v, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (v.go == null) yield break;
            var to = v.fleeTo - v.go.transform.position; to.y = 0f;
            v.go.transform.rotation = Quaternion.LookRotation(to.normalized);
            v.anim.CrossFadeInFixedTime("Run", 0.15f);
            v.anim.SetFloat("Speed", 1.1f);
            var snd = OpeningSound.Instance;
            if (Random.value < 0.7f) snd?.PlayAt(v.prefab.Contains("_F") ? "x_scream_woman_b" : "x_scream_man_b", v.head.position, 0.55f, 3f, 40f, 1.08f);
            float t = 0f;
            while (v.go != null && t < 6f)
            {
                t += Time.deltaTime;
                Vector3 p = Vector3.MoveTowards(v.go.transform.position, v.fleeTo, 5.2f * Time.deltaTime);
                p.y = Ground(p.x, p.z);
                v.go.transform.position = p;
                if ((p - v.fleeTo).sqrMagnitude < 0.25f) break;
                yield return null;
            }
            if (v.go != null) v.go.SetActive(false);
        }

        void BuildBars()
        {
            if (canvas != null) { canvas.gameObject.SetActive(true); StartCoroutine(ShowBars()); return; }
            canvas = UIKit.MakeCanvas("Corrupção (faixas)", 45);
            canvas.transform.SetParent(transform, false);
            barTop = UIKit.Rect("Topo", canvas.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0));
            barTop.gameObject.AddComponent<Image>().color = Color.black;
            barBottom = UIKit.Rect("Base", canvas.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 0));
            barBottom.gameObject.AddComponent<Image>().color = Color.black;
            StartCoroutine(ShowBars());
        }

        IEnumerator ShowBars() { while (bars < 1f) { bars = Mathf.MoveTowards(bars, 1f, Time.unscaledDeltaTime * 2.5f); SetBars(); yield return null; } }
        IEnumerator HideBars() { while (bars > 0f) { bars = Mathf.MoveTowards(bars, 0f, Time.unscaledDeltaTime * 2f); SetBars(); yield return null; } if (canvas != null) canvas.gameObject.SetActive(false); }
        void SetBars() { float h = 120f * Mathf.SmoothStep(0f, 1f, bars); barTop.sizeDelta = new Vector2(0, h); barBottom.sizeDelta = new Vector2(0, h); }

        /// <summary>Um Eco com corpo de aldeão (variante aleatória); null se os prefabs não existirem.</summary>
        public static GameObject RandomEcoPrefab()
        {
            if (ecoPrefabs == null) ecoPrefabs = Resources.LoadAll<GameObject>("Enemies");
            return ecoPrefabs.Length > 0 ? ecoPrefabs[Random.Range(0, ecoPrefabs.Length)] : null;
        }
        static GameObject[] ecoPrefabs;

        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
