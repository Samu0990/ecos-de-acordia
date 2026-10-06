using Aren;
using Aren.Combat;
using UnityEngine;

namespace Elyndra.World
{
    /// <summary>
    /// Contramotivo do Vórtice "Sino que Lembra Amanhãs" (Valtéria). A regra: os moradores reagem ao que
    /// AINDA vai acontecer — tudo segue o toque do sino, que bate num compasso fixo. Golpear o sino NO
    /// tempo é exatamente o que o padrão prevê (ele absorve e o compasso recomeça). O Contramotivo é
    /// improvisar: três golpes seguidos no CONTRATEMPO (longe das batidas) provam que a regra não é
    /// absoluta e o Vórtice se desfaz. O sino é um alvo para a mira do Aren só enquanto o Vórtice existe.
    /// </summary>
    public class ContramotivoOffbeatBell : MonoBehaviour, IDamageable
    {
        public VortexZone vortex;
        public Transform bell;
        public float beat = 1.8f;
        public int needed = 3;
        public float onBeatWindow = 0.32f;

        int streak; float t; bool registered; float swing;
        public Team Team => Team.Enemy;
        public bool Alive => vortex != null && vortex.PlayerInside && !WorldState.VortexBroken(vortex.vortexId);
        public Vector3 AimPoint => (bell != null ? bell.position : transform.position);
        public float BodyRadius => 0.8f;

        void Update()
        {
            bool want = Alive;
            if (want != registered)
            {
                registered = want;
                if (want) CombatRegistry.Register(this); else CombatRegistry.Unregister(this);
            }
            if (vortex == null || WorldState.VortexBroken(vortex.vortexId)) return;
            float prev = t;
            t += Time.deltaTime;
            if (Mathf.Floor(prev / beat) != Mathf.Floor(t / beat))
            {
                swing = 1f;
                if (vortex.PlayerInside) ArenAudio.Play(Sfx.Bell, AimPoint, 0.55f, 0.82f);
            }
            swing = Mathf.MoveTowards(swing, 0f, Time.deltaTime * 1.6f);
            if (bell != null) bell.localRotation = Quaternion.Euler(Mathf.Sin(t * 6f) * 18f * swing, 0, 0);
        }

        void OnDisable() { if (registered) { CombatRegistry.Unregister(this); registered = false; } }

        public bool TakeHit(in HitData hit)
        {
            if (!Alive) return false;
            float phase = Mathf.Repeat(t, beat);
            float toBeat = Mathf.Min(phase, beat - phase);
            var hud = Aren.UI.ArenHUD.Instance;
            if (toBeat < onBeatWindow)
            {
                streak = 0;
                hud?.Toast("O sino já sabia… (golpe no tempo)", new Color(0.7f, 0.55f, 0.9f), 1.2f);
                ArenAudio.Play(Sfx.Bell, AimPoint, 0.8f, 0.7f);
            }
            else
            {
                streak++;
                swing = 1f;
                hud?.Toast("Contratempo " + streak + "/" + needed, Aren.UI.UIKit.Gold, 1.0f);
                ArenAudio.Play(Sfx.Bell, AimPoint, 0.8f, 1.25f + 0.1f * streak);
                if (streak >= needed) vortex.BreakWithContramotivo();
            }
            return true;
        }
    }
}
