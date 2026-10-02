using Aren.Combat;
using UnityEngine;

namespace Aren.Enemies
{
    /// <summary>
    /// O Cervo Corrompido — o primeiro possuído de Campanula (Bíblia de Lore: "seus passos
    /// passaram a acontecer meio segundo antes de suas pernas se moverem").
    ///
    /// Três ataques com leitura diferente:
    /// - Garra (perto): aviso com anel dourado → contra-atacável.
    /// - Pancada (muito perto): sobe os braços e bate no chão, onda de choque → contra-atacável.
    /// - Investida (média distância): SEM anel; um eco fantasma corre o caminho antes dele
    ///   (o passo que chega antes da perna) → só esquiva.
    /// Na metade da vida ruge, invoca dois Ecos e fica mais rápido.
    /// </summary>
    public class EnemyDeer : EnemyBase
    {
        public GameObject minionPrefab;

        enum Atk { Claw, Slam, Charge }
        Atk current;
        Animator anim;
        float animMove;
        bool phase2, roaring;
        float roarT;
        Vector3 chargeDir, chargeStart;
        float chargeTravel;
        float stepTimer;
        static readonly int MoveHash = Animator.StringToHash("MoveSpeed");
        static readonly int SpeedHash = Animator.StringToHash("StateSpeed");
        static readonly int LocoHash = Animator.StringToHash("LocoSpeed");

        protected override bool IsArmored => State == EnemyState.Telegraph || State == EnemyState.Attack || roaring;

        protected override void Awake()
        {
            // valores do chefe ANTES do base.Awake (ele copia maxHealth para a vida atual)
            needsToken = false;
            isBoss = true;
            displayName = "O Cervo Corrompido";
            subtitle = "o primeiro possuído — seus passos chegam antes das pernas";
            maxHealth = 300f; stability = 140f; staggerRecover = 18f;
            bodyRadius = 0.75f; aimHeight = 1.6f; headHeight = 2.7f;
            walkSpeed = 2.4f; chaseSpeed = 3.6f; circleRadius = 5f; aggroRange = 40f;
            attackCooldown = 1.1f; knockbackScale = 0.2f;
            stunTime = 1.1f; knockdownTime = 2.2f; deathTime = 2.6f; hurtTime = 0.18f; spawnTime = 1.6f;
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

        float Faster => phase2 ? 0.8f : 1f;

        public override bool CounterWindowOpen => State == EnemyState.Telegraph && current != Atk.Charge;

        // ------------------------------------------------------------ escolha de ataque

        protected override bool TryStartAttack(float dist)
        {
            if (roaring) return false;
            if (dist <= 3.4f && Random.value < 0.45f) current = Atk.Slam;
            else if (dist <= 3.0f) current = Atk.Claw;
            else if (dist >= 5.5f && dist <= 17f) current = Atk.Charge;
            else return false;
            switch (current)
            {
                case Atk.Claw: telegraphTime = 0.72f * Faster; attackActive = 0.18f; attackRecover = 0.6f; attackDamage = 18f; attackKnockback = 6f; attackRange = 3.0f; attackArc = 85f; break;
                case Atk.Slam: telegraphTime = 0.95f * Faster; attackActive = 0.12f; attackRecover = 0.95f; break;
                case Atk.Charge: telegraphTime = 0.85f * Faster; attackActive = 1.15f; attackRecover = 0.9f; break;
            }
            Enter(EnemyState.Telegraph);
            return true;
        }

        protected override void OnTelegraphStart()
        {
            if (current == Atk.Charge)
            {
                // sem anel: o "pré-eco" fantasma corre o caminho primeiro
                ArenAudio.Play(Sfx.DeerCharge, AimPoint, 1f);
                Vector3 to = player != null ? Flat(player.position - transform.position) : transform.forward;
                chargeDir = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward;
                ArenVFX.Lightning(AimPoint, AimPoint + chargeDir * 4.5f, ArenVFX.CorruptColor, 0.35f);
                StartCoroutine(PreEcho());
            }
            else
            {
                base.OnTelegraphStart();
                if (current == Atk.Slam)
                {
                    ArenAudio.Play(Sfx.DeerGrowl, AimPoint, 0.9f, 0.85f);
                    Vector3 ground = transform.position + transform.forward * 1.3f + Vector3.up * 0.1f;
                    ArenVFX.Lightning(AimPoint + Vector3.up * 2.4f, ground, ArenVFX.CorruptColor, 0.42f);
                    ArenVFX.Ring(ground, 0.25f, 2.4f, telegraphTime, ArenVFX.CorruptColor * 0.65f, 0.07f, true);
                }
            }
        }

        System.Collections.IEnumerator PreEcho()
        {
            // três fantasmas ao longo da rota, do mais perto ao mais longe
            for (int i = 1; i <= 3; i++)
            {
                Vector3 off = chargeDir * (i * 3.6f);
                ArenVFX.Afterimage(gameObject, ArenVFX.CorruptColor, 0.9f, off, 0.75f);
                ArenAudio.Play(Sfx.DeerStep, transform.position + off, 0.7f, 0.8f);
                ArenVFX.Dust(transform.position + off + Vector3.up * 0.1f, Vector3.up * 0.6f, new Color(0.2f, 0.05f, 0.1f, 0.6f), 3, 0.8f);
                yield return new WaitForSeconds(0.12f);
            }
        }

        // ------------------------------------------------------------ estados

        protected override void OnEnterState(EnemyState s)
        {
            switch (s)
            {
                case EnemyState.Spawning:
                    Play("Roar", 0f, 1f, 0f);
                    ArenAudio.Play(Sfx.DeerGrowl, AimPoint, 1f, 0.8f);
                    break;
                case EnemyState.Idle:
                case EnemyState.Chase:
                case EnemyState.Circle: Play("Locomotion", 0.2f); break;
                case EnemyState.Telegraph:
                    if (current == Atk.Claw) Play("Attack", 0.1f, 0.45f / telegraphTime, 0.05f);
                    else if (current == Atk.Slam) Play("Slam", 0.12f, 0.33f / telegraphTime, 0.02f);
                    else Play("ChargeWind", 0.12f, 0.6f, 0.0f);
                    break;
                case EnemyState.Attack:
                    if (current == Atk.Claw) anim?.SetFloat(SpeedHash, 1.5f);
                    else if (current == Atk.Slam) anim?.SetFloat(SpeedHash, 1.2f);
                    else
                    {
                        Play("Charge", 0.05f, 1.6f, 0.05f);
                        chargeStart = transform.position; chargeTravel = 0f;
                        transform.rotation = Quaternion.LookRotation(chargeDir);
                    }
                    break;
                case EnemyState.Recover: anim?.SetFloat(SpeedHash, 1f); break;
                case EnemyState.Hurt: break;   // chefe não interrompe a animação a cada golpe leve
                case EnemyState.Stunned: Play("Stagger", 0.06f, 0.8f, 0.05f); break;
                case EnemyState.Knockdown: Play("Knock", 0.06f, 1.0f, 0.05f); Invoke(nameof(GetUp), knockdownTime - 1.0f); break;
                case EnemyState.Dead: Play("Knock", 0.06f, 0.8f, 0.05f); break;
            }
        }

        void GetUp() { if (State == EnemyState.Knockdown) Play("GetUp", 0.2f, 1.3f, 0.1f); }

        protected override void TickTelegraph()
        {
            SetMove(0f);
            if (player != null && current != Atk.Charge) FaceTowards(player.position - transform.position, 0.6f);
            if (current == Atk.Charge)
            {
                // trava a direção um pouco antes de sair (dá para ler e esquivar)
                if (stateTime < telegraphTime * 0.6f && player != null)
                {
                    Vector3 to = Flat(player.position - transform.position);
                    if (to.sqrMagnitude > 0.01f) chargeDir = Vector3.Slerp(chargeDir, to.normalized, 0.1f).normalized;
                }
                FaceTowards(chargeDir, 2f);
            }
            if (stateTime >= telegraphTime) Enter(EnemyState.Attack);
        }

        protected override void TickAttack()
        {
            switch (current)
            {
                case Atk.Claw:
                    base.TickAttack();
                    break;
                case Atk.Slam:
                    if (!AttackHitDone)
                    {
                        MarkAttackHit();
                        Vector3 c = transform.position + transform.forward * 1.3f;
                        ArenVFX.Lightning(c + Vector3.up * 4.5f, c + Vector3.up * 0.1f, ArenVFX.CorruptColor, 0.3f);
                        ArenVFX.Ring(c + Vector3.up * 0.08f, 0.5f, 5f, 0.45f, ArenVFX.CorruptColor, 0.18f, true);
                        ArenVFX.CorruptionBurst(c + Vector3.up * 0.3f, 1.5f);
                        for (int i = 0; i < 14; i++)
                        {
                            float a = i / 14f * Mathf.PI * 2f;
                            Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                            ArenVFX.Dust(c + d * 0.8f + Vector3.up * 0.2f, d * 6f + Vector3.up, new Color(0.4f, 0.32f, 0.26f, 0.6f), 1, 1f);
                        }
                        ArenAudio.Play(Sfx.PulseBoom, c, 0.9f, 0.6f);
                        GameFeel.Shake(0.5f);
                        var p = CombatRegistry.Player;
                        if (p != null && p.Alive)
                        {
                            Vector3 d = Flat(p.transform.position - c);
                            if (d.magnitude < 4.8f) HitPlayer(22f, 8f, d.sqrMagnitude > 0.01f ? d : transform.forward);
                        }
                    }
                    if (stateTime >= attackActive) Enter(EnemyState.Recover);
                    break;
                case Atk.Charge:
                    float speed = 13f * (phase2 ? 1.15f : 1f);
                    Vector3 step = chargeDir * speed * Time.deltaTime;
                    // para em parede
                    if (Physics.SphereCast(AimPoint, 0.6f, chargeDir, out var wall, step.magnitude + 0.4f, ~0, QueryTriggerInteraction.Ignore)
                        && !wall.collider.transform.IsChildOf(transform) && wall.collider.attachedRigidbody == null)
                    {
                        GameFeel.Shake(0.35f);
                        ArenVFX.CorruptionBurst(wall.point, 1f);
                        Enter(EnemyState.Recover);
                        break;
                    }
                    MoveRaw(step);
                    chargeTravel += step.magnitude;
                    stepTimer -= Time.deltaTime;
                    if (stepTimer <= 0f) { stepTimer = 0.16f; ArenAudio.Play(Sfx.DeerStep, transform.position, 0.8f); ArenVFX.Dust(transform.position + Vector3.up * 0.1f, -chargeDir * 2f + Vector3.up, new Color(0.45f, 0.38f, 0.3f, 0.55f), 2, 0.9f); }
                    if (!AttackHitDone)
                    {
                        var pl = CombatRegistry.Player;
                        if (pl != null && pl.Alive)
                        {
                            Vector3 d = Flat(pl.transform.position - transform.position);
                            if (d.magnitude < bodyRadius + 0.9f && Vector3.Dot(d.normalized, chargeDir) > -0.2f)
                            {
                                MarkAttackHit();
                                HitPlayer(25f, 11f, chargeDir + Vector3.Cross(Vector3.up, chargeDir) * (Vector3.Dot(d, Vector3.Cross(Vector3.up, chargeDir)) > 0 ? 0.6f : -0.6f));
                            }
                        }
                    }
                    if (stateTime >= attackActive || chargeTravel > 18f) Enter(EnemyState.Recover);
                    break;
            }
        }

        void MoveRaw(Vector3 step)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Move(step);
            else { transform.position += step; SnapToGround(); }
        }

        protected override void Update()
        {
            base.Update();
            if (!Alive) return;
            // segunda fase: rugido + dois Ecos + mais rápido
            if (!phase2 && NormalizedHealth < 0.5f && (State == EnemyState.Chase || State == EnemyState.Circle))
            {
                phase2 = true; roaring = true; roarT = 0f;
                Play("Roar", 0.15f, 1.1f, 0f);
                ArenAudio.Play(Sfx.DeerGrowl, AimPoint, 1f, 0.7f);
                ArenAudio.Play(Sfx.BellCorrupt, AimPoint + Vector3.up * 20f, 0.8f, 1.1f);
                Campanula.RiftPulse.Instance?.Burst(1f);
                GameFeel.Shake(0.4f);
                ArenVFX.Ring(transform.position + Vector3.up * 0.1f, 0.5f, 7f, 0.6f, ArenVFX.CorruptColor, 0.12f, true);
                for (int i = -1; i <= 1; i++)
                {
                    Vector3 end = transform.position + transform.right * i * 2.2f + transform.forward * (2.4f - Mathf.Abs(i)) + Vector3.up * 0.1f;
                    ArenVFX.Lightning(AimPoint + Vector3.up * 4f, end, ArenVFX.CorruptColor, 0.48f);
                }
                if (minionPrefab != null)
                    for (int i = -1; i <= 1; i += 2)
                        Instantiate(minionPrefab, transform.position + transform.right * i * 3.5f + Vector3.up * 0.1f, transform.rotation);
            }
            if (roaring)
            {
                roarT += Time.deltaTime;
                SetMove(0f);
                if (roarT > 1.6f) { roaring = false; Play("Locomotion", 0.25f); }
            }
            // passos que chegam antes da perna: som/poeira adiantados ao andar
            if (State == EnemyState.Chase || State == EnemyState.Circle)
            {
                stepTimer -= Time.deltaTime;
                if (animMove > 0.4f && stepTimer <= 0f)
                {
                    stepTimer = 0.55f / Mathf.Max(0.6f, animMove / 2f);
                    Vector3 ahead = transform.position + transform.forward * animMove * 0.5f;
                    ArenAudio.Play(Sfx.DeerStep, ahead, 0.55f, Random.Range(0.85f, 0.95f));
                    ArenVFX.Dust(ahead + Vector3.up * 0.05f, Vector3.up * 0.4f, new Color(0.25f, 0.06f, 0.12f, 0.45f), 1, 0.6f);
                }
            }
        }

        protected override void SetMove(float speed)
        {
            if (roaring) speed = 0f;
            animMove = Mathf.MoveTowards(animMove, speed, Time.deltaTime * 6f);
            if (anim != null)
            {
                anim.SetFloat(MoveHash, animMove);
                anim.SetFloat(LocoHash, Mathf.Max(1f, animMove / 1.6f));
            }
        }

        public override bool TakeHit(in HitData hit)
        {
            bool r = base.TakeHit(hit);
            if (r) ArenAudio.Play(Sfx.DeerGrowl, AimPoint, 0.25f, Random.Range(1.1f, 1.3f));
            return r;
        }
    }
}
