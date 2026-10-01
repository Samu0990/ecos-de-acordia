using UnityEngine;

namespace Aren.Combat
{
    /// <summary>
    /// Escolha de alvo do Freeflow (doc Freeflow §7–9): a direção do analógico manda;
    /// sem direção, mantém o alvo atual ou pega o mais próximo à frente. Um alvo atual só
    /// é trocado se o novo for claramente melhor (histerese) — evita "pular" entre dois
    /// inimigos lado a lado.
    /// </summary>
    public static class TargetResolver
    {
        public struct Params
        {
            public float range;          // alcance de busca (m, até a borda do corpo)
            public float coneHalfAngle;  // cone em torno da intenção (graus)
            public float maxHeightDiff;
        }

        public static readonly Params Default = new Params { range = 6.5f, coneHalfAngle = 62f, maxHeightDiff = 2.2f };

        public static IDamageable Resolve(Vector3 origin, Vector3 facing, Vector3 intent, IDamageable current, Params p)
        {
            IDamageable best = null;
            float bestScore = float.MaxValue;
            float curScore = float.MaxValue;
            bool hasIntent = intent.sqrMagnitude > 0.01f;
            Vector3 refDir = hasIntent ? intent.normalized : facing.normalized;
            float cone = hasIntent ? p.coneHalfAngle : 100f;

            var list = CombatRegistry.Enemies;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || !e.Alive) continue;
                Vector3 d = e.transform.position - origin;
                if (Mathf.Abs(d.y) > p.maxHeightDiff) continue;
                d.y = 0;
                float dist = Mathf.Max(0f, d.magnitude - e.BodyRadius);
                if (dist > p.range) continue;
                float ang = d.sqrMagnitude < 0.0001f ? 0f : Vector3.Angle(refDir, d);
                if (ang > cone && dist > 1.2f) continue;   // muito perto conta mesmo fora do cone
                // ângulo pesa mais que distância quando há intenção (o jogador apontou)
                float score = hasIntent ? ang / cone * 1.4f + dist / p.range * 0.6f
                                        : ang / cone * 0.6f + dist / p.range * 1.4f;
                if (e == current) curScore = score;
                if (score < bestScore) { bestScore = score; best = e; }
            }

            // histerese: fica no alvo atual se a diferença for pequena
            if (current != null && current.Alive && curScore < float.MaxValue && curScore <= bestScore + 0.18f)
                return current;
            return best;
        }
    }
}
