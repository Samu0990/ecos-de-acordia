using System.Collections.Generic;
using Aren.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace Elyndra.World
{
    /// <summary>
    /// Zona de inimigos do mundo (não aparecem ao acaso): quando o Aren chega a <see cref="activation"/> m
    /// e a condição do WorldState vale, nascem <see cref="count"/> inimigos dos ids pedidos (bestiário),
    /// com vida/dano pelo NÍVEL da zona (zonas de dificuldade: Valtéria 1, Fronteira Muda 5). Some quando o
    /// Aren se afasta sem lutar; limpa = fica limpa (ou volta, se respawn). Id sem prefab → PLACEHOLDER
    /// visível com o nome do inimigo (troca automática quando o prefab entrar no bestiário).
    /// Os marcadores provisórios ficam num pool (reaproveitados entre zonas). Enquanto
    /// <see cref="EnemyCatalog.HumansOnly"/> valer, um id de animal vira um humano da mesma distorção.
    /// </summary>
    public class EnemySpawnZone : MonoBehaviour
    {
        public string id = "";
        public string label = "";
        public string[] enemyIds = { "sussurrante" };
        public int count = 2;
        public float radius = 7f;
        public float activation = 42f;
        public float despawn = 95f;
        [Range(1, 5)] public int tier = 1;
        public string requires = "";
        public bool respawn = true;

        readonly List<EnemyBase> alive = new List<EnemyBase>();
        readonly List<GameObject> markers = new List<GameObject>();
        bool active, cleared, engaged;
        float nextCheck;
        static readonly List<EnemySpawnZone> all = new List<EnemySpawnZone>();
        /// <summary>Testes automáticos: nenhuma zona nasce enquanto true.</summary>
        public static bool Suppress;
        static readonly Stack<GameObject> markerPool = new Stack<GameObject>();
        static Transform poolRoot, holdRoot;

        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        void Start()
        {
            if (!respawn && !string.IsNullOrEmpty(id) && WorldState.Has("zona:" + id)) cleared = true;
        }

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.35f + Random.value * 0.1f;
            var p = RegionFlow.Instance != null ? RegionFlow.Instance.Player : null;
            if (p == null) return;
            float d = Vector3.Distance(p.position, transform.position);
            if (!active)
            {
                if (!cleared && !Suppress && d < activation && WorldState.Check(requires)) Spawn();
                return;
            }
            alive.RemoveAll(e => e == null || !e.Alive);
            foreach (var e in alive) if (e.LastDamagedTime > 0) engaged = true;
            if (alive.Count == 0 && markers.Count == 0) { active = false; Clear(); return; }
            if (alive.Count == 0 && markers.Count > 0 && d > despawn) { Despawn(); return; }
            if (d > despawn && !engaged) Despawn();
        }

        void Spawn()
        {
            active = true; engaged = false;
            var cat = EnemyCatalog.Load();
            for (int i = 0; i < count; i++)
            {
                string eid = enemyIds.Length > 0 ? enemyIds[i % enemyIds.Length] : "sussurrante";
                var entry = cat != null ? cat.Find(eid) : null;
                if (entry != null && !entry.human && EnemyCatalog.HumansOnly) entry = cat.HumanFor(entry.distortion);
                Vector3 pos = PointNear(transform.position, radius);
                float yaw = Random.Range(0f, 360f);
                if (entry != null && entry.prefab != null) SpawnEnemy(entry, pos, yaw);
                else SpawnMarker(entry, eid, pos, yaw);
            }
        }

        void SpawnEnemy(EnemyCatalog.Entry entry, Vector3 pos, float yaw)
        {
            // instancia sob um pai DESLIGADO: o Awake (que copia maxHealth para a vida) só roda depois do ajuste
            if (holdRoot == null) { var h = new GameObject("_NascendoInimigos"); h.SetActive(false); holdRoot = h.transform; }
            var go = Instantiate(entry.prefab, pos, Quaternion.Euler(0, yaw, 0), holdRoot);
            var e = go.GetComponent<EnemyBase>();
            if (e != null)
            {
                float k = entry.healthScale * (1f + 0.3f * (tier - 1));
                e.maxHealth *= k;
                e.attackDamage *= 1f + 0.18f * (tier - 1);
                if (!string.IsNullOrEmpty(entry.displayName)) e.displayName = entry.displayName;
                if (e.isBoss && !entry.id.EndsWith("_alfa")) e.isBoss = false;   // corpo de chefe usado como inimigo comum
            }
            go.transform.SetParent(transform, true);
            if (e != null)
            {
                alive.Add(e);
                e.OnDied += d => Aren.World.Notas.Add(d.isBoss ? 600 : 100 + 10 * Mathf.Max(0, ChainNow()));
            }
        }

        static int ChainNow()
        {
            var c = FindAnyObjectByType<Aren.Combat.ArenCombat>();
            return c != null ? c.ChainCount : 0;
        }

        void SpawnMarker(EnemyCatalog.Entry entry, string eid, Vector3 pos, float yaw)
        {
            var m = markerPool.Count > 0 ? markerPool.Pop() : PlaceholderMarker.Create();
            m.transform.SetParent(transform, true);
            m.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            m.SetActive(true);
            m.GetComponent<PlaceholderMarker>().Set(entry != null ? entry.displayName : eid, entry != null ? entry.distortion : Distortion.Nenhuma);
            markers.Add(m);
        }

        void Despawn()
        {
            foreach (var e in alive) if (e != null) Destroy(e.gameObject);
            alive.Clear();
            ReturnMarkers();
            active = false;
        }

        void ReturnMarkers()
        {
            if (poolRoot == null) { poolRoot = new GameObject("_PoolMarcadores").transform; }
            foreach (var m in markers) { if (m == null) continue; m.SetActive(false); m.transform.SetParent(poolRoot, false); markerPool.Push(m); }
            markers.Clear();
        }

        void Clear()
        {
            ReturnMarkers();
            if (!respawn)
            {
                cleared = true;
                if (!string.IsNullOrEmpty(id)) WorldState.SetFlag("zona:" + id);
            }
        }

        /// <summary>Depois da morte do Aren: zonas ativas somem e voltam a esperar.</summary>
        public static void ResetAllNear(Vector3 p, float r)
        {
            foreach (var z in all.ToArray())
                if (z.active && (z.transform.position - p).sqrMagnitude < r * r) z.Despawn();
        }

        public static Vector3 PointNear(Vector3 c, float r)
        {
            for (int k = 0; k < 8; k++)
            {
                var off = Random.insideUnitCircle * r;
                var q = c + new Vector3(off.x, 0, off.y);
                if (NavMesh.SamplePosition(q, out var hit, 4f, NavMesh.AllAreas)) return hit.position;
            }
            if (Physics.Raycast(c + Vector3.up * 20f, Vector3.down, out var h2, 60f, ~((1 << 2) | (1 << 10)), QueryTriggerInteraction.Ignore)) return h2.point;
            return c;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.2f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.color = new Color(0.9f, 0.6f, 0.2f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, activation);
        }
    }
}
