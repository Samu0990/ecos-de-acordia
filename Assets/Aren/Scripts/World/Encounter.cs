using System.Collections.Generic;
using Aren.Enemies;
using UnityEngine;

namespace Aren.World
{
    /// <summary>
    /// Encontro em ondas: quando o jogador entra no raio, nascem os inimigos de cada onda
    /// (com pequeno intervalo entre eles para ler a chegada). Reinicia limpo se o jogador
    /// morrer no meio.
    /// </summary>
    public class Encounter
    {
        public string name;
        public Vector3 center;
        public float triggerRadius = 12f;
        public List<List<(GameObject prefab, Vector3 pos)>> waves = new List<List<(GameObject, Vector3)>>();
        public System.Action<int> onWaveStart;
        public System.Action onComplete;
        public System.Action<EnemyBase> onSpawn;

        public bool Started { get; private set; }
        public bool Done { get; set; }
        public int Wave { get; private set; } = -1;
        readonly List<EnemyBase> alive = new List<EnemyBase>();
        float nextWaveAt = -1f;
        int pendingSpawns;
        public int Defeated { get; private set; }

        public void Tick(Vector3 playerPos, MonoBehaviour host)
        {
            if (Done) return;
            if (!Started)
            {
                Vector3 d = playerPos - center; d.y = 0;
                if (d.magnitude <= triggerRadius) { Started = true; StartWave(0, host); }
                return;
            }
            alive.RemoveAll(e => e == null || !e.Alive);
            if (pendingSpawns == 0 && alive.Count == 0)
            {
                if (Wave + 1 < waves.Count)
                {
                    if (nextWaveAt < 0f) nextWaveAt = Time.time + 1.6f;
                    else if (Time.time >= nextWaveAt) { nextWaveAt = -1f; StartWave(Wave + 1, host); }
                }
                else { Done = true; onComplete?.Invoke(); }
            }
        }

        void StartWave(int i, MonoBehaviour host)
        {
            Wave = i;
            onWaveStart?.Invoke(i);
            host.StartCoroutine(SpawnWave(waves[i]));
        }

        System.Collections.IEnumerator SpawnWave(List<(GameObject prefab, Vector3 pos)> list)
        {
            pendingSpawns = list.Count;
            foreach (var (prefab, pos) in list)
            {
                var go = Object.Instantiate(prefab, pos, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                var e = go.GetComponent<EnemyBase>();
                if (e != null)
                {
                    alive.Add(e);
                    e.OnDied += _ => Defeated++;
                    onSpawn?.Invoke(e);
                }
                pendingSpawns--;
                yield return new WaitForSeconds(0.35f);
            }
        }

        /// <summary>Jogador morreu: some com quem está vivo e o encontro volta a esperar.</summary>
        public void ResetIfActive()
        {
            if (!Started || Done) return;
            foreach (var e in alive) if (e != null) Object.Destroy(e.gameObject);
            alive.Clear();
            Started = false; Wave = -1; nextWaveAt = -1f; pendingSpawns = 0;
            ThreatDirector.Reset();
        }

        public int AliveCount => alive.Count;
    }
}
