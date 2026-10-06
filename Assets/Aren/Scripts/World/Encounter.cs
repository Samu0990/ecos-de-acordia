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
        /// <summary>Trancado (uma cena está tocando): não começa mesmo com o jogador no raio.</summary>
        public bool Locked;
        /// <summary>Direção de cada inimigo da primeira onda (null = aleatória).</summary>
        public List<float> firstWaveYaw;
        bool instantNext;
        public bool Done { get; set; }
        public int Wave { get; private set; } = -1;
        readonly List<EnemyBase> alive = new List<EnemyBase>();
        float nextWaveAt = -1f;
        int pendingSpawns;
        public int Defeated { get; private set; }

        public void Tick(Vector3 playerPos, MonoBehaviour host)
        {
            if (Done || Locked) return;
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

        /// <summary>Começa já (sem esperar o raio); 'instant' = todos da onda no mesmo quadro.</summary>
        public void ForceStart(MonoBehaviour host, bool instant)
        {
            if (Started || Done) return;
            Started = true; Locked = false; instantNext = instant;
            StartWave(0, host);
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
            bool instant = instantNext; instantNext = false;
            int k = 0;
            bool first = Wave == 0;
            foreach (var (prefab, pos) in list)
            {
                float yaw = first && firstWaveYaw != null && k < firstWaveYaw.Count ? firstWaveYaw[k] : Random.Range(0f, 360f);
                k++;
                var go = Object.Instantiate(prefab, pos, Quaternion.Euler(0, yaw, 0));
                var e = go.GetComponent<EnemyBase>();
                if (e != null)
                {
                    alive.Add(e);
                    e.OnDied += d =>
                    {
                        Defeated++;
                        var cb = Object.FindAnyObjectByType<Aren.Combat.ArenCombat>();
                        Notas.Add(d.isBoss ? 1000 : 100 + 10 * (cb != null ? cb.ChainCount : 0));
                    };
                    onSpawn?.Invoke(e);
                }
                pendingSpawns--;
                if (!instant) yield return new WaitForSeconds(0.35f);
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

        /// <summary>Volta ao estado inicial (atalho de testes).</summary>
        public void ResetFull()
        {
            ResetIfActive();
            Started = false; Done = false; Wave = -1;
        }
    }
}
