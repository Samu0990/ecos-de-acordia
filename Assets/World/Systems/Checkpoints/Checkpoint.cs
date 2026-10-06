using UnityEngine;

namespace Elyndra.World
{
    /// <summary>
    /// Ponto de retorno (pequeno sino de pedra). Ao passar, vira o lugar onde o Aren volta se cair e fica
    /// salvo no WorldState (sair do jogo e voltar reabre aqui). Acende uma luz quando ativado.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class Checkpoint : MonoBehaviour
    {
        public string id = "";
        public string displayName = "Ponto de retorno";
        public Transform spawn;
        public Light glow;
        public Renderer flame;
        bool active;

        void Start()
        {
            active = WorldState.LastCheckpoint == id && WorldState.LastScene == gameObject.scene.name;
            if (glow != null) glow.enabled = active;
            if (flame != null) flame.enabled = active;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<Climbing.ThirdPersonController>() == null || RegionFlow.Instance == null) return;
            var t = spawn != null ? spawn : transform;
            RegionFlow.Instance.SetCheckpoint(t.position, t.eulerAngles.y, id);
            if (active) return;
            active = true;
            if (glow != null) glow.enabled = true;
            if (flame != null) flame.enabled = true;
            Aren.ArenAudio.Play(Aren.Sfx.Bell, transform.position + Vector3.up, 0.45f, 1.3f);
            Aren.UI.ArenHUD.Instance?.Toast(displayName, Aren.UI.UIKit.Gold, 1.6f);
        }
    }
}
