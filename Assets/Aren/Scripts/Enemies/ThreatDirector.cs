using System.Collections.Generic;
using UnityEngine;

namespace Aren.Enemies
{
    /// <summary>
    /// Regula quem ataca (doc Freeflow §20): no máximo N inimigos com "ficha" de ataque ao
    /// mesmo tempo e um intervalo mínimo entre inícios de ataque — o jogador consegue ler
    /// cada aviso. Os demais circulam em volta esperando a vez.
    /// </summary>
    public static class ThreatDirector
    {
        public static int MaxAttackers = 2;
        public static float MinGap = 0.55f;
        static readonly List<EnemyBase> holders = new List<EnemyBase>(4);
        static float lastGrant = -10f;

        public static bool RequestToken(EnemyBase e)
        {
            holders.RemoveAll(h => h == null || !h.Alive || !h.HoldsToken);
            if (holders.Contains(e)) return true;
            if (holders.Count >= MaxAttackers) return false;
            if (Time.time - lastGrant < MinGap) return false;
            holders.Add(e);
            lastGrant = Time.time;
            return true;
        }

        public static void Release(EnemyBase e) => holders.Remove(e);

        public static void Reset() { holders.Clear(); lastGrant = -10f; }
    }
}
