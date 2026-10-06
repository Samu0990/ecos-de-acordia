using System.Collections.Generic;
using UnityEngine;

namespace Elyndra.World
{
    /// <summary>
    /// Streaming dentro do reino: um grupo de conteúdo (bairro, vila, masmorra exterior, vegetação densa)
    /// só fica ATIVO quando o Aren está a menos de <see cref="radius"/> (com folga para não piscar).
    /// Barato (verifica a cada meio segundo, todas as células juntas). Ao lado disso, cada reino é uma cena
    /// separada carregada de forma assíncrona (RegionTravel) — o continente nunca está todo na memória.
    /// </summary>
    public class StreamingCell : MonoBehaviour
    {
        public float radius = 260f;
        public GameObject content;
        static readonly List<StreamingCell> all = new List<StreamingCell>();
        static float nextCheck;

        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        void Update()
        {
            if (Time.unscaledTime < nextCheck || all.Count == 0 || all[0] != this) return;
            nextCheck = Time.unscaledTime + 0.5f;
            var p = RegionFlow.Instance != null ? RegionFlow.Instance.Player : null;
            Vector3 c = p != null ? p.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            foreach (var cell in all)
            {
                if (cell.content == null) continue;
                float d2 = (cell.transform.position - c).sqrMagnitude;
                bool on = cell.content.activeSelf;
                float r = cell.radius * (on ? 1.15f : 1f);
                bool want = d2 < r * r;
                if (want != on) cell.content.SetActive(want);
            }
        }
    }
}
