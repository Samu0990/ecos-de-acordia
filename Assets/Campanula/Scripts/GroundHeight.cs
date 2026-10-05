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
