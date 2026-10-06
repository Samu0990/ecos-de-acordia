using Aren.Combat;
using UnityEngine;

namespace Aren.Enemies
{
    /// <summary>
    /// SUSSURRANTE — "Eco Primordial · Rasgos" (prancha do autor). Devotos da Primeira Melodia cujas
    /// vozes foram tomadas pelos ecos da Fenda: um aldeão corrompido de 2,85 m com uma lâmina de
    /// obsidiana. Comportamento da prancha:
    ///  - persegue o jogador em MÉDIA distância (circula a ~5,5 m, espreitando) e entra rápido;
    ///  - ataca com CORTES RÁPIDOS (2–3 golpes encadeados, cada um com o anel de counter) e, de mais
    ///    longe, com uma INVESTIDA cortante (só esquiva: o "pré-eco" corre na frente);
    ///  - pode iniciar um GRITO que causa acúmulo de DISTORÇÃO (área; só se foge ou interrompe com
    ///    golpe pesado/habilidade);
    ///  - fica VULNERÁVEL depois dos ataques mais pesados (golpe pesado e grito): toma mais dano e
    ///    cambaleia fácil.
    /// Animações: UAL2 (CC0) retargetadas + clipes próprios de músculo (Grito, Morte, Ascensão)
    /// gerados pelo SussurranteSetup. Efeitos: SussurranteFX.
    /// </summary>
    public class EnemySussurrante : EnemyBase
    {
        public enum Atk { Slash, Lunge, Heavy, Scream }

        [Header("Sussurrante")]
        public float screamRadius = 8.5f;
        public float screamCooldown = 11f;
        public float heavyCooldown = 7f;
        public float distortionPerSecond = 34f;
        public float vulnerableTime = 1.7f;

        Animator anim;
        SussurranteFX fx;
        static readonly int MoveHash = Animator.StringToHash("MoveSpeed");
        static readonly int SpeedHash = Animator.StringToHash("StateSpeed");
        static readonly int LocoHash = Animator.StringToHash("LocoSpeed");
        float animMove, animMoveVelocity;

        Atk current;
        int comboLeft, comboIndex;
        float nextScream, nextHeavy, vulnerableUntil = -1f;
        bool screamStarted, heavyDouble;
        float screamTick;
        Vector3 lungeDir;
        bool cinematic;

        // contato medido nos clipes da UAL2 (velocidade máxima da mão direita, ArtSource/Sussurrante)
        const float ContactA = 0.267f, ContactB = 0.267f, ContactC = 0.667f, ContactDash = 0.33f, ContactHeavy = 0.40f, ContactHeavy2 = 1.83f;
        const float ScreamWindup = 1.05f, ScreamHold = 1.3f;

        public bool Vulnerable => Time.time < vulnerableUntil;

        /// <summary>-eda-no-suss: tira o Sussurrante da vila e dos encontros (A/B de desempenho, testes).</summary>
        public static bool Disabled => disabled ??= System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-eda-no-suss") >= 0;
        static bool? disabled;
        public override bool CounterWindowOpen => State == EnemyState.Telegraph && (current == Atk.Slash);

        protected override void Awake()
        {
            displayName = "Sussurrante";
            subtitle = "Eco Primordial · Rasgos — eles ainda se lembram de serem homens";
            base.Awake();
            anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
            // o EnemyBase põe 2 ossos por vértice em tudo; o LOD0 (perto da câmera) volta a 4
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (!smr.name.EndsWith("LOD1")) smr.quality = SkinQuality.Bone4;
            fx = GetComponent<SussurranteFX>();
            if (fx == null) fx = gameObject.AddComponent<SussurranteFX>();
            fx.Init();
            nextScream = Time.time + Random.Range(4f, 7f);
            nextHeavy = Time.time + Random.Range(3f, 6f);
        }

        /// <summary>Corpo de cena (a transformação na vila): sem IA até o fim da cena.</summary>
        public void SetCinematic(bool on)
        {
            cinematic = on;
            if (agent != null) agent.enabled = !on;
            var col = GetComponent<CapsuleCollider>(); if (col != null) col.enabled = !on;
            if (on) CombatRegistry.Unregister(this); else CombatRegistry.Register(this);
        }

        protected override void Start()
        {
            if (cinematic) return;
            if (quietSpawn) { Enter(EnemyState.Idle); return; }
            // não abre portal: se forma das lascas, dos pés para cima
            Enter(EnemyState.Spawning);
            fx.Materialize(spawnTime * 0.9f);
            fx.Say("Audio/Eleven/corrupt_transform", 0.6f, 0.85f);
            ArenVFX.Ring(transform.position + Vector3.up * 0.06f, 0.3f, 2.2f, spawnTime, SussurranteFX.Violet * 0.7f, 0.08f, true);
        }

        public void PlayCinematic(string state, float fade, float speed = 1f, float offset = 0f) => Play(state, fade, speed, offset);
        public SussurranteFX FX => fx;

        void Play(string state, float fade, float speed = 1f, float offset = 0f)
        {
            if (anim == null) return;
            anim.SetFloat(SpeedHash, speed);
            anim.CrossFadeInFixedTime(state, fade, 0, offset);
        }

        protected override void Update()
        {
            if (cinematic) return;
            base.Update();
            if (State == EnemyState.Attack && current == Atk.Scream) TickScreamHold();
        }

        // ------------------------------------------------------------ escolha de ataque

        protected override bool TryStartAttack(float dist)
        {
            float now = Time.time;
            bool screamOk = now >= nextScream && dist >= 3.0f && dist <= screamRadius - 1f;
            if (screamOk && Random.value < 0.75f) current = Atk.Scream;
            else if (dist <= attackRange + 0.35f)
            {
                current = (now >= nextHeavy && Random.value < 0.35f) ? Atk.Heavy : Atk.Slash;
                comboLeft = current == Atk.Slash ? Random.Range(1, 3) : 0;   // 2 ou 3 cortes no total
                comboIndex = 0;
            }
            else if (dist <= 7.5f && dist >= 3.6f && Random.value < 0.55f) current = Atk.Lunge;
            else return false;
            ConfigureAttack();
            Enter(EnemyState.Telegraph);
            return true;
        }

        void ConfigureAttack()
        {
            switch (current)
            {
                case Atk.Slash:
                    telegraphTime = comboIndex == 0 ? 0.56f : 0.3f;
                    attackActive = 0.2f; attackRecover = comboLeft > 0 ? 0f : 0.55f;
                    attackDamage = 12f; attackKnockback = 4.5f; attackRange = 2.7f; attackArc = 80f;
                    break;
                case Atk.Lunge:
                    telegraphTime = 0.5f; attackActive = 0.36f; attackRecover = 0.75f;
                    attackDamage = 15f; attackKnockback = 7f; attackArc = 70f;
                    break;
                case Atk.Heavy:
                    telegraphTime = 0.95f; attackActive = 1.6f; attackRecover = vulnerableTime;
                    attackDamage = 22f; attackKnockback = 9f; attackArc = 110f;
                    heavyDouble = Random.value < 0.6f;
                    break;
                case Atk.Scream:
                    telegraphTime = ScreamWindup; attackActive = ScreamHold; attackRecover = vulnerableTime;
                    break;
            }
        }

        protected override void OnTelegraphStart()
        {
            switch (current)
            {
                case Atk.Slash:
                    base.OnTelegraphStart();   // anel de counter + som
                    fx.SetCharge(0.6f);
                    if (comboIndex == 0) fx.Say(Random.value < 0.5f ? "Audio/Eleven/growl_a" : "Audio/Eleven/growl_c", 0.55f, Random.Range(0.78f, 0.88f));
                    break;
                case Atk.Lunge:
                {
                    Vector3 to = player != null ? Flat(player.position - transform.position) : transform.forward;
                    lungeDir = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward;
                    fx.SetCharge(0.9f);
                    fx.Say("Audio/Eleven/growl_b", 0.65f, 0.8f);
                    StartCoroutine(PreEcho(lungeDir, 2));
                    break;
                }
                case Atk.Heavy:
                {
                    fx.SetCharge(1f);
                    fx.PulseVeins(0.6f);
                    fx.Say("Audio/Eleven/growl_c", 0.75f, 0.7f);
                    Vector3 ground = transform.position + transform.forward * 2.2f + Vector3.up * 0.08f;
                    ArenVFX.Ring(ground, 0.3f, 2.6f, telegraphTime, SussurranteFX.Violet * 0.75f, 0.08f, true);
                    break;
                }
                case Atk.Scream:
                    screamStarted = false;
                    fx.Say("Audio/Eleven/inhale_vacuum", 0.85f, 0.9f);
                    fx.SetEyes(2.5f);
                    Hint(ref screamHint, "O <b>grito</b> do Sussurrante acumula <b>Distorção</b>: saia da onda (<b>Ctrl</b>) ou interrompa com <b>Q/E/R</b>", 6f);
                    break;
            }
        }

        System.Collections.IEnumerator PreEcho(Vector3 dir, int n)
        {
            for (int i = 1; i <= n; i++)
            {
                ArenVFX.Afterimage(gameObject, SussurranteFX.Violet, 0.7f, dir * (i * 2.2f), 0.65f);
                yield return new WaitForSeconds(0.1f);
            }
        }

        protected override void TickTelegraph()
        {
            SetMove(0f);
            float k = Mathf.Clamp01(stateTime / Mathf.Max(0.01f, telegraphTime));
            if (player != null && current != Atk.Lunge) FaceTowards(player.position - transform.position, current == Atk.Scream ? 0.35f : 0.6f);
            if (current == Atk.Lunge && player != null)
            {
                // ajusta a rota até a metade do aviso (depois fica comprometido)
                if (k < 0.5f)
                {
                    Vector3 to = Flat(player.position - transform.position);
                    if (to.sqrMagnitude > 0.01f) lungeDir = Vector3.RotateTowards(lungeDir, to.normalized, 2.5f * Time.deltaTime, 0f);
                }
                FaceTowards(lungeDir, 0.9f);
            }
            if (current == Atk.Scream) fx.ScreamWindup(k);
            if (current == Atk.Heavy) fx.SetCharge(0.6f + 0.4f * k);
            if (stateTime >= telegraphTime) Enter(EnemyState.Attack);
        }

        protected override void TickAttack()
        {
            switch (current)
            {
                case Atk.Slash:
                    if (!AttackHitDone && stateTime > 0.03f && stateTime < 0.17f) SwingHit(attackRange, attackArc, attackDamage, attackKnockback, HitKind.Light, 2.5f);
                    if (stateTime >= attackActive)
                    {
                        fx.Trail(false);
                        if (comboLeft > 0)
                        {
                            comboLeft--; comboIndex++;
                            ConfigureAttack();
                            Enter(EnemyState.Telegraph);
                        }
                        else Enter(EnemyState.Recover);
                    }
                    break;
                case Atk.Lunge:
                {
                    // investida: cobre a distância em ~0,3 s e corta no fim
                    if (stateTime < 0.3f)
                    {
                        Vector3 step = lungeDir * 9.5f * Time.deltaTime;
                        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Move(step); else transform.position += step;
                    }
                    if (!AttackHitDone && stateTime > 0.18f && stateTime < 0.34f) SwingHit(2.9f, 75f, attackDamage, attackKnockback, HitKind.Heavy, 3.5f);
                    if (stateTime >= attackActive) { fx.Trail(false); Enter(EnemyState.Recover); }
                    break;
                }
                case Atk.Heavy:
                {
                    // 1º golpe logo no começo (o aviso já levou o clipe até perto do contato); 2º opcional
                    float t2 = (ContactHeavy2 - ContactHeavy) / 1.05f;
                    if (!AttackHitDone && stateTime > 0.02f && stateTime < 0.16f) HeavyHit();
                    if (heavyDouble && AttackHitDone && stateTime > t2 - 0.05f && stateTime < t2 + 0.1f && !secondDone) { secondDone = true; HeavyHit(); }
                    fx.Trail((stateTime < 0.2f) || (heavyDouble && Mathf.Abs(stateTime - t2) < 0.2f));
                    float end = heavyDouble ? t2 + 0.35f : 0.75f;
                    if (stateTime >= end) { fx.Trail(false); BeginVulnerable(); Enter(EnemyState.Recover); }
                    break;
                }
                case Atk.Scream:
                    if (stateTime >= attackActive) { fx.ScreamEnd(); BeginVulnerable(); Enter(EnemyState.Recover); }
                    break;
            }
        }

        bool secondDone;

        void HeavyHit()
        {
            MarkAttackHit();
            Vector3 tip = transform.position + transform.forward * 2.3f;
            ArenVFX.Ring(new Vector3(tip.x, transform.position.y + 0.06f, tip.z), 0.2f, 3.2f, 0.45f, SussurranteFX.Violet, 0.16f, true);
            ArenVFX.Dust(tip + Vector3.up * 0.1f, Vector3.up * 1.2f, new Color(0.18f, 0.15f, 0.2f, 0.7f), 10, 1.0f);
            ArenVFX.Sparks(tip + Vector3.up * 0.2f, Vector3.up, SussurranteFX.Violet, 14, 6f, 80f);
            GameFeel.Shake(0.4f, transform.forward);
            fx.Say("Audio/Eleven/rock_burst", 0.7f, 1.1f);
            var p = CombatRegistry.Player;
            if (p == null || !p.Alive) return;
            Vector3 d = Flat(p.transform.position - transform.position);
            Vector3 dt = Flat(p.transform.position - tip);
            bool inArc = d.magnitude - p.BodyRadius <= 3.3f && Vector3.Angle(transform.forward, d) <= attackArc * 0.5f;
            if (inArc || dt.magnitude < 1.6f) HitPlayer(attackDamage, attackKnockback, d.sqrMagnitude > 0.01f ? d : transform.forward);
        }

        void SwingHit(float range, float arc, float dmg, float kb, HitKind kind, float lunge)
        {
            // pequeno avanço junto com o corte
            Vector3 fwd = transform.forward;
            Vector3 step = fwd * lunge * Time.deltaTime;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Move(step); else transform.position += step;
            var p = CombatRegistry.Player;
            if (p == null || !p.Alive) return;
            Vector3 d = Flat(p.transform.position - transform.position);
            if (d.magnitude - p.BodyRadius > range + 0.35f) return;
            if (Vector3.Angle(fwd, d) > arc) return;
            MarkAttackHit();
            var h = new HitData
            {
                damage = dmg, point = p.AimPoint - d.normalized * 0.3f, direction = d.normalized,
                knockback = kb, stagger = 0, kind = kind, team = Team.Enemy, source = gameObject
            };
            if (p.TakeHit(h)) ArenVFX.Sparks(p.AimPoint, -d.normalized, SussurranteFX.Violet, 6, 4f, 60f);
        }

        void TickScreamHold()
        {
            if (!screamStarted)
            {
                screamStarted = true;
                fx.ScreamRelease(screamRadius);
                fx.Say(Random.value < 0.5f ? "Audio/Eleven/scream_choir_a" : "Audio/Eleven/scream_choir_b", 1f, Random.Range(0.8f, 0.86f));
                fx.Say("Audio/Samples/el_sonic_pulse", 0.6f, 0.7f);
                screamTick = 0f;
            }
            // a onda "bate": acumula Distorção em quem estiver dentro, com pulsos visíveis
            screamTick -= Time.deltaTime;
            var p = CombatRegistry.Player;
            if (p != null && p.Alive)
            {
                Vector3 d = Flat(p.transform.position - transform.position);
                float r = d.magnitude;
                if (r <= screamRadius)
                {
                    float near = Mathf.Lerp(1.25f, 0.55f, r / screamRadius);
                    Distortion.Add(distortionPerSecond * near * Time.deltaTime, transform.position);
                    if (r < 2.4f && stateTime < 0.1f) HitPlayer(6f, 6f, d.sqrMagnitude > 0.01f ? d : transform.forward);
                }
            }
            if (screamTick <= 0f) { screamTick = 0.28f; fx.ScreamPulse(screamRadius); }
        }

        static bool screamHint, vulnerableHint;

        static void Hint(ref bool shown, string text, float seconds)
        {
            if (shown) return;
            shown = true;
            Aren.UI.ArenHUD.Instance?.ShowHint(text, seconds);
        }

        void BeginVulnerable()
        {
            vulnerableUntil = Time.time + vulnerableTime + 0.3f;
            fx.SetEyes(0.35f);   // exausto: os olhos quase apagam (janela de punição)
            Hint(ref vulnerableHint, "Depois do golpe pesado e do grito ele fica <b>exausto</b>: é a hora de castigar", 5f);
            if (current == Atk.Scream) nextScream = Time.time + screamCooldown * Random.Range(0.85f, 1.2f);
            if (current == Atk.Heavy) nextHeavy = Time.time + heavyCooldown * Random.Range(0.85f, 1.25f);
            fx.SetCharge(0f);
        }

        // ------------------------------------------------------------ dano

        protected override bool IsArmored => (State == EnemyState.Attack && current != Atk.Slash) ||
                                             (State == EnemyState.Telegraph && (current == Atk.Scream || current == Atk.Heavy));

        public override bool TakeHit(in HitData hit)
        {
            if (!Alive || hit.team == Team.Enemy) return false;
            var h = hit;
            if (Vulnerable) { h.damage *= 1.5f; h.stagger = Mathf.Max(h.stagger, stability * 0.6f) * 1.6f; }
            // golpe pesado/habilidade interrompe o grito no aviso (o leve só fere)
            bool interruptScream = State == EnemyState.Telegraph && current == Atk.Scream && h.kind != HitKind.Light;
            bool hit2 = base.TakeHit(h);
            if (hit2)
            {
                fx.HitBurst(h.point, h.direction, h.kind != HitKind.Light || Vulnerable);
                fx.Say(Random.value < 0.5f ? "Audio/Samples/el_enemy_hit1" : "Audio/Samples/el_enemy_hit2", 0.5f, Random.Range(0.7f, 0.82f));
                if (interruptScream && Alive && State == EnemyState.Telegraph) { fx.ScreamEnd(); nextScream = Time.time + 4f; Enter(EnemyState.Stunned); }
            }
            return hit2;
        }

        public override void OnCountered(Vector3 from)
        {
            fx.Trail(false);
            fx.SetCharge(0f);
            fx.HitBurst(AimPoint, Flat(transform.position - from), true);
            base.OnCountered(from);
        }

        protected override void Die(in HitData hit)
        {
            fx.Trail(false);
            fx.ScreamEnd();
            base.Die(hit);
            fx.DeathFX();
            fx.Say(Random.value < 0.5f ? "Audio/Eleven/disintegrate_a" : "Audio/Eleven/disintegrate_b", 0.9f, 0.9f);
            fx.Say("Audio/Eleven/scream_choir_b", 0.35f, 0.62f);
            Vector3 drop = transform.position + Vector3.up * 0.9f;
            StartCoroutine(DropLater(drop));
        }

        System.Collections.IEnumerator DropLater(Vector3 p)
        {
            yield return new WaitForSeconds(1.35f);
            EchoFragment.Spawn(transform.position + Vector3.up * 0.7f);
        }

        // ------------------------------------------------------------ animação

        protected override void OnEnterState(EnemyState s)
        {
            switch (s)
            {
                case EnemyState.Spawning: Play("Rise", 0f, 1f / Mathf.Max(0.3f, spawnTime) * 1.6f, 0f); break;
                case EnemyState.Idle:
                case EnemyState.Chase:
                case EnemyState.Circle: Play("Locomotion", 0.22f); fx.SetEyes(1f); break;
                case EnemyState.Telegraph:
                    switch (current)
                    {
                        case Atk.Slash:
                        {
                            // A, B, C: o aviso leva o clipe até pouco antes do contato
                            string st = comboIndex == 0 ? "SlashA" : (comboIndex == 1 ? "SlashB" : "SlashC");
                            float contact = comboIndex == 0 ? ContactA : (comboIndex == 1 ? ContactB : ContactC);
                            Play(st, comboIndex == 0 ? 0.12f : 0.06f, (contact - 0.07f) / telegraphTime, 0f);
                            break;
                        }
                        case Atk.Lunge: Play("Dash", 0.12f, (ContactDash - 0.12f) / telegraphTime, 0f); break;
                        case Atk.Heavy: Play("Heavy", 0.15f, (ContactHeavy - 0.06f) / telegraphTime, 0f); break;
                        case Atk.Scream: Play("Scream", 0.2f, 1f, 0f); break;
                    }
                    break;
                case EnemyState.Attack:
                    secondDone = false;
                    if (current == Atk.Scream) anim?.SetFloat(SpeedHash, 1f);
                    else if (current == Atk.Heavy) anim?.SetFloat(SpeedHash, 1.05f);
                    else anim?.SetFloat(SpeedHash, 1.25f);
                    if (current != Atk.Scream) { fx.Trail(true); fx.Say(Random.value < 0.5f ? "Audio/Samples/el_swing1" : "Audio/Samples/el_swing2", 0.8f, Random.Range(0.72f, 0.8f)); }
                    if (current != Atk.Scream) fx.Say("Sussurrante/Audio/suss_blade_ring", 0.5f, Random.Range(0.95f, 1.08f));
                    break;
                case EnemyState.Recover:
                    fx.SetCharge(0f);
                    if (Vulnerable) Play("Hurt", 0.25f, 0.55f, 0.15f);   // exausto: cambaleia (janela de punição)
                    else anim?.SetFloat(SpeedHash, 1f);
                    break;
                case EnemyState.Hurt: fx.Trail(false); fx.SetCharge(0f); Play("Hurt", 0.05f, 1.5f, 0.05f); break;
                case EnemyState.Stunned: fx.Trail(false); fx.SetCharge(0f); fx.ScreamEnd(); Play("Hurt", 0.05f, 0.5f, 0.05f); break;
                case EnemyState.Knockdown: fx.Trail(false); fx.SetCharge(0f); fx.ScreamEnd(); Play("Knock", 0.05f, 1.1f, 0.05f); break;
                case EnemyState.Dead: Play("Death", 0.08f, 1f, 0f); break;
            }
            if (s == EnemyState.Knockdown) Invoke(nameof(GetUpLater), knockdownTime - 1.0f);
            if (s == EnemyState.Knockdown) Invoke(nameof(FallSound), 0.45f);
            if (s == EnemyState.Dead) Invoke(nameof(FallSound), 0.7f);
        }

        void FallSound() { if (this != null) fx.Say("Audio/Eleven/body_fall", 0.65f, 0.78f); }

        void GetUpLater()
        {
            if (State == EnemyState.Knockdown) Play("GetUp", 0.2f, 1.3f, 0.1f);
        }

        protected override void SetMove(float speed)
        {
            animMove = Mathf.SmoothDamp(animMove, speed, ref animMoveVelocity, 0.12f, Mathf.Infinity, Time.deltaTime);
            if (anim == null) return;
            anim.SetFloat(MoveHash, animMove);
            // o andar de zumbi da UAL2 no corpo de 2,85 m cobre ~2,2 m/s; a corrida ~5,5 m/s
            float natural = animMove <= 2.2f ? 2.2f : 5.5f;
            anim.SetFloat(LocoHash, Mathf.Clamp(animMove / natural, 0.75f, 1.6f));
        }
    }
}
