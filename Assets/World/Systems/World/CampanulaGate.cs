using UnityEngine;

namespace Elyndra.World
{
    /// <summary>Gatilho do Portão Leste de Campânula (sem RegionFlow: lá quem manda é o GameFlow).</summary>
    public class CampanulaGate : MonoBehaviour
    {
        public GameObject lockedVisual;
        float lastHint = -10f;
        const string Requires = "flag:campanula_cervo";

        Transform player; BoxCollider box; bool playerInside; float nextFind;

        void Update()
        {
            if (lockedVisual != null) lockedVisual.SetActive(!WorldState.Check(Requires));
            // também confere a posição do jogador (não depende só da física de triggers)
            if (player == null && Time.time > nextFind) { nextFind = Time.time + 1f; var t = FindAnyObjectByType<Climbing.ThirdPersonController>(); if (t != null) player = t.transform; }
            if (box == null) box = GetComponent<BoxCollider>();
            if (player == null || box == null) return;
            var lp = transform.InverseTransformPoint(player.position + Vector3.up * 0.9f) - box.center;
            var h = box.size * 0.5f;
            bool inside = Mathf.Abs(lp.x) <= h.x && Mathf.Abs(lp.y) <= h.y && Mathf.Abs(lp.z) <= h.z;
            if (inside && !playerInside) TryPass();
            playerInside = inside;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<Climbing.ThirdPersonController>() == null) return;
            TryPass();
        }

        void TryPass()
        {
            if (RegionTravel.Busy) return;
            if (!WorldState.Check(Requires))
            {
                if (Time.time - lastHint > 4f) { lastHint = Time.time; Aren.UI.ArenHUD.Instance?.ShowHint("A estrada leste está tomada pela Ressonância errada. Vença o que espera no Campo dos Cascos.", 5f); }
                return;
            }
            RegionTravel.Go("Valteria", "portao_campanula", "VALTÉRIA", "pelo Portão Leste de Campânula");
        }
    }
}
