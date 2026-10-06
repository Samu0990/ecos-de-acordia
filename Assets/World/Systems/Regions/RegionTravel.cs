using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Elyndra.World
{
    /// <summary>
    /// Viagem entre cenas do mundo (reinos, masmorras, mapa, Campânula). Escurece, mostra o nome do
    /// destino e da rota, carrega a cena de forma ASSÍNCRONA (a vizinha pode ter sido pré-carregada pelo
    /// portão quando o jogador chegou perto) e o RegionFlow da cena nova coloca o Aren no portão de chegada.
    /// Cada reino é uma cena própria: o mundo inteiro nunca fica carregado de uma vez.
    /// </summary>
    public static class RegionTravel
    {
        /// <summary>Id do portão onde o Aren deve aparecer na próxima cena ("" = ponto inicial/checkpoint).</summary>
        public static string ArrivalGate = "";
        public static string FromScene = "";
        public static bool Busy { get; private set; }

        static Runner runner;
        static AsyncOperation preload;
        static string preloadScene;

        class Runner : MonoBehaviour
        {
            public CanvasGroup group;
            public Text title, sub;
        }

        static Runner R
        {
            get
            {
                if (runner != null) return runner;
                var go = new GameObject("ViagemEntreReinos");
                Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<Runner>();
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
                var cs = go.AddComponent<CanvasScaler>(); cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; cs.referenceResolution = new Vector2(1920, 1080); cs.matchWidthOrHeight = 0.5f;
                runner.group = go.AddComponent<CanvasGroup>(); runner.group.alpha = 0; runner.group.blocksRaycasts = false;
                var bg = new GameObject("Preto", typeof(RectTransform)).AddComponent<Image>();
                bg.transform.SetParent(go.transform, false); bg.color = Color.black;
                var rt = bg.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
                runner.title = Aren.UI.GothicUI.Label("Destino", go.transform, "", Aren.UI.GothicUI.Cinzel, 64, Aren.UI.GothicUI.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1600, 100));
                runner.sub = Aren.UI.GothicUI.Label("Rota", go.transform, "", Aren.UI.GothicUI.Garamond, 28, Aren.UI.GothicUI.BoneDim, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1600, 60));
                return runner;
            }
        }

        /// <summary>
        /// Começa a carregar a cena vizinha em segundo plano (sem ativar). Só reinos e masmorras: Campânula e o mapa
        /// NÃO (uma carga com ativação pendente trava a fila do Unity; se o jogador desistir e for para outro lugar,
        /// ela teria de ser ativada — e Campânula ativada abre a tela de carregamento dela, que ficava presa).
        /// </summary>
        public static void Preload(string scene)
        {
            if (scene == WorldCanon.CampanulaScene || scene == WorldCanon.WorldMapScene) return;
            if (Busy || preload != null || string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene)) return;
            preload = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            if (preload == null) return;
            preload.allowSceneActivation = false;
            preloadScene = scene;
        }

        public static bool CanLoad(string scene) => !string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene);

        public static void Go(string scene, string arrivalGate, string title = null, string subtitle = null)
        {
            if (Busy) return;
            if (!CanLoad(scene)) { Debug.LogWarning("[Viagem] cena não está no build: " + scene); return; }
            R.StartCoroutine(Travel(scene, arrivalGate, title, subtitle));
        }

        static IEnumerator Travel(string scene, string arrivalGate, string title, string subtitle)
        {
            Busy = true;
            var r = R;
            var def = WorldCanon.RegionByScene(scene);
            r.title.text = title ?? (def != null ? def.name.ToUpperInvariant() : scene);
            r.sub.text = subtitle ?? (def != null ? def.epithet : "");
            r.group.blocksRaycasts = true;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.6f) { r.group.alpha = t; yield return null; }
            r.group.alpha = 1f;
            Aren.Combat.GameFeel.SetPaused(false);
            ArrivalGate = arrivalGate ?? "";
            FromScene = SceneManager.GetActiveScene().name;
            WorldState.Save();
            AsyncOperation op;
            if (preload != null && preloadScene == scene) op = preload;
            else
            {
                if (preload != null) { preload.allowSceneActivation = true; while (!preload.isDone) yield return null; }
                op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            }
            preload = null; preloadScene = null;
            op.allowSceneActivation = true;
            float shown = Time.realtimeSinceStartup;
            while (!op.isDone) yield return null;
            // segura o nome do destino um instante (leitura) e clareia
            while (Time.realtimeSinceStartup - shown < 1.2f) yield return null;
            for (float t = 1; t > 0f; t -= Time.unscaledDeltaTime / 0.9f) { r.group.alpha = t; yield return null; }
            r.group.alpha = 0f; r.group.blocksRaycasts = false;
            Busy = false;
        }
    }
}
