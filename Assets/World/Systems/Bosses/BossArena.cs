using UnityEngine;
using UnityEngine.Events;

namespace Elyndra.World
{
    /// <summary>
    /// Arena de uma das Sete Notas Corrompidas (ou de um miniboss). Pronta para o chefe de verdade:
    /// entrada e saída, ponto do chefe, pontos de câmera cinematográfica, checkpoint antes da porta,
    /// espaço para VFX e a "névoa de luta" que fecha a arena. Os chefes ainda não existem em código:
    /// ao entrar, a arena anuncia a Nota (regra + Contramotivo do cânone) e chama os ganchos
    /// <see cref="onArenaEnter"/>/<see cref="onBossDefeated"/>; com um prefab em <see cref="bossPrefab"/>
    /// ela já faz a luta (vida, barra de chefe, porta que abre ao vencer).
    /// </summary>
    public class BossArena : MonoBehaviour
    {
        public string noteId = "";           // "do", "re"… (vazio = miniboss)
        public string minibossName = "";
        public string minibossEnemyId = "";  // id do bestiário para minibosses que já têm corpo
        public float radius = 30f;
        public Transform entrance, exit, bossSpawn, vfxAnchor;
        public Transform[] cameraPoints;
        public GameObject fightFog;          // fecha a arena durante a luta
        public GameObject bossPlaceholder;   // corpo provisório (ex.: Dó ainda formando o corpo)
        public GameObject bossPrefab;
        public string requires = "";
        public UnityEvent onArenaEnter, onBossDefeated;

        public static event System.Action<BossArena> ArenaEntered, BossDefeated;
        /// <summary>Testes automáticos: as arenas anunciam mas não começam a luta.</summary>
        public static bool Suppress;
        public NoteDef Note => WorldCanon.Note(noteId);
        bool inside, fighting;
        Aren.Enemies.EnemyBase boss;
        float shown = -100f;

        public bool Defeated => !string.IsNullOrEmpty(noteId) ? WorldState.NoteReafinada(noteId) : WorldState.Has("miniboss:" + minibossName);

        void Start()
        {
            if (fightFog != null) fightFog.SetActive(false);
            if (bossPlaceholder != null) bossPlaceholder.SetActive(!Defeated);
        }

        void Update()
        {
            var p = RegionFlow.Instance != null ? RegionFlow.Instance.Player : null;
            if (p == null) return;
            bool now = (p.position - transform.position).sqrMagnitude < radius * radius && WorldState.Check(requires);
            if (now && !inside) Enter();
            inside = now;
            if (fighting && boss != null && !boss.Alive) Win();
        }

        void Enter()
        {
            if (Defeated) return;
            ArenaEntered?.Invoke(this);
            onArenaEnter?.Invoke();
            var hud = Aren.UI.ArenHUD.Instance;
            if (Time.time - shown > 20f && hud != null)
            {
                shown = Time.time;
                var n = Note;
                if (n != null)
                {
                    hud.ShowArea(n.name, n.epithet + " · " + n.arena);
                    hud.ShowHint("<b>Regra:</b> " + n.rule + "\n<b>Contramotivo:</b> " + n.contramotivo + (bossPrefab == null ? "\n<i>(chefe ainda não implementado — arena pronta)</i>" : ""), 9f);
                }
                else hud.ShowArea(minibossName, "miniboss");
            }
            if (!fighting && !Suppress) StartFight();
        }

        void StartFight()
        {
            GameObject prefab = bossPrefab;
            if (prefab == null && !string.IsNullOrEmpty(minibossEnemyId))
            {
                var cat = EnemyCatalog.Load();
                var e = cat != null ? cat.Find(minibossEnemyId) : null;
                if (e != null && (e.human || !EnemyCatalog.HumansOnly)) prefab = e.prefab;
            }
            if (prefab == null || bossSpawn == null) return;
            fighting = true;
            if (fightFog != null) fightFog.SetActive(true);
            var go = Instantiate(prefab, bossSpawn.position, bossSpawn.rotation);
            boss = go.GetComponent<Aren.Enemies.EnemyBase>();
            if (boss != null)
            {
                boss.isBoss = true;
                if (!string.IsNullOrEmpty(minibossName)) { boss.displayName = minibossName; boss.subtitle = "miniboss"; }
            }
            Aren.ArenAudio.PlaySting(Aren.Sting.Boss);
        }

        void Win()
        {
            fighting = false;
            if (fightFog != null) fightFog.SetActive(false);
            if (!string.IsNullOrEmpty(noteId)) WorldState.ReafinarNote(noteId);
            else WorldState.SetFlag("miniboss:" + minibossName);
            if (bossPlaceholder != null) bossPlaceholder.SetActive(false);
            BossDefeated?.Invoke(this);
            onBossDefeated?.Invoke();
            Aren.ArenAudio.PlaySting(Aren.Sting.Clear);
        }

        /// <summary>Teste/depuração: dá a luta como vencida (abre as rotas que dependem dela).</summary>
        public void DebugDefeat() => Win();

        void OnDrawGizmos()
        {
            Gizmos.color = string.IsNullOrEmpty(noteId) ? new Color(1f, 0.6f, 0.2f, 0.5f) : new Color(1f, 0.15f, 0.25f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
            if (bossSpawn != null) Gizmos.DrawSphere(bossSpawn.position, 1.2f);
        }
    }
}
