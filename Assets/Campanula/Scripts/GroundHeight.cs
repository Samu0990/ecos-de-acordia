using UnityEngine;

namespace Campanula
{
    /// <summary>Altura do chão em tempo de jogo (raio de cima para baixo, ignora jogador e gatilhos).</summary>
    public static class GroundHeight
    {
        public static float At(float x, float z, float fallback = 0f)
        {
            int mask = ~((1 << 10) | (1 << 8));   // sem Player e sem Ledge
            if (Physics.Raycast(new Vector3(x, 250f, z), Vector3.down, out var hit, 500f, mask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return fallback;
        }
    }

    /// <summary>
    /// Relevo dos morros em volta da vila (o builder e a paisagem distante usam a mesma função, para os
    /// dois se encontrarem na borda): o anel de morros de antes (12–34 m) agora com cristas e ravinas
    /// (ruído de cristas em 3 oitavas) e calombos — antes eram bolhas lisas.
    /// </summary>
    public static class Relief
    {
        public static float HillMask(float x, float z)
        {
            float r = Mathf.Max(Mathf.Abs(x - 15f) / 1.15f, Mathf.Abs(z));
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(105f, 150f, r));
        }

        public static float Hills(float x, float z)
        {
            float hill = HillMask(x, z);
            if (hill <= 0f) return 0f;
            float n = Mathf.PerlinNoise(x * 0.018f + 3.1f, z * 0.018f + 7.7f);
            float h = hill * (12f + 22f * n);
            // cristas e ravinas: 1 − |2p − 1| faz linhas finas no alto (cristas) e vales largos
            float rid = 0f, a = 1f, f = 0.032f;
            for (int o = 0; o < 3; o++)
            {
                float v = 1f - Mathf.Abs(Mathf.PerlinNoise(x * f + 11.3f * o + 0.7f, z * f + 5.1f * o + 1.9f) * 2f - 1f);
                rid += v * v * a; a *= 0.45f; f *= 2.13f;
            }
            h += hill * hill * (rid - 0.75f) * 6f;
            // calombos de poucos metros (o chão nunca é um plano liso)
            h += hill * (Mathf.PerlinNoise(x * 0.12f + 2.3f, z * 0.12f + 8.1f) - 0.5f) * 1.6f;
            // o vale do riacho: ele entra pelo norte e sai pelo sul por entre os morros (antes a faixa de
            // água subia a encosta e aparecia em pedaços — os "retângulos claros" no morro do sul)
            float dxs = Mathf.Abs(x - StreamMath.Center(z));
            float valley = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(7f, 32f, dxs + (Mathf.PerlinNoise(z * 0.05f, 3.3f) - 0.5f) * 8f));
            h *= 1f - 0.93f * valley;
            return h;
        }
    }

    /// <summary>Traçado do riacho a leste da vila (o builder e o áudio usam o mesmo).</summary>
    public static class StreamMath
    {
        public static float Center(float z) => 42f + Mathf.Sin(z * 0.045f) * 2.2f;

        // O desfiladeiro (referência de Campânula do storyboard: pontes de arcos sobre um cânion com
        // cachoeiras): o riacho cai de uma vez na cabeceira norte e corre 16 m abaixo da cidade; ao sul o
        // leito sobe devagar até virar riacho de novo.
        public const float GorgeNorth = 62f, GorgeSouth = -64f, GorgeDepth = 16f, GorgeHalfTop = 9f;

        /// <summary>Profundidade do desfiladeiro (m, ≥ 0) no ponto: paredes íngremes de ~3 m na borda, fundo plano.</summary>
        public static float Gorge(float x, float z)
        {
            float n = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(GorgeNorth + 1.5f, GorgeNorth - 1.5f, z));   // cabeceira: degrau (a cachoeira)
            float s = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(GorgeSouth - 46f, GorgeSouth, z));            // sul: sobe devagar
            float along = n * s;
            if (along <= 0f) return 0f;
            float dx = Mathf.Abs(x - Center(z));
            float side = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(GorgeHalfTop, GorgeHalfTop - 3.2f, dx));
            return GorgeDepth * along * side;
        }

        /// <summary>Quanto do desfiladeiro existe naquela altura do riacho (0 fora, 1 no trecho fundo).</summary>
        public static float GorgeAlong(float z) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(GorgeNorth + 1.5f, GorgeNorth - 1.5f, z)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(GorgeSouth - 46f, GorgeSouth, z));
    }
}
