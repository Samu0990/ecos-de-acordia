using System.Collections.Generic;
using Climbing;
using UnityEngine;

namespace Aren.Combat
{
    public enum CombatState { Free, Attack, Dodge, Counter, Action, Hurt, Dead }

    /// <summary>
    /// Ação genérica (habilidades): estado de animação + duração + callbacks. O ArenCombat
    /// cuida de travar o movimento do DPS, da flauta e de devolver o controle no fim.
    /// </summary>
    public class ActionSpec
    {
        public string stateName;
        public float duration = 0.6f;
        public float crossFade = 0.08f;
        public float animSpeed = 1f;
        public float startOffset = 0f;
        public bool superArmor;       // dano não interrompe
        public bool invulnerable;
        public float cancelFrom = 999f;
        public System.Action<float> onUpdate;   // tempo desde o início
        public System.Action<float> onFixedUpdate;
        public System.Action onEnd;
        public System.Func<bool> keepAlive;     // ex.: Contracanto carregando enquanto segura
    }

    /// <summary>
    /// Máquina de estados do combate do Aren (Freeflow): combo de 4 golpes da flauta,
    /// esquiva com i-frames, counter/parry, reação a dano e ações de habilidade.
    ///
    /// Integração com o DPS (sem reescrever o parkour): enquanto ocupado, desliga
    /// allowMovement (input/rotação do DPS) e stopMotion (velocidade do DPS) e bloqueia
    /// ações novas de vault; a velocidade do Rigidbody passa a ser do combate. No fim
    /// devolve tudo. Assistência de ataque nunca teleporta: só velocidade limitada.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public class ArenCombat : MonoBehaviour
    {
        [Header("Combo da flauta (M1-1..4)")]
        public AttackData[] combo = new AttackData[4];
        public AttackData counterAttack;

        [Header("Esquiva")]
        public string dodgeState = "Aren Dodge";
        public float dodgeDistance = 4.4f;
        public float dodgeDuration = 0.36f;
        public float iframeStart = 0.0f;
        public float iframeEnd = 0.28f;
        public float dodgeCancelFrom = 0.24f;
        public float dodgeAnimSpeed = 1.25f;

        [Header("Counter")]
        public float counterRange = 8f;
        public string parryState = "Aren Parry";
        public float parryDuration = 0.36f;
        public float parryBlockWindow = 0.22f;

        [Header("Dano recebido")]
        public string hurtState = "Aren Hurt";
        public float hurtDuration = 0.4f;
        public float hurtGrace = 0.55f;
        public string deathState = "Aren Death";

        [Header("Geral")]
        public float chainTimeout = 3f;
        public float targetRange = 6.5f;

        public CombatState State { get; private set; }
        public bool Busy => State != CombatState.Free;
        public IDamageable Target { get; private set; }
        public int ChainCount { get; private set; }
        public float ChainTimeLeft => Mathf.Max(0f, chainExpire - Time.time);
        public float LastCombatTime { get; private set; } = -100f;
        public bool InCombatRecently => Time.time - LastCombatTime < 4f;
        public AttackData CurrentAttack => curAttack;
        /// <summary>Golpe atual já fez contato (Contracanto pode sair do recovery).</summary>
        public bool AttackContactDone => State == CombatState.Attack && atkHitDone;
        public int ComboStep => comboIndex;   // próximo golpe (0..3)
        public bool Invulnerable
        {
            get
            {
                if (Time.time < graceUntil) return true;
                switch (State)
                {
                    case CombatState.Dodge: return stateTime >= iframeStart && stateTime <= iframeEnd;
                    case CombatState.Counter: return !parryWhiff;
                    case CombatState.Action: return curAction != null && curAction.invulnerable;
                    case CombatState.Dead: return true;
                }
                return false;
            }
        }
        public bool SuperArmor => State == CombatState.Action && curAction != null && curAction.superArmor;

        public event System.Action<IDamageable, HitData, AttackData> OnHitLanded;
        public event System.Action<AttackData> OnSwing;
        public event System.Action OnPerfectDodge;
        public event System.Action<int> OnCounter;      // nº de inimigos contra-atacados
        public event System.Action OnHurt;
        public event System.Action<CombatState> OnStateChanged;
        public event System.Action OnChainBroken;

        // refs
        ThirdPersonController tpc;
        MovementCharacterController move;
        VaultingController vaulting;
        ClimbController climb;
        JumpPredictionController jp;
        InputCharacterController dpsInput;
        Animator anim;
        Rigidbody rb;
        ArenInput input;
        ArenFlute flute;

        // estado
        float stateTime;
        float chainExpire;
        float graceUntil;
        int comboIndex;
        float comboExpire;
        AttackData curAttack;
        float atkStartup;           // startup efetivo (estica em avanço longo)
        bool atkHitDone, atkSwingDone;
        Vector3 atkDir;
        Vector3 lungeGoal;

        Vector3 dodgeDir;
        Vector3 dodgeStartPos;
        bool perfectDodgeDone;
        bool parryWhiff;
        float counterHitTime;
        bool counterHitDone;
        ActionSpec curAction;
        Vector3 hurtVel;
        float lastJumpSeen;
        static readonly int CombatSpeedHash = Animator.StringToHash("CombatSpeed");
        readonly List<IDamageable> tmpList = new List<IDamageable>(16);

        void Awake()
        {
            tpc = GetComponent<ThirdPersonController>();
            move = GetComponent<MovementCharacterController>();
            vaulting = GetComponent<VaultingController>();
            climb = GetComponent<ClimbController>();
            jp = GetComponent<JumpPredictionController>();
            dpsInput = GetComponent<InputCharacterController>();
            anim = GetComponent<Animator>();
            rb = GetComponent<Rigidbody>();
            input = GetComponent<ArenInput>();
            flute = GetComponent<ArenFlute>();

            // golpes/habilidades sempre de frente para o alvo (os clipes da UAL2 giram o tronco);
            // vale também na transição de saída para a locomoção, enquanto o estado ainda é de combate
            var torso = GetComponent<TorsoFacingLock>();
            if (torso == null) torso = gameObject.AddComponent<TorsoFacingLock>();
            int dodgeHash = Animator.StringToHash(dodgeState);
            torso.active = () =>
            {
                var st = anim.GetCurrentAnimatorStateInfo(0);
                return st.IsTag("Combat") && st.shortNameHash != dodgeHash;
            };
        }

        // ------------------------------------------------------------ helpers de estado

        /// <summary>Parkour em andamento ou no ar: combate não começa.</summary>
        public bool ParkourBusy =>
            tpc.dummy || tpc.isVaulting || tpc.isJumping || !tpc.isGrounded
            || (climb != null && climb.CurrentClimbState != ClimbController.ClimbState.None)
            || (jp != null && jp.curPoint != null);

        public bool CanStartAction => State == CombatState.Free && !ParkourBusy;

        void Enter(CombatState s)
        {
            bool wasFree = State == CombatState.Free;
            State = s;
            stateTime = 0f;
            if (s != CombatState.Free)
            {
                LastCombatTime = Time.time;
                if (wasFree) LockDPS(true);
                if (flute != null && s != CombatState.Hurt && s != CombatState.Dead) flute.Draw();
            }
            else
            {
                LockDPS(false);
                anim.SetFloat(CombatSpeedHash, 1f);
            }
            OnStateChanged?.Invoke(s);
        }

        void LockDPS(bool on)
        {
            tpc.allowMovement = !on;
            move.stopMotion = on;
            if (vaulting != null) vaulting.blockNewActions = on;
            if (!on)
            {
                // devolve a velocidade ao DPS sem "tranco": ele parte do valor atual
                move.ResetSpeed();
            }
        }

        void ReturnToLocomotion(float fade = 0.18f)
        {
            Vector2 m = dpsInput != null ? dpsInput.movement : Vector2.zero;
            string st = m.magnitude > 0.2f ? (dpsInput.run ? "Base Layer.Run.Run" : "Base Layer.Walk") : "Base Layer.Idle";
            anim.CrossFadeInFixedTime(st, fade, 0);
        }

        void Face(Vector3 dir, float maxDegPerSec)
        {
            dir.y = 0;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(dir);
            Quaternion r = maxDegPerSec <= 0 ? target
                : Quaternion.RotateTowards(rb.rotation, target, maxDegPerSec * Time.fixedDeltaTime);
            rb.MoveRotation(r);
            transform.rotation = r;
        }

        void SetHorizontalVelocity(Vector3 v)
        {
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        Vector3 Chest => transform.position + Vector3.up * 1.15f;

        // ------------------------------------------------------------ loop

        void Update()
        {
            stateTime += Time.deltaTime;
            if (Time.time > chainExpire && ChainCount > 0) { ChainCount = 0; OnChainBroken?.Invoke(); }
            if (State == CombatState.Free && Time.time > comboExpire) comboIndex = 0;
            if (Target != null && !CombatRegistry.IsValid(Target)) Target = null;

            // alvo "de olho" para o HUD mesmo parado (sem roubar o alvo do golpe)
            if (State == CombatState.Free && input != null && InCombatRecently)
                Target = TargetResolver.Resolve(transform.position, transform.forward,
                    input.IntentDirection, Target, ParamsFor(targetRange));
            ArenVFX.SetCombatTarget(InCombatRecently && CombatRegistry.IsValid(Target) ? Target.transform : null,
                CombatRegistry.IsValid(Target) ? Target.BodyRadius : 0.65f);

            if (dpsInput != null && dpsInput.LastJumpPressedTime > lastJumpSeen)
            {
                lastJumpSeen = dpsInput.LastJumpPressedTime;
                OnJumpPressed();
            }

            switch (State)
            {
                case CombatState.Free: UpdateFree(); break;
                case CombatState.Attack: UpdateAttack(); break;
                case CombatState.Dodge: UpdateDodge(); break;
                case CombatState.Counter: UpdateCounter(); break;
                case CombatState.Action: UpdateAction(); break;
                case CombatState.Hurt: if (stateTime >= hurtDuration) { Enter(CombatState.Free); ReturnToLocomotion(0.2f); } break;
            }
        }

        void OnDisable()
        {
            ArenVFX.SetCombatTarget(null);
        }

        void FixedUpdate()
        {
            switch (State)
            {
                case CombatState.Attack: FixedAttack(); break;
                case CombatState.Dodge: FixedDodge(); break;
                case CombatState.Counter: FixedCounter(); break;
                case CombatState.Action:
                    if (curAction != null && curAction.onFixedUpdate != null) curAction.onFixedUpdate(stateTime);
                    else SetHorizontalVelocity(Vector3.MoveTowards(new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z), Vector3.zero, 30f * Time.fixedDeltaTime));
                    break;
                case CombatState.Hurt:
                    hurtVel = Vector3.MoveTowards(hurtVel, Vector3.zero, 14f * Time.fixedDeltaTime);
                    SetHorizontalVelocity(hurtVel);
                    break;
                case CombatState.Dead:
                    SetHorizontalVelocity(Vector3.zero);
                    break;
            }
        }

        void OnJumpPressed()
        {
            // Espaço cancela o recovery (pulo/parkour saem do buffer de 0.15 s do ArenJump)
            if (State == CombatState.Attack && curAttack != null && stateTime >= CancelTime(curAttack))
            { EndToFree(0.12f); return; }
            if (State == CombatState.Dodge && stateTime >= dodgeCancelFrom)
            { EndToFree(0.12f); }
        }

        void UpdateFree()
        {
            if (input == null) return;
            if (!input.buffer.TryPeek(out var q)) return;
            if (ParkourBusy) return;   // fica no buffer (0.2 s) — pousou a tempo, sai
            TryConsume(q);
        }

        /// <summary>Tenta executar a ação da fila. Retorna true se consumiu.</summary>
        bool TryConsume(BufferedAction q)
        {
            switch (q.action)
            {
                case ArenAction.Counter:
                    input.buffer.TryConsume(ArenAction.Counter, out _);
                    StartCounter();
                    return true;
                case ArenAction.Dodge:
                    input.buffer.TryConsume(ArenAction.Dodge, out var d);
                    StartDodge(d.direction);
                    return true;
                case ArenAction.Attack:
                    input.buffer.TryConsume(ArenAction.Attack, out var a);
                    StartAttack(combo[Mathf.Clamp(comboIndex, 0, combo.Length - 1)], a.direction);
                    return true;
                case ArenAction.Ability:
                case ArenAction.Ability2:
                case ArenAction.Ability3:
                    var ab = GetComponent<ArenAbilities>();
                    if (ab != null && ab.TryCast(q.action, q.direction))
                    {
                        input.buffer.TryConsume(q.action, out _);
                        return true;
                    }
                    input.buffer.TryConsume(q.action, out _);   // sem recurso/recarga: descarta
                    return false;
            }
            return false;
        }

        void EndToFree(float fade)
        {
            if (State == CombatState.Action && curAction != null)
            {
                var a = curAction; curAction = null;
                a.onEnd?.Invoke();
            }
            if (State == CombatState.Attack && curAttack != null)
                comboExpire = Time.time + curAttack.comboKeep;
            Enter(CombatState.Free);
            ReturnToLocomotion(fade);
        }

        // ------------------------------------------------------------ ataque

        TargetResolver.Params ParamsFor(float range)
        {
            var p = TargetResolver.Default;
            p.range = range;
            return p;
        }

        float CancelTime(AttackData a) => a.cancelFrom + (atkStartup - a.startup);

        public void StartAttack(AttackData a, Vector3 intent)
        {
            if (a == null) return;
            if (State != CombatState.Free && State != CombatState.Attack && State != CombatState.Dodge) return;
            if (State == CombatState.Free) Enter(CombatState.Attack);
            else { State = CombatState.Attack; stateTime = 0f; LastCombatTime = Time.time; OnStateChanged?.Invoke(State); }

            curAttack = a;
            atkHitDone = atkSwingDone = false;
            comboIndex = (comboIndex + 1) % combo.Length;

            Target = TargetResolver.Resolve(transform.position, transform.forward, intent, Target, ParamsFor(a.magnetRange));
            atkStartup = a.startup;

            if (Target != null)
            {
                Vector3 to = Target.transform.position - transform.position; to.y = 0;
                float dist = to.magnitude;
                atkDir = dist > 0.01f ? to / dist : transform.forward;
                float standOff = Target.BodyRadius + a.reach * 0.62f;
                float lunge = Mathf.Max(0f, dist - standOff);
                lungeGoal = Target.transform.position - atkDir * standOff;

                // avanço longo estica o startup (no máximo +0.24 s) em vez de teleportar
                atkStartup = Mathf.Clamp(lunge / a.magnetMaxSpeed, a.startup, a.startup + 0.24f);
            }
            else
            {
                atkDir = intent.sqrMagnitude > 0.01f ? intent.normalized : transform.forward;
                lungeGoal = transform.position + atkDir * a.stepNoTarget;

            }

            // a velocidade da animação faz o contato do clipe cair exatamente no fim do startup
            float spd = a.animSpeed * (a.startup / Mathf.Max(0.05f, atkStartup));
            anim.SetFloat(CombatSpeedHash, spd);
            anim.CrossFadeInFixedTime(a.stateName, a.crossFade, 0, a.startOffset);
        }

        void UpdateAttack()
        {
            var a = curAttack;
            float swingAt = Mathf.Max(0f, atkStartup - 0.05f);
            if (!atkSwingDone && stateTime >= swingAt)
            {
                atkSwingDone = true;
                OnSwing?.Invoke(a);
                Vector3 fwd = atkDir;
                ArenVFX.Slash(Chest + fwd * 0.35f, Quaternion.LookRotation(fwd), a.slashRoll, a.slashRadius, a.slashColor, 0.2f);
                ArenAudio.Play(Sfx.Whoosh, Chest, 0.55f, 0.9f + 0.08f * a.noteIndex);
            }
            if (!atkHitDone && stateTime >= atkStartup)
            {
                atkHitDone = true;
                anim.SetFloat(CombatSpeedHash, a.animSpeed);
                DoHits(a, atkDir, a.reach, a.arcHalfAngle, Target);
            }

            float total = atkStartup + a.active + a.recovery;
            if (stateTime >= CancelTime(a) && input != null && input.buffer.TryPeek(out var q))
            {
                // ataque segurado vira Contracanto (só depois do contato, nunca atrasa o golpe)
                if (TryConsume(q)) return;
            }
            if (stateTime >= total) EndToFree(0.2f);
        }

        void FixedAttack()
        {
            var a = curAttack;
            if (stateTime < atkStartup)
            {
                // avanço magnético: velocidade limitada até a posição de golpe
                if (CombatRegistry.IsValid(Target))
                {
                    Vector3 to = Target.transform.position - transform.position; to.y = 0;
                    if (to.sqrMagnitude > 0.0001f) atkDir = to.normalized;
                    lungeGoal = Target.transform.position - atkDir * (Target.BodyRadius + a.reach * 0.62f);
                }
                Vector3 rem = lungeGoal - transform.position; rem.y = 0;
                float tLeft = Mathf.Max(Time.fixedDeltaTime, atkStartup - stateTime);
                Vector3 v = Vector3.ClampMagnitude(rem / tLeft, a.magnetMaxSpeed);
                SetHorizontalVelocity(v);
                Face(atkDir, 1100f);
            }
            else
            {
                Vector3 hv = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                SetHorizontalVelocity(Vector3.MoveTowards(hv, Vector3.zero, 45f * Time.fixedDeltaTime));
            }
        }

        /// <summary>
        /// Aplica um golpe em arco. Alvo principal é garantido dentro de alcance+folga
        /// (assistência); outros só dentro de alcance e arco. Retorna quantos acertou.
        /// </summary>
        public int DoHits(AttackData a, Vector3 dir, float reach, float arcHalf, IDamageable primary, float damageMul = 1f, bool fromEcho = false)
        {
            int landed = 0;
            Vector3 origin = transform.position;
            var list = CombatRegistry.Enemies;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (!CombatRegistry.IsValid(e)) continue;
                Vector3 d = e.transform.position - origin;
                if (Mathf.Abs(d.y) > 2.2f) continue;
                d.y = 0;
                float dist = d.magnitude - e.BodyRadius;
                bool hit = (e == primary && dist <= reach + 0.7f)
                        || (dist <= reach && (d.sqrMagnitude < 0.01f || Vector3.Angle(dir, d) <= arcHalf));
                if (!hit) continue;
                Vector3 hdir = d.sqrMagnitude > 0.01f ? d.normalized : dir;
                var h = new HitData
                {
                    damage = a.damage * damageMul,
                    point = e.AimPoint - hdir * e.BodyRadius * 0.7f,
                    direction = hdir,
                    knockback = a.knockback,
                    stagger = a.stagger * damageMul,
                    kind = a.kind,
                    team = Team.Player,
                    source = gameObject
                };
                if (e.TakeHit(h))
                {
                    landed++;
                    ArenVFX.Impact(h.point, hdir, fromEcho ? ArenVFX.EchoColor : a.slashColor, a.kind == HitKind.Heavy ? 1.4f : 1f);
                    OnHitLanded?.Invoke(e, h, a);
                }
            }
            if (landed > 0)
            {
                RegisterHit(landed);
                if (!fromEcho)
                {
                    GameFeel.Hitstop(a.hitstop * (landed > 1 ? 1.25f : 1f));
                    GameFeel.Shake(a.shake, dir);
                }
                ArenAudio.Note(a.noteIndex, fromEcho ? 0.45f : 0.9f, a.kind == HitKind.Heavy ? 1.3f : 1f);
                ArenAudio.Play(a.kind == HitKind.Heavy ? Sfx.ImpactHeavy : Sfx.ImpactLight, origin + dir, fromEcho ? 0.4f : 0.85f, Random.Range(0.94f, 1.06f));
            }
            else if (!fromEcho)
            {
                ArenAudio.Note(a.noteIndex, 0.35f, 0.6f);   // golpe no ar ainda é música, mais baixo
            }
            return landed;
        }

        /// <summary>Conta acerto para a Cadência (combo sem levar dano).</summary>
        public void RegisterHit(int count)
        {
            ChainCount += count;
            chainExpire = Time.time + chainTimeout;
            LastCombatTime = Time.time;
        }

        // ------------------------------------------------------------ esquiva

        /// <summary>Inimigo mais perigoso por perto: quem está avisando o golpe ganha; senão o mais próximo.</summary>
        IDamageable NearestThreat(float radius)
        {
            IDamageable best = null; float bestScore = float.MaxValue;
            var list = CombatRegistry.Enemies;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (!CombatRegistry.IsValid(e)) continue;
                Vector3 d = e.transform.position - transform.position; d.y = 0f;
                float dist = d.magnitude;
                if (dist > radius) continue;
                float score = dist - (e is ICounterable c && c.CounterWindowOpen ? 100f : 0f);
                if (score < bestScore) { bestScore = score; best = e; }
            }
            return best;
        }

        public void StartDodge(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.01f)
            {
                // sem direção: passo lateral com o perigo à direita. O clipe da esquiva
                // (Shield_Dash) gira o tronco ~65° para a direita, então ele desvia olhando para
                // o inimigo em vez de dar as costas (o antigo recuo virava o Aren de costas).
                var threat = NearestThreat(7f);
                if (threat != null)
                {
                    Vector3 to = threat.transform.position - transform.position; to.y = 0f;
                    dir = to.sqrMagnitude > 0.01f ? Quaternion.Euler(0f, -90f, 0f) * to.normalized : -transform.forward;
                }
                else dir = -transform.forward;
            }
            dir.y = 0; dir.Normalize();
            if (State == CombatState.Free) Enter(CombatState.Dodge);
            else { State = CombatState.Dodge; stateTime = 0f; OnStateChanged?.Invoke(State); }
            dodgeDir = dir;
            dodgeStartPos = transform.position;
            perfectDodgeDone = false;
            transform.rotation = Quaternion.LookRotation(dir);
            rb.MoveRotation(transform.rotation);
            anim.SetFloat(CombatSpeedHash, dodgeAnimSpeed);
            anim.CrossFadeInFixedTime(dodgeState, 0.05f, 0, 0f);
            ArenAudio.Play(Sfx.Dodge, transform.position, 0.8f, Random.Range(0.95f, 1.05f));
            ArenVFX.DodgeBurst(transform, dir);
            comboExpire = Mathf.Max(comboExpire, Time.time + dodgeDuration + 0.5f);   // dodge bridge
        }

        void UpdateDodge()
        {
            if (stateTime >= dodgeCancelFrom && input != null && input.buffer.TryPeek(out var q) && q.action != ArenAction.Dodge)
            {
                if (TryConsume(q)) return;
            }
            if (stateTime >= dodgeDuration)
            {
                if (input != null && input.buffer.TryPeek(out var q2) && TryConsume(q2)) return;
                EndToFree(0.16f);
            }
        }

        void FixedDodge()
        {
            // curva ease-out: arranca forte e freia no fim (ação física, não "velocity = 1000")
            float t = Mathf.Clamp01(stateTime / dodgeDuration);
            float speed = dodgeDistance / dodgeDuration * 2f * (1f - t);   // integral = dodgeDistance
            SetHorizontalVelocity(dodgeDir * speed);
        }

        /// <summary>Chamado pelo ArenHealth quando um golpe foi ignorado pelos i-frames.</summary>
        public void NotifyDodgedHit()
        {
            if (State != CombatState.Dodge || perfectDodgeDone) return;
            perfectDodgeDone = true;
            GameFeel.SlowMo(0.35f, 0.35f);
            ArenAudio.Play(Sfx.PerfectDodge, transform.position, 0.9f);
            ArenVFX.Ring(transform.position + Vector3.up * 0.05f, 0.4f, 3.2f, 0.4f, ArenVFX.EchoColor, 0.12f, true);
            OnPerfectDodge?.Invoke();
        }

        // ------------------------------------------------------------ counter

        readonly List<ICounterable> counterList = new List<ICounterable>(8);
        IDamageable counterTarget;

        public void StartCounter()
        {
            counterList.Clear();
            counterTarget = null;
            float best = float.MaxValue;
            var list = CombatRegistry.Enemies;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (!CombatRegistry.IsValid(e) || !(e is ICounterable c) || !c.CounterWindowOpen) continue;
                Vector3 d = e.transform.position - transform.position; d.y = 0;
                float dist = d.magnitude;
                if (dist > counterRange) continue;
                counterList.Add(c);
                if (dist < best) { best = dist; counterTarget = e; }
            }

            if (State == CombatState.Free) Enter(CombatState.Counter);
            else { State = CombatState.Counter; stateTime = 0f; OnStateChanged?.Invoke(State); }

            if (counterTarget != null)
            {
                parryWhiff = false;
                bool perfect = false;
                foreach (var c in counterList)
                {
                    if (c.CounterWindowProgress > 0.6f) perfect = true;
                    c.OnCountered(transform.position);
                }
                Target = counterTarget;
                Vector3 to = counterTarget.transform.position - transform.position; to.y = 0;
                float dist = to.magnitude;
                // avança até o alvo (máx. 0.2 s) e golpeia
                counterHitTime = Mathf.Clamp((dist - counterTarget.BodyRadius - 1.0f) / 16f, 0.06f, 0.2f);
                counterHitDone = false;
                var a = counterAttack;
                float spd = a != null ? a.animSpeed * (a.startup / counterHitTime) : 1f;
                anim.SetFloat(CombatSpeedHash, Mathf.Clamp(spd, 0.8f, 3f));
                anim.CrossFadeInFixedTime(a != null ? a.stateName : parryState, 0.04f, 0, a != null ? a.startOffset : 0f);
                GameFeel.SlowMo(0.3f, perfect ? 0.25f : 0.45f);
                ArenAudio.Play(Sfx.CounterHit, transform.position, 1f);
                ArenVFX.Ring(counterTarget.AimPoint, 0.2f, 1.6f, 0.25f, ArenVFX.GoldColor, 0.1f, false);
                if (perfect) GetComponent<ArenAbilities>()?.AddResonance(8f);
                OnCounter?.Invoke(counterList.Count);
            }
            else
            {
                parryWhiff = true;
                anim.SetFloat(CombatSpeedHash, 1.3f);
                anim.CrossFadeInFixedTime(parryState, 0.06f, 0, 0f);
                ArenAudio.Play(Sfx.ParryWhiff, transform.position, 0.6f);
            }
        }

        void UpdateCounter()
        {
            if (!parryWhiff)
            {
                var a = counterAttack;
                if (!counterHitDone && stateTime >= counterHitTime)
                {
                    counterHitDone = true;
                    if (a != null)
                    {
                        anim.SetFloat(CombatSpeedHash, a.animSpeed);
                        Vector3 dir = counterTarget != null ? (counterTarget.transform.position - transform.position) : transform.forward;
                        dir.y = 0; dir.Normalize();
                        ArenVFX.Slash(Chest + dir * 0.3f, Quaternion.LookRotation(dir), a.slashRoll, a.slashRadius, a.slashColor, 0.22f);
                        DoHits(a, dir, a.reach, a.arcHalfAngle, counterTarget);
                    }
                }
                float total = counterHitTime + (a != null ? a.active + a.recovery : 0.4f);
                if (stateTime >= (a != null ? counterHitTime + a.active + a.recovery * 0.45f : 0.3f)
                    && input != null && input.buffer.TryPeek(out var q) && TryConsume(q)) return;
                if (stateTime >= total) EndToFree(0.18f);
            }
            else
            {
                if (stateTime >= parryDuration) EndToFree(0.15f);
            }
        }

        void FixedCounter()
        {
            if (!parryWhiff && counterTarget != null && stateTime < counterHitTime)
            {
                Vector3 to = counterTarget.transform.position - transform.position; to.y = 0;
                float standOff = counterTarget.BodyRadius + 0.95f;
                Vector3 goal = counterTarget.transform.position - to.normalized * standOff;
                Vector3 rem = goal - transform.position; rem.y = 0;
                float tLeft = Mathf.Max(Time.fixedDeltaTime, counterHitTime - stateTime);
                SetHorizontalVelocity(Vector3.ClampMagnitude(rem / tLeft, 18f));
                Face(to, 2000f);
            }
            else
            {
                Vector3 hv = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                SetHorizontalVelocity(Vector3.MoveTowards(hv, Vector3.zero, 50f * Time.fixedDeltaTime));
            }
        }

        /// <summary>Parry aberto (whiff) bloqueia golpe que chega logo depois → vira counter.</summary>
        public bool TryParryIncoming(GameObject attacker)
        {
            if (State != CombatState.Counter || !parryWhiff || stateTime > parryBlockWindow) return false;
            var c = attacker != null ? attacker.GetComponent<ICounterable>() : null;
            if (c == null) return false;
            State = CombatState.Free;   // reentra limpo
            StartCounterForced(c, attacker.GetComponent<IDamageable>());
            return true;
        }

        void StartCounterForced(ICounterable c, IDamageable target)
        {
            State = CombatState.Counter; stateTime = 0f;
            parryWhiff = false;
            counterTarget = target;
            c.OnCountered(transform.position);
            Target = target;
            counterHitTime = 0.08f;
            counterHitDone = false;
            var a = counterAttack;
            anim.SetFloat(CombatSpeedHash, a != null ? a.animSpeed * a.startup / counterHitTime : 1f);
            if (a != null) anim.CrossFadeInFixedTime(a.stateName, 0.03f, 0, a.startOffset);
            GameFeel.SlowMo(0.3f, 0.4f);
            ArenAudio.Play(Sfx.CounterHit, transform.position, 1f);
            OnCounter?.Invoke(1);
        }

        // ------------------------------------------------------------ ações (habilidades)

        public bool BeginAction(ActionSpec spec)
        {
            if (spec == null) return false;
            if (State == CombatState.Hurt || State == CombatState.Dead) return false;
            if (State == CombatState.Free && ParkourBusy) return false;
            if (State == CombatState.Action && curAction != null) { var old = curAction; curAction = null; old.onEnd?.Invoke(); }
            if (State == CombatState.Free) Enter(CombatState.Action);
            else { State = CombatState.Action; stateTime = 0f; LastCombatTime = Time.time; if (flute != null) flute.Draw(); OnStateChanged?.Invoke(State); }
            curAction = spec;
            if (!string.IsNullOrEmpty(spec.stateName))
            {
                anim.SetFloat(CombatSpeedHash, spec.animSpeed);
                anim.CrossFadeInFixedTime(spec.stateName, spec.crossFade, 0, spec.startOffset);
            }
            return true;
        }

        public float ActionTime => State == CombatState.Action ? stateTime : 0f;
        public ActionSpec CurrentActionSpec => State == CombatState.Action ? curAction : null;

        void UpdateAction()
        {
            var a = curAction;
            if (a == null) { EndToFree(0.15f); return; }
            a.onUpdate?.Invoke(stateTime);
            if (curAction != a) return;   // o callback trocou de ação
            bool alive = a.keepAlive != null ? a.keepAlive() : stateTime < a.duration;
            if (stateTime >= a.cancelFrom && input != null && input.buffer.TryPeek(out var q) && TryConsume(q)) return;
            if (!alive) EndToFree(0.2f);
        }

        /// <summary>Encerra a ação atual por fora (ex.: soltou o botão do Contracanto).</summary>
        public void EndAction() { if (State == CombatState.Action) EndToFree(0.2f); }

        public void FaceTowards(Vector3 dir) => Face(dir, 0f);
        public void SetPlanarVelocity(Vector3 v) => SetHorizontalVelocity(v);

        // ------------------------------------------------------------ dano

        /// <summary>Chamado pelo ArenHealth depois de aplicar o dano.</summary>
        public void ReceiveHurt(in HitData hit, bool died)
        {
            ChainCount = 0; chainExpire = 0f; OnChainBroken?.Invoke();
            comboIndex = 0;
            LastCombatTime = Time.time;
            if (died) { Die(); return; }
            OnHurt?.Invoke();
            if (SuperArmor) return;
            if (State == CombatState.Action && curAction != null) { var a = curAction; curAction = null; a.onEnd?.Invoke(); }
            if (State == CombatState.Free) Enter(CombatState.Hurt);
            else { State = CombatState.Hurt; stateTime = 0f; OnStateChanged?.Invoke(State); }
            hurtVel = hit.direction * hit.knockback;
            graceUntil = Time.time + hurtGrace;
            Face(-hit.direction, 0f);
            anim.SetFloat(CombatSpeedHash, 1.5f);
            anim.CrossFadeInFixedTime(hurtState, 0.04f, 0, 0.02f);
        }

        void Die()
        {
            if (State == CombatState.Action && curAction != null) { var a = curAction; curAction = null; a.onEnd?.Invoke(); }
            if (State == CombatState.Free) LockDPS(true);
            State = CombatState.Dead; stateTime = 0f;
            OnStateChanged?.Invoke(State);
            anim.SetFloat(CombatSpeedHash, 1f);
            anim.CrossFadeInFixedTime(deathState, 0.1f, 0, 0f);
            if (input != null) input.buffer.Clear();
        }

        /// <summary>Renascer no checkpoint (GameFlow).</summary>
        public void Revive()
        {
            curAction = null;
            graceUntil = Time.time + 1.5f;
            comboIndex = 0;
            Enter(CombatState.Free);
            anim.Play("Idle", 0, 0f);
        }

        /// <summary>Levanta do chão (LayToIdle) — usado ao renascer.</summary>
        public void PlayGetUp()
        {
            BeginAction(new ActionSpec { stateName = "Aren Revive", duration = 1.2f, animSpeed = 1.25f, invulnerable = true, crossFade = 0.05f });
        }

        /// <summary>Força o fim de qualquer ação (cutscene, menu, teleporte de checkpoint).</summary>
        public void ForceFree()
        {
            if (State == CombatState.Free) return;
            if (curAction != null) { var a = curAction; curAction = null; a.onEnd?.Invoke(); }
            Enter(CombatState.Free);
        }
    }
}
