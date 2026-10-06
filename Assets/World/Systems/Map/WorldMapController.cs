using System.Collections.Generic;
using Aren.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Elyndra.World
{
    /// <summary>
    /// Mapa de Elyndra (cena WorldMap): o continente em relevo com os 13 reinos, as rotas (abertas, trancadas e
    /// secretas descobertas), Campânula e a Fenda ao norte. ←/→ (ou A/D, ou clique) escolhem o reino, o painel
    /// gótico mostra cultura, Nota, Relíquia, Vórtices e masmorra; Enter viaja (só para reinos já descobertos —
    /// "-eda-map-free" libera todos para teste); Esc/M volta para onde o Aren estava.
    /// </summary>
    public class WorldMapController : MonoBehaviour
    {
        [System.Serializable] public class Pin { public RegionId id; public Transform t; }
        public List<Pin> pins = new List<Pin>();
        public Camera cam;
        public float kmToUnits = 20f;

        int sel;
        Text title, epithet, body, footer;
        Vector3 camTarget; float camDist = 520f, yaw = 0f;
        static bool Free { get { foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-eda-map-free") return true; return false; } }

        void Start()
        {
            GameSettings.Load();
            BuildUI();
            var from = WorldCanon.RegionByScene(RegionFlow.ResumeScene ?? "");
            sel = 0;
            for (int i = 0; i < pins.Count; i++) if (from != null && pins[i].id == from.id) sel = i;
            Select(sel, true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Aren.UI.GameFlowState.InGame = false;
        }

        void BuildUI()
        {
            var canvas = UIKit.MakeCanvas("Mapa de Elyndra", 30);
            var t = canvas.transform;
            var panel = UIKit.Rect("Painel", t, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(627, 900));
            var img = UIKit.Img("Moldura", panel, null, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(627, 900));
            img.sprite = GothicUI.G("settings_panel"); img.type = Image.Type.Simple;
            title = GothicUI.Label("Reino", panel, "", GothicUI.Cinzel, 40, GothicUI.Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(520, 60));
            epithet = GothicUI.Label("Epíteto", panel, "", GothicUI.Garamond, 22, GothicUI.BoneDim, TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 228), new Vector2(520, 60));
            body = GothicUI.Label("Texto", panel, "", GothicUI.Garamond, 21, GothicUI.Bone, TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(510, 520));
            footer = GothicUI.Label("Rodapé", t, "", GothicUI.Garamond, 24, GothicUI.Bone, TextAnchor.MiddleRight, new Vector2(1, 0), new Vector2(-460, 40), new Vector2(880, 40));
            GothicUI.Label("Título", t, "ELYNDRA", GothicUI.Cinzel, 64, GothicUI.Bone, TextAnchor.MiddleRight, new Vector2(1, 1), new Vector2(-260, -60), new Vector2(480, 90));
        }

        void Select(int i, bool snap)
        {
            sel = (i % pins.Count + pins.Count) % pins.Count;
            var pin = pins[sel];
            var d = WorldCanon.Region(pin.id);
            camTarget = pin.t.position;
            if (snap) cam.transform.position = camTarget + Quaternion.Euler(52f, yaw, 0) * Vector3.back * camDist;
            bool known = WorldState.Discovered(d.id) || d.id == RegionId.Valteria;
            title.text = d.name.ToUpperInvariant();
            epithet.text = d.epithet;
            var note = string.IsNullOrEmpty(d.noteId) ? null : WorldCanon.Note(d.noteId);
            var relic = string.IsNullOrEmpty(d.relicId) ? null : WorldCanon.Relic(d.relicId);
            var dg = WorldCanon.Dungeon(d.dungeonId);
            var sb = new System.Text.StringBuilder();
            sb.Append("<b>Identidade:</b> ").Append(d.identity).Append("\n");
            sb.Append("<b>Tradição:</b> ").Append(d.tradition).Append("\n");
            sb.Append("<b>Conflito:</b> ").Append(d.conflict).Append("\n\n");
            sb.Append("<b>Nota:</b> ").Append(note != null ? note.name + " — " + note.epithet + (WorldState.NoteReafinada(note.id) ? " (reafinada)" : "") : "nenhuma").Append("\n");
            sb.Append("<b>Relíquia de Vael:</b> ").Append(relic != null ? relic.name + " (Custódio: " + relic.custodio + ")" : "nenhuma").Append("\n");
            var vs = new List<string>(); foreach (var v in d.vortices) { var vd = WorldCanon.Vortex(v); if (vd != null) vs.Add(vd.name); }
            sb.Append("<b>Vórtices:</b> ").Append(string.Join(", ", vs)).Append("\n");
            sb.Append("<b>Masmorra:</b> ").Append(dg != null ? dg.name : "—").Append("\n");
            sb.Append("<b>Minibosses:</b> ").Append(string.Join(", ", d.minibosses)).Append("\n");
            sb.Append("<b>Perigo:</b> ").Append(new string('◆', d.dangerTier)).Append(new string('◇', 5 - d.dangerTier)).Append("\n");
            sb.Append("<b>Rotas:</b> ");
            var rs = new List<string>();
            foreach (var r in WorldCanon.RoutesOf(d.id)) rs.Add(WorldCanon.Region(WorldCanon.Other(r, d.id)).name + (WorldState.Check(r.requires) ? "" : " (fechada)") + (r.secret ? " [secreta]" : ""));
            sb.Append(string.Join(" · ", rs));
            sb.Append($"\n\n<i>A Fenda fica a {WorldCanon.FendaDistanceKm(d.id):0} km daqui.</i>");
            body.text = sb.ToString();
            footer.text = (known || Free ? "[Enter] viajar  ·  " : "(ainda não descoberto)  ·  ") + "[←/→] reinos  ·  [Esc] voltar";
        }

        void Update()
        {
            var kb = Keyboard.current; var gp = Gamepad.current;
            if ((kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) || (gp != null && gp.dpad.right.wasPressedThisFrame)) Select(sel + 1, false);
            if ((kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) || (gp != null && gp.dpad.left.wasPressedThisFrame)) Select(sel - 1, false);
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && cam != null)
            {
                var ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out var hit, 5000f))
                    for (int i = 0; i < pins.Count; i++) if (hit.transform == pins[i].t || hit.transform.IsChildOf(pins[i].t)) Select(i, false);
            }
            if (Mouse.current != null) { yaw += Mouse.current.rightButton.isPressed ? Mouse.current.delta.ReadValue().x * 0.2f : 0f; camDist = Mathf.Clamp(camDist - Mouse.current.scroll.ReadValue().y * 0.4f, 180f, 1400f); }
            var want = camTarget + Quaternion.Euler(52f, yaw, 0) * Vector3.back * camDist;
            cam.transform.position = Vector3.Lerp(cam.transform.position, want, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 4f));
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(camTarget - cam.transform.position), 1f - Mathf.Exp(-Time.unscaledDeltaTime * 6f));
            bool enter = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) || (gp != null && gp.buttonSouth.wasPressedThisFrame);
            bool back = (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.mKey.wasPressedThisFrame)) || (gp != null && gp.buttonEast.wasPressedThisFrame);
            if (enter)
            {
                var d = WorldCanon.Region(pins[sel].id);
                if ((WorldState.Discovered(d.id) || d.id == RegionId.Valteria || Free) && RegionTravel.CanLoad(d.scene)) RegionTravel.Go(d.scene, "");
            }
            if (back)
            {
                string to = !string.IsNullOrEmpty(RegionFlow.ResumeScene) ? RegionFlow.ResumeScene : WorldCanon.CampanulaScene;
                RegionTravel.Go(to, "");
            }
        }
    }
}
