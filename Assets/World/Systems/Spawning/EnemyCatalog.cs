using System.Collections.Generic;
using UnityEngine;

namespace Elyndra.World
{
    /// <summary>
    /// Bestiário de Elyndra: id → prefab. Inimigo sem prefab ainda (lobos, corvos, cervos variantes…)
    /// aparece como PLACEHOLDER marcado no mundo. Para trocar: arraste o prefab novo na entrada certa
    /// (Assets/World/Data/EnemyCatalog.asset) — as zonas de inimigos já pedem pelo id.
    /// </summary>
    [CreateAssetMenu(menuName = "Elyndra/Bestiário")]
    public class EnemyCatalog : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public string id;
            public string displayName;
            [Tooltip("O que era antes (hospedeiro VIVO)")] public string host;
            public Distortion distortion;
            [TextArea] public string reading;     // leitura de combate que não depende de dano bruto
            public GameObject prefab;              // null = ainda não existe (placeholder)
            [Tooltip("Corpo provisório: usa o prefab de outro inimigo")] public bool provisionalBody;
            public float healthScale = 1f;
            public int tier = 1;
        }

        public List<Entry> entries = new List<Entry>();

        public Entry Find(string id)
        {
            foreach (var e in entries) if (e.id == id) return e;
            return null;
        }

        static EnemyCatalog cached;
        public static EnemyCatalog Load()
        {
            if (cached == null) cached = Resources.Load<EnemyCatalog>("Elyndra/EnemyCatalog");
            return cached;
        }
    }
}
