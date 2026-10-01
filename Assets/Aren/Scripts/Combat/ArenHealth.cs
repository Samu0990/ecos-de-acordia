using UnityEngine;

namespace Aren.Combat
{
    /// <summary>Vida do Aren. Os i-frames e o parry vêm do ArenCombat.</summary>
    [RequireComponent(typeof(ArenCombat))]
    public class ArenHealth : MonoBehaviour, IDamageable
    {
        public float maxHealth = 100f;
        [Tooltip("Regeneração fora de combate (HP/s), começa após 'regenDelay' s sem levar dano.")]
        public float regenPerSecond = 6f;
        public float regenDelay = 6f;

        public float Health { get; private set; }
        public float Normalized => Health / maxHealth;
        public Team Team => Team.Player;
        public bool Alive => Health > 0f;
        public Vector3 AimPoint => transform.position + Vector3.up * 1.15f;
        public float BodyRadius => 0.35f;
        public float LastDamageTime { get; private set; } = -100f;

        public event System.Action<float, HitData> OnDamaged;   // dano efetivo
        public event System.Action OnDied;
        public event System.Action OnRevived;

        ArenCombat combat;

        void Awake()
        {
            combat = GetComponent<ArenCombat>();
            Health = maxHealth;
        }

        void OnEnable() => CombatRegistry.Register(this);
        void OnDisable() => CombatRegistry.Unregister(this);

        void Update()
        {
            if (Alive && Health < maxHealth && Time.time - LastDamageTime > regenDelay && !combat.InCombatRecently)
                Health = Mathf.Min(maxHealth, Health + regenPerSecond * Time.deltaTime);
        }

        public bool TakeHit(in HitData hit)
        {
            if (!Alive || hit.team == Team.Player) return false;
            if (combat.TryParryIncoming(hit.source)) return false;
            if (combat.Invulnerable)
            {
                combat.NotifyDodgedHit();
                return false;
            }
            float dmg = hit.damage;
            Health = Mathf.Max(0f, Health - dmg);
            LastDamageTime = Time.time;
            bool died = Health <= 0f;
            ArenAudio.Play(Sfx.Hurt, AimPoint, 0.9f);
            ArenVFX.Impact(hit.point, hit.direction, ArenVFX.CorruptColor, 1.1f);
            GameFeel.Hitstop(0.06f);
            GameFeel.Shake(0.35f, hit.direction);
            combat.ReceiveHurt(hit, died);
            OnDamaged?.Invoke(dmg, hit);
            if (died) OnDied?.Invoke();
            return true;
        }

        public void Revive(float fraction = 1f)
        {
            Health = maxHealth * fraction;
            combat.Revive();
            OnRevived?.Invoke();
        }

        public void Heal(float amount) { if (Alive) Health = Mathf.Min(maxHealth, Health + amount); }
    }
}
