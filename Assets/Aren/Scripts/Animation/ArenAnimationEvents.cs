using Aren.Combat;
using UnityEngine;

namespace Aren
{
    /// <summary>
    /// Receptor central dos Animation Events do Aren. O componente fica no mesmo objeto do
    /// Animator (exigencia da Unity) e encaminha cada marcador para o sistema que realmente
    /// possui a regra de jogo. Os receptores validam o estado atual e ignoram eventos tardios.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class ArenAnimationEvents : MonoBehaviour
    {
        ArenFootsteps footsteps;
        ArenCombat combat;
        ArenAbilities abilities;

        public int FootstepEvents { get; private set; }
        public int AttackEvents { get; private set; }
        public int AbilityEvents { get; private set; }

        public string DebugStatus
        {
            get
            {
                int footFallback = footsteps != null ? footsteps.BoneFallbackCount : 0;
                int attackFallback = combat != null ? combat.AnimationFallbackCount : 0;
                int abilityFallback = abilities != null ? abilities.AnimationFallbackCount : 0;
                return $"passos_evt={FootstepEvents} passos_fallback={footFallback} " +
                       $"ataques_evt={AttackEvents} ataques_fallback={attackFallback} " +
                       $"habilidades_evt={AbilityEvents} habilidades_fallback={abilityFallback}";
            }
        }

        void Awake() => Cache();

        void Cache()
        {
            if (footsteps == null) footsteps = GetComponent<ArenFootsteps>();
            if (combat == null) combat = GetComponent<ArenCombat>();
            if (abilities == null) abilities = GetComponent<ArenAbilities>();
        }

        // Os nomes abaixo sao gravados nos AnimationClips por ArenAnimationEventSetup.
        public void FootstepLeft()
        {
            Cache();
            if (footsteps != null && footsteps.AnimationFootstep(true)) FootstepEvents++;
        }

        public void FootstepRight()
        {
            Cache();
            if (footsteps != null && footsteps.AnimationFootstep(false)) FootstepEvents++;
        }

        public void AttackSwing()
        {
            Cache();
            if (combat != null && combat.AnimationAttackSwing()) AttackEvents++;
        }

        public void AttackContact()
        {
            Cache();
            if (combat != null && combat.AnimationAttackContact()) AttackEvents++;
        }

        public void AttackFinisherBeat(int beat)
        {
            Cache();
            if (combat != null && combat.AnimationAttackFinisherBeat(beat)) AttackEvents++;
        }

        public void PulseRelease()
        {
            Cache();
            if (abilities != null && abilities.AnimationPulseRelease()) AbilityEvents++;
        }

        public void BladePrepare()
        {
            Cache();
            if (abilities != null && abilities.AnimationBladePrepare()) AbilityEvents++;
        }

        public void BladeRelease()
        {
            Cache();
            if (abilities != null && abilities.AnimationBladeRelease()) AbilityEvents++;
        }

        public void ContracantoRelease()
        {
            Cache();
            if (abilities != null && abilities.AnimationContracantoRelease()) AbilityEvents++;
        }
    }
}
