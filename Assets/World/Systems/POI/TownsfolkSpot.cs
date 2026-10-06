using UnityEngine;

namespace Elyndra.World
{
    /// <summary>
    /// Morador de verdade num ponto de NPC das cidades (os aldeões Quaternius CC0 de Campânula, em
    /// Resources/Villagers): aparece quando o Aren chega perto, parado no gesto do papel (lanterna,
    /// braços cruzados, chamando…), e some quando ele se afasta — as vilas deixam de ser cenário vazio
    /// sem pesar no notebook. Provisório até cada NPC ter modelo, diálogo por gesto e rotina próprios.
    /// </summary>
    public class TownsfolkSpot : MonoBehaviour
    {
        public string prefab = "Villager_M1";
        public string idle = "Idle_FoldArms";
        public float showDistance = 75f;
        GameObject body;
        float next;

        static readonly string[] Prefabs = { "Villager_M1", "Villager_F1", "Villager_M2", "Villager_F2" };
        static readonly string[] Idles = { "Idle_FoldArms", "Idle_Lantern", "Idle_No", "Call", "Yes" };

        /// <summary>Escolha estável (mesmo morador no mesmo lugar) a partir do nome do ponto.</summary>
        public void Pick(string seed, string role)
        {
            int h = Mathf.Abs((seed ?? "").GetHashCode());
            prefab = Prefabs[h % Prefabs.Length];
            string r = (role ?? "").ToLowerInvariant();
            idle = r.Contains("vended") || r.Contains("pregoeira") || r.Contains("mercadora") ? "Call"
                 : r.Contains("taverneiro") || r.Contains("sobrevivente") ? "Yes"
                 : r.Contains("monge") || r.Contains("regente") ? "Idle_Lantern"
                 : Idles[(h / 7) % Idles.Length];
        }

        void Update()
        {
            if (Time.time < next) return;
            next = Time.time + 0.5f + Random.value * 0.2f;
            var p = RegionFlow.Instance != null ? RegionFlow.Instance.Player : null;
            if (p == null) return;
            bool near = (p.position - transform.position).sqrMagnitude < showDistance * showDistance;
            if (near && body == null) Spawn();
            else if (!near && body != null) { Destroy(body); body = null; }
        }

        void Spawn()
        {
            var src = Resources.Load<GameObject>("Villagers/" + prefab);
            if (src == null) return;
            body = Instantiate(src, transform.position, transform.rotation, transform);
            var anim = body.GetComponent<Animator>();
            if (anim != null)
            {
                anim.cullingMode = AnimatorCullingMode.CullCompletely;
                anim.Play(idle, 0, Random.value);
                anim.SetFloat("Speed", Random.Range(0.9f, 1.1f));
            }
        }
    }
}
