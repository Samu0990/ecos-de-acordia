using System.Collections.Generic;
using UnityEngine;

namespace Aren.Combat
{
    /// <summary>
    /// Lâmina de Frequência: nota comprimida que vira um corte em arco, atravessa inimigos
    /// (cada um apanha uma vez) e é acompanhada por dois harmônicos menores.
    /// </summary>
    public class FrequencyBlade : MonoBehaviour
    {
        Vector3 dir;
        float speed, range, traveled;
        AttackData data;
        GameObject owner;
        ArenCombat combat;
        readonly HashSet<IDamageable> hit = new HashSet<IDamageable>();
        const float Radius = 0.95f;
        Transform visual;

        public static FrequencyBlade Spawn(Vector3 origin, Vector3 direction, float speed, float range, AttackData data, GameObject owner, ArenCombat combat)
        {
            var go = new GameObject("FrequencyBlade");
            go.transform.position = origin;
            go.transform.rotation = Quaternion.LookRotation(direction);
            var b = go.AddComponent<FrequencyBlade>();
            b.dir = direction.normalized; b.speed = speed; b.range = range;
            b.data = data; b.owner = owner; b.combat = combat;
            b.visual = ArenVFX.CreateBladeVisual(go.transform);
            return b;
        }

        void Update()
        {
            float step = speed * Time.deltaTime;
            Vector3 from = transform.position;
            transform.position += dir * step;
            traveled += step;

            // parede/cenário: estoura na superfície
            if (Physics.Raycast(from, dir, out var wall, step, ~0, QueryTriggerInteraction.Ignore)
                && wall.collider.GetComponentInParent<IDamageable>() == null && wall.collider.attachedRigidbody == null)
            {
                ArenVFX.Impact(wall.point, -wall.normal, data.slashColor, 0.9f);
                ArenAudio.Play(Sfx.BladeHit, wall.point, 0.5f, 1.3f);
                Finish();
                return;
            }

            var list = CombatRegistry.Enemies;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (e == null || !e.Alive || hit.Contains(e)) continue;
                Vector3 d = e.AimPoint - transform.position;
                if (d.magnitude > Radius + e.BodyRadius) continue;
                hit.Add(e);
                var h = new HitData
                {
                    damage = data.damage, point = e.AimPoint - dir * e.BodyRadius, direction = new Vector3(dir.x, 0, dir.z).normalized,
                    knockback = data.knockback, stagger = data.stagger, kind = HitKind.Ability, team = Team.Player, source = owner
                };
                if (e.TakeHit(h))
                {
                    ArenVFX.Impact(h.point, dir, data.slashColor, 1.15f);
                    ArenAudio.Play(Sfx.BladeHit, h.point, 0.85f, Random.Range(0.97f, 1.08f));
                    ArenAudio.Note(data.noteIndex + hit.Count, 0.6f, 1.1f);
                    GameFeel.Hitstop(0.035f);
                    if (combat != null) combat.RegisterHit(1);
                }
            }

            if (traveled >= range) Finish();
        }

        void Finish()
        {
            if (visual != null) ArenVFX.DetachAndFade(visual, 0.15f);
            Destroy(gameObject);
        }
    }
}
