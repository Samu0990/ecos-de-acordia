using UnityEngine;

namespace Elyndra.World
{
    /// <summary>
    /// Passagem entre cenas (rota para outro reino, entrada de masmorra, volta para Campânula). Gatilho
    /// que leva o Aren para <see cref="targetScene"/> e o faz aparecer no portão <see cref="targetGate"/>
    /// de lá. Pode estar trancada por uma condição do WorldState (ex.: "nota:do" — as rotas de Valtéria só
    /// abrem depois de Dó Partido); trancada, mostra o motivo e mantém o véu/escombros visíveis.
    /// Quando o jogador chega perto, a cena vizinha começa a carregar em segundo plano (streaming).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class RegionGate : MonoBehaviour
    {
        public string id = "";
        public string targetScene = "";
        public string targetGate = "";
        public string routeName = "";
        [Tooltip("Condição do WorldState para passar (vazio = aberta)")]
        public string requires = "";
        [TextArea] public string lockedMessage = "";
        [Tooltip("Onde o Aren aparece ao CHEGAR por esta passagem (de frente para dentro da região)")]
        public Transform arrival;
        [Tooltip("Ligado enquanto trancada (véu, escombros); desligado quando abre")]
        public GameObject lockedVisual;
        public GameObject openVisual;
        public float preloadDistance = 55f;

        float lastHint = -10f;
        bool preloaded, isOpen;
        public bool Open => WorldState.Check(requires);

        void OnEnable() { WorldState.OnChanged += Refresh; Refresh(); }
        void OnDisable() { WorldState.OnChanged -= Refresh; }

        void Refresh()
        {
            bool open = isOpen = Open;
            if (lockedVisual != null) lockedVisual.SetActive(!open);
            if (openVisual != null) openVisual.SetActive(open);
        }

        void Update()
        {
            if (preloaded || !isOpen || RegionFlow.Instance == null || RegionFlow.Instance.Player == null) return;
            if ((RegionFlow.Instance.Player.position - transform.position).sqrMagnitude < preloadDistance * preloadDistance)
            {
                preloaded = true;
                RegionTravel.Preload(targetScene);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<Climbing.ThirdPersonController>() == null) return;
            if (RegionTravel.Busy) return;
            if (!Open)
            {
                if (Time.time - lastHint > 4f)
                {
                    lastHint = Time.time;
                    Aren.UI.ArenHUD.Instance?.ShowHint(string.IsNullOrEmpty(lockedMessage) ? "O caminho ainda está fechado." : lockedMessage, 5f);
                }
                return;
            }
            if (!RegionTravel.CanLoad(targetScene))
            {
                Aren.UI.ArenHUD.Instance?.ShowHint(routeName + ": esta região ainda não está no build.", 4f);
                return;
            }
            var def = WorldCanon.RegionByScene(targetScene);
            string title = def != null ? def.name.ToUpperInvariant() : null;
            string sub = !string.IsNullOrEmpty(routeName) ? "pela " + routeName : null;
            if (targetScene.StartsWith("D_")) { var dd = WorldCanon.Dungeon("d_" + targetScene.Substring(2)); if (dd != null) { title = dd.name.ToUpperInvariant(); sub = dd.theme; } }
            if (targetScene == WorldCanon.CampanulaScene) { title = "CAMPÂNULA"; sub = "Valtéria"; }
            RegionTravel.Go(targetScene, targetGate, title, sub);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Open ? new Color(0.3f, 1f, 0.6f, 0.35f) : new Color(1f, 0.3f, 0.3f, 0.35f);
            var bc = GetComponent<BoxCollider>();
            Gizmos.matrix = transform.localToWorldMatrix;
            if (bc != null) Gizmos.DrawCube(bc.center, bc.size);
        }
    }
}
