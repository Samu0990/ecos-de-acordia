using UnityEngine;

namespace Campanula
{
    /// <summary>
    /// A Fenda respira: pulso lento contínuo e pulsos fortes em momentos da história
    /// (13ª badalada, chegada do cervo). Sempre de frente para a câmera no eixo vertical.
    /// </summary>
    public class RiftPulse : MonoBehaviour
    {
        public static RiftPulse Instance { get; private set; }
        Material mat;
        float burst;
        static readonly int IdPulse = Shader.PropertyToID("_Pulse");

        void Awake()
        {
            Instance = this;
            var r = GetComponent<Renderer>();
            if (r != null) mat = r.material;
        }

        public void Burst(float strength = 1f) => burst = Mathf.Max(burst, strength);

        void Update()
        {
            burst = Mathf.MoveTowards(burst, 0f, Time.deltaTime * 0.6f);
            float p = 0.15f + 0.1f * Mathf.Sin(Time.time * 0.7f) + burst;
            if (mat != null) mat.SetFloat(IdPulse, Mathf.Clamp01(p));
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 to = transform.position - cam.transform.position; to.y = 0;
                if (to.sqrMagnitude > 1f) transform.rotation = Quaternion.LookRotation(to.normalized);
            }
        }
    }
}
