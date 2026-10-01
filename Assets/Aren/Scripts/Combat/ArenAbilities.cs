using System.Collections.Generic;
using UnityEngine;

namespace Aren.Combat
{
    public enum AbilityId { Pulso, Lamina, Eco, Contracanto }

    [System.Serializable]
    public class AbilityDef
    {
        public AbilityId id;
        public string displayName;
        public string keyLabel;
        public float cost;
        public float cooldown;
    }

    /// <summary>
    /// Recurso musical (Ressonância) + as 4 habilidades do doc de remake §17:
    /// Pulso de Ressonância (Q), Lâmina de Frequência (E), Eco Fantasma (R) e
    /// Contracanto (segurar ataque). Cada habilidade é uma ActionSpec do ArenCombat —
    /// um único dono do estado, sem duas máquinas brigando.
    /// </summary>
    [RequireComponent(typeof(ArenCombat))]
    public class ArenAbilities : MonoBehaviour
    {
        [Header("Ressonância")]
        public float maxResonance = 100f;
        public float startResonance = 50f;
        public float gainPerHit = 5f;
        public float gainPerCounter = 10f;
        public float passiveGain = 1.2f;   // /s — a demo deixa testar as habilidades sem farmar

        [Header("Habilidades")]
        public AbilityDef[] abilities =
        {
            new AbilityDef { id = AbilityId.Pulso, displayName = "Pulso de Ressonância", keyLabel = "Q", cost = 25f, cooldown = 7f },
            new AbilityDef { id = AbilityId.Lamina, displayName = "Lâmina de Frequência", keyLabel = "E", cost = 15f, cooldown = 2.5f },
            new AbilityDef { id = AbilityId.Eco, displayName = "Eco Fantasma", keyLabel = "R", cost = 35f, cooldown = 18f },
            new AbilityDef { id = AbilityId.Contracanto, displayName = "Contracanto", keyLabel = "Segurar", cost = 20f, cooldown = 6f },
        };

        [Header("Pulso")]
        public float pulseRadius = 6.5f;
        public float pulseDamage = 20f;
        [Header("Lâmina")]
        public float bladeSpeed = 26f;
        public float bladeRange = 20f;
        public float bladeDamage = 18f;
        [Header("Eco Fantasma")]
        public float echoDuration = 10f;
        public float echoDelay = 0.22f;
        public float echoDamageMul = 0.5f;
        [Header("Contracanto")]
        public float chargeHoldThreshold = 0.38f;
        public float[] chargeLevelTimes = { 0.45f, 1.0f, 1.6f };
        public float[] chargeCosts = { 20f, 35f, 50f };
        public float[] chargeDamage = { 30f, 50f, 80f };
        public float[] chargeLength = { 6f, 8f, 10.5f };
        public float chargeMaxTime = 2.6f;

        public float Resonance { get; private set; }
        public float ResonanceNormalized => Resonance / maxResonance;
        public bool EchoActive => Time.time < echoUntil;
        public float EchoTimeLeft => Mathf.Max(0f, echoUntil - Time.time);
        public bool Charging => charging;
        public int ChargeLevel => chargeLevel;
        public float ChargeTime => charging ? combat.ActionTime : 0f;

        public event System.Action<AbilityId> OnCast;
        public event System.Action<AbilityId> OnReady;
        public event System.Action<AbilityId> OnDenied;     // sem recurso/recarga
        public event System.Action<int> OnChargeLevel;

        ArenCombat combat;
        ArenInput input;
        ArenFlute flute;
        readonly float[] readyAt = new float[4];
        readonly bool[] wasReady = { true, true, true, true };
        float echoUntil;
        bool charging;
        int chargeLevel;
        readonly List<IDamageable> tmp = new List<IDamageable>(16);
        AttackData pulseData, bladeData, echoProxy, contraData;

        void Awake()
        {
            combat = GetComponent<ArenCombat>();
            input = GetComponent<ArenInput>();
            flute = GetComponent<ArenFlute>();
            Resonance = startResonance;
            combat.OnHitLanded += HandleHit;
            combat.OnCounter += n => AddResonance(gainPerCounter * n);
            combat.OnPerfectDodge += () => AddResonance(8f);

            pulseData = MakeData("Pulso", pulseDamage, 40f, 9f, HitKind.Ability, 4, new Color(0.6f, 0.95f, 1f));
            bladeData = MakeData("Lâmina", bladeDamage, 18f, 4f, HitKind.Ability, 3, new Color(0.7f, 1f, 0.95f));
            contraData = MakeData("Contracanto", 30f, 999f, 12f, HitKind.Contracanto, 5, ArenVFX.GoldColor);
        }

        static AttackData MakeData(string n, float dmg, float stagger, float kb, HitKind kind, int note, Color c)
        {
            var a = ScriptableObject.CreateInstance<AttackData>();
            a.name = n; a.damage = dmg; a.stagger = stagger; a.knockback = kb; a.kind = kind;
            a.noteIndex = note; a.slashColor = c; a.hitstop = 0.07f; a.shake = 0.45f;
            return a;
        }

        public AbilityDef Def(AbilityId id) => abilities[(int)id];
        public float CooldownLeft(AbilityId id) => Mathf.Max(0f, readyAt[(int)id] - Time.time);
        public float CooldownNormalized(AbilityId id) => Def(id).cooldown <= 0 ? 0f : CooldownLeft(id) / Def(id).cooldown;
        public bool CanAfford(AbilityId id) => Resonance >= Def(id).cost;
        public bool IsReady(AbilityId id) => CooldownLeft(id) <= 0f && CanAfford(id);

        public void AddResonance(float v) => Resonance = Mathf.Clamp(Resonance + v, 0f, maxResonance);

        void HandleHit(IDamageable target, HitData hit, AttackData a)
        {
            if (a == echoProxy) return;
            float mult = 1f + Mathf.Min(combat.ChainCount, 20) * 0.025f;
            if (a.kind == HitKind.Light || a.kind == HitKind.Heavy) AddResonance(gainPerHit * mult);

            // Eco Fantasma: repete o golpe com atraso, como um eco da frase musical
            if (EchoActive && (a.kind == HitKind.Light || a.kind == HitKind.Heavy))
                StartCoroutine(EchoStrike(target, a));
        }

        System.Collections.IEnumerator EchoStrike(IDamageable target, AttackData a)
        {
            // fantasma nasce da pose atual do Aren, levemente deslocado para o lado
            Vector3 side = transform.right * (Random.value < 0.5f ? -0.45f : 0.45f);
            ArenVFX.Afterimage(gameObject, ArenVFX.EchoColor, 0.5f, side, 0.85f);
            yield return new WaitForSeconds(echoDelay);
            if (target == null || !target.Alive) yield break;
            if (echoProxy == null) echoProxy = ScriptableObject.CreateInstance<AttackData>();
            echoProxy.damage = a.damage; echoProxy.stagger = a.stagger; echoProxy.knockback = a.knockback * 0.5f;
            echoProxy.kind = HitKind.Ability; echoProxy.noteIndex = a.noteIndex + 2; echoProxy.slashColor = ArenVFX.EchoColor;
            echoProxy.hitstop = 0f; echoProxy.shake = 0.1f;
            Vector3 dir = target.transform.position - transform.position; dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            dir.Normalize();
            ArenVFX.Slash(target.AimPoint - dir * 0.6f, Quaternion.LookRotation(dir), -a.slashRoll + 25f, a.slashRadius * 1.1f, ArenVFX.EchoColor, 0.22f);
            combat.DoHits(echoProxy, dir, 99f, 0f, target, echoDamageMul, true);
            ArenAudio.Play(Sfx.EchoGhost, target.AimPoint, 0.5f, 1f + a.noteIndex * 0.05f);
        }

        void Update()
        {
            if (combat.State != CombatState.Dead)
                AddResonance(passiveGain * (combat.InCombatRecently ? 1f : 0.5f) * Time.deltaTime);

            for (int i = 0; i < 4; i++)
            {
                bool r = IsReady((AbilityId)i);
                if (r && !wasReady[i]) { OnReady?.Invoke((AbilityId)i); }
                wasReady[i] = r;
            }

            // Contracanto: segurar o ataque depois do primeiro golpe (input nunca espera)
            if (!charging && input != null && input.AttackHeld && input.AttackHoldTime >= chargeHoldThreshold)
            {
                bool okState = combat.State == CombatState.Free ? combat.CanStartAction : combat.AttackContactDone;
                if (okState && CooldownLeft(AbilityId.Contracanto) <= 0f && Resonance >= chargeCosts[0])
                    StartCharge();
            }
        }

        public bool TryCast(ArenAction action, Vector3 intent)
        {
            AbilityId id = action == ArenAction.Ability ? AbilityId.Pulso
                         : action == ArenAction.Ability2 ? AbilityId.Lamina : AbilityId.Eco;
            if (!IsReady(id)) { OnDenied?.Invoke(id); ArenAudio.Play(Sfx.Denied, transform.position, 0.5f); return false; }
            bool ok = false;
            switch (id)
            {
                case AbilityId.Pulso: ok = CastPulse(); break;
                case AbilityId.Lamina: ok = CastBlade(intent); break;
                case AbilityId.Eco: ok = CastEcho(); break;
            }
            if (ok) Spend(id);
            return ok;
        }

        void Spend(AbilityId id)
        {
            var d = Def(id);
            Resonance = Mathf.Max(0f, Resonance - d.cost);
            readyAt[(int)id] = Time.time + d.cooldown;
            wasReady[(int)id] = false;
            OnCast?.Invoke(id);
        }

        // ------------------------------------------------------------ Pulso de Ressonância

        bool CastPulse()
        {
            bool fired = false;
            var spec = new ActionSpec
            {
                stateName = "Aren Cast Pulse",
                duration = 0.62f,
                animSpeed = 1.1f,
                superArmor = true,
                cancelFrom = 0.42f,
                onUpdate = t =>
                {
                    if (!fired && t >= 0.2f)
                    {
                        fired = true;
                        PulseNow();
                    }
                },
            };
            if (!combat.BeginAction(spec)) return false;
            ArenAudio.Play(Sfx.PulseCast, transform.position, 0.8f);
            ArenVFX.Ring(transform.position + Vector3.up * 0.06f, 1.4f, 0.3f, 0.2f, ArenVFX.FluteColor, 0.08f, true);   // inspiração (anel fechando)
            return true;
        }

        void PulseNow()
        {
            Vector3 c = transform.position;
            ArenVFX.PulseWave(c, pulseRadius);
            ArenAudio.Play(Sfx.PulseBoom, c, 1f);
            GameFeel.Hitstop(0.06f);
            GameFeel.Shake(0.55f);
            CombatRegistry.EnemiesInRadius(c, pulseRadius, tmp);
            int n = 0;
            foreach (var e in tmp)
            {
                Vector3 d = e.transform.position - c; d.y = 0;
                Vector3 dir = d.sqrMagnitude > 0.01f ? d.normalized : transform.forward;
                // mais perto = mais forte (onda perde energia)
                float fall = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(d.magnitude / pulseRadius));
                var h = new HitData
                {
                    damage = pulseDamage * fall, point = e.AimPoint - dir * e.BodyRadius, direction = dir,
                    knockback = 9f * fall, stagger = 40f, kind = HitKind.Ability, team = Team.Player, source = gameObject
                };
                if (e.TakeHit(h)) { n++; ArenVFX.Impact(h.point, dir, ArenVFX.FluteColor, 1.1f); }
            }
            if (n > 0) combat.RegisterHit(n);
        }

        // ------------------------------------------------------------ Lâmina de Frequência

        bool CastBlade(Vector3 intent)
        {
            var target = TargetResolver.Resolve(transform.position, transform.forward, intent, combat.Target,
                new TargetResolver.Params { range = bladeRange, coneHalfAngle = 35f, maxHeightDiff = 4f });
            Vector3 dir = target != null ? target.AimPoint - (transform.position + Vector3.up * 1.2f)
                        : (intent.sqrMagnitude > 0.01f ? intent : transform.forward);
            Vector3 flat = new Vector3(dir.x, 0, dir.z);
            bool fired = false;
            var spec = new ActionSpec
            {
                stateName = "Aren Cast Blade",
                duration = 0.5f,
                animSpeed = 1.35f,
                cancelFrom = 0.3f,
                onUpdate = t =>
                {
                    if (!fired && t >= 0.17f)
                    {
                        fired = true;
                        Vector3 origin = transform.position + Vector3.up * 1.2f + flat.normalized * 0.6f;
                        FrequencyBlade.Spawn(origin, dir.normalized, bladeSpeed, bladeRange, bladeData, gameObject, combat);
                        ArenAudio.Play(Sfx.BladeCast, origin, 0.9f);
                    }
                },
                onFixedUpdate = t => { combat.FaceTowards(flat); combat.SetPlanarVelocity(Vector3.zero); },
            };
            return combat.BeginAction(spec);
        }

        // ------------------------------------------------------------ Eco Fantasma

        bool CastEcho()
        {
            var spec = new ActionSpec
            {
                stateName = "Aren Cast Echo",
                duration = 0.5f,
                animSpeed = 1.2f,
                invulnerable = true,
                cancelFrom = 0.3f,
            };
            if (!combat.BeginAction(spec)) return false;
            echoUntil = Time.time + echoDuration;
            ArenAudio.Play(Sfx.EchoCast, transform.position, 0.9f);
            for (int i = 0; i < 3; i++)
            {
                float ang = i * 120f * Mathf.Deg2Rad;
                ArenVFX.Afterimage(gameObject, ArenVFX.EchoColor, 0.6f + i * 0.1f,
                    new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * 0.7f, 0.7f);
            }
            ArenVFX.Ring(transform.position + Vector3.up * 0.05f, 0.3f, 2.6f, 0.5f, ArenVFX.EchoColor, 0.1f, true);
            return true;
        }

        // ------------------------------------------------------------ Contracanto

        void StartCharge()
        {
            charging = true;
            chargeLevel = 0;
            var spec = new ActionSpec
            {
                stateName = "Aren Charge",
                crossFade = 0.12f,
                animSpeed = 1f,
                superArmor = true,
                keepAlive = () => input.AttackHeld && combat.ActionTime < chargeMaxTime,
                onUpdate = ChargeUpdate,
                onFixedUpdate = t =>
                {
                    Vector3 aim = input.IntentDirection.sqrMagnitude > 0.01f ? input.IntentDirection : CameraForward();
                    combat.FaceTowards(Vector3.Slerp(transform.forward, aim, 0.18f));
                    combat.SetPlanarVelocity(Vector3.zero);
                },
                onEnd = ReleaseCharge,
            };
            if (!combat.BeginAction(spec)) { charging = false; return; }
            ArenVFX.BeginCharge(transform);
            ArenAudio.BeginChargeTone(transform);
        }

        Vector3 CameraForward()
        {
            var cam = Camera.main;
            if (cam == null) return transform.forward;
            Vector3 f = cam.transform.forward; f.y = 0;
            return f.sqrMagnitude > 0.01f ? f.normalized : transform.forward;
        }

        void ChargeUpdate(float t)
        {
            int lvl = 0;
            for (int i = 0; i < chargeLevelTimes.Length; i++)
                if (t >= chargeLevelTimes[i] && Resonance >= chargeCosts[i]) lvl = i + 1;
            if (lvl > chargeLevel)
            {
                chargeLevel = lvl;
                OnChargeLevel?.Invoke(lvl);
                ArenAudio.Play(Sfx.ChargeLevel, transform.position, 0.7f, 1f + lvl * 0.12f);
                GameFeel.Shake(0.12f * lvl);
                GameFeel.FovPunch(-1.5f * lvl, 0.4f);
                ArenVFX.ChargeLevel(transform, lvl);
            }
            ArenVFX.UpdateCharge(transform, t / chargeLevelTimes[chargeLevelTimes.Length - 1], chargeLevel);
            ArenAudio.UpdateChargeTone(t / chargeLevelTimes[chargeLevelTimes.Length - 1]);
        }

        void ReleaseCharge()
        {
            if (!charging) return;
            charging = false;
            ArenVFX.EndCharge();
            ArenAudio.EndChargeTone();
            int lvl = chargeLevel;
            chargeLevel = 0;
            if (lvl <= 0) return;   // soltou cedo: não gasta nada

            int i = lvl - 1;
            Resonance = Mathf.Max(0f, Resonance - chargeCosts[i]);
            readyAt[(int)AbilityId.Contracanto] = Time.time + Def(AbilityId.Contracanto).cooldown;
            OnCast?.Invoke(AbilityId.Contracanto);

            // a liberação é uma ação nova (chamada a partir do fim da carga)
            bool fired = false;
            Vector3 dir = transform.forward;
            var spec = new ActionSpec
            {
                stateName = "Aren Release",
                duration = 0.75f,
                animSpeed = 1.15f,
                invulnerable = true,
                superArmor = true,
                cancelFrom = 0.55f,
                onUpdate = t =>
                {
                    if (!fired && t >= 0.14f)
                    {
                        fired = true;
                        ContracantoNow(dir, i);
                    }
                },
            };
            StartCoroutine(BeginNextFrame(spec));
        }

        System.Collections.IEnumerator BeginNextFrame(ActionSpec spec)
        {
            yield return null;   // o ArenCombat termina de encerrar a carga neste frame
            combat.BeginAction(spec);
        }

        void ContracantoNow(Vector3 dir, int i)
        {
            Vector3 c = transform.position;
            float len = chargeLength[i];
            ArenVFX.ContracantoWave(c + Vector3.up * 1.0f, dir, len, i + 1);
            ArenAudio.Play(Sfx.ContracantoRelease, c, 1f, 1f - i * 0.06f);
            GameFeel.Hitstop(0.1f + 0.03f * i);
            GameFeel.Shake(0.6f + 0.15f * i, dir);
            GameFeel.FovPunch(5f + 2f * i, 0.5f);
            contraData.damage = chargeDamage[i];
            contraData.knockback = 10f + 3f * i;
            int n = 0;
            var list = CombatRegistry.Enemies;
            for (int k = list.Count - 1; k >= 0; k--)
            {
                var e = list[k];
                if (e == null || !e.Alive) continue;
                Vector3 d = e.transform.position - c; d.y = 0;
                float along = Vector3.Dot(d, dir);
                float across = (d - dir * along).magnitude;
                // cone que alarga com a distância (onda abrindo)
                if (along < -0.5f || along > len + e.BodyRadius || across > 1.4f + along * 0.45f + e.BodyRadius) continue;
                Vector3 hd = d.sqrMagnitude > 0.01f ? d.normalized : dir;
                var h = new HitData
                {
                    damage = contraData.damage, point = e.AimPoint - hd * e.BodyRadius, direction = Vector3.Lerp(dir, hd, 0.4f).normalized,
                    knockback = contraData.knockback, stagger = 999f, kind = HitKind.Contracanto, team = Team.Player, source = gameObject
                };
                if (e.TakeHit(h)) { n++; ArenVFX.Impact(h.point, hd, ArenVFX.GoldColor, 1.8f); }
            }
            if (n > 0) combat.RegisterHit(n);
        }
    }
}
