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
        public Image backdrop; public Image marker;
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
            transform.localScale = Vector3.one * (1f + e * 0.018f);
            if (label != null) label.color = Color.Lerp(UIKit.Muted, UIKit.Bone, e);
            if (backdrop != null)
                backdrop.color = Color.Lerp(new Color(0.025f, 0.018f, 0.035f, 0.34f), new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.18f), e);
            if (marker != null)
                marker.color = new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, Mathf.Lerp(0.28f, 1f, e));
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
    /// Todas as telas de menu: inicial (a arte de referência animada, ver TitleScreen), pausa,
    /// configurações, controles, créditos, morte e fim da demo. O GameFlow decide qual mostrar.
    /// </summary>
    public class GameMenus : MonoBehaviour
    {
        public static GameMenus Instance { get; private set; }
        public enum Screen { None, Main, Pause, Settings, Controls, Credits, Death, End }
        public Screen Current { get; private set; } = Screen.None;

        public System.Action onStart, onResume, onRestartCheckpoint, onMainMenu, onQuit, onPlayAgain, onWorld;

        Canvas canvas;
        CanvasGroup fade; float fadeTarget, fadeSpeed = 2f;
        readonly Dictionary<Screen, CanvasGroup> screens = new Dictionary<Screen, CanvasGroup>();
        readonly Dictionary<Screen, GameObject> firstSelected = new Dictionary<Screen, GameObject>();
        Screen settingsReturn = Screen.Main, controlsReturn = Screen.Main, creditsReturn = Screen.Main;
        TitleScreen title;
        Text endStats, deathText;
        readonly List<System.Action> refreshers = new List<System.Action>();

        void Awake()
        {
            Instance = this;
            EnsureEventSystem();
            canvas = UIKit.MakeCanvas("Menus", 20);
            canvas.transform.SetParent(transform, false);
            title = TitleScreen.Build(canvas.transform);   // arte da tela inicial, atrás de tudo
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
            if (title != null) title.SetBackdrop(false);
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

        Button MakeButton(Transform parent, string text, Vector2 pos, System.Action onClick, float width = 420f, int size = 30, bool primary = false)
        {
            float height = primary ? 68f : 56f;
            var rt = UIKit.Rect("Botao_" + text, parent, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), pos, new Vector2(width, height));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIKit.S("ui_button"); img.type = Image.Type.Sliced;
            img.color = primary ? new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.16f) : new Color(0.025f, 0.018f, 0.035f, 0.34f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var content = UIKit.Rect("Conteudo", rt, Vector2.zero, Vector2.one, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var marker = UIKit.Img("Marcador", content, "ui_diamond", UIKit.GoldDim, new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(primary ? 18 : 13, primary ? 18 : 13));
            var lbl = UIKit.Label("Texto", content, text, primary ? UIKit.SerifBold : UIKit.Serif, size, UIKit.Muted, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(width * 0.5f + 22, 3), new Vector2(width - 44, 50));
            var wrt = UIKit.Rect("Onda", content, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(36, 4), new Vector2(width * 0.56f, 14));
            var wave = wrt.gameObject.AddComponent<UIWaveform>(); wave.thickness = 2f; wave.cycles = 5; wave.raycastTarget = false; wave.color = new Color(1, 1, 1, 0);
            var fx = rt.gameObject.AddComponent<UIButtonFX>(); fx.label = lbl; fx.wave = wave; fx.content = content; fx.backdrop = img; fx.marker = marker;
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
            if (title != null)
            {
                // a própria arte de referência: JOGAR · CONFIGURAÇÕES · SAIR (controles e
                // créditos ficam dentro de Configurações)
                title.BuildButtons(g.transform, () => onStart?.Invoke(), () => OpenSettings(Screen.Main), () => onQuit?.Invoke());
                firstSelected[Screen.Main] = title.First != null ? title.First.gameObject : null;
                return;
            }
            // reserva se a arte não estiver no projeto
            var t = g.transform;
            UIKit.Img("Escurecimento", t, null, new Color(0.012f, 0.009f, 0.018f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920, 1080));
            var panel = UIKit.Rect("MolduraCentral", t, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 920));
            UIKit.Img("Painel", panel, "ui_panel", new Color(0.055f, 0.038f, 0.045f, 0.96f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 920), Image.Type.Sliced);
            UIKit.Label("Titulo", panel, "ECOS DO\nCONTRACANTO", UIKit.Display, 70, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 174), new Vector2(720, 160));
            UIKit.Label("Subtitulo", panel, "Elyndra · Valtéria · Campânula", UIKit.Serif, 23, UIKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 61), new Vector2(650, 34));
            var b0 = MakeButton(panel, "JOGAR", new Vector2(104, -20), () => onStart?.Invoke(), 612f, 29, true);
            MakeButton(panel, "Configurações", new Vector2(104, -100), () => OpenSettings(Screen.Main), 612f);
            MakeButton(panel, "Sair", new Vector2(104, -168), () => onQuit?.Invoke(), 612f);
            firstSelected[Screen.Main] = b0.gameObject;
        }

        // ------------------------------------------------------------ pausa e configurações góticas (referência do autor)

        RectTransform pausePanel, settingsPanel, settingsViewport, settingsContent;
        CanvasGroup pauseGroup;
        GameObject pauseSettingsButton, checkpointRow;
        int backFrame = -1;
        /// <summary>O menu já usou o Esc/B deste quadro (fechou configurações etc.): o GameFlow não deve despausar.</summary>
        public bool BackHandledThisFrame => backFrame == Time.frameCount;
        bool SideBySide => Current == Screen.Settings && settingsReturn == Screen.Pause;
        static readonly Vector2 C = new Vector2(0.5f, 0.5f);

        CanvasGroup NewGothicScreen(Screen s, bool backdrop)
        {
            var rt = UIKit.Stretch(s.ToString(), canvas.transform);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            if (backdrop)
            {
                var dim = rt.gameObject.AddComponent<Image>();
                dim.color = new Color(0.012f, 0.008f, 0.01f, 0.38f);
                var v = UIKit.Img("Vinheta", rt, null, Color.white, C, Vector2.zero, Vector2.zero);
                v.sprite = GothicUI.G("pause_backdrop");
                v.rectTransform.anchorMin = Vector2.zero; v.rectTransform.anchorMax = Vector2.one; v.rectTransform.sizeDelta = Vector2.zero;
            }
            screens[s] = g;
            return g;
        }

        Button GothicMenuButton(Transform parent, string text, Vector2 pos, System.Action onClick, float scale = 1.25f, int size = 30)
        {
            var sp = GothicUI.G("button_normal");
            Vector2 sz = (sp != null ? sp.rect.size : new Vector2(440, 84)) * scale;
            var rt = UIKit.Rect("Botao_" + text, parent, C, C, C, pos, sz);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sp; img.color = Color.white;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var lbl = GothicUI.Label("Texto", rt, text, GothicUI.Cinzel, size, GothicUI.BoneDim, TextAnchor.MiddleCenter, C, new Vector2(0, 2), new Vector2(sz.x * 0.72f, sz.y));
            var fx = rt.gameObject.AddComponent<GothicButton>();
            fx.body = img; fx.normal = sp; fx.selectedSprite = GothicUI.G("button_selected"); fx.label = lbl;
            btn.onClick.AddListener(() => onClick?.Invoke());
            return btn;
        }

        void BuildPause()
        {
            var g = NewGothicScreen(Screen.Pause, true);
            pauseGroup = g;
            const float S = 1.1f;
            pausePanel = UIKit.Rect("PainelPausa", g.transform, C, C, C, Vector2.zero, new Vector2(600, 820) * S);
            GothicUI.Img("Moldura", pausePanel, "pause_panel", C, Vector2.zero, S);
            GothicUI.Label("Titulo", pausePanel, "PAUSA", GothicUI.Cinzel, 58, GothicUI.Bone, TextAnchor.MiddleCenter, C, new Vector2(0, 272), new Vector2(560, 90));
            var b0 = GothicMenuButton(pausePanel, "CONTINUAR", new Vector2(0, 122), () => onResume?.Invoke());
            pauseSettingsButton = GothicMenuButton(pausePanel, "CONFIGURAÇÕES", new Vector2(0, 8), () => OpenSettings(Screen.Pause)).gameObject;
            GothicMenuButton(pausePanel, "MENU PRINCIPAL", new Vector2(0, -106), () => onMainMenu?.Invoke());
            GothicMenuButton(pausePanel, "SAIR", new Vector2(0, -220), () => onQuit?.Invoke());
            firstSelected[Screen.Pause] = b0.gameObject;
        }

        void BuildSettings()
        {
            GameSettings.Load();
            var g = NewGothicScreen(Screen.Settings, false);
            const float S = 1.12f;
            settingsPanel = UIKit.Rect("PainelConfiguracoes", g.transform, C, C, C, Vector2.zero, new Vector2(560, 560) * S);
            GothicUI.Img("Moldura", settingsPanel, "settings_panel", C, Vector2.zero, S);
            GothicUI.Label("Titulo", settingsPanel, "CONFIGURAÇÕES", GothicUI.Cinzel, 38, GothicUI.Bone, TextAnchor.MiddleCenter, C, new Vector2(0, 183), new Vector2(470, 60));
            // fechar (X) — o mouse clica; Esc/B também fecham
            var close = UIKit.Rect("Fechar", settingsPanel, C, C, C, new Vector2(241, 183), new Vector2(54, 54));
            var ci = close.gameObject.AddComponent<Image>(); ci.sprite = GothicUI.G("close_button");
            var cb = close.gameObject.AddComponent<Button>();
            cb.navigation = new Navigation { mode = Navigation.Mode.None };
            var cc = cb.colors; cc.highlightedColor = new Color(1f, 0.72f, 0.62f); cc.pressedColor = new Color(0.8f, 0.4f, 0.35f); cb.colors = cc;
            cb.onClick.AddListener(CloseSettings);

            // a lista rola: as quatro primeiras linhas são as da referência, o resto aparece descendo
            settingsViewport = UIKit.Rect("Lista", settingsPanel, C, C, new Vector2(0.5f, 1f), new Vector2(0, 132), new Vector2(548, 404));
            settingsViewport.gameObject.AddComponent<RectMask2D>();
            settingsContent = UIKit.Rect("Conteudo", settingsViewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(548, 100));
            float y = -4f;
            GameObject first = null;

            void Changed() { GameSettings.Apply(); GameSettings.Save(); RefreshAll(); }

            (RectTransform rt, GothicButton fx) Row(string name, float h)
            {
                var rt = UIKit.Rect("Linha_" + name, settingsContent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), C, new Vector2(0, y - h * 0.5f), new Vector2(528, h - 4));
                var glow = rt.gameObject.AddComponent<Image>();
                glow.sprite = GothicUI.G("slider_fill"); glow.color = new Color(1f, 0.55f, 0.45f, 0f);
                var btn = rt.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
                var fx = rt.gameObject.AddComponent<GothicButton>(); fx.glow = glow; fx.glowMax = 0.16f;
                if (first == null) first = rt.gameObject;
                y -= h;
                return (rt, fx);
            }

            void Toggle(string name, System.Func<bool> get, System.Action flip)
            {
                var (rt, fx) = Row(name, 66);
                fx.label = GothicUI.Label("Rotulo", rt, name, GothicUI.Garamond, 28, GothicUI.Bone, TextAnchor.MiddleLeft, C, new Vector2(-82, 0), new Vector2(330, 50));
                var track = GothicUI.Img("Trilho", rt, "toggle_track", C, new Vector2(186, 0), 1.06f);
                var knob = GothicUI.Img("Botao", track.transform, "toggle_knob", C, Vector2.zero, 1.06f);
                var tg = rt.gameObject.AddComponent<GothicToggle>(); tg.knob = knob.rectTransform; tg.knobImg = knob; tg.get = get; tg.offX = -23f; tg.onX = 23f;
                rt.GetComponent<Button>().onClick.AddListener(() => { flip(); Changed(); });
                fx.onLeft = fx.onRight = () => { flip(); Changed(); ArenAudio.PlayUI(Sfx.UIMove, 0.4f); };
            }

            void Separator()
            {
                var line = UIKit.Img("Separador", settingsContent, null, new Color(0.62f, 0.47f, 0.3f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, y - 8), new Vector2(486, 2));
                UIKit.Img("Losango", line.transform, "ui_diamond", new Color(0.75f, 0.58f, 0.36f, 0.9f), C, Vector2.zero, new Vector2(12, 12));
                y -= 16;
            }

            void Slider(string name, System.Func<float> get, System.Action<float> set)
            {
                var (rt, fx) = Row(name, 98);
                fx.label = GothicUI.Label("Rotulo", rt, name, GothicUI.Garamond, 28, GothicUI.Bone, TextAnchor.MiddleLeft, C, new Vector2(-82, 22), new Vector2(330, 44));
                var pct = GothicUI.Label("Valor", rt, "", GothicUI.Garamond, 26, GothicUI.Bone, TextAnchor.MiddleRight, C, new Vector2(170, 22), new Vector2(160, 44));
                GothicUI.Img("Trilho", rt, "slider_track", C, new Vector2(0, -19), 1.04f);
                var fill = GothicUI.Img("Preenchimento", rt, "slider_fill", C, new Vector2(0, -19), 1.04f);
                fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                var area = UIKit.Rect("Area", rt, C, C, C, new Vector2(0, -19), new Vector2(428, 34));
                var hit = area.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0);
                var knob = GothicUI.Img("Caveira", rt, "slider_knob", C, new Vector2(0, -19), 1.06f);
                var sl = area.gameObject.AddComponent<GothicSlider>();
                sl.track = area; sl.fill = fill; sl.knob = knob.rectTransform; sl.valueText = pct; sl.selectOnDrag = rt.gameObject;
                sl.get = get; sl.set = v => { set(v); Changed(); };
                fx.onLeft = () => { set(Mathf.Clamp01(Mathf.Round((get() - 0.05f) * 20f) / 20f)); Changed(); ArenAudio.PlayUI(Sfx.UIMove, 0.4f); };
                fx.onRight = () => { set(Mathf.Clamp01(Mathf.Round((get() + 0.05f) * 20f) / 20f)); Changed(); ArenAudio.PlayUI(Sfx.UIMove, 0.4f); };
            }

            void Option(string name, System.Func<string> value, System.Action left, System.Action right)
            {
                var (rt, fx) = Row(name, 54);
                fx.label = GothicUI.Label("Rotulo", rt, name, GothicUI.Garamond, 25, GothicUI.Bone, TextAnchor.MiddleLeft, C, new Vector2(-70, 0), new Vector2(360, 44));
                var val = GothicUI.Label("Valor", rt, "", GothicUI.Garamond, 24, GothicUI.Bronze, TextAnchor.MiddleRight, C, new Vector2(150, 0), new Vector2(200, 44));
                refreshers.Add(() => val.text = "‹ " + value() + " ›");
                rt.GetComponent<Button>().onClick.AddListener(() => { right(); Changed(); });
                fx.onLeft = () => { left(); Changed(); ArenAudio.PlayUI(Sfx.UIMove, 0.4f); };
                fx.onRight = () => { right(); Changed(); ArenAudio.PlayUI(Sfx.UIMove, 0.4f); };
            }

            GameObject Action(string name, System.Action onClick)
            {
                var (rt, fx) = Row(name, 54);
                fx.label = GothicUI.Label("Rotulo", rt, name, GothicUI.Cinzel, 22, GothicUI.Bone, TextAnchor.MiddleCenter, C, Vector2.zero, new Vector2(500, 44));
                rt.GetComponent<Button>().onClick.AddListener(() => onClick());
                return rt.gameObject;
            }

            Toggle("Tela Cheia", () => GameSettings.Fullscreen, () => { GameSettings.Fullscreen = !GameSettings.Fullscreen; GameSettings.Apply(true); });
            Separator();
            Slider("Volume Geral", () => GameSettings.Master, v => GameSettings.Master = v);
            Slider("Música", () => GameSettings.Music, v => GameSettings.Music = v);
            Slider("Efeitos", () => GameSettings.Effects, v => GameSettings.Effects = v);
            Separator();
            Option("Timbre dos golpes", () => GameSettings.Instrument == CombatInstrument.Flute ? "Flauta" : "Ukulele",
                () => GameSettings.Instrument = GameSettings.Instrument == CombatInstrument.Flute ? CombatInstrument.Ukulele : CombatInstrument.Flute,
                () => GameSettings.Instrument = GameSettings.Instrument == CombatInstrument.Flute ? CombatInstrument.Ukulele : CombatInstrument.Flute);
            Option("Sensibilidade da câmera", () => GameSettings.Sensitivity.ToString("0.0") + "×", () => GameSettings.Sensitivity = Mathf.Clamp(GameSettings.Sensitivity - 0.1f, 0.3f, 2.5f), () => GameSettings.Sensitivity = Mathf.Clamp(GameSettings.Sensitivity + 0.1f, 0.3f, 2.5f));
            Option("Qualidade gráfica", () => GameSettings.QualityNames[GameSettings.Quality], () => GameSettings.Quality = (GameSettings.Quality + 2) % 3, () => GameSettings.Quality = (GameSettings.Quality + 1) % 3);
            Option("Escala 3D", () => Mathf.RoundToInt(GameSettings.RenderScale * 100) + "%", () => GameSettings.StepRenderScale(-1), () => GameSettings.StepRenderScale(1));
            Option("Resolução", GameSettings.ResolutionLabel,
                () => { var n = UnityEngine.Screen.resolutions.Length; GameSettings.ResolutionIndex = n == 0 ? -1 : (GameSettings.ResolutionIndex - 1 + n) % n; GameSettings.Apply(true); },
                () => { var n = UnityEngine.Screen.resolutions.Length; GameSettings.ResolutionIndex = n == 0 ? -1 : (GameSettings.ResolutionIndex + 1) % n; GameSettings.Apply(true); });
            Option("Sombras (pesado)", () => GameSettings.Shadows ? "Ligadas" : "Desligadas", () => GameSettings.Shadows = !GameSettings.Shadows, () => GameSettings.Shadows = !GameSettings.Shadows);
            Option("Tremor de câmera", () => GameSettings.Shake ? "Ligado" : "Desligado", () => GameSettings.Shake = !GameSettings.Shake, () => GameSettings.Shake = !GameSettings.Shake);
            Option("Mostrar FPS", () => GameSettings.ShowFps ? "Sim" : "Não", () => GameSettings.ShowFps = !GameSettings.ShowFps, () => GameSettings.ShowFps = !GameSettings.ShowFps);
            Separator();
            Action("CONTROLES", () => OpenControls(Screen.Settings));
            Action("CRÉDITOS", () => OpenCredits(Screen.Settings));
            checkpointRow = Action("VOLTAR AO ÚLTIMO PONTO", () => onRestartCheckpoint?.Invoke());
            settingsContent.sizeDelta = new Vector2(548, -y + 8);
            firstSelected[Screen.Settings] = first;
            RefreshAll();
        }

        public void OpenSettingsFromTest() => OpenSettings(Current == Screen.Main ? Screen.Main : Screen.Pause);
        public void CloseSettingsFromTest() => CloseSettings();

        void CloseSettings()
        {
            ArenAudio.PlayUI(Sfx.UIBack, 0.6f);
            backFrame = Time.frameCount;
            var back = settingsReturn;
            Show(back);
            if (back == Screen.Pause && pauseSettingsButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(pauseSettingsButton);
        }

        /// <summary>Painéis deslizam (pausa à esquerda + configurações à direita, como na referência) e a lista rola até a linha selecionada.</summary>
        void UpdateGothicLayout(float dt)
        {
            bool side = SideBySide;
            if (pauseSettingsButton != null) pauseSettingsButton.GetComponent<GothicButton>().forceHighlight = side;
            if (pausePanel != null)
            {
                var p = pausePanel.anchoredPosition;
                pausePanel.anchoredPosition = new Vector2(Mathf.Lerp(p.x, side ? -318f : 0f, 1f - Mathf.Exp(-dt * 12f)), 8f);
            }
            if (settingsPanel != null)
            {
                var p = settingsPanel.anchoredPosition;
                settingsPanel.anchoredPosition = new Vector2(Mathf.Lerp(p.x, side ? 342f : 0f, 1f - Mathf.Exp(-dt * 12f)), 0f);
            }
            if (Current == Screen.Settings && settingsContent != null && EventSystem.current != null)
            {
                var sel = EventSystem.current.currentSelectedGameObject;
                if (sel != null && sel.transform.parent == settingsContent)
                {
                    var rt = (RectTransform)sel.transform;
                    float top = -rt.anchoredPosition.y - rt.rect.height * 0.5f - 6f;    // distância do topo do conteúdo
                    float bottom = -rt.anchoredPosition.y + rt.rect.height * 0.5f + 6f;
                    float view = settingsViewport.rect.height;
                    float scroll = settingsContent.anchoredPosition.y;
                    if (top < scroll) scroll = top;
                    else if (bottom > scroll + view) scroll = bottom - view;
                    scroll = Mathf.Clamp(scroll, 0f, Mathf.Max(0f, settingsContent.sizeDelta.y - view));
                    settingsContent.anchoredPosition = new Vector2(0, Mathf.Lerp(settingsContent.anchoredPosition.y, scroll, 1f - Mathf.Exp(-dt * 14f)));
                }
            }
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
            var b = MakeButton(t, "Voltar", new Vector2(120, -330), () => Show(controlsReturn));
            firstSelected[Screen.Controls] = b.gameObject;
        }

        void BuildCredits()
        {
            var g = NewScreen(Screen.Credits, true);
            var t = g.transform;
            Title(t, "CRÉDITOS", "", new Vector2(640, 330), 56);
            string txt =
                "<b>Ecos do Contracanto</b> — demo (Elyndra · Valtéria)\n\n" +
                "Mundo de Campanula, rig do Aren, combate, efeitos e interface:\n" +
                "feitos para esta demo (Blender por script, Unity, síntese procedural).\n\n" +
                "Música do menu: Bard of Broken Bells — fornecida pelo autor do jogo\n" +
                "Flauta mágica 3D: fornecida pelo autor; otimização local para 18 mil triângulos\n" +
                "Referências de personagem, interface e ukulele: fornecidas pelo autor do jogo\n" +
                "(direitos/licenças dos itens fornecidos devem ser confirmados antes do lançamento)\n\n" +
                "Animações: Universal Animation Library 2 — Quaternius (CC0)\n" +
                "Props: Fantasy Props MegaKit — Quaternius (CC0)\n" +
                "Texturas fotográficas da vila e dos 13 reinos de Elyndra e objetos escaneados da Campânula (portas, escada do forte,\n  barris, caixotes, baldes, cestos, bancos, mesas, estátuas, roda de fiar, candelabros, lustres, chafarizes, vasos, plantas): Poly Haven — polyhaven.com (CC0)\n" +
                "Reinos de Elyndra (relevo, cidades, serras, céu): construtor procedural feito para o jogo\n" +
                "Aldeões: Modular Character Outfits Fantasy + Universal Base Characters — Quaternius (CC0)\n" +
                "Sussurrante: arte do autor; modelo base gerado com Tripo H3.1 (via Higgsfield) e FLUX.1 Kontext; rig, texturas, lâmina, sons e efeitos feitos para o jogo\n" +
                "Efeitos sonoros gerados por IA: ElevenLabs Sound Effects (elevenlabs.io)\n" +
                "Tela de carregamento: storyboard fornecido pelo autor do jogo\n" +
                "Efeitos sonoros gravados: 400 Sounds Pack — Chequered Ink\n" +
                "Nebulosas e estrelas: Seamless Space Backgrounds — Screaming Brain Studios (CC0)\n" +
                "Dissolução dos Ecos: Free Dissolve Shader — VOiD1 Gaming (adaptado para Built-in RP)\n" +
                "Sistema de parkour base: Dynamic Parkour System (MIT)\n" +
                "Animações de parkour: Mixamo (Adobe)\n" +
                "Fontes: Noto Serif · Noto Sans · Cinzel (Natanael Gama) · EB Garamond (Georg Duffner, Octavio Pardo) — SIL Open Font License\n" +
                "Arte da tela inicial: fornecida pelo autor do jogo (animada em pixel art para a demo)\n" +
                "Modelos do Aren e do cervo corrompido: fornecidos pelo autor do jogo";
            UIKit.Label("Texto", t, txt, UIKit.Serif, 19, UIKit.Bone, TextAnchor.UpperLeft, new Vector2(0, 0.5f), new Vector2(700, 60), new Vector2(1180, 560));
            var b = MakeButton(t, "Voltar", new Vector2(120, -330), () => Show(creditsReturn));
            firstSelected[Screen.Credits] = b.gameObject;
        }

        void BuildDeath()
        {
            var g = NewScreen(Screen.Death, true);
            var t = g.transform;
            UIKit.Label("Titulo", t, UIKit.Spaced("AREN CAIU", 2), UIKit.Display, 84, new Color(0.9f, 0.3f, 0.35f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1400, 120));
            UIKit.Img("Ornamento", t, "ui_ornament", UIKit.Crimson, new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(520, 32));
            deathText = UIKit.Label("Sub", t, "O eco se recompõe...", UIKit.Serif, 26, UIKit.Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1400, 40));
        }

        void BuildEnd()
        {
            var g = NewScreen(Screen.End, true);
            var t = g.transform;
            UIKit.Label("Titulo", t, UIKit.Spaced("O CERVO DE CONTRATEMPO SE CALA", 1), UIKit.Display, 60, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(1600, 100));
            UIKit.Img("Ornamento", t, "ui_ornament", UIKit.Gold, new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(520, 32));
            UIKit.Label("Lore", t, "O cervo se desfaz em silêncio. Muito além das montanhas, a Fenda continua aberta —\ne a leste, onde o brilho caiu, a Cratera do Primeiro Peso ainda vibra.", UIKit.Serif, 24, UIKit.Muted, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 130), new Vector2(1500, 80));
            endStats = UIKit.Label("Stats", t, "", UIKit.Sans, 24, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1000, 160));
            UIKit.Label("Obrigado", t, "Obrigado por jogar a demo.", UIKit.Serif, 26, UIKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(1000, 40));
            var b = GothicMenuButton(t, "SEGUIR PARA VALTÉRIA", new Vector2(0, -200), () => onWorld?.Invoke(), 1.25f, 26);
            GothicMenuButton(t, "JOGAR DE NOVO", new Vector2(-300, -320), () => onPlayAgain?.Invoke(), 0.9f, 22);
            GothicMenuButton(t, "MENU PRINCIPAL", new Vector2(300, -320), () => onMainMenu?.Invoke(), 0.9f, 22);
            firstSelected[Screen.End] = b.gameObject;
        }

        // ------------------------------------------------------------ controle

        void OpenSettings(Screen from) { settingsReturn = from; Show(Screen.Settings); }
        void OpenControls(Screen from) { controlsReturn = from; Show(Screen.Controls); }
        void OpenCredits(Screen from) { creditsReturn = from; Show(Screen.Credits); }

        void Hide(CanvasGroup g) { g.alpha = 0; g.interactable = false; g.blocksRaycasts = false; g.gameObject.SetActive(false); }

        public void Show(Screen s)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool keepPause = s == Screen.Settings && settingsReturn == Screen.Pause;
            foreach (var kv in screens) if (kv.Key != s && !(keepPause && kv.Key == Screen.Pause)) Hide(kv.Value);
            if (keepPause && pauseGroup != null) { pauseGroup.interactable = false; pauseGroup.blocksRaycasts = false; }
            if (checkpointRow != null) checkpointRow.SetActive(GameFlowState.InGame);
            long tHide = sw.ElapsedMilliseconds;
            Current = s;
            if (title != null)
            {
                // a arte fica atrás do menu inicial e das telas abertas a partir dele
                bool art = s == Screen.Main || (!GameFlowState.InGame && (s == Screen.Settings || s == Screen.Controls || s == Screen.Credits));
                title.SetBackdrop(art);
                long tBack = sw.ElapsedMilliseconds;
                title.SetDim(s != Screen.Main);
                if (sw.ElapsedMilliseconds > 200) Debug.Log($"[Menu] Show({s}) lento: esconder {tHide} ms, arte {tBack - tHide} ms, total {sw.ElapsedMilliseconds} ms");
                if (s == Screen.Main) title.OnShown();
            }
            if (s != Screen.None && screens.TryGetValue(s, out var g))
            {
                if (!g.gameObject.activeSelf) g.alpha = 0f;
                g.gameObject.SetActive(true);
                g.interactable = true; g.blocksRaycasts = true;
                if (firstSelected.TryGetValue(s, out var first) && first != null && EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(first);
                RefreshAll();
            }
            bool needCursor = s != Screen.None && s != Screen.Death;
            long tc = sw.ElapsedMilliseconds;
            if (!GameSettings.NoCursorLock) Cursor.lockState = needCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = needCursor;
            if (sw.ElapsedMilliseconds - tc > 200) Debug.Log($"[Menu] travar o cursor levou {sw.ElapsedMilliseconds - tc} ms (janela com foco: {Application.isFocused})");
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
            if (SideBySide && pauseGroup != null) pauseGroup.alpha = Mathf.MoveTowards(pauseGroup.alpha, 0.8f, dt * 4f);
            else if (Current == Screen.Pause && pauseGroup != null) pauseGroup.alpha = Mathf.MoveTowards(pauseGroup.alpha, 1f, dt * 4f);
            UpdateGothicLayout(dt);

            // voltar com Esc/B nas telas secundárias
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back)
            {
                if (Current == Screen.Settings) CloseSettings();
                else if (Current == Screen.Controls) { ArenAudio.PlayUI(Sfx.UIBack, 0.6f); backFrame = Time.frameCount; Show(controlsReturn); }
                else if (Current == Screen.Credits) { ArenAudio.PlayUI(Sfx.UIBack, 0.6f); backFrame = Time.frameCount; Show(creditsReturn); }
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
