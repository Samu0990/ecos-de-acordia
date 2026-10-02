using Aren.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Aren.Enemies
{
    public enum EnemyState { Spawning, Idle, Chase, Circle, Telegraph, Attack, Recover, Hurt, Stunned, Knockdown, Dead }

    /// <summary>
    /// IA base dos possuídos (doc Freeflow §20–24): persegue, circula esperando a vez
    /// (ThreatDirector), avisa o golpe com uma janela de counter legível, ataca, cambaleia,
    /// cai e se dissolve. Movimento por NavMeshAgent quando há NavMesh; senão, passo
    /// direto com separação entre inimigos e encaixe no chão.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public abstract class EnemyBase : MonoBehaviour, IDamageable, ICounterable
    {
        [Header("Identidade")]
        public string displayName = "Eco Possuído";
        public string subtitle = "";
        public bool isBoss;

        [Header("Vida")]
        public float maxHealth = 40f;
        public float stability = 30f;
        public float staggerRecover = 10f;

        [Header("Corpo")]
        public float bodyRadius = 0.45f;
        public float aimHeight = 1.1f;
        public float headHeight = 2.0f;

        [Header("Movimento")]
        public float walkSpeed = 2.0f;
        public float chaseSpeed = 3.4f;
        public float turnSpeed = 540f;
        public float circleRadius = 3.8f;
        public float aggroRange = 20f;

        [Header("Ataque")]
        public float attackRange = 1.8f;
        public float telegraphTime = 0.62f;
        public float attackActive = 0.14f;
        public float attackRecover = 0.75f;
        public float attackDamage = 12f;
        public float attackKnockback = 5f;
        public float attackCooldown = 1.4f;
        public float attackLungeSpeed = 4f;
        public float attackArc = 75f;

        [Tooltip("Multiplica o empurrão recebido (chefe quase não é empurrado).")]
        public float knockbackScale = 1f;
        [Tooltip("Precisa da ficha do ThreatDirector para atacar (chefe não precisa).")]
        public bool needsToken = true;

        [Header("Reações")]
        public float spawnTime = 1.2f;
        public float hurtTime = 0.32f;
        public float stunTime = 1.5f;
        public float knockdownTime = 1.9f;
        public float deathTime = 1.6f;

        public EnemyState State { get; private set; }
        public float Health { get; private set; }
        public float NormalizedHealth => Health / maxHealth;
        public float LastDamagedTime { get; private set; } = -100f;
        public bool HoldsToken { get; private set; }
        public float StateTime => stateTime;

        public Team Team => Team.Enemy;
        public bool Alive => State != EnemyState.Dead && Health > 0f;
        public Vector3 AimPoint => transform.position + Vector3.up * aimHeight;
        public float BodyRadius => bodyRadius;
        public Vector3 HeadPoint => transform.position + Vector3.up * headHeight;

        public virtual bool CounterWindowOpen => State == EnemyState.Telegraph;
        public float CounterWindowProgress => Mathf.Clamp01(stateTime / telegraphTime);

        public event System.Action<EnemyBase> OnDied;

        protected Transform player;
        protected NavMeshAgent agent;
        protected Renderer[] renderers;
        protected MaterialPropertyBlock mpb;
        protected float stateTime;
        protected float stagger;
        protected float lastAttackEnd = -10f;
        protected float flash;
        protected Vector3 knockVel;
        protected float circleAngle, circleDir = 1f, circleSwitchAt;
        bool attackHitDone;
        float nextPathTime;
        CapsuleCollider capsule;
        static readonly int IdFlash = Shader.PropertyToID("_Flash");
        static readonly int IdTele = Shader.PropertyToID("_Telegraph");
        static readonly int IdDis = Shader.PropertyToID("_Dissolve");

        protected virtual void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            capsule = GetComponent<CapsuleCollider>();
            renderers = GetComponentsInChildren<Renderer>();
            mpb = new MaterialPropertyBlock();
            Health = maxHealth;
            circleAngle = Random.value * 360f;
            circleDir = Random.value < 0.5f ? -1f : 1f;
            var rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            if (agent != null) { agent.updateRotation = false; agent.autoBraking = true; }
            if (GetComponent<BlobShadow>() == null) gameObject.AddComponent<BlobShadow>().radius = bodyRadius * 1.5f;
            // clipes da UAL2 (garra do zumbi, pancada/investida do cervo) giram o tronco para
            // o lado/de costas; a trava mantém o corpo virado para quem ele está atacando
            if (GetComponent<TorsoFacingLock>() == null) gameObject.AddComponent<TorsoFacingLock>();
        }

        protected virtual void OnEnable() => CombatRegistry.Register(this);
        protected virtual void OnDisable()
        {
            CombatRegistry.Unregister(this);
            if (HoldsToken) { ThreatDirector.Release(this); HoldsToken = false; }
        }

        protected virtual void Start()
        {
            Enter(EnemyState.Spawning);
            ArenAudio.Play(Sfx.EnemySpawn, transform.position, 0.7f);
            ArenVFX.CorruptionBurst(transform.position + Vector3.up * 0.4f, 1.1f);
            ArenVFX.SpawnPortal(transform.position, bodyRadius * 3.2f, spawnTime + 0.4f);
        }

        // ------------------------------------------------------------ estados

        protected void Enter(EnemyState s)
        {
            if (State == EnemyState.Dead && s != EnemyState.Dead) return;
            if (HoldsToken && s != EnemyState.Telegraph && s != EnemyState.Attack && s != EnemyState.Chase)
            {
                ThreatDirector.Release(this); HoldsToken = false;
            }
            State = s;
            stateTime = 0f;
            if (s == EnemyState.Telegraph) OnTelegraphStart();
            if (s == EnemyState.Attack) { attackHitDone = false; ArenAudio.Play(Sfx.EnemyAttack, AimPoint, 0.6f); }
            OnEnterState(s);
        }

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            stateTime += dt;
            flash = Mathf.MoveTowards(flash, 0f, dt * 7f);
            stagger = Mathf.Max(0f, stagger - staggerRecover * dt);
            if (player == null && CombatRegistry.Player != null) player = CombatRegistry.Player.transform;

            float dist = player != null ? Flat(player.position - transform.position).magnitude : 999f;
            bool playerAlive = CombatRegistry.Player != null && CombatRegistry.Player.Alive;

            switch (State)
            {
                case EnemyState.Spawning:
                    if (stateTime >= spawnTime) Enter(EnemyState.Chase);
                    break;
                case EnemyState.Idle:
                    SetMove(0f);
                    if (playerAlive && dist < aggroRange) Enter(EnemyState.Chase);
                    break;
                case EnemyState.Chase:
                    if (!playerAlive) { Enter(EnemyState.Idle); break; }
                    if (!needsToken)
                    {
                        if (Time.time - lastAttackEnd > attackCooldown && TryStartAttack(dist)) break;
                    }
                    else
                    {
                        if (!HoldsToken && dist < circleRadius + 1.2f)
                        {
                            if (Time.time - lastAttackEnd > attackCooldown && ThreatDirector.RequestToken(this)) HoldsToken = true;
                            else { Enter(EnemyState.Circle); break; }
                        }
                        if (HoldsToken && TryStartAttack(dist)) break;
                    }
                    MoveTo(player.position, HoldsToken ? chaseSpeed : walkSpeed + 0.6f);
                    FaceTowards(player.position - transform.position);
                    break;
                case EnemyState.Circle:
                    if (!playerAlive) { Enter(EnemyState.Idle); break; }
                    if (Time.time > circleSwitchAt) { circleSwitchAt = Time.time + Random.Range(2f, 4.5f); if (Random.value < 0.45f) circleDir = -circleDir; }
                    circleAngle += circleDir * (walkSpeed * 0.55f / circleRadius) * Mathf.Rad2Deg * dt;
                    Vector3 slot = player.position + Quaternion.Euler(0, circleAngle, 0) * Vector3.forward * circleRadius;
                    MoveTo(slot, walkSpeed * 0.8f);
                    FaceTowards(player.position - transform.position);
                    if (stateTime > 0.6f && Time.time - lastAttackEnd > attackCooldown && ThreatDirector.RequestToken(this))
                    { HoldsToken = true; Enter(EnemyState.Chase); }
                    else if (dist > circleRadius + 4f) Enter(EnemyState.Chase);
                    break;
                case EnemyState.Telegraph:
                    TickTelegraph();
                    break;
                case EnemyState.Attack:
                    TickAttack();
                    break;
                case EnemyState.Recover:
                    SetMove(0f);
                    if (stateTime >= attackRecover)
                    {
                        lastAttackEnd = Time.time;
                        ThreatDirector.Release(this); HoldsToken = false;
                        Enter(EnemyState.Chase);
                    }
                    break;
                case EnemyState.Hurt:
                    SetMove(0f);
                    if (stateTime >= hurtTime) Enter(EnemyState.Chase);
                    break;
                case EnemyState.Stunned:
                    SetMove(0f);
                    if (stateTime >= stunTime) Enter(EnemyState.Chase);
                    break;
                case EnemyState.Knockdown:
                    SetMove(0f);
                    if (stateTime >= knockdownTime) Enter(EnemyState.Chase);
                    break;
                case EnemyState.Dead:
                    SetMove(0f);
                    UpdateDissolve(Mathf.Clamp01((stateTime - deathTime * 0.35f) / (deathTime * 0.65f)));
                    if (stateTime >= deathTime) gameObject.SetActive(false);
                    break;
            }

            ApplyKnockback(dt);
            UpdateVisuals();
        }

        // ------------------------------------------------------------ ganchos de ataque (o chefe sobrescreve)

        protected virtual void OnTelegraphStart()
        {
            CounterIndicator.Show(this);
            ArenAudio.Play(Sfx.EnemyTelegraph, AimPoint, 0.85f);
        }

        protected virtual bool TryStartAttack(float dist)
        {
            if (dist > attackRange) return false;
            Enter(EnemyState.Telegraph);
            return true;
        }

        protected virtual void TickTelegraph()
        {
            SetMove(0f);
            if (player != null) FaceTowards(player.position - transform.position, 0.5f);
            if (stateTime >= telegraphTime) Enter(EnemyState.Attack);
        }

        protected virtual void TickAttack()
        {
            if (!attackHitDone) TryHitPlayer();
            if (stateTime >= attackActive) Enter(EnemyState.Recover);
        }

        protected void MarkAttackHit() => attackHitDone = true;
        protected bool AttackHitDone => attackHitDone;
        protected void EndAttackNow() { lastAttackEnd = Time.time; }

        /// <summary>Dano direto no jogador (usado pelos ataques especiais do chefe).</summary>
        protected bool HitPlayer(float damage, float knockback, Vector3 dir)
        {
            var p = CombatRegistry.Player;
            if (p == null || !p.Alive) return false;
            var h = new HitData
            {
                damage = damage, point = p.AimPoint, direction = Flat(dir).normalized,
                knockback = knockback, stagger = 0, kind = HitKind.Heavy, team = Team.Enemy, source = gameObject
            };
            return p.TakeHit(h);
        }

        // ------------------------------------------------------------ movimento

        protected static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        bool UseAgent => agent != null && agent.enabled && agent.isOnNavMesh;

        protected void MoveTo(Vector3 target, float speed)
        {
            if (UseAgent)
            {
                agent.speed = speed;
                agent.isStopped = false;
                if (Time.time >= nextPathTime) { agent.SetDestination(target); nextPathTime = Time.time + 0.25f; }
                SetMove(agent.velocity.magnitude);
                return;
            }
            Vector3 to = Flat(target - transform.position);
            float d = to.magnitude;
            Vector3 step = d > 0.05f ? to / d * Mathf.Min(speed * Time.deltaTime, d) : Vector3.zero;
            step += Separation() * Time.deltaTime;
            transform.position += step;
            SnapToGround();
            SetMove(step.magnitude / Mathf.Max(Time.deltaTime, 0.0001f));
        }

        Vector3 Separation()
        {
            Vector3 push = Vector3.zero;
            var list = CombatRegistry.Enemies;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i] as EnemyBase;
                if (e == null || e == this || !e.Alive) continue;
                Vector3 d = Flat(transform.position - e.transform.position);
                float m = d.magnitude;
                float min = bodyRadius + e.bodyRadius + 0.5f;
                if (m < min && m > 0.001f) push += d / m * (min - m) * 4f;
            }
            if (player != null)
            {
                Vector3 d = Flat(transform.position - player.position);
                float m = d.magnitude;
                float min = bodyRadius + 0.6f;
                if (m < min && m > 0.001f) push += d / m * (min - m) * 6f;
            }
            return push;
        }

        protected void SnapToGround()
        {
            Vector3 o = transform.position + Vector3.up * 1.2f;
            if (Physics.Raycast(o, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider != capsule && !hit.collider.transform.IsChildOf(transform))
                    transform.position = new Vector3(transform.position.x, Mathf.Lerp(transform.position.y, hit.point.y, 0.5f), transform.position.z);
            }
        }

        protected void StopAgent()
        {
            if (UseAgent) { agent.isStopped = true; agent.ResetPath(); }
        }

        protected void FaceTowards(Vector3 dir, float speedMul = 1f)
        {
            dir.y = 0;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * speedMul * Time.deltaTime);
        }

        void ApplyKnockback(float dt)
        {
            if (knockVel.sqrMagnitude < 0.0004f) { knockVel = Vector3.zero; return; }
            Vector3 step = knockVel * dt;
            // não atravessa paredes
            if (Physics.SphereCast(AimPoint, bodyRadius * 0.8f, step.normalized, out var hit, step.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform) && hit.collider.attachedRigidbody == null)
            {
                knockVel = Vector3.Reflect(knockVel, hit.normal) * 0.3f;
                step = Vector3.zero;
            }
            if (UseAgent) agent.Move(step);
            else { transform.position += step; SnapToGround(); }
            knockVel = Vector3.MoveTowards(knockVel, Vector3.zero, 16f * dt);
        }

        // ------------------------------------------------------------ ataque

        void TryHitPlayer()
        {
            // pequeno avanço junto com o golpe
            Vector3 fwd = transform.forward;
            if (UseAgent) agent.Move(fwd * attackLungeSpeed * Time.deltaTime);
            else transform.position += fwd * attackLungeSpeed * Time.deltaTime;

            var p = CombatRegistry.Player;
            if (p == null || !p.Alive) return;
            Vector3 d = Flat(p.transform.position - transform.position);
            if (d.magnitude - p.BodyRadius > attackRange + 0.35f) return;
            if (Vector3.Angle(fwd, d) > attackArc) return;
            attackHitDone = true;
            var h = new HitData
            {
                damage = attackDamage, point = p.AimPoint - d.normalized * 0.3f, direction = d.normalized,
                knockback = attackKnockback, stagger = 0, kind = HitKind.Light, team = Team.Enemy, source = gameObject
            };
            p.TakeHit(h);
        }

        // ------------------------------------------------------------ dano

        public virtual bool TakeHit(in HitData hit)
        {
            if (!Alive || hit.team == Team.Enemy) return false;
            Health = Mathf.Max(0f, Health - hit.damage);
            LastDamagedTime = Time.time;
            flash = 1f;
            stagger += hit.stagger;
            knockVel = Flat(hit.direction).normalized * hit.knockback * knockbackScale;
            ArenAudio.Play(Sfx.EnemyHurt, AimPoint, 0.6f, Random.Range(0.9f, 1.1f));
            ArenVFX.Sparks(AimPoint, hit.direction, ArenVFX.CorruptColor, 6, 5f, 60f);

            if (Health <= 0f) { Die(hit); return true; }

            bool armored = IsArmored;   // golpe saindo não é interrompido por golpe leve
            if (hit.kind == HitKind.Contracanto || stagger >= stability * 2f)
            { stagger = 0; knockVel *= 1.4f; Enter(EnemyState.Knockdown); }
            else if (stagger >= stability)
            { stagger = 0; Enter(EnemyState.Stunned); }
            else if (!armored || hit.kind != HitKind.Light)
            {
                if (State != EnemyState.Stunned && State != EnemyState.Knockdown) Enter(EnemyState.Hurt);
                else OnHitWhileDown();
            }
            return true;
        }

        protected virtual void OnHitWhileDown() { }
        protected virtual bool IsArmored => State == EnemyState.Attack;

        public virtual void OnCountered(Vector3 from)
        {
            if (!Alive) return;
            knockVel = Flat(transform.position - from).normalized * 3.5f * knockbackScale;
            stagger = 0f;
            Enter(EnemyState.Stunned);
        }

        protected virtual void Die(in HitData hit)
        {
            knockVel = Flat(hit.direction).normalized * (hit.knockback + 2f);
            Enter(EnemyState.Dead);
            if (capsule != null) capsule.enabled = false;
            CombatRegistry.Unregister(this);
            ArenAudio.Play(Sfx.EnemyDeath, AimPoint, 0.9f);
            ArenVFX.CorruptionBurst(AimPoint, 1.3f);
            ArenVFX.SoulMotes(AimPoint, isBoss ? 40 : 14);   // o eco volta para a Fenda
            OnDied?.Invoke(this);
        }

        // ------------------------------------------------------------ visual

        void UpdateVisuals()
        {
            float tele = State == EnemyState.Telegraph ? Mathf.Clamp01(stateTime / telegraphTime) : 0f;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(IdFlash, flash * flash);
                mpb.SetFloat(IdTele, tele);
                r.SetPropertyBlock(mpb);
            }
        }

        void UpdateDissolve(float k)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetFloat(IdDis, k);
                r.SetPropertyBlock(mpb);
            }
            if (k > 0f && Random.value < 0.25f) ArenVFX.SoulMotes(transform.position + Vector3.up * Random.Range(0.3f, 1.7f), 1);
            if (k > 0f && Random.value < 0.35f) ArenVFX.Dust(transform.position + Vector3.up * Random.Range(0.2f, 1.6f), Vector3.up * 1.5f, new Color(0.05f, 0.02f, 0.07f, 0.7f), 1, 0.5f);
        }

        /// <summary>Reinicia (encontros reaproveitam inimigos ao renascer o jogador).</summary>
        public void ResetEnemy(Vector3 pos, Quaternion rot)
        {
            gameObject.SetActive(true);
            if (UseAgent) agent.Warp(pos); else transform.position = pos;
            transform.rotation = rot;
            Health = maxHealth; stagger = 0; knockVel = Vector3.zero; flash = 0;
            if (capsule != null) capsule.enabled = true;
            State = EnemyState.Idle;
            UpdateDissolve(0f);
            CombatRegistry.Register(this);
            Enter(EnemyState.Spawning);
            ArenVFX.SpawnPortal(transform.position, bodyRadius * 3.2f, spawnTime + 0.4f);
        }

        protected abstract void OnEnterState(EnemyState s);
        protected abstract void SetMove(float speed);
    }
}
