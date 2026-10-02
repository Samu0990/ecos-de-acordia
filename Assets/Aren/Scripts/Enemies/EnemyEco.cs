using System.Collections.Generic;
using Aren.Combat;
using UnityEngine;

namespace Aren.Enemies
{
    /// <summary>
    /// Eco Possuído: aldeão tomado pela Corrupção (Bíblia de Lore — Campanula). Humanoide
    /// com clipes CC0 da UAL2 (andar/atacar de zumbi = movimento "fora do compasso").
    /// </summary>
    public class EnemyEco : EnemyBase
    {
        Animator anim;
        static readonly int MoveHash = Animator.StringToHash("MoveSpeed");
        static readonly int SpeedHash = Animator.StringToHash("StateSpeed");
        static readonly int LocoHash = Animator.StringToHash("LocoSpeed");
        float animMove, animMoveVelocity;
        bool rangedVariant;
        float rangedPulse;
        ResonanceOrbVisual rangedOrb;

        public bool IsRangedVariant => rangedVariant;
        public override bool CounterWindowOpen => !rangedVariant && base.CounterWindowOpen;

        protected override void Awake()
        {
            base.Awake();
            anim = GetComponentInChildren<Animator>();
            if (anim != null) anim.applyRootMotion = false;
        }

        void Play(string state, float fade, float speed = 1f, float offset = 0f)
        {
            if (anim == null) return;
            anim.SetFloat(SpeedHash, speed);
            anim.CrossFadeInFixedTime(state, fade, 0, offset);
        }

        /// <summary>Converte este Eco já instanciado em Eco Cantor, sem duplicar prefab.</summary>
        public void SetRangedVariant()
        {
            if (rangedVariant) return;
            rangedVariant = true;
            displayName = "Eco Cantor";
            subtitle = "uma nota corrompida procura o Aren";
            aggroRange = 28f;
            circleRadius = 11.5f;
            attackRange = 16f;
            attackCooldown = 2.35f;
            telegraphTime = 0.9f;
            attackActive = 0.2f;
            attackRecover = 0.75f;
            needsToken = false;   // um Cantor não ocupa a vaga dos dois atacantes corpo a corpo
            walkSpeed = 2.1f;
            chaseSpeed = 3.1f;
            rangedOrb = ArenVFX.CreateOrbVisual(transform, "CorruptBuffOrb", 0.2f, ArenVFX.CorruptColor);
            rangedOrb.transform.localPosition = Vector3.up * 1.55f;
            rangedOrb.SetVisual(ArenVFX.CorruptColor, 0.85f);
        }

        protected override bool TryStartAttack(float dist)
        {
            if (!rangedVariant) return base.TryStartAttack(dist);
            if (dist > attackRange) return false;
            Enter(EnemyState.Telegraph);
            return true;
        }

        protected override void OnTelegraphStart()
        {
            if (!rangedVariant) { base.OnTelegraphStart(); return; }
            rangedPulse = 0f;
            ArenAudio.Play(Sfx.EnemyTelegraph, AimPoint, 0.72f, 0.76f);
            ArenVFX.Ring(AimPoint, 0.08f, 0.82f, telegraphTime, ArenVFX.CorruptColor, 0.075f, false);
            ArenVFX.Lightning(AimPoint + Vector3.up * 0.55f, AimPoint, ArenVFX.CorruptColor, 0.24f);
        }

        protected override void TickTelegraph()
        {
            if (!rangedVariant) { base.TickTelegraph(); return; }
            SetMove(0f);
            if (player != null) FaceTowards(player.position - transform.position, 0.65f);
            rangedPulse -= Time.deltaTime;
            if (rangedPulse <= 0f)
            {
                rangedPulse = 0.18f;
                ArenVFX.Sparks(AimPoint + transform.forward * 0.3f, -transform.forward,
                    ArenVFX.CorruptColor, 1, 1.5f, 30f);
            }
            if (stateTime >= telegraphTime) Enter(EnemyState.Attack);
        }

        protected override void TickAttack()
        {
            if (!rangedVariant) { base.TickAttack(); return; }
            if (!AttackHitDone)
            {
                MarkAttackHit();
                Vector3 origin = AimPoint + transform.forward * 0.65f;
                Vector3 targetPoint = player != null ? player.position + Vector3.up * 1.05f : origin + transform.forward * 8f;
                var rb = player != null ? player.GetComponent<Rigidbody>() : null;
                if (rb != null) targetPoint += rb.linearVelocity * 0.18f;
                CorruptNoteProjectile.Spawn(origin, targetPoint - origin, 8.6f, 11f, gameObject);
                ArenVFX.Lightning(AimPoint, origin + transform.forward * 0.8f, ArenVFX.CorruptColor, 0.18f);
            }
            if (stateTime >= attackActive) Enter(EnemyState.Recover);
        }

        protected override void OnEnterState(EnemyState s)
        {
            switch (s)
            {
                case EnemyState.Spawning: Play("GetUp", 0.0f, 1.0f, 0.2f); break;
                case EnemyState.Idle:
                case EnemyState.Chase:
                case EnemyState.Circle: Play("Locomotion", 0.2f); break;
                // a garra sobe durante o aviso e desce no golpe (contato do clipe ≈ 0.62 s)
                case EnemyState.Telegraph: Play("Attack", 0.1f, (rangedVariant ? 0.36f : 0.45f) / telegraphTime, 0.05f); break;
                case EnemyState.Attack: anim?.SetFloat(SpeedHash, 1.4f); break;
                case EnemyState.Recover: anim?.SetFloat(SpeedHash, 1f); break;
                case EnemyState.Hurt: Play("Hurt", 0.05f, 1.6f, 0.05f); break;
                case EnemyState.Stunned: Play("Hurt", 0.05f, 0.55f, 0.05f); break;
                case EnemyState.Knockdown: Play("Knock", 0.05f, 1.2f, 0.05f); break;
                case EnemyState.Dead: Play("Knock", 0.05f, 1.1f, 0.05f); break;
            }
            if (s == EnemyState.Knockdown) Invoke(nameof(GetUpLater), knockdownTime - 0.9f);
            if (s == EnemyState.Knockdown || s == EnemyState.Dead) Invoke(nameof(FallSound), 0.42f);
        }

        void FallSound() => ArenAudio.Play(Sfx.BodyFall, transform.position, 0.55f, Random.Range(0.9f, 1.05f));

        void GetUpLater()
        {
            if (State == EnemyState.Knockdown) Play("GetUp", 0.2f, 1.4f, 0.1f);
        }

        protected override void Update()
        {
            base.Update();
            if (!rangedVariant || rangedOrb == null) return;
            float angle = Time.time * 115f * Mathf.Deg2Rad;
            rangedOrb.transform.position = AimPoint + Vector3.up * 0.42f
                + transform.right * (Mathf.Cos(angle) * 0.48f)
                + transform.forward * (Mathf.Sin(angle) * 0.22f);
            rangedOrb.SetVisual(ArenVFX.CorruptColor, Alive ? (State == EnemyState.Telegraph ? 1f : 0.72f) : 0f);
        }

        protected override void SetMove(float speed)
        {
            animMove = Mathf.SmoothDamp(animMove, speed, ref animMoveVelocity, 0.1f, Mathf.Infinity, Time.deltaTime);
            if (anim != null)
            {
                anim.SetFloat(MoveHash, animMove);
                // o clipe de andar é lento: acelera a animação em vez de deslizar os pés
                anim.SetFloat(LocoHash, Mathf.Max(1f, animMove / 1.4f));
            }
        }
    }

    /// <summary>Nota corrompida poolada disparada pelo Eco Cantor.</summary>
    public class CorruptNoteProjectile : MonoBehaviour
    {
        static readonly List<CorruptNoteProjectile> pool = new List<CorruptNoteProjectile>(8);
        static readonly RaycastHit[] hits = new RaycastHit[12];
        public static int SpawnedTotal { get; private set; }
        public static int ActiveCount
        {
            get { int n = 0; for (int i = 0; i < pool.Count; i++) if (pool[i] != null && pool[i].active) n++; return n; }
        }

        Vector3 direction;
        float speed, damage, life;
        GameObject owner;
        IDamageable target;
        ResonanceOrbVisual visual;
        bool active;

        public static CorruptNoteProjectile Spawn(Vector3 origin, Vector3 direction, float speed, float damage, GameObject owner)
        {
            CorruptNoteProjectile shot = null;
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null && !pool[i].active) { shot = pool[i]; break; }
            if (shot == null)
            {
                var go = new GameObject("CorruptNoteProjectile");
                shot = go.AddComponent<CorruptNoteProjectile>();
                shot.visual = ArenVFX.CreateOrbVisual(go.transform, "CorruptNote", 0.28f, ArenVFX.CorruptColor);
                pool.Add(shot);
            }
            shot.gameObject.SetActive(true);
            shot.transform.position = origin;
            shot.direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
            shot.speed = speed; shot.damage = damage; shot.owner = owner;
            shot.target = CombatRegistry.Player; shot.life = 3.2f; shot.active = true;
            shot.visual.transform.localPosition = Vector3.zero;
            shot.visual.transform.localScale = Vector3.one * 0.28f;
            shot.visual.SetVisual(ArenVFX.CorruptColor, 1f);
            shot.visual.ClearTrail();
            SpawnedTotal++;
            ArenVFX.Glyphs(origin, ArenVFX.CorruptColor, 2, 0.8f);
            return shot;
        }

        void Update()
        {
            if (!active) return;
            float dt = Time.deltaTime;
            life -= dt;
            if (life <= 0f || !CombatRegistry.IsValid(target)) { Finish(transform.position, -direction, false); return; }

            Vector3 desired = target.AimPoint - transform.position;
            if (desired.sqrMagnitude > 0.01f)
                direction = Vector3.RotateTowards(direction, desired.normalized, 1.15f * dt, 0f).normalized;
            float distance = speed * dt;
            Vector3 from = transform.position;
            Vector3 next = from + direction * distance;

            int count = Physics.SphereCastNonAlloc(from, 0.16f, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            RaycastHit wall = default;
            for (int i = 0; i < count; i++)
            {
                var col = hits[i].collider;
                if (col == null || (owner != null && col.transform.IsChildOf(owner.transform))) continue;
                if (col.GetComponentInParent<IDamageable>() != null || col.attachedRigidbody != null) continue;
                if (hits[i].distance < nearest) { nearest = hits[i].distance; wall = hits[i]; }
            }
            if (nearest < float.MaxValue)
            {
                transform.position = wall.point;
                Finish(wall.point, wall.normal, false);
                return;
            }

            transform.position = next;
            Vector3 segment = next - from;
            float u = segment.sqrMagnitude > 0.0001f
                ? Mathf.Clamp01(Vector3.Dot(target.AimPoint - from, segment) / segment.sqrMagnitude) : 1f;
            Vector3 closest = from + segment * u;
            if ((target.AimPoint - closest).magnitude <= 0.52f + target.BodyRadius * 0.45f)
            {
                Vector3 flat = direction; flat.y = 0f;
                if (flat.sqrMagnitude < 0.01f) flat = transform.forward;
                var hit = new HitData
                {
                    damage = damage, point = target.AimPoint, direction = flat.normalized,
                    knockback = 4.5f, stagger = 0f, kind = HitKind.Ability, team = Team.Enemy, source = owner
                };
                target.TakeHit(hit);
                Finish(target.AimPoint, -direction, true);
                return;
            }

            if (Random.value < dt * 12f)
                ArenVFX.Sparks(transform.position, -direction, ArenVFX.CorruptColor, 1, 1.8f, 18f);
        }

        void Finish(Vector3 point, Vector3 normal, bool hitPlayer)
        {
            if (!active) return;
            active = false;
            ArenVFX.Impact(point, normal, ArenVFX.CorruptColor, hitPlayer ? 0.9f : 0.65f);
            ArenVFX.Ring(point, 0.08f, hitPlayer ? 0.85f : 0.55f, 0.2f, ArenVFX.VoidColor * 1.5f, 0.045f, false);
            ArenAudio.Play(Sfx.BladeHit, point, hitPlayer ? 0.55f : 0.35f, 0.68f);
            visual.SetVisual(ArenVFX.CorruptColor, 0f);
            gameObject.SetActive(false);
        }

        void OnDisable() { active = false; }
    }
}
