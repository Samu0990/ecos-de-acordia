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

        public System.Action onStart, onResume, onRestartCheckpoint, onMainMenu, onQuit, onPlayAgain;

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
            UIKit.Label("Titulo", panel, "ECOS DE\nACORDIA", UIKit.Display, 70, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 174), new Vector2(720, 160));
            UIKit.Label("Subtitulo", panel, "A Ruptura do Contracanto", UIKit.Serif, 23, UIKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 61), new Vector2(650, 34));
            var b0 = MakeButton(panel, "JOGAR", new Vector2(104, -20), () => onStart?.Invoke(), 612f, 29, true);
            MakeButton(panel, "Configurações", new Vector2(104, -100), () => OpenSettings(Screen.Main), 612f);
            MakeButton(panel, "Sair", new Vector2(104, -168), () => onQuit?.Invoke(), 612f);
            firstSelected[Screen.Main] = b0.gameObject;
        }

        void BuildPause()
        {
            var g = NewScreen(Screen.Pause, true);
            var t = g.transform;
            var panel = UIKit.Rect("MolduraPausa", t, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(760, 790));
            UIKit.Img("Painel", panel, "ui_panel", new Color(0.045f, 0.025f, 0.03f, 0.97f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 790), Image.Type.Sliced);
            UIKit.Img("Emblema", panel, "ui_emblem", UIKit.Gold, new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(92, 92));
            UIKit.Label("Titulo", panel, UIKit.Spaced("PAUSA", 1), UIKit.Display, 58, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 226), new Vector2(650, 76));
            UIKit.Img("Ornamento", panel, "ui_ornament", UIKit.Gold, new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(400, 26));
            var b0 = MakeButton(panel, "CONTINUAR", new Vector2(90, 96), () => onResume?.Invoke(), 580f, 27, true);
            MakeButton(panel, "Configurações", new Vector2(90, 20), () => OpenSettings(Screen.Pause), 580f);
            MakeButton(panel, "Controles", new Vector2(90, -46), () => OpenControls(Screen.Pause), 580f);
            MakeButton(panel, "Voltar ao último ponto", new Vector2(90, -112), () => onRestartCheckpoint?.Invoke(), 580f);
            MakeButton(panel, "Menu principal", new Vector2(90, -178), () => onMainMenu?.Invoke(), 580f);
            MakeButton(panel, "Sair do jogo", new Vector2(90, -244), () => onQuit?.Invoke(), 580f);
            firstSelected[Screen.Pause] = b0.gameObject;
        }

        void BuildSettings()
        {
            GameSettings.Load();
            var g = NewScreen(Screen.Settings, true);
            var t = g.transform;
            var panel = UIKit.Rect("MolduraConfiguracoes", t, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1220, 980));
            UIKit.Img("Painel", panel, "ui_panel", new Color(0.04f, 0.025f, 0.032f, 0.97f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1220, 980), Image.Type.Sliced);
            UIKit.Label("Titulo", panel, UIKit.Spaced("CONFIGURAÇÕES"), UIKit.Display, 50, UIKit.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 400), new Vector2(1050, 70));
            UIKit.Img("Ornamento", panel, "ui_ornament", UIKit.Gold, new Vector2(0.5f, 0.5f), new Vector2(0, 354), new Vector2(440, 28));
            float y = 240; float step = 48;
            GameObject first = null;
            void Row(string name, System.Func<string> value, System.Action left, System.Action right)
            {
                var b = MakeButton(panel, name, new Vector2(160, y), () => { right(); GameSettings.Apply(); GameSettings.Save(); RefreshAll(); }, 900f, 26);
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
            Row("Timbre dos golpes", () => GameSettings.Instrument == CombatInstrument.Flute ? "Flauta" : "Ukulele",
                () => GameSettings.Instrument = GameSettings.Instrument == CombatInstrument.Flute ? CombatInstrument.Ukulele : CombatInstrument.Flute,
                () => GameSettings.Instrument = GameSettings.Instrument == CombatInstrument.Flute ? CombatInstrument.Ukulele : CombatInstrument.Flute);
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
            MakeButton(panel, "Controles", new Vector2(160, y - 20), () => OpenControls(Screen.Settings), 440f);
            MakeButton(panel, "Créditos", new Vector2(620, y - 20), () => OpenCredits(Screen.Settings), 440f);
            MakeButton(panel, "Voltar", new Vector2(160, y - 82), () => Show(settingsReturn), 900f);
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
            var b = MakeButton(t, "Voltar", new Vector2(120, -330), () => Show(controlsReturn));
            firstSelected[Screen.Controls] = b.gameObject;
        }

        void BuildCredits()
        {
            var g = NewScreen(Screen.Credits, true);
            var t = g.transform;
            Title(t, "CRÉDITOS", "", new Vector2(640, 330), 56);
            string txt =
                "<b>Ecos de Acordia: A Ruptura do Contracanto</b> — demo\n\n" +
                "Mundo de Campanula, rig do Aren, combate, efeitos e interface:\n" +
                "feitos para esta demo (Blender por script, Unity, síntese procedural).\n\n" +
                "Música do menu: Bard of Broken Bells — fornecida pelo autor do jogo\n" +
                "Flauta mágica 3D: fornecida pelo autor; otimização local para 18 mil triângulos\n" +
                "Referências de personagem, interface e ukulele: fornecidas pelo autor do jogo\n" +
                "(direitos/licenças dos itens fornecidos devem ser confirmados antes do lançamento)\n\n" +
                "Animações: Universal Animation Library 2 — Quaternius (CC0)\n" +
                "Props: Fantasy Props MegaKit — Quaternius (CC0)\n" +
                "Efeitos sonoros gravados: 400 Sounds Pack — Chequered Ink\n" +
                "Nebulosas e estrelas: Seamless Space Backgrounds — Screaming Brain Studios (CC0)\n" +
                "Dissolução dos Ecos: Free Dissolve Shader — VOiD1 Gaming (adaptado para Built-in RP)\n" +
                "Sistema de parkour base: Dynamic Parkour System (MIT)\n" +
                "Animações de parkour: Mixamo (Adobe)\n" +
                "Fontes: Noto Serif · Noto Sans (SIL Open Font License)\n" +
                "Arte da tela inicial: fornecida pelo autor do jogo (animada em pixel art para a demo)\n" +
                "Modelos do Aren e do cervo corrompido: fornecidos pelo autor do jogo";
            UIKit.Label("Texto", t, txt, UIKit.Serif, 22, UIKit.Bone, TextAnchor.UpperLeft, new Vector2(0, 0.5f), new Vector2(700, 60), new Vector2(1180, 500));
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
        void OpenControls(Screen from) { controlsReturn = from; Show(Screen.Controls); }
        void OpenCredits(Screen from) { creditsReturn = from; Show(Screen.Credits); }

        void Hide(CanvasGroup g) { g.alpha = 0; g.interactable = false; g.blocksRaycasts = false; g.gameObject.SetActive(false); }

        public void Show(Screen s)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var kv in screens) if (kv.Key != s) Hide(kv.Value);
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
                g.gameObject.SetActive(true);
                g.alpha = 0f; g.interactable = true; g.blocksRaycasts = true;
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

            // voltar com Esc/B nas telas secundárias
            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back)
            {
                if (Current == Screen.Settings) { ArenAudio.PlayUI(Sfx.UIBack, 0.6f); Show(settingsReturn); }
                else if (Current == Screen.Controls) { ArenAudio.PlayUI(Sfx.UIBack, 0.6f); Show(controlsReturn); }
                else if (Current == Screen.Credits) { ArenAudio.PlayUI(Sfx.UIBack, 0.6f); Show(creditsReturn); }
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
