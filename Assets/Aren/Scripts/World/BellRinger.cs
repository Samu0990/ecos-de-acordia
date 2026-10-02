using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aren.World
{
    /// <summary>
    /// Os Doze Sinos de Campanula. Toca uma sequência (cada sino balança e soa) e, quando
    /// pedido, a 13ª badalada — que não sai de sino nenhum: vem do céu (Bíblia de Lore).
    /// </summary>
    public class BellRinger : MonoBehaviour
    {
        readonly List<Transform> bells = new List<Transform>();
        readonly List<Quaternion> rest = new List<Quaternion>();
        readonly List<float> swing = new List<float>();
        public bool Ringing { get; private set; }

        void Start()
        {
            foreach (var t in GetComponentsInChildren<Transform>())
                if (t.name.StartsWith("Bell_")) { bells.Add(t); rest.Add(t.localRotation); swing.Add(0f); }
        }

        public void Toll(int count, bool thirteenth, System.Action onThirteenth = null)
        {
            if (!Ringing) StartCoroutine(TollRoutine(count, thirteenth, onThirteenth));
        }

        IEnumerator TollRoutine(int count, bool thirteenth, System.Action onThirteenth)
        {
            Ringing = true;
            Vector3 p = transform.position + Vector3.up * 21f;
            for (int i = 0; i < count; i++)
            {
                int b = bells.Count > 0 ? i % bells.Count : 0;
                if (b < swing.Count) swing[b] = 1f;
                ArenAudio.Play(Sfx.Bell, p, 0.75f, 1f - (i % 4) * 0.04f);
                yield return new WaitForSeconds(0.85f);
            }
            if (thirteenth)
            {
                yield return new WaitForSeconds(1.2f);
                ArenAudio.Play(Sfx.BellCorrupt, p + Vector3.up * 40f, 1f, 0.92f);
                Campanula.RiftPulse.Instance?.Burst(1f);
                Combat.GameFeel.Shake(0.35f);
                onThirteenth?.Invoke();
            }
            Ringing = false;
        }

        void Update()
        {
            for (int i = 0; i < bells.Count; i++)
            {
                if (swing[i] <= 0.001f) continue;
                swing[i] = Mathf.MoveTowards(swing[i], 0f, Time.deltaTime * 0.35f);
                float a = Mathf.Sin(Time.time * 5.5f + i) * 28f * swing[i];
                bells[i].localRotation = rest[i] * Quaternion.Euler(a, 0, 0);
            }
        }
    }
}
