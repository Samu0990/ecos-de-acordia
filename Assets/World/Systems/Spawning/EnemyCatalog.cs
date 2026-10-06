using System.Collections.Generic;
using UnityEngine;

namespace Elyndra.World
{
    /// <summary>
    /// Bestiário de Elyndra: id → prefab. Só SERES VIVOS são hospedeiros (pessoas, animais, plantas) —
    /// lugares e objetos nunca. Por decisão do autor (2026-10-06), POR ENQUANTO o mundo só tem inimigos
    /// HUMANOS corrompidos (<see cref="HumansOnly"/>): os humanos sem corpo próprio usam um corpo de
    /// aldeão provisório; os animais do cânone ficam guardados aqui para depois, sem aparecer no mundo.
    /// Para trocar um corpo: arraste o prefab novo na entrada certa (Assets/World/Resources/Elyndra/
    /// EnemyCatalog.asset) — as zonas de inimigos já pedem pelo id.
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
            [Tooltip("Hospedeiro humano (os únicos no mundo por enquanto)")] public bool human;
            public Distortion distortion;
            [TextArea] public string reading;     // leitura de combate que não depende de dano bruto
            public GameObject prefab;              // null = ainda não existe (placeholder)
            [Tooltip("Corpo provisório: usa o prefab de outro inimigo")] public bool provisionalBody;
            public float healthScale = 1f;
            public int tier = 1;
        }

        public List<Entry> entries = new List<Entry>();

        /// <summary>Por enquanto só humanos (pedido do autor). Desligar quando os animais tiverem corpo.</summary>
        public static bool HumansOnly = true;

        public Entry Find(string id)
        {
            foreach (var e in entries) if (e.id == id) return e;
            return null;
        }

        /// <summary>Um humano com a mesma distorção (ou o Sussurrante) para ocupar o lugar de um animal.</summary>
        public Entry HumanFor(Distortion d)
        {
            foreach (var e in entries) if (e.human && e.distortion == d && e.prefab != null) return e;
            return Find("sussurrante");
        }

        static EnemyCatalog cached;
        public static EnemyCatalog Load()
        {
            if (cached == null) cached = Resources.Load<EnemyCatalog>("Elyndra/EnemyCatalog");
            return cached;
        }
    }
}
