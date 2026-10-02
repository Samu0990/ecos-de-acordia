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
}
