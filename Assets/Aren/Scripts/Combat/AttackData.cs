using UnityEngine;

namespace Aren.Combat
{
    /// <summary>
    /// Dados de um golpe (Freeflow §11 / Bíblia de Design 29.1). Tempos em segundos de
    /// jogo; o clipe é acelerado/esticado para caber em startup+active+recovery.
    /// Valores iniciais vieram da análise de velocidade da mão em cada clipe UAL2
    /// (ArenCombatSetup.AnalyzeClips) e foram ajustados no playtest automatizado.
    /// </summary>
    [CreateAssetMenu(menuName = "Aren/Attack Data", fileName = "Attack_")]
    public class AttackData : ScriptableObject
    {
        [Header("Animação")]
        public string stateName = "Aren Atk1";
        [Tooltip("Velocidade do estado no Animator (1 = clipe original).")]
        public float animSpeed = 1f;
        public float crossFade = 0.06f;

        [Header("Tempos (s)")]
        public float startup = 0.18f;
        public float active = 0.08f;
        public float recovery = 0.32f;
        [Tooltip("A partir daqui (desde o início) outro ataque/dodge pode cancelar o recovery.")]
        public float cancelFrom = 0.30f;
        [Tooltip("Janela para o próximo golpe do combo continuar a sequência (após o fim).")]
        public float comboKeep = 0.55f;

        [Header("Dano")]
        public float damage = 10f;
        public float stagger = 10f;
        public float knockback = 2.5f;
        public HitKind kind = HitKind.Light;
        [Tooltip("Alcance a partir do peito do Aren até a borda do corpo do alvo.")]
        public float reach = 1.6f;
        [Tooltip("Meio-ângulo do arco atingido (graus).")]
        public float arcHalfAngle = 70f;
        public float hitstop = 0.05f;

        [Header("Movimento (assistência)")]
        [Tooltip("Distância máxima para o Aren buscar o alvo durante o startup (nunca teleporta).")]
        public float magnetRange = 5.5f;
        [Tooltip("Velocidade máxima do avanço magnético (m/s).")]
        public float magnetMaxSpeed = 14f;
        [Tooltip("Passo para frente sem alvo (m).")]
        public float stepNoTarget = 0.6f;

        [Header("Feedback")]
        public int noteIndex = 0;
        [Tooltip("Ângulo do arco de VFX em torno do eixo de visão (graus): 0 = horizontal da direita p/ esquerda.")]
        public float slashRoll = 0f;
        public float slashRadius = 1.45f;
        public Color slashColor = new Color(0.55f, 0.95f, 1f, 1f);
        public float shake = 0.15f;
    }
}
