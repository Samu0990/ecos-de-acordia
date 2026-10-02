using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Aren.UI
{
    /// <summary>Botão com microanimação (desliza, acende, onda sublinhando) e som (doc §33).</summary>
    public class UIButtonFX : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, ISubmitHandler, IPointerClickHandler
    {
        public Text label; public UIWaveform wave; public RectTransform content;
        public System.Action onLeft, onRight;
        bool selected; float k;
        static float lastMove;

        public void OnSelect(BaseEventData e) { selected = true; if (Time.unscaledTime - lastMove > 0.05f) ArenAudio.PlayUI(Sfx.UIMove, 0.5f); lastMove = Time.unscaledTime; }
        public void OnDeselect(BaseEventData e) => selected = false;
        public void OnPointerEnter(PointerEventData e) { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject); }
        public void OnSubmit(BaseEventData e) => ArenAudio.PlayUI(Sfx.UIConfirm, 0.7f);
        public void OnPointerClick(PointerEventData e) => ArenAudio.PlayUI(Sfx.UIConfirm, 0.7f);

        void Update()
        {
            k = Mathf.MoveTowards(k, selected ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            float e = k * k * (3 - 2 * k);
            if (content != null) content.anchoredPosition = new Vector2(e * 14f, 0);
            if (label != null) label.color = Color.Lerp(UIKit.Muted, UIKit.Bone, e);
            if (wave != null)
            {
                wave.color = new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, e);
                wave.Set(0.25f + 0.25f * Mathf.Sin(Time.unscaledTime * 2f), 1f, Time.unscaledTime * 1.6f);
            }
            if (selected && Keyboard.current != null)
            {
                if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame) { onLeft?.Invoke(); }
                if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame) { onRight?.Invoke(); }
            }
            if (selected && Gamepad.current != null)
            {
                if (Gamepad.current.dpad.left.wasPressedThisFrame) onLeft?.Invoke();
                if (Gamepad.current.dpad.right.wasPressedThisFrame) onRight?.Invoke();
            }
        }
    }

    /// <summary>
    /// Todas as telas de menu: inicial (sobre a vila ao vivo), pausa, configurações,
    /// controles, créditos, morte e fim da demo. O GameFlow decide qual mostrar.
    /// </summary>
    public class GameMenus : MonoBehaviour
    {
        public static GameMenus Instance { get; private set; }
        public enum Screen { None, Main, Pause, Settings, Controls, Credits, Death, End }
        public Screen Current { get; private set; } = Screen.None;

        public System.Action onStart, onResume, onRestartCheckpoint, onMainMenu, onQuit, onPlayAgain;

        Canvas canvas;
        CanvasGroup fade; float fadeTarget, fadeSpeed = 2f;
        readonly Dictionary<Screen, CanvasGroup> screens = new Dictionary<Screen, CanvasGroup>();
        readonly Dictionary<Screen, GameObject> firstSelected = new Dictionary<Screen, GameObject>();
        Screen settingsReturn = Screen.Main;
        Text endStats, deathText;
        readonly List<System.Action> refreshers = new List<System.Action>();
        RectTransform titleGlyphs;
        readonly List<RectTransform> motes = new List<RectTransform>();

        void Awake()
        {
            Instance = this;
            EnsureEventSystem();
            canvas = UIKit.MakeCanvas("Menus", 20);
            canvas.transform.SetParent(transform, false);
            BuildMain();
            BuildPause();
            BuildSettings();
            BuildControls();
            BuildCredits();
            BuildDeath();
            BuildEnd();
            var f = UIKit.Img("Fade", canvas.transform, null, Color.black, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            f.rectTransform.anchorMin = Vector2.zero; f.rectTransform.anchorMax = Vector2.one; f.rectTransform.sizeDelta = Vector2.zero;
            fade = f.gameObject.AddComponent<CanvasGroup>(); fade.blocksRaycasts = false; fade.alpha = 1f;
            foreach (var kv in screens) Hide(kv.Value);
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        // ------------------------------------------------------------ construção

        CanvasGroup NewScreen(Screen s, bool dim)
        {
            var rt = UIKit.Stretch(s.ToString(), canvas.transform);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            if (dim)
            {
                var bg = rt.gameObject.AddComponent<Image>();
                bg.color = new Color(0.02f, 0.015f, 0.03f, 0.55f);
                var left = UIKit.Rect("Painel", rt, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(1100, 0));
                var li = left.gameObject.AddComponent<Image>(); li.color = new Color(0.02f, 0.015f, 0.03f, 0.55f); li.raycastTarget = false;
                var fr = UIKit.Rect("PainelDegrade", rt, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(1100, 0), new Vector2(600, 0));
                var fi = fr.gameObject.AddComponent<Image>(); fi.sprite = UIKit.S("ui_fade_h"); fi.color = li.color; fi.raycastTarget = false;
            }
            screens[s] = g;
            return g;
        }

        Button MakeButton(Transform parent, string text, Vector2 pos, System.Action onClick, float width = 420f, int size = 30)
        {
            var rt = UIKit.Rect("Botao_" + text, parent, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), pos, new Vector2(width, 56));
            var img = rt.gameObject.AddComponent<Image>(); img.color = new Color(0, 0, 0, 0.001f);   // área clicável
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var content = UIKit.Rect("Conteudo", rt, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var lbl = UIKit.Label("Texto", content, text, UIKit.Serif, size, UIKit.Muted, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(width * 0.5f, 4), new Vector2(width, 50));
            var wrt = UIKit.Rect("Onda", content, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(0, 4), new Vector2(width * 0.6f, 14));
            var wave = wrt.gameObject.AddComponent<UIWaveform>(); wave.thickness = 2f; wave.cycles = 5; wave.raycastTarget = false; wave.color = new Color(1, 1, 1, 0);
            var fx = rt.gameObject.AddComponent<UIButtonFX>(); fx.label = lbl; fx.wave = wave; fx.content = content;
            btn.onClick.AddListener(() => onClick?.Invoke());
            return btn;
        }

        void Title(Transform parent, string title, string sub, Vector2 pos, int size = 74)
        {
            UIKit.Label("Titulo", parent, UIKit.Spaced(title, 1), UIKit.Display, size, UIKit.Bone, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), pos, new Vector2(1100, 110));
            UIKit.Img("Ornamento", parent, "ui_ornament", UIKit.Gold, new Vector2(0, 0.5f), pos + new Vector2(-300, -62), new Vector2(520, 32));
            if (!string.IsNullOrEmpty(sub))
                UIKit.Label("Sub", parent, sub, UIKit.Serif, 24, UIKit.Gold, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), pos + new Vector2(0, -98), new Vector2(1100, 36));
        }

        void BuildMain()
        {
            var g = NewScreen(Screen.Main, false);
            var t = g.transform;
            // gradiente escuro à esquerda (a vila aparece viva à direita)
            var left = UIKit.Rect("Esquerda", t, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(760, 0));
            var li = left.gameObject.AddComponent<Image>(); li.color = new Color(0.02f, 0.015f, 0.03f, 0.66f);
            var fadeR = UIKit.Rect("EsquerdaDegrade", t, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(760, 0), new Vector2(520, 0));
            var fi = fadeR.gameObject.AddComponent<Image>(); fi.sprite = UIKit.S("ui_fade_h"); fi.color = new Color(0.02f, 0.015f, 0.03f, 0.66f); fi.raycastTarget = false;
            Title(t, "ECOS DE ACORDIA", "A Ruptura do Contracanto  ·  demo", new Vector2(640, 250));
            var b0 = MakeButton(t, "Iniciar", new Vector2(120, 40), () => onStart?.Invoke());
            MakeButton(t, "Configurações", new Vector2(120, -26), () => OpenSettings(Screen.Main));
            MakeButton(t, "Controles", new Vector2(120, -92), () => Show(Screen.Controls));
            MakeButton(t, "Créditos", new Vector2(120, -158), () => Show(Screen.Credits));
            MakeButton(t, "Sair", new Vector2(120, -224), () => onQuit?.Invoke());
            firstSelected[Screen.Main] = b0.gameObject;
            UIKit.Label("Rodape", t, "Campanula, Vila dos Doze Sinos — o dia da Ruptura", UIKit.Serif, 16, UIKit.Muted, TextAnchor.LowerLeft,
                new Vector2(0, 0), new Vector2(560, 40), new Vector2(1000, 30));
            // partículas de "notas" subindo devagar na tela de título
            for (int i = 0; i < 18; i++)
            {
                var m = UIKit.Img("Mote" + i, t, i % 3 == 0 ? "ui_diamond" : "ui_glow", new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.0f), new Vector2(0, 0), Vector2.zero, Vector2.one * Random.Range(6f, 16f));
                m.rectTransform.anchoredPosition = new Vector2(Random.Range(40f, 1900f), Random.Range(0f, 1080f));
                motes.Add(m.rectTransform);
            }
        }

        void BuildPause()
        {
            var g = NewScreen(Screen.Pause, true);
            var t = g.transform;
            Title(t, "PAUSADO", "", new Vector2(640, 260), 64);
            var b0 = MakeButton(t, "Continuar", new Vector2(120, 80), () => onResume?.Invoke());
            MakeButton(t, "Configurações", new Vector2(120, 14), () => OpenSettings(Screen.Pause));
            MakeButton(t, "Controles", new Vector2(120, -52), () => Show(Screen.Controls));
            MakeButton(t, "Voltar ao último ponto", new Vector2(120, -118), () => onRestartCheckpoint?.Invoke(), 520f);
            MakeButton(t, "Menu principal", new Vector2(120, -184), () => onMainMenu?.Invoke());
            MakeButton(t, "Sair do jogo", new Vector2(120, -250), () => onQuit?.Invoke());
            firstSelected[Screen.Pause] = b0.gameObject;
        }

        void BuildSettings()
        {
            GameSettings.Load();
            var g = NewScreen(Screen.Settings, true);
            var t = g.transform;
            Title(t, "CONFIGURAÇÕES", "", new Vector2(640, 350), 56);
            float y = 240; float step = 48;
            GameObject first = null;
            void Row(string name, System.Func<string> value, System.Action left, System.Action right)
            {
                var b = MakeButton(t, name, new Vector2(120, y), () => { right(); GameSettings.Apply(); GameSettings.Save(); RefreshAll(); }, 900f, 26);
                var fx = b.GetComponent<UIButtonFX>();
                fx.onLeft = () => { left(); GameSettings.Apply(); GameSettings.Save(); RefreshAll(); ArenAudio.PlayUI(Sfx.UIMove, 0.4f); };
                fx.onRight = () => { right(); GameSettings.Apply(); GameSettings.Save(); RefreshAll(); ArenAudio.PlayUI(Sfx.UIMove, 0.4f); };
                var val = UIKit.Label("Valor", fx.content, value(), UIKit.SansBold, 22, UIKit.Gold, TextAnchor.MiddleRight, new Vector2(0, 0.5f), new Vector2(700, 4), new Vector2(360, 40));
                refreshers.Add(() => val.text = "‹  " + value() + "  ›");
                if (first == null) first = b.gameObject;
                y -= step;
            }
            string Pct(float v) => Mathf.RoundToInt(v * 100) + "%";
            Row("Volume geral", () => Pct(GameSettings.Master), () => GameSettings.Master = Mathf.Clamp01(GameSettings.Master - 0.1f), () => GameSettings.Master = Mathf.Clamp01(GameSettings.Master + 0.1f));
            Row("Música", () => Pct(GameSettings.Music), () => GameSettings.Music = Mathf.Clamp01(GameSettings.Music - 0.1f), () => GameSettings.Music = Mathf.Clamp01(GameSettings.Music + 0.1f));
            Row("Efeitos sonoros", () => Pct(GameSettings.Effects), () => GameSettings.Effects = Mathf.Clamp01(GameSettings.Effects - 0.1f), () => GameSettings.Effects = Mathf.Clamp01(GameSettings.Effects + 0.1f));
            Row("Sensibilidade da câmera", () => GameSettings.Sensitivity.ToString("0.0") + "×", () => GameSettings.Sensitivity = Mathf.Clamp(GameSettings.Sensitivity - 0.1f, 0.3f, 2.5f), () => GameSettings.Sensitivity = Mathf.Clamp(GameSettings.Sensitivity + 0.1f, 0.3f, 2.5f));
            Row("Qualidade gráfica", () => GameSettings.QualityNames[GameSettings.Quality], () => GameSettings.Quality = (GameSettings.Quality + 2) % 3, () => GameSettings.Quality = (GameSettings.Quality + 1) % 3);
            Row("Escala de renderização 3D", () => Mathf.RoundToInt(GameSettings.RenderScale * 100) + "%", () => GameSettings.StepRenderScale(-1), () => GameSettings.StepRenderScale(1));
            Row("Resolução", GameSettings.ResolutionLabel,
                () => { var n = UnityEngine.Screen.resolutions.Length; GameSettings.ResolutionIndex = n == 0 ? -1 : (GameSettings.ResolutionIndex - 1 + n) % n; GameSettings.Apply(true); },
                () => { var n = UnityEngine.Screen.resolutions.Length; GameSettings.ResolutionIndex = n == 0 ? -1 : (GameSettings.ResolutionIndex + 1) % n; GameSettings.Apply(true); });
            Row("Sombras em tempo real (pesado)", () => GameSettings.Shadows ? "Ligadas" : "Desligadas", () => GameSettings.Shadows = !GameSettings.Shadows, () => GameSettings.Shadows = !GameSettings.Shadows);
            Row("Tela cheia", () => GameSettings.Fullscreen ? "Sim" : "Não", () => { GameSettings.Fullscreen = !GameSettings.Fullscreen; GameSettings.Apply(true); }, () => { GameSettings.Fullscreen = !GameSettings.Fullscreen; GameSettings.Apply(true); });
            Row("Tremor de câmera", () => GameSettings.Shake ? "Ligado" : "Desligado", () => GameSettings.Shake = !GameSettings.Shake, () => GameSettings.Shake = !GameSettings.Shake);
            Row("Mostrar FPS", () => GameSettings.ShowFps ? "Sim" : "Não", () => GameSettings.ShowFps = !GameSettings.ShowFps, () => GameSettings.ShowFps = !GameSettings.ShowFps);
            MakeButton(t, "Voltar", new Vector2(120, y - 20), () => Show(settingsReturn));
            firstSelected[Screen.Settings] = first;
            RefreshAll();
        }

        void RefreshAll() { foreach (var r in refreshers) r(); }

        void BuildControls()
        {
            var g = NewScreen(Screen.Controls, true);
            var t = g.transform;
            Title(t, "CONTROLES", "teclado e mouse  ·  controle", new Vector2(640, 330), 56);
            string[,] rows =
            {
                { "Mover", "W A S D", "Analógico esquerdo" },
                { "Correr", "Shift", "RT" },
                { "Pular · Escalar · Vault", "Espaço", "A" },
                { "Soltar da borda · Deslizar", "C", "B" },
                { "Atacar (combo de 4 golpes)", "Clique esquerdo", "X" },
                { "Contracanto (carregar e soltar)", "Segurar clique esquerdo", "Segurar X" },
                { "Contra-ataque (quando o anel dourado fechar)", "Clique direito", "Y" },
                { "Esquiva", "Ctrl esquerdo", "RB" },
                { "Pulso de Ressonância", "Q", "LB" },
                { "Lâmina de Frequência", "E", "LT" },
                { "Eco Fantasma", "R", "Direcional ↑" },
                { "Pausa", "Esc", "Start" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float y = 210 - i * 40;
                UIKit.Label("A" + i, t, rows[i, 0], UIKit.Serif, 21, UIKit.Bone, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(420, y), new Vector2(620, 34));
                UIKit.Label("B" + i, t, rows[i, 1], UIKit.SansBold, 20, UIKit.Gold, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(1020, y), new Vector2(560, 34));
                UIKit.Label("C" + i, t, rows[i, 2], UIKit.Sans, 18, UIKit.Muted, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(1440, y), new Vector2(400, 34));
            }
            var b = MakeButton(t, "Voltar", new Vector2(120, -330), () => Show(settingsReturnForControls()));
            firstSelected[Screen.Controls] = b.gameObject;
        }

        Screen settingsReturnForControls() => GameFlowState.InGame ? Screen.Pause : Screen.Main;

        void BuildCredits()
        {
            var g = NewScreen(Screen.Credits, true);
            var t = g.transform;
            Title(t, "CRÉDITOS", "", new Vector2(640, 330), 56);
            string txt =
                "<b>Ecos de Acordia: A Ruptura do Contracanto</b> — demo\n\n" +
                "Mundo de Campanula, rig do Aren, combate, efeitos, interface e todos os sons:\n" +
                "feitos para esta demo (Blender por script, Unity, síntese procedural).\n\n" +
                "Animações: Universal Animation Library 2 — Quaternius (CC0)\n" +
                "Sistema de parkour base: Dynamic Parkour System (MIT)\n" +
                "Animações de parkour: Mixamo (Adobe)\n" +
                "Fontes: Noto Serif · Noto Sans (SIL Open Font License)\n" +
                "Modelos do Aren e do cervo corrompido: fornecidos pelo autor do jogo";
            UIKit.Label("Texto", t, txt, UIKit.Serif, 22, UIKit.Bone, TextAnchor.UpperLeft, new Vector2(0, 0.5f), new Vector2(700, 40), new Vector2(1180, 420));
            var b = MakeButton(t, "Voltar", new Vector2(120, -330), () => Show(Screen.Main));
            firstSelected[Screen.Credits] = b.gameObject;
        }

        void BuildDeath()
        {
            var g = NewScreen(Screen.Death, true);
            var t = g.transform;
            var title = UIKit.Label("Titulo", t, UIKit.Spaced("AREN CAIU", 2), UIKit.Display, 84, new Color(0.9f, 0.3f, 0.35f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1400, 120));
            UIKit.Img("Ornamento", t, "ui_ornament", UIKit.Crimson, new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(520, 32));
            deathText = UIKit.Label("Sub", t, "O eco se recompõe...", UIKit.Serif, 26, UIKit.Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1400, 40));
        }

        void BuildEnd()
        {
            var g = NewScreen(Screen.End, true);
            var t = g.transform;
            UIKit.Label("Titulo", t, UIKit.Spaced("A PRIMEIRA NOTA SILENCIADA", 1), UIKit.Display, 60, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(1600, 100));
            UIKit.Img("Ornamento", t, "ui_ornament", UIKit.Gold, new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(520, 32));
            UIKit.Label("Lore", t, "O cervo se desfaz em silêncio. Lá no alto, a Fenda continua aberta —\ne em Campanula os sinos tocam treze vezes.", UIKit.Serif, 24, UIKit.Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 130), new Vector2(1500, 80));
            endStats = UIKit.Label("Stats", t, "", UIKit.Sans, 24, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1000, 160));
            UIKit.Label("Obrigado", t, "Obrigado por jogar a demo.", UIKit.Serif, 26, UIKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(1000, 40));
            var b = MakeButton(t, "Jogar de novo", new Vector2(760, -200), () => onPlayAgain?.Invoke());
            MakeButton(t, "Menu principal", new Vector2(760, -266), () => onMainMenu?.Invoke());
            firstSelected[Screen.End] = b.gameObject;
        }

        // ------------------------------------------------------------ controle

        void OpenSettings(Screen from) { settingsReturn = from; Show(Screen.Settings); }

        void Hide(CanvasGroup g) { g.alpha = 0; g.interactable = false; g.blocksRaycasts = false; g.gameObject.SetActive(false); }

        public void Show(Screen s)
        {
            foreach (var kv in screens) if (kv.Key != s) Hide(kv.Value);
            Current = s;
            if (s != Screen.None && screens.TryGetValue(s, out var g))
            {
                g.gameObject.SetActive(true);
                g.alpha = 0f; g.interactable = true; g.blocksRaycasts = true;
                if (firstSelected.TryGetValue(s, out var first) && first != null && EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(first);
                RefreshAll();
            }
            bool needCursor = s != Screen.None && s != Screen.Death;
            Cursor.lockState = needCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = needCursor;
        }

        public void SetEndStats(string text) => endStats.text = text;

        public void FadeTo(float alpha, float speed = 2f) { fadeTarget = alpha; fadeSpeed = speed; }
        public bool FadeDone => Mathf.Abs(fade.alpha - fadeTarget) < 0.01f;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            fade.alpha = Mathf.MoveTowards(fade.alpha, fadeTarget, dt * fadeSpeed);
            if (Current != Screen.None && screens.TryGetValue(Current, out var g))
                g.alpha = Mathf.MoveTowards(g.alpha, 1f, dt * 4f);

            // motes do título
            if (Current == Screen.Main)
                for (int i = 0; i < motes.Count; i++)
                {
                    var m = motes[i];
                    var p = m.anchoredPosition;
                    p.y += dt * (14f + i * 1.3f);
                    p.x += Mathf.Sin(Time.unscaledTime * 0.5f + i) * dt * 8f;
                    if (p.y > 1120f) { p.y = -20f; p.x = Random.Range(40f, 1900f); }
                    m.anchoredPosition = p;
                    var im = m.GetComponent<Image>();
                    UIKit.SetAlpha(im, 0.15f + 0.25f * Mathf.Sin(Time.unscaledTime * 1.3f + i * 2.1f) * Mathf.Sin(Time.unscaledTime * 1.3f + i * 2.1f));
                }

            // voltar com Esc/B nas telas secundárias
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back)
            {
                if (Current == Screen.Settings) { ArenAudio.PlayUI(Sfx.UIBack, 0.6f); Show(settingsReturn); }
                else if (Current == Screen.Controls) { ArenAudio.PlayUI(Sfx.UIBack, 0.6f); Show(settingsReturnForControls()); }
                else if (Current == Screen.Credits) { ArenAudio.PlayUI(Sfx.UIBack, 0.6f); Show(Screen.Main); }
            }
            // mouse sumiu da seleção: reseleciona para teclado/controle continuarem funcionando
            if (Current != Screen.None && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null
                && firstSelected.TryGetValue(Current, out var f) && f != null
                && (Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)))
                EventSystem.current.SetSelectedGameObject(f);
        }
    }

    /// <summary>Estado global mínimo que a UI consulta sem depender do GameFlow.</summary>
    public static class GameFlowState
    {
        public static bool InGame;
    }
}
