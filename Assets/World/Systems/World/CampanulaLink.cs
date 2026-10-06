using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Elyndra.World
{
    /// <summary>
    /// Liga Campânula (a cena da demo, gerada pelo CampanulaBuilder) ao resto de Elyndra SEM mexer na cena:
    /// ao carregar Campânula, cria o Portão Leste (fim da estrada depois do Campo dos Cascos) que leva a
    /// Valtéria — fechado até o Cervo de Contratempo ser vencido — e, quando o Aren volta de Valtéria, faz
    /// ele aparecer no portão (sem menu, sem abertura, encontros já vencidos).
    /// </summary>
    public static class CampanulaLink
    {
        public static readonly Vector3 GatePos = new Vector3(148f, 0f, 9.5f);
        public static readonly Vector3 ArrivePos = new Vector3(134f, 0f, 9.5f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            var s = SceneManager.GetActiveScene();
            if (s.name == WorldCanon.CampanulaScene) OnLoaded(s, LoadSceneMode.Single);
        }

        static void OnLoaded(Scene s, LoadSceneMode m)
        {
            if (s.name != WorldCanon.CampanulaScene) return;
            if (GameObject.Find("Portão Leste — Valtéria") != null) return;
            BuildGate();
            if (RegionTravel.ArrivalGate == "valteria")
            {
                RegionTravel.ArrivalGate = "";
                var go = new GameObject("Chegada de Valtéria");
                go.AddComponent<Arriver>();
            }
        }

        static float GroundY(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out var hit, 120f, ~((1 << 2) | (1 << 10)), QueryTriggerInteraction.Ignore)) return hit.point.y;
            return 0f;
        }

        static void BuildGate()
        {
            var root = new GameObject("Portão Leste — Valtéria");
            var p = GatePos; p.y = GroundY(p);
            root.transform.SetPositionAndRotation(p, Quaternion.Euler(0, 90f, 0));
            var stone = Resources.Load<Material>("Elyndra/GateStone");
            var veil = Resources.Load<Material>("Elyndra/GateVeil");
            var glow = Resources.Load<Material>("Elyndra/GateGlow");
            void Block(string n, Vector3 local, Vector3 size, Material mat, bool col)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = n; b.transform.SetParent(root.transform, false);
                b.transform.localPosition = local; b.transform.localScale = size;
                if (mat != null) b.GetComponent<MeshRenderer>().sharedMaterial = mat;
                if (!col) Object.Destroy(b.GetComponent<Collider>());
            }
            Block("Pilar", new Vector3(-4.6f, 3.5f, 0), new Vector3(1.4f, 7f, 1.8f), stone, true);
            Block("Pilar", new Vector3(4.6f, 3.5f, 0), new Vector3(1.4f, 7f, 1.8f), stone, true);
            Block("Verga", new Vector3(0, 7.4f, 0), new Vector3(10.6f, 1.2f, 2f), stone, true);
            Block("Lanterna", new Vector3(-4.6f, 7.9f, -1f), Vector3.one * 0.5f, glow, false);
            Block("Lanterna", new Vector3(4.6f, 7.9f, -1f), Vector3.one * 0.5f, glow, false);
            var locked = new GameObject("Trancado"); locked.transform.SetParent(root.transform, false);
            var v = GameObject.CreatePrimitive(PrimitiveType.Cube); v.name = "Véu"; v.transform.SetParent(locked.transform, false);
            v.transform.localPosition = new Vector3(0, 3.4f, 0); v.transform.localScale = new Vector3(7.8f, 6.8f, 0.2f);
            if (veil != null) v.GetComponent<MeshRenderer>().sharedMaterial = veil;
            var trig = new GameObject("Gatilho"); trig.transform.SetParent(root.transform, false); trig.transform.localPosition = new Vector3(0, 2.5f, 1.5f); trig.layer = 2;
            var bc = trig.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(8f, 5f, 2.5f);
            var gate = trig.AddComponent<CampanulaGate>(); gate.lockedVisual = locked;
        }

        class Arriver : MonoBehaviour
        {
            IEnumerator Start()
            {
                var flow = Aren.World.GameFlow.Instance;
                float t = 0;
                while ((flow == null || !flow.Loaded) && t < 30f) { t += Time.unscaledDeltaTime; flow = Aren.World.GameFlow.Instance; yield return null; }
                if (flow != null) { var p = ArrivePos; p.y = GroundY(p); flow.ArriveFromWorld(p, 270f); }
                Destroy(gameObject);
            }
        }
    }
}
