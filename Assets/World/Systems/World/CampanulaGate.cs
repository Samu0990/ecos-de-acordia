using UnityEngine;

namespace Elyndra.World
{
    /// <summary>Gatilho do Portão Leste de Campânula (sem RegionFlow: lá quem manda é o GameFlow).</summary>
    public class CampanulaGate : MonoBehaviour
    {
        public GameObject lockedVisual;
        float lastHint = -10f;
        const string Requires = "flag:campanula_cervo";

        void Update() { if (lockedVisual != null) lockedVisual.SetActive(!WorldState.Check(Requires)); }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<Climbing.ThirdPersonController>() == null || RegionTravel.Busy) return;
            if (!WorldState.Check(Requires))
            {
                if (Time.time - lastHint > 4f) { lastHint = Time.time; Aren.UI.ArenHUD.Instance?.ShowHint("A estrada leste está tomada pela Ressonância errada. Vença o que espera no Campo dos Cascos.", 5f); }
                return;
            }
            RegionTravel.Go("Valteria", "portao_campanula", "VALTÉRIA", "pelo Portão Leste de Campânula");
        }
    }
}
