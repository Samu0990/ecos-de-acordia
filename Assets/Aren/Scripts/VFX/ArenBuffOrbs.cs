using Aren.Combat;
using UnityEngine;

namespace Aren
{
    /// <summary>
    /// Orbes-nota que traduzem Ressonância e buffs sem aumentar o HUD: aparecem durante
    /// o combate, ganham roxo no Eco Fantasma e dourado ao carregar o Contracanto.
    /// São três objetos persistentes; não há Instantiate/Destroy por quadro.
    /// </summary>
    [DefaultExecutionOrder(510)]
    public class ArenBuffOrbs : MonoBehaviour
    {
        readonly ResonanceOrbVisual[] orbs = new ResonanceOrbVisual[3];
        readonly float[] alpha = new float[3];
        ArenAbilities abilities;
        ArenCombat combat;
        Camera cam;

        public int ActiveCount { get; private set; }

        void Start()
        {
            abilities = GetComponent<ArenAbilities>();
            combat = GetComponent<ArenCombat>();
            for (int i = 0; i < orbs.Length; i++)
            {
                orbs[i] = ArenVFX.CreateOrbVisual(transform, "BuffOrb_" + (i + 1), 0.17f, ArenVFX.FluteColor);
                orbs[i].SetVisual(ArenVFX.FluteColor, 0f);
                orbs[i].ClearTrail();
            }
        }

        void LateUpdate()
        {
            if (abilities == null) return;
            if (cam == null) cam = Camera.main;

            float resonance = abilities.ResonanceNormalized;
            int wanted = resonance > 0.08f ? 1 : 0;
            if (resonance > 0.42f) wanted = 2;
            if (resonance > 0.75f) wanted = 3;
            bool empowered = abilities.EchoActive || abilities.Charging;
            if (empowered) wanted = 3;
            bool visible = empowered || (combat != null && combat.InCombatRecently);
            ActiveCount = visible ? wanted : 0;

            Color color = abilities.Charging ? ArenVFX.GoldColor
                : abilities.EchoActive ? ArenVFX.EchoColor : ArenVFX.FluteColor;
            Vector3 center = transform.position + Vector3.up * 1.28f;
            Vector3 viewForward = cam != null ? cam.transform.forward : transform.forward;
            viewForward.y = 0f;
            if (viewForward.sqrMagnitude < 0.01f) viewForward = transform.forward;
            viewForward.Normalize();
            Vector3 viewRight = Vector3.Cross(Vector3.up, viewForward).normalized;

            for (int i = 0; i < orbs.Length; i++)
            {
                float targetAlpha = visible && i < wanted ? (abilities.Charging ? 1f : 0.78f) : 0f;
                alpha[i] = Mathf.MoveTowards(alpha[i], targetAlpha, Time.deltaTime * 4.5f);
                float angle = Time.time * (82f + i * 7f) + i * 120f;
                float rad = angle * Mathf.Deg2Rad;
                float radius = abilities.Charging ? 0.68f : 0.78f;
                Vector3 offset = viewRight * (Mathf.Cos(rad) * radius)
                    + viewForward * (Mathf.Sin(rad) * radius * 0.42f)
                    + Vector3.up * (Mathf.Sin(rad * 1.7f) * 0.12f + i * 0.035f);
                orbs[i].transform.position = center + offset;
                float pulse = 1f + Mathf.Sin(Time.time * 7f + i * 2.1f) * 0.12f;
                orbs[i].transform.localScale = Vector3.one * 0.17f * pulse;
                orbs[i].SetVisual(color, alpha[i]);
            }
        }

        void OnDisable()
        {
            ActiveCount = 0;
            for (int i = 0; i < orbs.Length; i++)
                if (orbs[i] != null) orbs[i].SetVisual(ArenVFX.FluteColor, 0f);
        }
    }
}
