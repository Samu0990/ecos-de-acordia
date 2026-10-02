using UnityEngine;

namespace Aren
{
    /// <summary>
    /// Sombra "blob" macia no chão sob personagens (Aren, Ecos, cervo). Com as sombras em
    /// tempo real desligadas (o padrão no Intel UHD — elas custam ~10 FPS por causa do passe
    /// de profundidade), é ela que mantém os personagens "pisando" no mundo. Encolhe e some
    /// conforme o personagem sobe (pulo, escalada).
    /// </summary>
    public class BlobShadow : MonoBehaviour
    {
        public float radius = 0.55f;
        public float maxHeight = 4f;
        public static bool Enabled = true;
        Transform quad;
        MeshRenderer mr;
        MaterialPropertyBlock mpb;
        static Material mat;
        static Mesh mesh;
        static readonly int IdColor = Shader.PropertyToID("_Color");
        int mask;

        void Start()
        {
            if (mat == null)
            {
                mat = new Material(Resources.Load<Shader>("Shaders/ArenFXAlpha"));
                mat.mainTexture = Resources.Load<Texture2D>("VFX/fx_soft");
                mat.renderQueue = 2450;   // depois do opaco, antes dos transparentes
            }
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f) };
                mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                mesh.RecalculateBounds();
            }
            var go = new GameObject("BlobShadow");
            quad = go.transform;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mpb = new MaterialPropertyBlock();
            mask = ~((1 << 8) | (1 << 10) | (1 << 2));   // ignora Ledge, Player, Ignore Raycast
        }

        void LateUpdate()
        {
            if (quad == null) return;
            bool on = Enabled && isActiveAndEnabled;
            if (on && Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out var hit, maxHeight + 0.5f, mask, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform))
            {
                float h = Mathf.Max(0f, transform.position.y - hit.point.y);
                float k = 1f - Mathf.Clamp01(h / maxHeight);
                quad.gameObject.SetActive(true);
                quad.position = hit.point + hit.normal * 0.03f;
                quad.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                quad.localScale = Vector3.one * radius * 2f * Mathf.Lerp(0.6f, 1f, k);
                mpb.SetColor(IdColor, new Color(0, 0, 0, 0.55f * k));
                mr.SetPropertyBlock(mpb);
            }
            else quad.gameObject.SetActive(false);
        }

        void OnDisable() { if (quad != null) quad.gameObject.SetActive(false); }
        void OnDestroy() { if (quad != null) Destroy(quad.gameObject); }
    }
}
