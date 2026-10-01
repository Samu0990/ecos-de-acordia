using System.Collections.Generic;
using UnityEngine;

namespace Aren.Combat
{
    public enum Team { Player, Enemy }

    public enum HitKind
    {
        Light,      // M1 1-3
        Heavy,      // finalizador, contra-ataque
        Ability,    // Pulso, Lâmina, Eco
        Contracanto
    }

    /// <summary>Tudo que um golpe carrega até quem apanha.</summary>
    public struct HitData
    {
        public float damage;
        public Vector3 point;
        public Vector3 direction;   // horizontal, de quem bate para quem apanha
        public float knockback;     // m/s iniciais
        public float stagger;       // pontos de desequilíbrio
        public HitKind kind;
        public Team team;
        public GameObject source;
    }

    public interface IDamageable
    {
        Team Team { get; }
        bool Alive { get; }
        Transform transform { get; }
        /// <summary>Ponto de mira (peito) — VFX e alvo da Lâmina nascem aqui.</summary>
        Vector3 AimPoint { get; }
        /// <summary>Raio aproximado do corpo (para distância de golpe).</summary>
        float BodyRadius { get; }
        /// <summary>Retorna true se o golpe acertou (false = invulnerável/ignorado).</summary>
        bool TakeHit(in HitData hit);
    }

    /// <summary>Inimigo que pode ser contra-atacado durante o telegraph do ataque.</summary>
    public interface ICounterable
    {
        bool CounterWindowOpen { get; }
        /// <summary>0..1 dentro da janela (1 = golpe saindo). Counter tardio (>0.6) = perfeito.</summary>
        float CounterWindowProgress { get; }
        void OnCountered(Vector3 from);
    }

    /// <summary>
    /// Registro estático de alvos vivos. Evita FindObjectsOfType e OverlapSphere com
    /// alocação em todo golpe (o notebook alvo é fraco).
    /// </summary>
    public static class CombatRegistry
    {
        public static readonly List<IDamageable> Enemies = new List<IDamageable>(32);
        public static IDamageable Player;

        public static void Register(IDamageable d)
        {
            if (d.Team == Team.Player) { Player = d; return; }
            if (!Enemies.Contains(d)) Enemies.Add(d);
        }

        public static void Unregister(IDamageable d)
        {
            if (d.Team == Team.Player) { if (Player == d) Player = null; return; }
            Enemies.Remove(d);
        }

        /// <summary>Inimigos vivos dentro do raio (horizontal), sem alocação: preenche a lista dada.</summary>
        public static void EnemiesInRadius(Vector3 center, float radius, List<IDamageable> result)
        {
            result.Clear();
            float r2 = radius * radius;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var e = Enemies[i];
                if (e == null || !e.Alive) continue;
                Vector3 d = e.transform.position - center; d.y = 0;
                float rr = radius + e.BodyRadius;
                if (d.sqrMagnitude <= rr * rr) result.Add(e);
            }
        }

        public static int AliveEnemyCount()
        {
            int n = 0;
            for (int i = 0; i < Enemies.Count; i++) if (Enemies[i] != null && Enemies[i].Alive) n++;
            return n;
        }
    }
}
