using UnityEngine;

namespace Aren.Enemies
{
    /// <summary>
    /// Eco Possuído: aldeão tomado pela Corrupção (Bíblia de Lore — Campanula). Humanoide
    /// com clipes CC0 da UAL2 (andar/atacar de zumbi = movimento "fora do compasso").
    /// </summary>
    public class EnemyEco : EnemyBase
    {
        Animator anim;
        static readonly int MoveHash = Animator.StringToHash("MoveSpeed");
        static readonly int SpeedHash = Animator.StringToHash("StateSpeed");
        static readonly int LocoHash = Animator.StringToHash("LocoSpeed");
        float animMove;

        protected override void Awake()
        {
            base.Awake();
            anim = GetComponentInChildren<Animator>();
            if (anim != null) anim.applyRootMotion = false;
        }

        void Play(string state, float fade, float speed = 1f, float offset = 0f)
        {
            if (anim == null) return;
            anim.SetFloat(SpeedHash, speed);
            anim.CrossFadeInFixedTime(state, fade, 0, offset);
        }

        protected override void OnEnterState(EnemyState s)
        {
            switch (s)
            {
                case EnemyState.Spawning: Play("GetUp", 0.0f, 1.0f, 0.2f); break;
                case EnemyState.Idle:
                case EnemyState.Chase:
                case EnemyState.Circle: Play("Locomotion", 0.2f); break;
                // a garra sobe durante o aviso e desce no golpe (contato do clipe ≈ 0.62 s)
                case EnemyState.Telegraph: Play("Attack", 0.1f, 0.45f / telegraphTime, 0.05f); break;
                case EnemyState.Attack: anim?.SetFloat(SpeedHash, 1.4f); break;
                case EnemyState.Recover: anim?.SetFloat(SpeedHash, 1f); break;
                case EnemyState.Hurt: Play("Hurt", 0.05f, 1.6f, 0.05f); break;
                case EnemyState.Stunned: Play("Hurt", 0.05f, 0.55f, 0.05f); break;
                case EnemyState.Knockdown: Play("Knock", 0.05f, 1.2f, 0.05f); break;
                case EnemyState.Dead: Play("Knock", 0.05f, 1.1f, 0.05f); break;
            }
            if (s == EnemyState.Knockdown) Invoke(nameof(GetUpLater), knockdownTime - 0.9f);
            if (s == EnemyState.Knockdown || s == EnemyState.Dead) Invoke(nameof(FallSound), 0.42f);
        }

        void FallSound() => ArenAudio.Play(Sfx.BodyFall, transform.position, 0.55f, Random.Range(0.9f, 1.05f));

        void GetUpLater()
        {
            if (State == EnemyState.Knockdown) Play("GetUp", 0.2f, 1.4f, 0.1f);
        }

        protected override void SetMove(float speed)
        {
            animMove = Mathf.MoveTowards(animMove, speed, Time.deltaTime * 8f);
            if (anim != null)
            {
                anim.SetFloat(MoveHash, animMove);
                // o clipe de andar é lento: acelera a animação em vez de deslizar os pés
                anim.SetFloat(LocoHash, Mathf.Max(1f, animMove / 1.4f));
            }
        }
    }
}
