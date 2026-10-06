using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Aren.UI
{
    /// <summary>
    /// UI gótica da referência do autor (menu de pausa, configurações e HUD): moldura de bronze escuro,
    /// faixa de título vermelha, caveiras, flâmulas, botões com pontas de lança e sliders com botão de
    /// caveira. Os sprites são desenhados por código em Tools/texgen/gen_gothic_ui.py
    /// (Resources/UI/Gothic); fontes Cinzel (títulos/botões) e EB Garamond (rótulos), ambas SIL OFL.
    /// </summary>
    public static class GothicUI
    {
        public static readonly Color Bone = new Color(0.93f, 0.88f, 0.79f, 1f);
        public static readonly Color BoneDim = new Color(0.66f, 0.61f, 0.54f, 1f);
        public static readonly Color Red = new Color(0.82f, 0.15f, 0.12f, 1f);
        public static readonly Color Teal = new Color(0.4f, 0.88f, 0.88f, 1f);
        public static readonly Color Bronze = new Color(0.8f, 0.64f, 0.42f, 1f);

        static Font cinzel, garamond;
        public static Font Cinzel { get { if (cinzel == null) cinzel = Resources.Load<Font>("UI/Fonts/Cinzel"); return cinzel != null ? cinzel : UIKit.Display; } }
        public static Font Garamond { get { if (garamond == null) garamond = Resources.Load<Font>("UI/Fonts/EBGaramond"); return garamond != null ? garamond : UIKit.Serif; } }

        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static Sprite G(string name)
        {
            if (!sprites.TryGetValue(name, out var s) || s == null)
            {
                s = Resources.Load<Sprite>("UI/Gothic/" + name);
                sprites[name] = s;
            }
            return s;
        }

        /// <summary>Imagem com o tamanho nativo do sprite × escala.</summary>
        public static Image Img(string name, Transform parent, string sprite, Vector2 anchor, Vector2 pos, float scale, Color? color = null)
        {
            var sp = G(sprite);
            Vector2 size = sp != null ? sp.rect.size * scale : new Vector2(64, 64) * scale;
            var im = UIKit.Img(name, parent, null, color ?? Color.white, anchor, pos, size);
            im.sprite = sp;
            return im;
        }

        public static Text Label(string name, Transform parent, string text, Font font, int size, Color color, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var t = UIKit.Label(name, parent, text, font, size, color, align, anchor, pos, box);
            var sh = t.GetComponent<Shadow>();
            if (sh != null) { sh.effectColor = new Color(0, 0, 0, 0.9f); sh.effectDistance = new Vector2(2f, -2f); }
            return t;
        }

        /// <summary>1250 → "1.250" (separador de milhar do português).</summary>
        public static string Thousands(int v) => v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');
    }

    /// <summary>
    /// Botão/linha gótica: ao ser selecionado troca o sprite (normal → vermelho com pontas de lança),
    /// acende o rótulo e uma faixa de brilho; ←/→ chamam onLeft/onRight (sliders e opções).
    /// </summary>
    public class GothicButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, ISubmitHandler, IPointerClickHandler
    {
        public Image body; public Sprite normal, selectedSprite;
        public Text label; public Graphic glow; public float glowMax = 0.55f;
        public Color labelOff = GothicUI.BoneDim, labelOn = GothicUI.Bone;
        public System.Action onLeft, onRight;
        public bool Selected { get; private set; }
        /// <summary>Fica aceso mesmo sem seleção (o botão que abriu o painel ao lado).</summary>
        public bool forceHighlight;
        float k; bool shownOn;
        static float lastMove;

        public void OnSelect(BaseEventData e)
        {
            Selected = true;
            if (Time.unscaledTime - lastMove > 0.05f) ArenAudio.PlayUI(Sfx.UIMove, 0.5f);
            lastMove = Time.unscaledTime;
        }
        public void OnDeselect(BaseEventData e) { Selected = false; }
        public void OnPointerEnter(PointerEventData e) { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject); }
        public void OnSubmit(BaseEventData e) => ArenAudio.PlayUI(Sfx.UIConfirm, 0.7f);
        public void OnPointerClick(PointerEventData e) => ArenAudio.PlayUI(Sfx.UIConfirm, 0.7f);

        void OnDisable() { Selected = false; k = 0f; shownOn = false; if (body != null && normal != null) body.sprite = normal; }

        void Update()
        {
            bool on = Selected || forceHighlight;
            if (on != shownOn && body != null) { shownOn = on; var sp = on ? selectedSprite : normal; if (sp != null) body.sprite = sp; }
            k = Mathf.MoveTowards(k, on ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            float e = k * k * (3 - 2 * k);
            if (label != null) label.color = Color.Lerp(labelOff, labelOn, e);
            if (glow != null) { var c = glow.color; c.a = e * glowMax; glow.color = c; }
            transform.localScale = Vector3.one * (1f + e * 0.015f);
            if (!Selected) return;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame) onLeft?.Invoke();
                if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame) onRight?.Invoke();
            }
            if (Gamepad.current != null)
            {
                if (Gamepad.current.dpad.left.wasPressedThisFrame || Gamepad.current.leftStick.left.wasPressedThisFrame) onLeft?.Invoke();
                if (Gamepad.current.dpad.right.wasPressedThisFrame || Gamepad.current.leftStick.right.wasPressedThisFrame) onRight?.Invoke();
            }
        }
    }

    /// <summary>Slider gótico: trilho de bronze, preenchimento vermelho e botão de caveira; arrastar com o mouse.</summary>
    public class GothicSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public RectTransform track;      // área útil (0..1) do trilho
        public Image fill;
        public RectTransform knob;
        public Text valueText;
        public GameObject selectOnDrag;   // a linha inteira (selecionável) do slider
        public System.Func<float> get;
        public System.Action<float> set;
        float shown = -1f;

        public void OnPointerDown(PointerEventData e) => Drag(e);
        public void OnDrag(PointerEventData e) => Drag(e);

        void Drag(PointerEventData e)
        {
            if (track == null || set == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out var lp)) return;
            float v = Mathf.Clamp01((lp.x - track.rect.xMin) / track.rect.width);
            set(Mathf.Round(v * 20f) / 20f);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(selectOnDrag != null ? selectOnDrag : gameObject);
        }

        void Update()
        {
            if (get == null) return;
            float v = get();
            shown = shown < 0 ? v : Mathf.MoveTowards(shown, v, Time.unscaledDeltaTime * 3f);
            if (fill != null) fill.fillAmount = shown;
            if (knob != null && track != null) knob.anchoredPosition = new Vector2(track.anchoredPosition.x + track.rect.xMin + track.rect.width * shown, knob.anchoredPosition.y);
            if (valueText != null) valueText.text = Mathf.RoundToInt(v * 100f) + "%";
        }
    }

    /// <summary>Interruptor: o botão vermelho desliza para a direita quando ligado.</summary>
    public class GothicToggle : MonoBehaviour
    {
        public RectTransform knob; public Image knobImg;
        public float offX = -24f, onX = 24f;
        public System.Func<bool> get;
        float k = -1f;

        void Update()
        {
            if (get == null || knob == null) return;
            float target = get() ? 1f : 0f;
            k = k < 0 ? target : Mathf.MoveTowards(k, target, Time.unscaledDeltaTime * 6f);
            float e = k * k * (3 - 2 * k);
            knob.anchoredPosition = new Vector2(Mathf.Lerp(offX, onX, e), knob.anchoredPosition.y);
            if (knobImg != null) knobImg.color = Color.Lerp(new Color(0.45f, 0.42f, 0.4f), Color.white, e);
        }
    }
}
